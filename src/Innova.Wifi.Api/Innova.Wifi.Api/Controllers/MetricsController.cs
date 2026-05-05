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
        if (metric == null) return BadRequest("Veri boş olamaz.");

        try
        {
            await _repo.InsertMetricAsync(metric);
            // Mentörünün notundaki başarılı dönüş (200 OK)
            return Ok(new { message = "Veri başarıyla veritabanına kaydedildi." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Sunucu hatası: {ex.Message}");
        }
    }
}