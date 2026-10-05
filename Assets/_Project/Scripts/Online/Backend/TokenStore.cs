using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Project.Online.Backend
{
    /// <summary>
    /// JWT + refresh token'ı makine anahtarıyla AES şifreleyip PlayerPrefs'te saklar.
    /// </summary>
    public sealed class TokenStore
    {
        public const string PrefsKeyAccess = "harekat.online.access.v1";
        public const string PrefsKeyRefresh = "harekat.online.refresh.v1";
        public const string PrefsKeyExpires = "harekat.online.expires.v1";

        private readonly Func<byte[]> _keyProvider;

        public TokenStore(Func<byte[]> keyProvider = null)
        {
            _keyProvider = keyProvider ?? DeriveMachineKey;
        }

        public string AccessToken { get; private set; }
        public string RefreshToken { get; private set; }
        public DateTimeOffset ExpiresAt { get; private set; }

        public bool HasRefreshToken => !string.IsNullOrEmpty(RefreshToken);
        public bool HasAccessToken => !string.IsNullOrEmpty(AccessToken);

        public bool IsAccessExpired(TimeSpan skew)
        {
            if (!HasAccessToken)
                return true;
            return DateTimeOffset.UtcNow + skew >= ExpiresAt;
        }

        public void Load()
        {
            AccessToken = DecryptPref(PrefsKeyAccess);
            RefreshToken = DecryptPref(PrefsKeyRefresh);
            var expRaw = PlayerPrefs.GetString(PrefsKeyExpires, "");
            if (!DateTimeOffset.TryParse(expRaw, out var exp))
                exp = DateTimeOffset.MinValue;
            ExpiresAt = exp;
        }

        public void Save(string accessToken, string refreshToken, DateTimeOffset expiresAt)
        {
            AccessToken = accessToken ?? "";
            RefreshToken = refreshToken ?? "";
            ExpiresAt = expiresAt;
            EncryptPref(PrefsKeyAccess, AccessToken);
            EncryptPref(PrefsKeyRefresh, RefreshToken);
            PlayerPrefs.SetString(PrefsKeyExpires, expiresAt.ToString("o"));
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            AccessToken = "";
            RefreshToken = "";
            ExpiresAt = DateTimeOffset.MinValue;
            PlayerPrefs.DeleteKey(PrefsKeyAccess);
            PlayerPrefs.DeleteKey(PrefsKeyRefresh);
            PlayerPrefs.DeleteKey(PrefsKeyExpires);
            PlayerPrefs.Save();
        }

        public static byte[] DeriveMachineKey()
        {
            var seed = (SystemInfo.deviceUniqueIdentifier ?? "harekat")
                       + "|" + UnityEngine.Application.identifier
                       + "|HAREKAT.ONLINE.TOKEN.V1";
            using (var sha = SHA256.Create())
                return sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
        }

        /// <summary>Testler için: sabit anahtarla şifrele/çöz.</summary>
        public static string EncryptToBase64(string plain, byte[] key)
        {
            if (string.IsNullOrEmpty(plain))
                return "";

            using (var aes = Aes.Create())
            {
                aes.Key = NormalizeKey(key);
                aes.GenerateIV();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                {
                    var plainBytes = Encoding.UTF8.GetBytes(plain);
                    var cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                    var payload = new byte[aes.IV.Length + cipher.Length];
                    Buffer.BlockCopy(aes.IV, 0, payload, 0, aes.IV.Length);
                    Buffer.BlockCopy(cipher, 0, payload, aes.IV.Length, cipher.Length);
                    return Convert.ToBase64String(payload);
                }
            }
        }

        public static string DecryptFromBase64(string cipherBase64, byte[] key)
        {
            if (string.IsNullOrEmpty(cipherBase64))
                return "";

            try
            {
                var payload = Convert.FromBase64String(cipherBase64);
                if (payload.Length < 17)
                    return "";

                using (var aes = Aes.Create())
                {
                    aes.Key = NormalizeKey(key);
                    var iv = new byte[16];
                    Buffer.BlockCopy(payload, 0, iv, 0, 16);
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    var cipherLen = payload.Length - 16;
                    var cipher = new byte[cipherLen];
                    Buffer.BlockCopy(payload, 16, cipher, 0, cipherLen);

                    using (var decryptor = aes.CreateDecryptor())
                    {
                        var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                        return Encoding.UTF8.GetString(plain);
                    }
                }
            }
            catch (Exception)
            {
                return "";
            }
        }

        private void EncryptPref(string key, string plain)
        {
            var encrypted = EncryptToBase64(plain, _keyProvider());
            PlayerPrefs.SetString(key, encrypted);
        }

        private string DecryptPref(string key)
        {
            var raw = PlayerPrefs.GetString(key, "");
            return DecryptFromBase64(raw, _keyProvider());
        }

        private static byte[] NormalizeKey(byte[] key)
        {
            if (key == null || key.Length == 0)
                return DeriveMachineKey();

            if (key.Length == 32)
                return key;

            using (var sha = SHA256.Create())
                return sha.ComputeHash(key);
        }
    }
}
