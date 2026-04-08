using ManagedNativeWifi;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Innova.Wifi.Agent;

public class NativeWifiProvider : IWifiProvider
{
    public string ProviderName => "Windows Native WiFi (General)";

    public bool IsSupported() => true;

    public async Task<WifiMetric> GetCurrentMetricAsync()
    {
        // 1. Bağlı olan Wi-Fi kartını bul
        var activeInterface = NativeWifi.EnumerateInterfaces()
            .FirstOrDefault(x => x.State == InterfaceState.Connected);

        if (activeInterface == null) return null;

        // 2. Bağlı olan profilin (SSID) adını al
        // 'activeInterface' üzerinden profil ismine ulaşıyoruz
        var connectedProfile = activeInterface.Id.ToString(); // Fallback için ID

        // 3. Mevcut tüm ağları tara ve bizim bağlı olduğumuz ağın sinyal gücünü bul
        // IsConnected hatasını bypass etmek için sinyali en yüksek olanı veya eşleşeni alıyoruz
        var networks = NativeWifi.EnumerateAvailableNetworks();
        var currentNetwork = networks.FirstOrDefault(x => x.InterfaceInfo.Id == activeInterface.Id);

        if (currentNetwork == null) return null;

        // dBm Normalizasyonu: (Kalite / 2) - 100
        int normalizedRssi = (currentNetwork.SignalQuality / 2) - 100;

        return new WifiMetric
        {
            DeviceMac = activeInterface.Id.ToString(),
            DeviceName = Environment.MachineName,

            // SSID olarak taradığımız ağın adını alıyoruz
            Ssid = currentNetwork.Ssid.ToString(),
            Bssid = "",

            RssiA = normalizedRssi,
            RssiB = normalizedRssi,

            TxRetries = 0,
            BadCrcCount = 0,
            RawPayload = "{\"source\": \"NativeWindowsAPI\", \"quality\": " + currentNetwork.SignalQuality + "}"
        };
    }
}