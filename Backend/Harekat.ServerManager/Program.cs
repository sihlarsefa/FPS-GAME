using Harekat.ServerManager;
using Harekat.ServerManager.Services;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Harekat.ServerManager";
});

#if WINDOWS
LoggingBuilderExtensions.AddEventLog(builder.Logging);
builder.Logging.AddFilter<EventLogLoggerProvider>(level => level >= LogLevel.Warning);
#endif

builder.Services.Configure<ServerManagerOptions>(builder.Configuration.GetSection("ServerManager"));
builder.Services.AddHttpClient<BackendApiClient>();
builder.Services.AddSingleton<PortPool>();
builder.Services.AddSingleton<MatchProcessRegistry>();
builder.Services.AddSingleton<LogRotator>();
builder.Services.AddSingleton<ProcessResourceMonitor>();
builder.Services.AddHostedService<MatchSpawnWorker>();
builder.Services.AddHostedService<HeartbeatWorker>();
builder.Services.AddHostedService<MetricsHttpWorker>();
builder.Services.AddHostedService<LogRotationWorker>();

var host = builder.Build();
host.Run();
