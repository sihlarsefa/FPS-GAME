using Harekat.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Harekat.Infrastructure.Security;

/// <summary>Geliştirme için sahte e-posta servisi — token'ı loga yazar.</summary>
public sealed class FakeEmailService : IEmailService
{
    private readonly ILogger<FakeEmailService> _logger;

    public FakeEmailService(ILogger<FakeEmailService> logger) => _logger = logger;

    public Task SendVerificationAsync(string email, string token, CancellationToken ct = default)
    {
        _logger.LogInformation("E-posta doğrulama [{Email}] token={Token}", email, token);
        return Task.CompletedTask;
    }
}
