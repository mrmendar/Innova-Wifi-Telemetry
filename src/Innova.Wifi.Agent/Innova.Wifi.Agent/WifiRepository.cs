using System;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Microsoft.Extensions.Configuration; // Bunu eklemeyi unutma

namespace Innova.Wifi.Agent;

public class WifiRepository
{
    private readonly string _connectionString;

    // Şifreyi kodun içine yazmak yerine, IConfiguration üzerinden 'appsettings.json'dan çekiyoruz.
    public WifiRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Bağlantı dizesi bulunamadı!");
    }

    public async Task InsertMetricAsync(WifiMetric metric)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        const string sql = @"
            INSERT INTO wifi_metrics (device_mac, device_name, bssid, ssid, band, channel, rssi_a, rssi_b, tx_retries, bad_crc_count, raw_payload)
            VALUES (@DeviceMac, @DeviceName, @Bssid, @Ssid, @Band, @Channel, @RssiA, @RssiB, @TxRetries, @BadCrcCount, CAST(@RawPayload AS jsonb))";

        await connection.ExecuteAsync(sql, metric);
    }
}