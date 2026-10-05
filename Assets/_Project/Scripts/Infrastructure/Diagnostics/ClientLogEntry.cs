using System;
using UnityEngine;

namespace Project.Infrastructure.Diagnostics
{
    /// <summary>Halka tamponda tutulan tek log satırı.</summary>
    public readonly struct ClientLogEntry
    {
        public readonly DateTimeOffset Timestamp;
        public readonly LogType Type;
        public readonly string Message;
        public readonly string StackTrace;

        public ClientLogEntry(DateTimeOffset timestamp, LogType type, string message, string stackTrace)
        {
            Timestamp = timestamp;
            Type = type;
            Message = message ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
        }

        public override string ToString()
        {
            var stamp = Timestamp.ToString("o");
            if (string.IsNullOrEmpty(StackTrace))
                return $"[{stamp}] {Type}: {Message}";
            return $"[{stamp}] {Type}: {Message}\n{StackTrace}";
        }
    }
}
