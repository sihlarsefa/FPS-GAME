using Project.Core.Interfaces;
using UnityEngine;

namespace Project.Infrastructure.Persistence
{
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        private const string Prefix = "harekat.";

        public bool HasKey(string key) => PlayerPrefs.HasKey(Prefix + key);
        public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(Prefix + key, fallback);
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(Prefix + key, fallback);
        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(Prefix + key, value);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(Prefix + key, value);
        public void Save() => PlayerPrefs.Save();
    }
}
