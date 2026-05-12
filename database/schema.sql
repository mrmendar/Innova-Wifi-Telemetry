-- wifi_metrics tablosunu oluştur
CREATE TABLE IF NOT EXISTS wifi_metrics (
    id SERIAL PRIMARY KEY,
    device_mac VARCHAR(50),      -- C# DeviceMac
    device_name VARCHAR(100),    -- C# DeviceName
    bssid VARCHAR(50),           -- C# Bssid
    ssid VARCHAR(100),           -- C# Ssid
    band VARCHAR(20),            -- C# Band
    channel INTEGER,             -- C# Channel
    rssi_a INTEGER,              -- C# RssiA
    rssi_b INTEGER,              -- C# RssiB
    tx_retries BIGINT,           -- C# TxRetries
    bad_crc_count BIGINT,        -- C# BadCrcCount
    raw_payload JSONB,           -- C# RawPayload (JSONB hızlı sorgulama sağlar)
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP -- Grafana için zaman damgası
);

-- Grafana sorgularını hızlandırmak için indeks
CREATE INDEX IF NOT EXISTS idx_wifi_metrics_created_at ON wifi_metrics(created_at);