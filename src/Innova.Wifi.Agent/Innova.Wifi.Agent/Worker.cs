using Intel.Telemetry.Api.Client;
using Intel.Telemetry.Api.Commands.Data;
using Intel.Telemetry.Api.Message.Version;
using System.Text.Json.Nodes;

namespace Innova.Wifi.Agent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly WifiRepository _repo = new();
    private int _lastRssi = 0;

    public Worker(ILogger<Worker> logger) => _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Innova Wi-Fi Ajaný baþlatýlýyor...");

        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ica_partner_config_evaluation_signed.json");

        try
        {
            // 1. SDK Baðlantýsý (Örnek koddaki gibi SchemaDataContainer ile)
            await using var client = TelemetryClient.CreateNew(new FileInfo(configPath));

            // NOT: Eðer SchemaDataContainer sýnýfýn yoksa, en azýndan boþ bir Dictionary mantýðý kurmalýsýn.
            // Ama en iyisi örnek projedeki SchemaDataContainer.cs dosyasýný projene eklemendir.
            var schemaDataContainer = new Intel.Telemetry.Test.App.SchemaContainer.SchemaDataContainer();

            var context = await client.InitializeClientContextAsync(
                TimeSpan.FromSeconds(15),
                schemaDataContainer.AddOrUpdateSchemaEntries);

            _logger.LogInformation("Intel Telemetry SDK baþarýyla baðlandý.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 2. Wi-Fi Komutu (Örnek koddaki parametrelerin aynýsý)
                    var command = new JsonCommand
                    {
                        Name = "Get Wi-Fi Connection Statistics",
                        TimeOut = TimeSpan.FromSeconds(30), // KRÝTÝK: Örnek kodda var, bizde yoktu!
                        Version = new MessageVersion { Major = 3, Minor = 0 },
                        ResponseVersion = 3,
                        CommandParameter = new JsonObject { ["BssType"] = "Primary BSS" }
                    };

                    // 3. Komutu Gönder
                    var response = await context.TelemetryCommandSender.SendTelemetryCommandAsync(command);

                    if (response.Header != null && !response.Header.HasFailed && response.Payload != null)
                    {
                        var payload = response.Payload;
                        var linkInfo = payload["Link Information"]?[0];

                        if (linkInfo != null)
                        {
                            int currentRssi = int.Parse(linkInfo["Latest RSSI A Beacon Counter"]?.ToString() ?? "0");

                            if (Math.Abs(currentRssi - _lastRssi) >= 3 || _lastRssi == 0)
                            {
                                var metric = new WifiMetric
                                {
                                    DeviceMac = payload["STA MAC Address"]?.ToString() ?? "Unknown",
                                    DeviceName = Environment.MachineName,
                                    Bssid = linkInfo["BSSID"]?.ToString() ?? "Unknown",
                                    Ssid = payload["SSID"]?.ToString() ?? "Unknown",
                                    Band = linkInfo["Band"]?.ToString() ?? "Unknown",
                                    Channel = int.Parse(linkInfo["Channel"]?.ToString() ?? "0"),
                                    RssiA = currentRssi,
                                    RssiB = int.Parse(linkInfo["Latest RSSI B Beacon Counter"]?.ToString() ?? "0"),
                                    TxRetries = long.Parse(linkInfo["Tx Retries in Link Counter"]?.ToString() ?? "0"),
                                    BadCrcCount = long.Parse(linkInfo["Bad-CRCs in Link Counter"]?.ToString() ?? "0"),
                                    RawPayload = payload.ToString()
                                };

                                await _repo.InsertMetricAsync(metric);
                                _lastRssi = currentRssi;
                                _logger.LogInformation("[VERÝTABANI KAYIT] SSID: {Ssid} | RSSI: {Rssi} dBm", metric.Ssid, currentRssi);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Komut Hatasý: {Reason}", response.Header.FailureReason);
                    }
                }
                catch (Exception loopEx)
                {
                    _logger.LogError("Döngü Hatasý: {Message}", loopEx.Message);
                }

                await Task.Delay(5000, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "SDK Baþlatýlamadý!");
        }
    }
}