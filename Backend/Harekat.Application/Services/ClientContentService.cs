using Harekat.Application.Abstractions;
using Harekat.Application.Common;
using Harekat.Application.Dtos;
using Harekat.Domain.Entities;

namespace Harekat.Application.Services;

public sealed class ClientContentService
{
    private readonly INewsRepository _news;
    private readonly IClientVersionRepository _versions;
    private readonly IUnitOfWork _uow;

    public ClientContentService(INewsRepository news, IClientVersionRepository versions, IUnitOfWork uow)
    {
        _news = news;
        _versions = versions;
        _uow = uow;
    }

    public async Task<IReadOnlyList<NewsItemDto>> ListNewsAsync(string? language, int take = 20, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 100);
        var items = await _news.ListPublishedAsync(NormalizeLang(language), take, ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<NewsItemDto>> ListAllNewsAdminAsync(int take = 50, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var items = await _news.ListAllAsync(take, ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<NewsItemDto> CreateNewsAsync(UpsertNewsRequest req, CancellationToken ct = default)
    {
        ValidateNews(req);
        var item = new NewsItem
        {
            Title = req.Title.Trim(),
            Body = req.Body.Trim(),
            Language = NormalizeLang(req.Language),
            Author = string.IsNullOrWhiteSpace(req.Author) ? null : req.Author.Trim(),
            IsPublished = req.IsPublished,
            SortOrder = req.SortOrder,
            PublishedAt = req.PublishedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _news.AddAsync(item, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<NewsItemDto> UpdateNewsAsync(Guid id, UpsertNewsRequest req, CancellationToken ct = default)
    {
        ValidateNews(req);
        var item = await _news.GetByIdAsync(id, ct)
                   ?? throw new AppException("Haber bulunamadı.", 404);
        item.Title = req.Title.Trim();
        item.Body = req.Body.Trim();
        item.Language = NormalizeLang(req.Language);
        item.Author = string.IsNullOrWhiteSpace(req.Author) ? null : req.Author.Trim();
        item.IsPublished = req.IsPublished;
        item.SortOrder = req.SortOrder;
        if (req.PublishedAt.HasValue)
            item.PublishedAt = req.PublishedAt.Value;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await _news.UpdateAsync(item, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task DeleteNewsAsync(Guid id, CancellationToken ct = default)
    {
        _ = await _news.GetByIdAsync(id, ct)
            ?? throw new AppException("Haber bulunamadı.", 404);
        await _news.DeleteAsync(id, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<ClientVersionDto> GetVersionAsync(string? channel, CancellationToken ct = default)
    {
        var ch = NormalizeChannel(channel);
        var v = await _versions.GetAsync(ch, ct)
                ?? throw new AppException("Sürüm bilgisi bulunamadı.", 404);
        return ToDto(v);
    }

    public async Task<ClientVersionDto> UpsertVersionAsync(UpsertClientVersionRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Version))
            throw new AppException("Sürüm zorunlu.", 400);
        if (string.IsNullOrWhiteSpace(req.PatchUrl))
            throw new AppException("PatchUrl zorunlu.", 400);
        if (string.IsNullOrWhiteSpace(req.Sha256) || req.Sha256.Trim().Length != 64)
            throw new AppException("SHA-256 64 hex karakter olmalı.", 400);
        if (!req.Sha256.Trim().All(Uri.IsHexDigit))
            throw new AppException("SHA-256 yalnızca hex karakter içermeli.", 400);

        var entity = new ClientVersion
        {
            Channel = NormalizeChannel(req.Channel),
            Version = req.Version.Trim(),
            PatchUrl = req.PatchUrl.Trim(),
            Sha256 = req.Sha256.Trim().ToLowerInvariant(),
            PatchSizeBytes = Math.Max(0, req.PatchSizeBytes ?? 0),
            ReleaseNotes = string.IsNullOrWhiteSpace(req.ReleaseNotes) ? null : req.ReleaseNotes.Trim(),
            Mandatory = req.Mandatory,
            PublishedAt = DateTimeOffset.UtcNow
        };
        await _versions.UpsertAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    private static void ValidateNews(UpsertNewsRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            throw new AppException("Başlık zorunlu.", 400);
        if (string.IsNullOrWhiteSpace(req.Body))
            throw new AppException("İçerik zorunlu.", 400);
    }

    private static string NormalizeLang(string? lang) =>
        string.IsNullOrWhiteSpace(lang) ? "tr" : lang.Trim().ToLowerInvariant();

    private static string NormalizeChannel(string? channel) =>
        string.IsNullOrWhiteSpace(channel) ? "stable" : channel.Trim().ToLowerInvariant();

    private static NewsItemDto ToDto(NewsItem n) =>
        new(n.Id, n.Title, n.Body, n.Language, n.Author, n.PublishedAt, n.SortOrder);

    private static ClientVersionDto ToDto(ClientVersion v) =>
        new(v.Channel, v.Version, v.PatchUrl, v.Sha256, v.PatchSizeBytes, v.ReleaseNotes, v.Mandatory, v.PublishedAt);
}
