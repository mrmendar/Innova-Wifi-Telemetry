using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Innova.Wifi.Agent;

public class MacWifiProvider : IWifiProvider
{
    public string ProviderName => "macOS airport Provider";

    public async Task<WifiMetric?> GetCurrentMetricAsync()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                // Mac'in standart Wi-Fi araç yolu
                FileName = "/System/Library/PrivateFrameworks/Apple80211.framework/Versions/Current/Resources/airport",
                Arguments = "-I", // Interface bilgilerini getir
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // airport çıktısı "anahtar: değer" şeklindedir.
            // Örn: " agrCtlRSSI: -45" ve " SSID: Innova_Wifi"
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            string? ssid = lines.FirstOrDefault(l => l.Contains(" SSID:"))?.Split(':').LastOrDefault()?.Trim();
            string? rssiStr = lines.FirstOrDefault(l => l.Contains("agrCtlRSSI:"))?.Split(':').LastOrDefault()?.Trim();

            if (string.IsNullOrEmpty(ssid) || string.IsNullOrEmpty(rssiStr)) return null;

            return new WifiMetric
            {
                Ssid = ssid,
                RssiA = int.TryParse(rssiStr, out int r) ? r : 0
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public bool IsSupported() => true;
}