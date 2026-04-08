using Intel.Telemetry.Api.Client;
using Intel.Telemetry.Api.Commands.Data;
using Intel.Telemetry.Api.Message.Version;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Innova.Wifi.Agent;

public class IntelWifiProvider : IWifiProvider
{
    private object? _context;
    private object? _client;
    private readonly string _configPath;

    public string ProviderName => "Intel Connectivity Analytics (ICA) SDK";

    public IntelWifiProvider()
    {
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ica_partner_config_evaluation_signed.json");
    }

    public bool IsSupported() => File.Exists(_configPath);

    public async Task InitializeAsync()
    {
        var client = TelemetryClient.CreateNew(new FileInfo(_configPath));
        _client = client;

        var schemaContainer = new Intel.Telemetry.Test.App.SchemaContainer.SchemaDataContainer();

        // dynamic cast ile SDK başlatma
        _context = await ((dynamic)client).InitializeClientContextAsync(
            TimeSpan.FromSeconds(15),
            (Action<dynamic>)((data) => schemaContainer.AddOrUpdateSchemaEntries(data)));
    }

    public async Task<WifiMetric?> GetCurrentMetricAsync()
    {
        if (_context == null) return null;

        try
        {
            var command = new JsonCommand
            {
                Name = "Get Wi-Fi Connection Statistics",
                TimeOut = TimeSpan.FromSeconds(30),
                Version = new MessageVersion { Major = 3, Minor = 0 },
                ResponseVersion = 3,
                CommandParameter = new JsonObject { ["BssType"] = "Primary BSS" }
            };

            // 1. TelemetryCommandSender özelliğini bul
            var senderProp = _context.GetType().GetProperty("TelemetryCommandSender");
            var sender = senderProp?.GetValue(_context);
            if (sender == null) return null;

            // 2. KRİTİK DÜZELTME: Belirsiz eşleşmeyi (Ambiguous match) önlemek için 
            // sadece JsonCommand tipinde tek parametre alan metodu arıyoruz.
            var method = sender.GetType().GetMethods()
                .FirstOrDefault(m => m.Name == "SendTelemetryCommandAsync" &&
                                     m.GetParameters().Length == 1 &&
                                     m.GetParameters()[0].ParameterType == typeof(JsonCommand));

            if (method == null) return null;

            // 3. Metodu çalıştır ve Task'ı bekle
            var task = (Task)method.Invoke(sender, new object[] { command })!;
            await task;

            // 4. Sonucu (Result) çek
            var resultProp = task.GetType().GetProperty("Result");
            dynamic response = resultProp?.GetValue(task)!;

            if (response.Header == null || response.Header.HasFailed || response.Payload == null)
            {
                if (response.Header != null && response.Header.HasFailed)
                    Console.WriteLine($"[INTEL SDK] Komut Başarısız: {response.Header.FailureReason}");
                return null;
            }

            var payload = response.Payload;
            var linkInfo = payload["Link Information"]?[0];
            if (linkInfo == null) return null;

            return new WifiMetric
            {
                DeviceMac = payload["STA MAC Address"]?.ToString() ?? "Unknown",
                DeviceName = Environment.MachineName,
                Bssid = linkInfo["BSSID"]?.ToString() ?? "Unknown",
                Ssid = payload["SSID"]?.ToString() ?? "Unknown",
                Band = linkInfo["Band"]?.ToString() ?? "Unknown",
                Channel = int.Parse(linkInfo["Channel"]?.ToString() ?? "0"),
                RssiA = int.Parse(linkInfo["Latest RSSI A Beacon Counter"]?.ToString() ?? "0"),
                RssiB = int.Parse(linkInfo["Latest RSSI B Beacon Counter"]?.ToString() ?? "0"),
                TxRetries = long.Parse(linkInfo["Tx Retries in Link Counter"]?.ToString() ?? "0"),
                BadCrcCount = long.Parse(linkInfo["Bad-CRCs in Link Counter"]?.ToString() ?? "0"),
                RawPayload = payload.ToString()
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[INTEL SDK KRİTİK HATA] {ex.Message}");
            return null;
        }
    }
}