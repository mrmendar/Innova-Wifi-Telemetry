
CREATE TABLE IF NOT EXISTS wifi_metrics (
    id SERIAL PRIMARY KEY,
    device_name TEXT,
    device_mac TEXT,
    rssi_a INT,
    raw_payload JSONB,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);


CREATE OR REPLACE VIEW device_alert_status AS
SELECT 
    device_name,
    device_mac,
    CASE 
        WHEN (NOW() - created_at) > INTERVAL '1 minute' THEN 'CRITICAL' 
        ELSE 'OK'
    END as status_level
FROM (
    SELECT DISTINCT ON (device_mac) * FROM wifi_metrics ORDER BY device_mac, created_at DESC
) as latest;