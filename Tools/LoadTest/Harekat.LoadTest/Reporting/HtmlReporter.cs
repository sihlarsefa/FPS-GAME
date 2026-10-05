using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Harekat.LoadTest.Metrics;
using Harekat.LoadTest.Options;

namespace Harekat.LoadTest.Reporting;

/// <summary>
/// HTML rapor + önceki koşu ile karşılaştırma.
/// </summary>
public static class HtmlReporter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Write(
        RunSummary current,
        SloEvaluation slo,
        IReadOnlyList<string> bottlenecks,
        string directory,
        RunSummary? previous = null)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{current.RunId}.html");

        string Delta(double cur, double? prev, bool lowerIsBetter)
        {
            if (prev is null)
            {
                return "—";
            }

            var d = cur - prev.Value;
            var improved = lowerIsBetter ? d < 0 : d > 0;
            var cls = Math.Abs(d) < 0.0001 ? "flat" : improved ? "good" : "bad";
            var sign = d > 0 ? "+" : "";
            return $"<span class=\"{cls}\">{sign}{d.ToString("F2", CultureInfo.InvariantCulture)}</span>";
        }

        var opRows = new StringBuilder();
        foreach (var op in current.Operations.Values.OrderBy(o => o.Name))
        {
            OperationSummary? prevOp = null;
            if (previous is not null)
            {
                previous.Operations.TryGetValue(op.Name, out prevOp);
            }

            opRows.Append("<tr>")
                .Append("<td>").Append(WebUtility.HtmlEncode(op.Name)).Append("</td>")
                .Append("<td>").Append(op.Total).Append("</td>")
                .Append("<td>").Append(op.ErrorRate.ToString("P2", CultureInfo.InvariantCulture)).Append("</td>")
                .Append("<td>").Append(op.Latency.P50Ms.ToString("F1", CultureInfo.InvariantCulture)).Append("</td>")
                .Append("<td>").Append(op.Latency.P95Ms.ToString("F1", CultureInfo.InvariantCulture)).Append("</td>")
                .Append("<td>").Append(op.Latency.P99Ms.ToString("F1", CultureInfo.InvariantCulture)).Append("</td>")
                .Append("<td>").Append(Delta(op.Latency.P95Ms, prevOp?.Latency.P95Ms, true)).Append("</td>")
                .AppendLine("</tr>");
        }

        var sloRows = new StringBuilder();
        foreach (var c in slo.Checks)
        {
            sloRows.Append("<tr class=\"").Append(c.Passed ? "ok" : "fail").Append("\">")
                .Append("<td>").Append(WebUtility.HtmlEncode(c.Name)).Append("</td>")
                .Append("<td>").Append(c.Actual.ToString("F2", CultureInfo.InvariantCulture)).Append("</td>")
                .Append("<td>").Append(c.Threshold.ToString("F2", CultureInfo.InvariantCulture)).Append("</td>")
                .Append("<td>").Append(c.Passed ? "GEÇTİ" : "KALDI").Append("</td>")
                .AppendLine("</tr>");
        }

        var bottleneckList = new StringBuilder();
        foreach (var b in bottlenecks)
        {
            bottleneckList.Append("<li>").Append(WebUtility.HtmlEncode(b)).AppendLine("</li>");
        }

        var compareNote = previous is null
            ? "<p class=\"muted\">Karşılaştırma yok ( --compare &lt;run-id&gt; ile ekleyin ).</p>"
            : $"<p>Önceki koşu: <strong>{WebUtility.HtmlEncode(previous.RunId)}</strong> ({WebUtility.HtmlEncode(previous.Scenario)})</p>";

        var chartData = JsonSerializer.Serialize(new
        {
            labels = new[] { "p50", "p95", "p99" },
            current = new[] { current.Overall.P50Ms, current.Overall.P95Ms, current.Overall.P99Ms },
            previous = previous is null
                ? null
                : new[] { previous.Overall.P50Ms, previous.Overall.P95Ms, previous.Overall.P99Ms }
        }, JsonOpts);

        var sloBadge = slo.Passed ? "pass" : "fail";
        var sloText = slo.Passed ? "GEÇTİ" : "KALDI";

        var sb = new StringBuilder(16_384);
        sb.Append("""
            <!DOCTYPE html>
            <html lang="tr">
            <head>
              <meta charset="utf-8" />
              <title>HAREKÂT Yük Testi — 
            """);
        sb.Append(WebUtility.HtmlEncode(current.RunId));
        sb.Append("""
            </title>
              <style>
                :root { --bg:#0f1419; --card:#1a222c; --text:#e7ecf1; --muted:#8b9aab; --accent:#3d9a6a; --bad:#c44b4b; --good:#3d9a6a; --line:#2a3542; }
                body { font-family: "Segoe UI", system-ui, sans-serif; background: var(--bg); color: var(--text); margin: 0; padding: 2rem; }
                h1 { font-size: 1.4rem; margin: 0 0 .25rem; }
                h2 { font-size: 1.05rem; margin: 1.5rem 0 .75rem; border-bottom: 1px solid var(--line); padding-bottom: .35rem; }
                .muted { color: var(--muted); }
                .grid { display: grid; grid-template-columns: repeat(auto-fit,minmax(140px,1fr)); gap: .75rem; margin: 1rem 0; }
                .card { background: var(--card); border-radius: 8px; padding: .9rem 1rem; }
                .card .v { font-size: 1.35rem; font-weight: 600; }
                .card .l { font-size: .75rem; color: var(--muted); text-transform: uppercase; letter-spacing: .04em; }
                table { width: 100%; border-collapse: collapse; background: var(--card); border-radius: 8px; overflow: hidden; }
                th, td { padding: .55rem .7rem; text-align: left; border-bottom: 1px solid var(--line); font-size: .9rem; }
                th { color: var(--muted); font-weight: 500; }
                tr.ok td:last-child { color: var(--good); }
                tr.fail td:last-child { color: var(--bad); font-weight: 600; }
                .badge { display: inline-block; padding: .2rem .55rem; border-radius: 4px; font-size: .8rem; font-weight: 600; }
                .badge.pass { background: #1e3d2f; color: var(--good); }
                .badge.fail { background: #3d1e1e; color: var(--bad); }
                .good { color: var(--good); } .bad { color: var(--bad); } .flat { color: var(--muted); }
                ul { line-height: 1.55; }
                canvas { max-width: 480px; background: var(--card); border-radius: 8px; padding: .5rem; }
              </style>
            </head>
            <body>
              <h1>HAREKÂT Yük Testi Raporu</h1>
              <p class="muted">
            """);
        sb.Append(WebUtility.HtmlEncode(current.RunId)).Append(" · ")
            .Append(WebUtility.HtmlEncode(current.Scenario)).Append(" · ")
            .Append(current.EndedUtc.ToString("u")).AppendLine("</p>");
        sb.Append("<p><span class=\"badge ").Append(sloBadge).Append("\">SLO ").Append(sloText).AppendLine("</span></p>");
        sb.AppendLine(compareNote);
        sb.AppendLine("<div class=\"grid\">");
        AppendCard(sb, "RPS", current.RequestsPerSecond.ToString("F1", CultureInfo.InvariantCulture),
            Delta(current.RequestsPerSecond, previous?.RequestsPerSecond, false));
        AppendCard(sb, "Hata oranı", current.ErrorRate.ToString("P2", CultureInfo.InvariantCulture),
            Delta(current.ErrorRate, previous?.ErrorRate, true));
        AppendCard(sb, "p50 ms", current.Overall.P50Ms.ToString("F1", CultureInfo.InvariantCulture),
            Delta(current.Overall.P50Ms, previous?.Overall.P50Ms, true));
        AppendCard(sb, "p95 ms", current.Overall.P95Ms.ToString("F1", CultureInfo.InvariantCulture),
            Delta(current.Overall.P95Ms, previous?.Overall.P95Ms, true));
        AppendCard(sb, "p99 ms", current.Overall.P99Ms.ToString("F1", CultureInfo.InvariantCulture),
            Delta(current.Overall.P99Ms, previous?.Overall.P99Ms, true));
        AppendCard(sb, "İstek", current.TotalRequests.ToString(CultureInfo.InvariantCulture), "");
        sb.AppendLine("</div>");

        sb.AppendLine("<h2>Gecikme karşılaştırması</h2>");
        sb.AppendLine("<canvas id=\"lat\" width=\"480\" height=\"240\"></canvas>");
        sb.AppendLine("<h2>SLO eşikleri</h2><table><thead><tr><th>Metrik</th><th>Gerçek</th><th>Eşik</th><th>Sonuç</th></tr></thead><tbody>");
        sb.Append(sloRows);
        sb.AppendLine("</tbody></table>");
        sb.AppendLine("<h2>İşlemler</h2><table><thead><tr><th>İşlem</th><th>n</th><th>Hata</th><th>p50</th><th>p95</th><th>p99</th><th>Δ p95</th></tr></thead><tbody>");
        sb.Append(opRows);
        sb.AppendLine("</tbody></table>");
        sb.AppendLine("<h2>Darboğaz rehberi</h2><ul>");
        sb.Append(bottleneckList);
        sb.AppendLine("</ul>");
        sb.AppendLine("<script>");
        sb.Append("const data = ").Append(chartData).AppendLine(";");
        sb.AppendLine("""
            const canvas = document.getElementById('lat');
            const ctx = canvas.getContext('2d');
            const W = canvas.width, H = canvas.height, pad = 40;
            const all = data.current.concat(data.previous || [0]);
            const max = Math.max(...all, 1) * 1.15;
            function bar(vals, color, offset) {
              const n = vals.length, bw = (W - pad*2) / n * 0.35;
              vals.forEach((v,i) => {
                const x = pad + i * ((W-pad*2)/n) + offset;
                const h = (v/max) * (H - pad*2);
                ctx.fillStyle = color;
                ctx.fillRect(x, H - pad - h, bw, h);
              });
            }
            ctx.fillStyle = '#1a222c'; ctx.fillRect(0,0,W,H);
            ctx.strokeStyle = '#2a3542'; ctx.beginPath(); ctx.moveTo(pad, pad); ctx.lineTo(pad, H-pad); ctx.lineTo(W-pad, H-pad); ctx.stroke();
            bar(data.current, '#3d9a6a', 8);
            if (data.previous) bar(data.previous, '#5b7ea6', 8 + ((W-pad*2)/3)*0.35);
            ctx.fillStyle = '#8b9aab'; ctx.font = '12px sans-serif';
            data.labels.forEach((l,i) => ctx.fillText(l, pad + i*((W-pad*2)/3) + 16, H - 16));
            </script>
            </body>
            </html>
            """);

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }

    private static void AppendCard(StringBuilder sb, string label, string value, string deltaHtml)
    {
        sb.Append("<div class=\"card\"><div class=\"l\">").Append(label).Append("</div><div class=\"v\">")
            .Append(value).Append("</div>").Append(deltaHtml).AppendLine("</div>");
    }
}
