using Microsoft.AspNetCore.Mvc;
using Innova.Wifi.Api.Models;
using Innova.Wifi.Api.Data;

namespace Innova.Wifi.Api.Controllers;

[ApiController]
[Route("api/[controller]")] // Adres: api/metrics
public class MetricsController : ControllerBase
{
    private readonly WifiRepository _repo;

    // Dependency Injection ile Repository'yi içeri alıyoruz
    public MetricsController(WifiRepository repo)
    {
        _repo = repo;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] WifiMetric metric)
    {
        // 1. Güvenlik Kontrolü: Gelen veri boş mu?
        if (metric == null)
        {
            return BadRequest("Veri boş olamaz.");
        }

        try
        {
            // 2. KRİTİK DÜZELTME: Repository içindeki InsertMetricAsync metodunu çağırıyoruz.
            // Bu satır veriyi PostgreSQL'e Dapper üzerinden yazdırır.
            await _repo.InsertMetricAsync(metric);

            // 3. Başarı Mesajı: Veri DB'ye başarıyla yazıldığında ajana bu döner.
            return Ok(new { message = "Veri başarıyla veritabanına kaydedildi." });
        }
        catch (Exception ex)
        {
            // 4. Hata Yönetimi: Bir aksilik olursa (bağlantı kopması vb.) detaylı hata döner.
            return StatusCode(500, $"Sunucu hatası: {ex.InnerException?.Message ?? ex.Message}");
        }
    }
}