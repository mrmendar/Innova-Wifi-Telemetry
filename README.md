# 🛰️ Innova Wi-Fi Telemetry Agent

Bu proje, kurumsal ağların sağlık durumunu izlemek amacıyla geliştirilmiş; Windows, Linux ve macOS platformlarında çalışabilen yüksek hassasiyetli bir telemetri çözümüdür.

## 🚀 Öne Çıkan Özellikler

*   **Hibrit Veri Sağlayıcı:** Intel Connectivity Analytics (ICA) SDK desteği ile sürücü seviyesinde derinlik, desteklenmeyen cihazlarda ise Native API geçişi.
*   **Çevrimdışı Dayanıklılık (Resilience):** Bağlantı koptuğunda verileri SQLite (offline_cache.db) üzerinde saklama ve bağlantı geldiğinde otomatik senkronizasyon.
*   **Dockerize Mimari:** PostgreSQL, Merkezi API ve Grafana bileşenlerinin tek komutla ayağa kaldırılması.
*   **Akıllı Alarmlar:** Kritik RSSI seviyeleri ve cihaz canlılık takibi için önceden yapılandırılmış Grafana Alerting kuralları.

## 🏗️ Sistem Mimarisi

Sistem, uç birimlerden (Agent) merkezi sunucuya veri akışı sağlayan mikroservis tabanlı bir yapı üzerindedir:
*   **Agent (C# .NET 8):** Metrik toplama ve önbellek yönetimi.
*   **Merkezi API (ASP.NET Core 8):** Veri doğrulama ve işleme.
*   **Grafana:** Aksiyon odaklı dashboard ve görselleştirme.

## 🛠️ Kurulum ve Çalıştırma

### 1. Konteyner Sistemini Başlatın
`deploy` klasörüne gidin ve Docker Compose ile tüm servisleri ayağa kaldırın:
```bash
cd deploy
docker compose up -d --build
Grafana: http://localhost:3000

Merkezi API: http://localhost:5001
2. Ajanı (Agent) Yapılandırın
src/Innova.Wifi.Agent klasörü içindeki appsettings.Example.json dosyasını appsettings.json olarak kopyalayın ve API adresini güncelleyin:

"ApiSettings": {
    "MetricsUrl": "http://localhost:5001/api/Metrics",
    "ApiKey": "YOUR_SECRET_KEY"
}
📊 Kritik Metrikler
RSSI: -80 dBm altı "Kör Nokta" olarak işaretlenir.

SNR: Sinyal temizliği analizi.

Liveness Check: 1 dakikadan uzun süre veri göndermeyen cihazlar "OFFLINE" kabul edilir.