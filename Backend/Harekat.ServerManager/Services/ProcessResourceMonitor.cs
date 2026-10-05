using System.Diagnostics;

namespace Harekat.ServerManager.Services;

public readonly record struct ProcessResources(double CpuPercent, long WorkingSetMb);

/// <summary>
/// Process CPU (%) ve RAM (MB) örnekleme. CPU, ardışık TotalProcessorTime farkından hesaplanır.
/// </summary>
public sealed class ProcessResourceMonitor
{
    private readonly Dictionary<int, (TimeSpan Cpu, DateTime Utc)> _samples = new();
    private readonly object _gate = new();

    public ProcessResources Sample(Process process)
    {
        try
        {
            process.Refresh();
            var now = DateTime.UtcNow;
            var cpu = process.TotalProcessorTime;
            var ramMb = process.WorkingSet64 / (1024L * 1024L);

            lock (_gate)
            {
                if (_samples.TryGetValue(process.Id, out var prev))
                {
                    var wall = (now - prev.Utc).TotalMilliseconds;
                    if (wall > 50)
                    {
                        var cpuMs = (cpu - prev.Cpu).TotalMilliseconds;
                        var pct = 100.0 * cpuMs / (wall * Environment.ProcessorCount);
                        _samples[process.Id] = (cpu, now);
                        return new ProcessResources(Math.Clamp(pct, 0, 100 * Environment.ProcessorCount), ramMb);
                    }
                }

                _samples[process.Id] = (cpu, now);
                return new ProcessResources(0, ramMb);
            }
        }
        catch
        {
            return new ProcessResources(0, 0);
        }
    }

    public ProcessResources SampleAggregate(IEnumerable<Process> processes)
    {
        double cpu = 0;
        long ram = 0;
        foreach (var p in processes)
        {
            var s = Sample(p);
            cpu += s.CpuPercent;
            ram += s.WorkingSetMb;
        }
        return new ProcessResources(cpu, ram);
    }

    public void Forget(int processId)
    {
        lock (_gate)
            _samples.Remove(processId);
    }
}
