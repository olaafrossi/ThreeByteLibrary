// Created by Three Byte Intemedia, Inc. | project: PCController |
// Created: 2021 03 16
// by Olaaf Rossi

using System;
using System.Collections.Generic;

namespace ThreeByteLibrary.Dotnet
{
    public interface IProcessMonitor
    {
        string ProcessName { get; }
        string ExecutionString { get; }
        int MaxResourceSnapshots { get; set; }
        TimeSpan ResourceSnapshotInterval { get; set; }
        TimeSpan UnresponsiveTimeout { get; set; }
        event EventHandler<ProcessEventArgs> ProcessEvent;
        event EventHandler ProcessExited;
        IEnumerable<ResourceSnapshot> GetSnapshotHistory();
        void Kill();
        void Dispose();
    }
}