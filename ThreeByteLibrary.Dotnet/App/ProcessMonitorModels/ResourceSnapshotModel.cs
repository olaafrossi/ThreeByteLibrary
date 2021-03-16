using System;
using System.Diagnostics;

namespace ThreeByte.App
{
    public class ResourceSnapshotModel
    {
        private ResourceSnapshotModel()
        {
            Timestamp = DateTime.Now;
        }

        public DateTime Timestamp { get; }
        public long PeakPagedMemorySize { get; private set; }
        public long PeakWorkingSet { get; private set; }
        public long PrivateMemorySize { get; private set; }
        public int ThreadCount { get; private set; }
        public int HandleCount { get; private set; }
        public bool IsNotResponding { get; private set; }

        public static ResourceSnapshotModel FromProcess(Process process)
        {
            ResourceSnapshotModel newSnapshotModel = new ResourceSnapshotModel
            {
                PeakPagedMemorySize = process.PeakPagedMemorySize64,
                PeakWorkingSet = process.PeakWorkingSet64,
                PrivateMemorySize = process.PrivateMemorySize64,
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount,
                IsNotResponding = !process.Responding
            };
            return newSnapshotModel;
        }

        public override string ToString()
        {
            return string.Format(
                "{0} - PeakPaged: {1} PeakWorking: {2} PrivateMemory: {3} ThreadCount: {4} HandleCount: {5} IsNotResponding: {6}",
                Timestamp, PeakPagedMemorySize, PeakWorkingSet, PrivateMemorySize, ThreadCount, HandleCount,
                IsNotResponding);
        }
    }
}