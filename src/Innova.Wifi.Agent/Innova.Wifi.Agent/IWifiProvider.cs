namespace Innova.Wifi.Agent;

public interface IWifiProvider
{
    // Hangi sağlayıcının (Intel/Windows) aktif olduğunu anlamak için
    string ProviderName { get; }

    // Veri çekme metodumuz
    Task<WifiMetric> GetCurrentMetricAsync();

    // Sağlayıcının bu bilgisayarda çalışıp çalışmadığını kontrol eder
    bool IsSupported();
}