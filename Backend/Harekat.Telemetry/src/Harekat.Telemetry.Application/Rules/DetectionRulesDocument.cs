using System.Text.Json.Serialization;
using Harekat.Telemetry.Domain.Enums;

namespace Harekat.Telemetry.Application.Rules;

public sealed class DetectionRulesDocument
{
    public string Version { get; set; } = "1";
    public List<DetectionRuleDefinition> Rules { get; set; } = [];
}

public sealed class DetectionRuleDefinition
{
    public string Id { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public SuspicionRuleKind Kind { get; set; }
    public double ScoreWeight { get; set; } = 25;
    public Dictionary<string, double> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
