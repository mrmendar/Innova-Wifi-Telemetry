using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Innova.Wifi.Agent;

public class LinuxWifiProvider : IWifiProvider
{
    public string ProviderName => "Linux nmcli Provider";

    public async Task<WifiMetric?> GetCurrentMetricAsync()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "nmcli",
                Arguments = "-t -f ACTIVE,SSID,SIGNAL dev wifi",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var activeLine = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                   .FirstOrDefault(l => l.StartsWith("yes"));

            if (string.IsNullOrEmpty(activeLine)) return null;

            var parts = activeLine.Split(':');
            if (parts.Length < 3) return null;

            int quality = int.TryParse(parts[2], out int q) ? q : 0;
            int rssi = (quality / 2) - 100;

            // DÜZELTME: CapturedAt satırını sildik çünkü WifiMetric modelinde bu alan yok.
            // Zaman bilgisi zaten Worker.cs içinde RawPayload'a ekleniyor.
            return new WifiMetric
            {
                Ssid = parts[1],
                RssiA = rssi
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