using System;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Innova.Wifi.Api.Models; // Models namespace'inin doğruluğundan emin ol

namespace Innova.Wifi.Api.Data; // Proje adına uygun namespace

public class WifiRepository
{
    private readonly string _connectionString;

    public WifiRepository(IConfiguration configuration)
    {
        // appsettings.json dosyasından "DefaultConnection" değerini okur
        _connectionString = configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Bağlantı dizesi bulunamadı!");
    }

    public async Task InsertMetricAsync(WifiMetric metric)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        // PostgreSQL için JSONB cast işlemi çok kritik, bunu koruman iyi olmuş.
        const string sql = @"
            INSERT INTO wifi_metrics 
            (device_mac, device_name, bssid, ssid, band, channel, rssi_a, rssi_b, tx_retries, bad_crc_count, raw_payload)
            VALUES 
            (@DeviceMac, @DeviceName, @Bssid, @Ssid, @Band, @Channel, @RssiA, @RssiB, @TxRetries, @BadCrcCount, CAST(@RawPayload AS jsonb))";

        await connection.ExecuteAsync(sql, metric);
    }
}