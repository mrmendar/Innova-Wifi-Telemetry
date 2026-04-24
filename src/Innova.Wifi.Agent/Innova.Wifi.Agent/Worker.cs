using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Innova.Wifi.Agent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly WifiRepository _repo;
    private readonly IConfiguration _configuration;
    private IWifiProvider? _activeProvider;
    private string? _cachedMac;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _repo = new WifiRepository(configuration);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Innova Wi-Fi Telemetry Agent baþlatýlýyor...");

        // 1. ADIM: Saðlayýcý Seçimi
        await InitializeProviderAsync();

        // Fiziksel MAC adresini uygulama baþlarken bir kez çekiyoruz
        _cachedMac = GetPhysicalMacAddress();

        // 2. ADIM: Ana Veri Toplama Döngüsü
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(">>> Döngü çalýþýyor, veri bekleniyor...");

            try
            {
                if (_activeProvider == null)
                {
                    _logger.LogWarning("Aktif saðlayýcý bulunamadý, yeniden baþlatýlýyor...");
                    await InitializeProviderAsync();
                    continue;
                }

                WifiMetric? metric = null;

                // --- GELÝÞTÝRÝLMÝÞ FALLBACK VE YETKÝ KONTROLÜ ---
                try
                {
                    metric = await _activeProvider.GetCurrentMetricAsync();
                }
                catch (Exception ex)
                {
                    // Hata mesajýný analiz et (Özellikle Error 5 / Access Denied kontrolü)
                    string errorMsg = ex.Message;

                    if (errorMsg.Contains("Access is denied") || errorMsg.Contains("ErrorCode: 5"))
                    {
                        _logger.LogError("!!! KRÝTÝK YETKÝ HATASI: Windows Konum izinleri kapalý! " +
                                         "Lütfen Ayarlar > Gizlilik > Konum > 'Masaüstü uygulamalarýnýn konumunuza eriþmesine izin ver' seçeneðini aktif edin.");
                    }
                    else
                    {
                        _logger.LogError("Saðlayýcý veri çekerken hata fýrlattý: {Msg}", errorMsg);
                    }
                }

                // KRÝTÝK: Eðer Intel null döndüyse veya hata verdiyse, anýnda Native'e geç ve tekrar dene
                if (metric == null && _activeProvider is IntelWifiProvider)
                {
                    _logger.LogWarning("!!! Intel SDK veri çekemedi (Lisans/Kontrat sorunu). Native Windows moduna otomatik geçiþ yapýlýyor.");

                    _activeProvider = new NativeWifiProvider();

                    // Native üzerinden tekrar deniyoruz
                    try
                    {
                        metric = await _activeProvider.GetCurrentMetricAsync();
                    }
                    catch (Exception ex) when (ex.Message.Contains("Access is denied") || ex.Message.Contains("ErrorCode: 5"))
                    {
                        _logger.LogError("!!! Native modda da YETKÝ HATASI: Konum hizmetlerini açmanýz gerekiyor.");
                    }
                }

                if (metric == null)
                {
                    _logger.LogWarning("!!! Veri çekilemedi: {Provider} þu an veri saðlayamýyor.", _activeProvider.ProviderName);
                }
                else
                {
                    // Veri tekilleþtirme ve tamamlama
                    metric.DeviceMac = _cachedMac;
                    metric.DeviceName = Environment.MachineName;

                    // Grafana payload hazýrlýðý
                    var payloadObj = new
                    {
                        source = _activeProvider.ProviderName,
                        captured_at = DateTime.Now,
                        os_version = Environment.OSVersion.ToString(),
                        status = _activeProvider is NativeWifiProvider ? "Fallback Mode" : "High-Fidelity Mode"
                    };
                    metric.RawPayload = JsonSerializer.Serialize(payloadObj);

                    // Veritabanýna Kayýt
                    await _repo.InsertMetricAsync(metric);

                    _logger.LogInformation("[KAYIT BAÞARILI] Source: {Source}, MAC: {Mac}, RSSI: {Rssi} dBm",
                        _activeProvider.ProviderName, metric.DeviceMac, metric.RssiA);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Döngü içerisinde beklenmedik genel hata: {Msg}", ex.Message);
            }

            // 5 saniyelik periyot
            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task InitializeProviderAsync()
    {
        var intel = new IntelWifiProvider();

        if (intel.IsSupported())
        {
            try
            {
                await intel.InitializeAsync();
                _activeProvider = intel;
                _logger.LogInformation("Cihaz Intel ICA destekliyor. Full telemetri modu aktif.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Intel SDK bulundu ama baþlatýlamadý. Native Windows moduna geçiliyor.");
                _activeProvider = new NativeWifiProvider();
            }
        }
        else
        {
            _activeProvider = new NativeWifiProvider();
            _logger.LogInformation("Donaným Intel ICA desteklemiyor. Genel (Native) Wi-Fi modu aktif.");
        }
    }

    private string GetPhysicalMacAddress()
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
                                     n.OperationalStatus == OperationalStatus.Up);

            if (nic != null)
            {
                var addr = nic.GetPhysicalAddress().ToString();
                return string.Join(":", Enumerable.Range(0, addr.Length / 2)
                             .Select(i => addr.Substring(i * 2, 2)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Fiziksel MAC adresi alýnýrken hata: {Msg}", ex.Message);
        }

        return "00:00:00:00:00:00";
    }
}