namespace Harekat.ServerManager;

public sealed class ServerManagerOptions
{
    public string BackendBaseUrl { get; set; } = "http://127.0.0.1:3208";
    public string PublicHost { get; set; } = "127.0.0.1";
    public string Region { get; set; } = "tr";
    public string ServerKey { get; set; } = "ChangeMeServerManagerKey16+";
    public string GameServerExe { get; set; } = @"C:\harekat\game\HAREKAT_Server.exe";
    public int PortMin { get; set; } = 7777;
    public int PortMax { get; set; } = 7900;
    public int WarmReadySlots { get; set; } = 2;
    public int MaxPlayersPerMatch { get; set; } = 100;
    public int PollIntervalSeconds { get; set; } = 3;
    public int HeartbeatIntervalSeconds { get; set; } = 10;
    public int MaxCpuPercent { get; set; } = 90;
    public int MaxRamMb { get; set; } = 6144;
    public string LogDirectory { get; set; } = @"C:\harekat\logs\gameservers";
    public int LogRetainDays { get; set; } = 7;
    public int MetricsPort { get; set; } = 9183;
    public int RestartDelaySeconds { get; set; } = 5;
    public int MaxRestartsPerHour { get; set; } = 6;
}
