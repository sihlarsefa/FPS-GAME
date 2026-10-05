using System;
using System.Text.RegularExpressions;

namespace Project.Online.Backend
{
    /// <summary>HTTP / API hatalarını Türkçe kullanıcı mesajına çevirir.</summary>
    public static class BackendErrors
    {
        public static string ToTurkish(int statusCode, string body)
        {
            var detail = ExtractMessage(body);

            switch (statusCode)
            {
                case 0:
                    return "Sunucuya bağlanılamadı. İnternet bağlantınızı kontrol edin.";
                case 400:
                    return string.IsNullOrEmpty(detail)
                        ? "İstek geçersiz. Bilgileri kontrol edin."
                        : detail;
                case 401:
                    return string.IsNullOrEmpty(detail)
                        ? "Oturum geçersiz veya süresi doldu. Tekrar giriş yapın."
                        : detail;
                case 403:
                    return "Bu işlem için yetkiniz yok.";
                case 404:
                    return string.IsNullOrEmpty(detail) ? "Kayıt bulunamadı." : detail;
                case 409:
                    return string.IsNullOrEmpty(detail)
                        ? "Çakışma: kullanıcı adı veya e-posta zaten kayıtlı."
                        : detail;
                case 429:
                    return "Çok fazla istek. Biraz bekleyip tekrar deneyin.";
                case 500:
                case 502:
                case 503:
                    return "Sunucu geçici olarak yanıt vermiyor. Daha sonra tekrar deneyin.";
                case 408:
                    return "İstek zaman aşımına uğradı.";
                default:
                    if (statusCode >= 500)
                        return "Sunucu hatası (" + statusCode + ").";
                    if (!string.IsNullOrEmpty(detail))
                        return detail;
                    return "Beklenmeyen hata (HTTP " + statusCode + ").";
            }
        }

        public static string NetworkFailure(string reason)
        {
            if (string.IsNullOrEmpty(reason))
                return "Ağ hatası.";
            if (reason.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                return "İstek zaman aşımına uğradı.";
            return "Ağ hatası: bağlantı kurulamadı.";
        }

        private static string ExtractMessage(string body)
        {
            if (string.IsNullOrEmpty(body))
                return null;

            // {"message":"..."} veya {"title":"...","detail":"..."} (ProblemDetails)
            if (BackendJson.TryGetString(body, "detail", out var detail) && !string.IsNullOrEmpty(detail))
                return Sanitize(detail);
            if (BackendJson.TryGetString(body, "message", out var message) && !string.IsNullOrEmpty(message))
                return Sanitize(message);
            if (BackendJson.TryGetString(body, "title", out var title) && !string.IsNullOrEmpty(title))
                return Sanitize(title);

            var trimmed = body.Trim();
            if (trimmed.Length > 0 && trimmed[0] != '{' && trimmed[0] != '[')
                return Sanitize(trimmed);

            return null;
        }

        private static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            text = Regex.Replace(text, @"\s+", " ").Trim();
            if (text.Length > 240)
                text = text.Substring(0, 237) + "...";
            return text;
        }
    }
}
