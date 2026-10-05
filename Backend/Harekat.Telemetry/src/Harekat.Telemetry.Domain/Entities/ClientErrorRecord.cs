namespace Harekat.Telemetry.Domain.Entities;

/// <summary>İstemci hata / çökme raporu (oyundan gelen).</summary>
public sealed class ClientErrorRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Trigger { get; set; } = "";
    public string Version { get; set; } = "";
    public string Scene { get; set; } = "";
    public string Platform { get; set; } = "";
    public string DeviceModel { get; set; } = "";
    public string OperatingSystem { get; set; } = "";
    public string ProcessorType { get; set; } = "";
    public int ProcessorCount { get; set; }
    public int SystemMemoryMb { get; set; }
    public string GraphicsDeviceName { get; set; } = "";
    public int GraphicsMemoryMb { get; set; }
    public string UnityVersion { get; set; } = "";
    public string ExceptionType { get; set; } = "";
    public string Message { get; set; } = "";
    public string StackTrace { get; set; } = "";
    public string RecentLogsJson { get; set; } = "[]";
    public string? ClientIp { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
