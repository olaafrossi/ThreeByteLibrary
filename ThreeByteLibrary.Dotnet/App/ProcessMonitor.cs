using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
// ReSharper disable CheckNamespace
// ReSharper disable once ArrangeModifiersOrder

namespace ThreeByteLibrary.Dotnet
{
    public class ProcessMonitor : IDisposable, IProcessMonitor
    {
        //private static readonly ILog log = LogManager.GetLogger(typeof(ProcessMonitor));

        public string ProcessName { get; private set; }
        public string ExecutionString { get; private set; }

        public int MaxResourceSnapshots { get; set; }
        public TimeSpan ResourceSnapshotInterval { get; set; }
        public TimeSpan UnresponsiveTimeout { get; set; }

        public event EventHandler<ProcessEventArgs> ProcessEvent;

        public event EventHandler<ResourceSnapshot> ResourceEvent;

        private void RaiseProcessEvent(string message)
        {
            Log.Logger.Debug("Process Event [{0}]", message);
            if (ProcessEvent != null)
            {
                ProcessEvent(this, new ProcessEventArgs(message));
            }
        }


        public event EventHandler ProcessExited;

        private void RaiseProcessExited()
        {
            if (ProcessExited != null)
            {
                ProcessExited(this, EventArgs.Empty);
            }
        }

        public ProcessMonitor(string processName, string executionString)
        {
            ProcessName = processName;
            ExecutionString = executionString;

            //Reasonable defaults
            MaxResourceSnapshots = 1000;
            ResourceSnapshotInterval = TimeSpan.FromSeconds(15);
            UnresponsiveTimeout = TimeSpan.FromMinutes(1);

            ThreadPool.QueueUserWorkItem(MonitorProcess);
        }

        private Process _monitoringProcess;
        private readonly object _monitoringProcessLock = new object();

        private Queue<ResourceSnapshot> _resourceSnapshots = new Queue<ResourceSnapshot>();

        private void MonitorProcess(object state)
        {

            try
            {
                string executionString = ExecutionString;
                Log.Logger.Information("Launching {0}", executionString);
                while (!_disposed)
                {
                    Process p = new Process();
                    p.StartInfo = new ProcessStartInfo(executionString);
                    p.Start();
                    Thread.Sleep(5000);  //Wait at least 5 seconds before doing anything else to prevent spiraling out of control

                    //Look for process
                    foreach (Process proc in Process.GetProcesses())
                    {
                        if (proc.ProcessName.StartsWith(ProcessName))
                        {
                            Log.Logger.Information("Found Monitor Process: {0}", proc.ProcessName);
                            lock (_monitoringProcessLock)
                            {
                                _monitoringProcess = proc;
                            }

                            ClearSnapshotHistory();
                            DateTime unresponsiveTime = DateTime.Now;
                            bool exited = proc.WaitForExit((int)(ResourceSnapshotInterval.TotalMilliseconds));
                            while (!exited)
                            {
                                //The process is still running.  This is good, just take a snapshot of it
                                lock (_monitoringProcessLock)
                                {
                                    ResourceSnapshot snapshot = LogResourceSnapshot(_monitoringProcess);
                                    ResourceEvent(this, snapshot);
                                    if (snapshot.IsNotResponding)
                                    {
                                        if (DateTime.Now >= unresponsiveTime + UnresponsiveTimeout)
                                        {
                                            Log.Logger.Warning("Application is not responding.  Will kill.");
                                            Kill();
                                        }
                                    }
                                    else
                                    {
                                        unresponsiveTime = DateTime.Now;
                                    }
                                }
                                exited = proc.WaitForExit((int)(ResourceSnapshotInterval.TotalMilliseconds));
                            }

                            //Once the process exits, then dump some stats and restart it
                            lock (_monitoringProcessLock)
                            {
                                RaiseProcessExited();
                                _monitoringProcess = null;
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                RaiseProcessEvent(string.Format("Error with program: {0}", ex.Message));
            }

        }


        public ResourceSnapshot LogResourceSnapshot(Process proc)
        {
            proc.Refresh();
            ResourceSnapshot snapshot = ResourceSnapshot.FromProcess(proc);
            Log.Logger.Debug("Process Snapshot: {0}", snapshot);

            lock (_resourceSnapshots)
            {
                _resourceSnapshots.Enqueue(snapshot);
                while (_resourceSnapshots.Count > MaxResourceSnapshots)
                {
                    _resourceSnapshots.Dequeue(); //Just abandon old resource snapshots
                }
            }
            return snapshot;
        }

        public IEnumerable<ResourceSnapshot> GetSnapshotHistory()
        {
            //Make sure that you do not give access to the underlying queue
            List<ResourceSnapshot> snapshots;
            lock (_resourceSnapshots)
            {
                snapshots = _resourceSnapshots.Reverse().ToList();
            }

            foreach (ResourceSnapshot s in snapshots)
            {
                yield return s;
            }
        }

        private void ClearSnapshotHistory()
        {
            lock (_resourceSnapshots)
            {
                _resourceSnapshots.Clear();
            }
        }

        public void Kill()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("ProcessMonitor");
            }
            try
            {
                lock (_monitoringProcessLock)
                {
                    _monitoringProcess.Kill();
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error("Cannot kill process: {0}", ex);
            }
        }

        private bool _disposed = false;
        public void Dispose()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("ProcessMonitor");
            }
            _disposed = true;
            //Will cause the background thread to exit
        }
    }

    public class ProcessEventArgs : EventArgs
    {
        public string Message { get; private set; }

        public ProcessEventArgs(string message)
        {
            Message = message;
        }
    }

    public class ResourceSnapshot
    {
        public DateTime Timestamp { get; private set; }
        public long PeakPagedMemorySize { get; private set; }
        public long PeakWorkingSet { get; private set; }
        public long PrivateMemorySize { get; private set; }
        public int ThreadCount { get; private set; }
        public int HandleCount { get; private set; }
        public bool IsNotResponding { get; private set; }

        private ResourceSnapshot()
        {
            Timestamp = DateTime.Now;
        }

        public static ResourceSnapshot FromProcess(Process process)
        {
            ResourceSnapshot newSnapshot = new ResourceSnapshot()
            {
                PeakPagedMemorySize = process.PeakPagedMemorySize64,
                PeakWorkingSet = process.PeakWorkingSet64,
                PrivateMemorySize = process.PrivateMemorySize64,
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount,
                IsNotResponding = !process.Responding
            };
            return newSnapshot;
        }

        public override string ToString()
        {
            return string.Format("{0} - PeakPaged: {1} PeakWorking: {2} PrivateMemory: {3} ThreadCount: {4} HandleCount: {5} IsNotResponding: {6}",
                Timestamp, PeakPagedMemorySize, PeakWorkingSet, PrivateMemorySize, ThreadCount, HandleCount, IsNotResponding);
        }
    }
}