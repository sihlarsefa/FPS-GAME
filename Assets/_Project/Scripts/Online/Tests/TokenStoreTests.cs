using System;
using NUnit.Framework;
using Project.Online.Backend;
using UnityEngine;

namespace Project.Online.Tests
{
    [TestFixture]
    public sealed class TokenStoreTests
    {
        private static readonly byte[] TestKey = new byte[32];

        [SetUp]
        public void SetUp()
        {
            for (var i = 0; i < TestKey.Length; i++)
                TestKey[i] = (byte)(i + 3);
            ClearPrefs();
        }

        [TearDown]
        public void TearDown()
        {
            ClearPrefs();
        }

        [Test]
        public void EncryptDecrypt_RoundTrip()
        {
            var cipher = TokenStore.EncryptToBase64("secret-token", TestKey);
            Assert.IsFalse(string.IsNullOrEmpty(cipher));
            Assert.AreNotEqual("secret-token", cipher);
            Assert.AreEqual("secret-token", TokenStore.DecryptFromBase64(cipher, TestKey));
        }

        [Test]
        public void SaveLoadClear_PersistsTokens()
        {
            var store = new TokenStore(() => TestKey);
            var expires = DateTimeOffset.UtcNow.AddHours(1);
            store.Save("access-A", "refresh-R", expires);

            Assert.IsTrue(store.HasAccessToken);
            Assert.IsTrue(store.HasRefreshToken);
            Assert.IsFalse(store.IsAccessExpired(TimeSpan.FromSeconds(30)));

            var reloaded = new TokenStore(() => TestKey);
            reloaded.Load();
            Assert.AreEqual("access-A", reloaded.AccessToken);
            Assert.AreEqual("refresh-R", reloaded.RefreshToken);
            Assert.AreEqual(expires.ToString("o"), reloaded.ExpiresAt.ToString("o"));

            reloaded.Clear();
            Assert.IsFalse(reloaded.HasAccessToken);
            Assert.IsFalse(reloaded.HasRefreshToken);

            var empty = new TokenStore(() => TestKey);
            empty.Load();
            Assert.IsFalse(empty.HasAccessToken);
        }

        [Test]
        public void IsAccessExpired_RespectsSkew()
        {
            var store = new TokenStore(() => TestKey);
            store.Save("a", "r", DateTimeOffset.UtcNow.AddSeconds(10));
            Assert.IsTrue(store.IsAccessExpired(TimeSpan.FromSeconds(30)));
            Assert.IsFalse(store.IsAccessExpired(TimeSpan.FromSeconds(5)));
        }

        [Test]
        public void EnsureFreshTokenAsync_NoRefresh_ReturnsFalse()
        {
            var tokens = new TokenStore(() => TestKey);
            tokens.Clear();
            using (var client = new BackendClient("http://127.0.0.1:9", tokens, timeoutSeconds: 5, maxRetries: 0))
            {
                var ok = client.EnsureFreshTokenAsync().GetAwaiter().GetResult();
                Assert.IsFalse(ok);
            }
        }

        [Test]
        public void EnsureFreshTokenAsync_ValidAccess_ReturnsTrueWithoutRefresh()
        {
            var tokens = new TokenStore(() => TestKey);
            tokens.Save("live-access", "refresh", DateTimeOffset.UtcNow.AddHours(2));
            using (var client = new BackendClient("http://127.0.0.1:9", tokens, timeoutSeconds: 5, maxRetries: 0))
            {
                var ok = client.EnsureFreshTokenAsync().GetAwaiter().GetResult();
                Assert.IsTrue(ok);
                Assert.AreEqual("live-access", client.Tokens.AccessToken);
            }
        }

        private static void ClearPrefs()
        {
            PlayerPrefs.DeleteKey(TokenStore.PrefsKeyAccess);
            PlayerPrefs.DeleteKey(TokenStore.PrefsKeyRefresh);
            PlayerPrefs.DeleteKey(TokenStore.PrefsKeyExpires);
            PlayerPrefs.Save();
        }
    }
}
