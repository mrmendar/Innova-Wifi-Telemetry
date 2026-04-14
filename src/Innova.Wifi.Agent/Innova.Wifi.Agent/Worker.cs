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
    private string? _cachedMac; // Performans için MAC adresini bir kez alýp saklýyoruz

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

                var metric = await _activeProvider.GetCurrentMetricAsync();

                if (metric == null)
                {
                    _logger.LogWarning("!!! Veri çekilemedi: {Provider} 'null' döndü.", _activeProvider.ProviderName);
                }
                else
                {
                    // --- KRÝTÝK DÜZELTME: VERÝ TEKÝLLEÞTÝRME VE TAMAMLAMA ---

                    // Provider ne döndürürse döndürsün, biz gerçek fiziksel MAC'i yazýyoruz
                    metric.DeviceMac = _cachedMac;
                    metric.DeviceName = Environment.MachineName;

                    // Grafana'daki 'source' filtresinin çalýþmasý için RawPayload'u dolduruyoruz
                    var payloadObj = new
                    {
                        source = _activeProvider.ProviderName,
                        captured_at = DateTime.Now,
                        os_version = Environment.OSVersion.ToString()
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
                _logger.LogError("Beklenmedik Hata: {Msg}", ex.Message);
            }

            // Mentörünün istediði gibi 5 saniyede bir (veri kaybý olmadan stabil çalýþma)
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
                _logger.LogWarning("Intel SDK bulundu ama baþlatýlamadý. Native Windows moduna geçiliyor. Hata: {Msg}", ex.Message);
                _activeProvider = new NativeWifiProvider();
            }
        }
        else
        {
            _activeProvider = new NativeWifiProvider();
            _logger.LogInformation("Donaným Intel ICA desteklemiyor. Genel (Native) Wi-Fi modu aktif.");
        }
    }

    // --- SÝHÝRLÝ DOKUNUÞ: FÝZÝKSEL MAC ADRESÝNÝ ÇEKEN METOD ---
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
                // 84144DF5AFDC -> 84:14:4D:F5:AF:DC formatýna çevir
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