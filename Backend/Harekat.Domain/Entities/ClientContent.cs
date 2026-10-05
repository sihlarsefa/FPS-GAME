namespace Harekat.Domain.Entities;

/// <summary>Launcher haber akışı öğesi (MSSQL / bellek).</summary>
public sealed class NewsItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Language { get; set; } = "tr";
    public string? Author { get; set; }
    public bool IsPublished { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>İstemci sürüm / yama kaydı (kanal başına bir aktif sürüm).</summary>
public sealed class ClientVersion
{
    public string Channel { get; set; } = "stable";
    public string Version { get; set; } = "0.1.0";
    public string PatchUrl { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long PatchSizeBytes { get; set; }
    public string? ReleaseNotes { get; set; }
    public bool Mandatory { get; set; }
    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;
}
