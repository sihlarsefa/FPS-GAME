using System;
using System.Collections.Generic;

namespace Project.Infrastructure.Diagnostics
{
    /// <summary>
    /// İstemci hata/çökme raporu — Backend <c>POST /client-errors</c> gövdesiyle birebir.
    /// </summary>
    [Serializable]
    public sealed class ClientErrorReport
    {
        public string Id;
        public string Trigger;
        public string Version;
        public string Scene;
        public string Platform;
        public string DeviceModel;
        public string OperatingSystem;
        public string ProcessorType;
        public int ProcessorCount;
        public int SystemMemoryMb;
        public string GraphicsDeviceName;
        public int GraphicsMemoryMb;
        public string UnityVersion;
        public string ExceptionType;
        public string Message;
        public string StackTrace;
        public string[] RecentLogs;
        public string CreatedAtUtc;
    }

    [Serializable]
    public sealed class ClientErrorReportList
    {
        public List<ClientErrorReport> Items = new();
    }
}
