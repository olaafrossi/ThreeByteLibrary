// Created by Three Byte Intemedia, Inc. | project: PCController |
// Created: 2021 04 01
// by Olaaf Rossi

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ThreeByteLibrary.Dotnet
{
    public interface IProcessMonitor
    {
        string ProcessName { get; }
        string ExecutionString { get; }
        int MaxResourceSnapshots { get; set; }
        TimeSpan ResourceSnapshotInterval { get; set; }
        TimeSpan UnresponsiveTimeout { get; set; }
        int TakeResourceSnapShotInSeconds { get; set; }
        event EventHandler<ProcessEventArgs> ProcessEvent;
        event EventHandler<ResourceSnapshot> ResourceEvent;
        event EventHandler ProcessExited;
        ResourceSnapshot LogResourceSnapshot(Process proc);
        IEnumerable<ResourceSnapshot> GetSnapshotHistory();
        void Kill();
        void Dispose();
    }
}