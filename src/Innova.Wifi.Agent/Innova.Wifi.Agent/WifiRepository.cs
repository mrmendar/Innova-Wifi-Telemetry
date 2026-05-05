using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite; // SQLite için gerekli
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Innova.Wifi.Agent;

public class WifiRepository
{
    private readonly HttpClient _httpClient;
    private readonly string _apiEndpoint;
    private readonly string _apiKey;
    private readonly ILogger<WifiRepository> _logger;
    private readonly string _connectionString = "Data Source=offline_cache.db";

    public WifiRepository(HttpClient httpClient, IConfiguration configuration, ILogger<WifiRepository> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _apiEndpoint = configuration["ApiSettings:MetricsUrl"]
                      ?? throw new InvalidOperationException("API adresi bulunamadı!");

        _apiKey = configuration["ApiSettings:ApiKey"]
                  ?? throw new InvalidOperationException("API Key bulunamadı!");

        // SQLite Veritabanını ve Tabloyu Hazırla
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS OfflineMetrics (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Data TEXT NOT NULL,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            );";
        command.ExecuteNonQuery();
    }

    public async Task InsertMetricAsync(WifiMetric metric)
    {
        bool isSuccess = false;

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);

            var response = await _httpClient.PostAsJsonAsync(_apiEndpoint, metric);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Veri başarıyla API'ye iletildi.");
                isSuccess = true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("API hatası ({Code}). Veri yerel depoya kaydediliyor.", response.StatusCode);
                await SaveToOfflineCache(metric);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("API'ye ulaşılamadı. Veri yerel depoya yedekleniyor. Hata: {Msg}", ex.Message);
            await SaveToOfflineCache(metric);
        }

        // Eğer son gönderim başarılıysa, bekleyen eski verileri gönder
        if (isSuccess)
        {
            await SyncOfflineDataAsync();
        }
    }

    private async Task SaveToOfflineCache(WifiMetric metric)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO OfflineMetrics (Data) VALUES ($data)";
            command.Parameters.AddWithValue("$data", JsonSerializer.Serialize(metric));

            await command.ExecuteNonQueryAsync();
            _logger.LogInformation("Veri çevrimdışı kullanım için SQLite'a mühürlendi.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical("KRİTİK: SQLite'a yazılırken hata oluştu! {Msg}", ex.Message);
        }
    }

    private async Task SyncOfflineDataAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var selectCommand = connection.CreateCommand();
        selectCommand.CommandText = "SELECT Id, Data FROM OfflineMetrics ORDER BY Id ASC LIMIT 10";

        var toDelete = new List<int>();

        using var reader = await selectCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            int id = reader.GetInt32(0);
            string data = reader.GetString(1);
            var metric = JsonSerializer.Deserialize<WifiMetric>(data);

            if (metric != null)
            {
                try
                {
                    var response = await _httpClient.PostAsJsonAsync(_apiEndpoint, metric);
                    if (response.IsSuccessStatusCode)
                    {
                        toDelete.Add(id);
                        _logger.LogInformation("Yerelde bekleyen veri (ID: {Id}) başarıyla API'ye gönderildi.", id);
                    }
                }
                catch { break; } // İnternet tekrar gittiyse döngüden çık
            }
        }
        reader.Close();

        // Başarıyla gönderilenleri yerelden sil
        if (toDelete.Count > 0)
        {
            var deleteCommand = connection.CreateCommand();
            deleteCommand.CommandText = $"DELETE FROM OfflineMetrics WHERE Id IN ({string.Join(",", toDelete)})";
            await deleteCommand.ExecuteNonQueryAsync();
        }
    }
}