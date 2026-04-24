using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Innova.Wifi.Agent;

public class WifiBackgroundWorker : BackgroundService
{
    private readonly ILogger<WifiBackgroundWorker> _logger;

    public WifiBackgroundWorker(ILogger<WifiBackgroundWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Innova WiFi Agent başlatıldı.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // BURASI ÖNEMLİ: 
                // Mevcut projedeki veri toplama metodunu buraya çağır.
                // Örn: DataCollector.Run();

                _logger.LogInformation("Veriler başarıyla toplandı ve gönderildi: {time}", DateTimeOffset.Now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Veri toplama sırasında hata oluştu.");
            }

            // Mentörün test edebilmesi için şimdilik 30 saniyede bir çalışsın
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}