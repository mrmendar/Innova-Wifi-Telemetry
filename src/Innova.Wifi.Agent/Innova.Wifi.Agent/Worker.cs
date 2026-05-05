using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices; // Ýþletim sistemi tespiti için kritik
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

    public Worker(ILogger<Worker> logger, IConfiguration configuration, WifiRepository repo)
    {
        _logger = logger;
        _configuration = configuration;
        _repo = repo;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Innova Wi-Fi Telemetry Agent baþlatýlýyor...");

        // 1. ADIM: Ýþletim Sistemine Göre Saðlayýcý Seçimi (Cross-Platform)
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
                    string errorMsg = ex.Message;
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                       (errorMsg.Contains("Access is denied") || errorMsg.Contains("ErrorCode: 5")))
                    {
                        _logger.LogError("!!! KRÝTÝK YETKÝ HATASI: Windows Konum izinleri kapalý! " +
                                         "Lütfen Ayarlar > Gizlilik > Konum > 'Masaüstü uygulamalarýnýn konumunuza eriþmesine izin ver' seçeneðini aktif edin.");
                    }
                    else
                    {
                        _logger.LogError("Saðlayýcý veri çekerken hata fýrlattý: {Msg}", errorMsg);
                    }
                }

                // Windows'a özel Intel -> Native Fallback mantýðý
                if (metric == null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _activeProvider is IntelWifiProvider)
                {
                    _logger.LogWarning("!!! Intel SDK veri çekemedi. Native Windows moduna otomatik geçiþ yapýlýyor.");
                    _activeProvider = new NativeWifiProvider();

                    try { metric = await _activeProvider.GetCurrentMetricAsync(); }
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
                    // Veri tamamlama
                    metric.DeviceMac = _cachedMac;
                    metric.DeviceName = Environment.MachineName;

                    // Payload hazýrlýðý
                    var payloadObj = new
                    {
                        source = _activeProvider.ProviderName,
                        captured_at = DateTime.Now,
                        os_description = RuntimeInformation.OSDescription,
                        architecture = RuntimeInformation.OSArchitecture.ToString(),
                        status = GetProviderStatus()
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

            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task InitializeProviderAsync()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
                catch (Exception)
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
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _activeProvider = new LinuxWifiProvider();
            _logger.LogInformation("Linux platformu algýlandý. nmcli saðlayýcýsý aktif.");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            _activeProvider = new MacWifiProvider();
            _logger.LogInformation("macOS platformu algýlandý. airport saðlayýcýsý aktif.");
        }
        else
        {
            _logger.LogError("Desteklenmeyen iþletim sistemi: {OS}", RuntimeInformation.OSDescription);
            _activeProvider = null;
        }
    }

    private string GetProviderStatus()
    {
        if (_activeProvider is IntelWifiProvider) return "High-Fidelity Mode (Intel)";
        if (_activeProvider is NativeWifiProvider) return "Standard Mode (Native)";
        return "Cross-Platform Mode";
    }

    private string GetPhysicalMacAddress()
    {
        try
        {
            // GERÇEK FÝZÝKSEL ADAPTÖRÜ BULMAK ÝÇÝN GELÝÞTÝRÝLMÝÞ FÝLTRELEME
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n =>
                    // Wi-Fi kartý tipi veya açýklamasý kontrolü
                    (n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                     n.Description.ToLower().Contains("wlan") ||
                     n.Description.ToLower().Contains("wi-fi")) &&

                    // Sadece aktif (Up) olanlar
                    n.OperationalStatus == OperationalStatus.Up &&

                    // --- SANAL ADAPTÖR FÝLTRELERÝ (Mükerrer kaydý önler) ---
                    !n.Description.ToLower().Contains("virtual") &&            // Sanal (Docker/Hyper-V)
                    !n.Description.ToLower().Contains("pseudo") &&             // Sahte adaptörler
                    !n.Description.ToLower().Contains("microsoft wi-fi direct") && // Wi-Fi Direct hileleri
                    !n.Description.ToLower().Contains("adapter - vethernet"));  // Sanal Ethernet köprüleri

            if (nic != null)
            {
                var addr = nic.GetPhysicalAddress().ToString();
                if (!string.IsNullOrEmpty(addr))
                {
                    return string.Join(":", Enumerable.Range(0, addr.Length / 2)
                                 .Select(i => addr.Substring(i * 2, 2)));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Fiziksel MAC adresi alýnýrken hata: {Msg}", ex.Message);
        }
        return "00:00:00:00:00:00";
    }
}