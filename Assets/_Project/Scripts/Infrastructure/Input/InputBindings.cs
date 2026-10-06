using System;
using Project.Application.Services;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Infrastructure.Input
{
    /// <summary>Tuş atamaları (PlayerPrefs JSON) ve gamepad hassasiyeti. Okuyucu ve ayar paneli ortak kullanır.</summary>
    public static class InputBindings
    {
        private const string PrefsKey = "input.bindings.v1";
        private const string StickKey = "input.stickSensitivity";
        public const float MinStick = 0.2f;
        public const float MaxStick = 3f;

        [Serializable] private sealed class Dto { public string data; }

        private static InputBindingMap _map;
        private static float _stick = -1f;

        public static event Action Changed;

        public static InputBindingMap Map => _map ??= Load();

        public static float StickSensitivity
        {
            get
            {
                if (_stick < 0f)
                {
                    try { _stick = PlayerPrefs.GetFloat(StickKey, 1f); } catch { _stick = 1f; }
                    _stick = Mathf.Clamp(_stick, MinStick, MaxStick);
                }
                return _stick;
            }
            set
            {
                _stick = Mathf.Clamp(value, MinStick, MaxStick);
                try { PlayerPrefs.SetFloat(StickKey, _stick); PlayerPrefs.Save(); } catch { }
            }
        }

        private static InputBindingMap Load()
        {
            try
            {
                var json = PlayerPrefs.GetString(PrefsKey, string.Empty);
                if (!string.IsNullOrEmpty(json))
                    return InputBindingMap.Deserialize(JsonUtility.FromJson<Dto>(json)?.data);
            }
            catch { }
            return new InputBindingMap();
        }

        public static void Save()
        {
            try
            {
                PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(new Dto { data = Map.Serialize() }));
                PlayerPrefs.Save();
            }
            catch { }
            Changed?.Invoke();
        }

        public static void Assign(BindAction action, Key key)
        {
            Map.Set(action, key.ToString());
            Save();
        }

        public static void ResetDefaults()
        {
            Map.ResetToDefaults();
            StickSensitivity = 1f;
            Save();
        }

        public static bool Held(BindAction action)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            foreach (var name in Map.Get(action))
                if (Enum.TryParse(name, out Key k) && k != Key.None && kb[k].isPressed) return true;
            return false;
        }

        public static bool Pressed(BindAction action)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            foreach (var name in Map.Get(action))
                if (Enum.TryParse(name, out Key k) && k != Key.None && kb[k].wasPressedThisFrame) return true;
            return false;
        }

        /// <summary>Eylemin ilk atanmış tuşunun görünen adı (ör. "F", "Boşluk"); atama yoksa "-".</summary>
        public static string KeyLabel(BindAction action)
        {
            foreach (var name in Map.Get(action))
            {
                if (!Enum.TryParse(name, out Key k) || k == Key.None) continue;
                switch (k)
                {
                    case Key.Space: return "Boşluk";
                    case Key.LeftShift: case Key.RightShift: return "Shift";
                    case Key.LeftCtrl: case Key.RightCtrl: return "Ctrl";
                    case Key.LeftAlt: case Key.RightAlt: return "Alt";
                    case Key.Tab: return "Tab";
                    case Key.Enter: return "Enter";
                }
                var kb = Keyboard.current;
                var d = kb != null ? kb[k].displayName : null;
                return string.IsNullOrEmpty(d) ? name : d.ToUpperInvariant();
            }
            return "-";
        }

        /// <summary>"[F]" gibi köşeli tuş etiketi.</summary>
        public static string Bracket(BindAction action) => "[" + KeyLabel(action) + "]";

        /// <summary>Bu karede basılan ilk klavye tuşu (Esc dahil); yoksa Key.None.</summary>
        public static Key CapturePressedKey()
        {
            var kb = Keyboard.current;
            if (kb == null) return Key.None;
            foreach (var control in kb.allKeys)
                if (control.wasPressedThisFrame) return control.keyCode;
            return Key.None;
        }
    }
}
