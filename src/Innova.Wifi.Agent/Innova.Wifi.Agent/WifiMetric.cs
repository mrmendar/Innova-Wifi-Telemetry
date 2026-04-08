public class WifiMetric
{
    public string? DeviceMac { get; set; }
    public string? DeviceName { get; set; }
    public string? Bssid { get; set; }
    public string? Ssid { get; set; }
    public string? Band { get; set; }
    public int Channel { get; set; }
    public int RssiA { get; set; }
    public int RssiB { get; set; }
    public long TxRetries { get; set; }
    public long BadCrcCount { get; set; }
    public string? RawPayload { get; set; }
}