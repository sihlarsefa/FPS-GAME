using System;
using System.Globalization;

namespace Project.Infrastructure.Diagnostics
{
    /// <summary>Diagnostics için hafif komut satırı okuyucu (Presentation'a bağımlı değil).</summary>
    public static class DiagnosticsCommandLine
    {
        private static string[] _args;

        public static string[] Args
        {
            get
            {
                if (_args == null)
                    _args = Environment.GetCommandLineArgs() ?? Array.Empty<string>();
                return _args;
            }
        }

        public static bool HasArg(string name)
        {
            var args = Args;
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static bool TryGetArg(string name, out string value)
        {
            var args = Args;
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    value = args[i + 1];
                    return !string.IsNullOrEmpty(value);
                }
            }

            value = null;
            return false;
        }

        public static bool TryGetInt(string name, out int value)
        {
            value = 0;
            return TryGetArg(name, out var text)
                   && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryGetFloat(string name, out float value)
        {
            value = 0f;
            return TryGetArg(name, out var text)
                   && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Testlerde argümanları zorlamak için.</summary>
        public static void OverrideArgsForTests(string[] args) => _args = args ?? Array.Empty<string>();
    }
}
