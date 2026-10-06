using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.Audio
{
    [Serializable]
    public sealed class RadioLine
    {
        public string key;
        public string role;
        public string text;
        public string tone;
        public float dur;
    }

    [Serializable]
    internal sealed class RadioLineFile
    {
        public RadioLine[] lines;
    }

    /// <summary>
    /// Telsiz replik kitabı (Resources/Audio/radio_lines.json). Anahtara tam eşleşme ya da "anahtar_" önekiyle
    /// seçim yapar; konuşan role göre filtreler. Saf mantık (sahne gerektirmez).
    /// </summary>
    public sealed class RadioLineBook
    {
        public const string ResourcePath = "Audio/radio_lines";

        private readonly List<RadioLine> _lines = new List<RadioLine>(256);
        private readonly List<RadioLine> _scratch = new List<RadioLine>(32);

        public int Count => _lines.Count;

        public static RadioLineBook FromJson(string json)
        {
            var book = new RadioLineBook();
            if (string.IsNullOrEmpty(json))
                return book;

            try
            {
                var file = JsonUtility.FromJson<RadioLineFile>(json);
                if (file?.lines != null)
                {
                    for (var i = 0; i < file.lines.Length; i++)
                    {
                        var l = file.lines[i];
                        if (l != null && !string.IsNullOrEmpty(l.key) && !string.IsNullOrEmpty(l.text))
                            book._lines.Add(l);
                    }
                }
            }
            catch (Exception)
            {
                // Bozuk dosya: boş kitap (sistem sessiz kalır).
            }

            return book;
        }

        public static RadioLineBook LoadFromResources()
        {
            try
            {
                var asset = Resources.Load<TextAsset>(ResourcePath);
                return FromJson(asset != null ? asset.text : null);
            }
            catch (Exception)
            {
                return new RadioLineBook();
            }
        }

        public static bool Matches(string lineKey, string key)
        {
            if (string.IsNullOrEmpty(lineKey) || string.IsNullOrEmpty(key))
                return false;

            return lineKey == key || (lineKey.Length > key.Length && lineKey[key.Length] == '_' && lineKey.StartsWith(key, StringComparison.Ordinal));
        }

        /// <summary>
        /// Anahtarlardan birini seçer. <paramref name="role"/> doluysa yalnızca o rolün repliklerini dener
        /// (<paramref name="anyRoleFallback"/> true ise bulunamazsa herhangi bir rolden). <paramref name="roll"/> 0..1.
        /// </summary>
        public RadioLine Pick(IList<string> keys, string role, float roll, bool anyRoleFallback = false)
        {
            if (keys == null || keys.Count == 0 || _lines.Count == 0)
                return null;

            Collect(keys, role);
            if (_scratch.Count == 0 && anyRoleFallback && !string.IsNullOrEmpty(role))
                Collect(keys, null);
            if (_scratch.Count == 0)
                return null;

            var index = Mathf.Clamp((int)(Mathf.Clamp01(roll) * _scratch.Count), 0, _scratch.Count - 1);
            return _scratch[index];
        }

        public RadioLine Pick(string key, string role, float roll, bool anyRoleFallback = false)
        {
            return Pick(new[] { key }, role, roll, anyRoleFallback);
        }

        public bool Has(string key, string role)
        {
            Collect(new[] { key }, role);
            return _scratch.Count > 0;
        }

        private void Collect(IList<string> keys, string role)
        {
            _scratch.Clear();
            for (var i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                if (!string.IsNullOrEmpty(role) && !string.Equals(line.role, role, StringComparison.Ordinal))
                    continue;

                for (var k = 0; k < keys.Count; k++)
                {
                    if (Matches(line.key, keys[k]))
                    {
                        _scratch.Add(line);
                        break;
                    }
                }
            }
        }
    }
}
