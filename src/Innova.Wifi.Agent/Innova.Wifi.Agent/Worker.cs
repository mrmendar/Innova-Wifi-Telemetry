
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Innova.Wifi.Agent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly WifiRepository _repo;
    private readonly IConfiguration _configuration;
    private IWifiProvider? _activeProvider; // Deðiþken ismini sabitledik
    private int _lastRssi = 0;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        // Repository artýk ayarlarý IConfiguration üzerinden güvenli bir þekilde alýyor
        _repo = new WifiRepository(configuration);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Innova Wi-Fi Telemetry Agent baþlatýlýyor...");

        // 1. ADIM: Saðlayýcý Seçimi
        await InitializeProviderAsync();

        // 2. ADIM: Ana Veri Toplama Döngüsü
        while (!stoppingToken.IsCancellationRequested)
        {
            // Bu log her 5 saniyede bir çalýþtýðýný teyit eder
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
                    // Þimdilik deðiþim kontrolünü (Math.Abs) kapattýk ki veriyi anýnda görelim
                    _logger.LogInformation("--- Veri yakalandý! SSID: {Ssid}, RSSI: {Rssi} dBm (Source: {Source})",
                        metric.Ssid, metric.RssiA, _activeProvider.ProviderName);

                    await _repo.InsertMetricAsync(metric);
                    _logger.LogInformation("[KAYIT BAÞARILI] Veritabanýna yazýldý.");

                    _lastRssi = metric.RssiA;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Beklenmedik Hata: {Msg}", ex.Message);
            }

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
}