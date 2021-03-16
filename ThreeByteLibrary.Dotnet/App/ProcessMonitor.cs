using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ThreeByte.App
{
    public class ProcessMonitor : IDisposable
    {
        private readonly IConfiguration _config;
        private readonly ILogger<ProcessMonitor> _log;
        private readonly object _monitoringProcessLock = new();

        private bool _disposed;

        private Process _monitoringProcess;

        private Queue<ResourceSnapshotModel> _resourceSnapshots = new Queue<ResourceSnapshotModel>();

        public ProcessMonitor(ILogger<ProcessMonitor> log, IConfiguration config)
        {
            _log = log;
            _config = config;

            //ProcessName = processName;
            //ExecutionString = executionString;

            //Reasonable defaults
            MaxResourceSnapshots = 1000;
            ResourceSnapshotInterval = TimeSpan.FromMinutes(5);
            UnresponsiveTimeout = TimeSpan.FromMinutes(1);

            ThreadPool.QueueUserWorkItem(MonitorProcess);
        }

        public string ProcessName { get; }
        public string ExecutionString { get; }

        public int MaxResourceSnapshots { get; set; }
        public TimeSpan ResourceSnapshotInterval { get; set; }
        public TimeSpan UnresponsiveTimeout { get; set; }

        public event EventHandler<ProcessMonitorMessages> MessageHit;

        public void Dispose()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("ProcessMonitor");
            }

            _disposed = true;
            //Will cause the background thread to exit
        }

        //public event EventHandler<ProcessEventArgs> ProcessEvent;

        //private void RaiseProcessEvent(string message)
        //{
        //    //log.DebugFormat("Process Event [{0}]", message);
        //    if (ProcessEvent != null)
        //    {
        //        ProcessEvent(this, new ProcessEventArgs(message));
        //    }
        //}

        public event EventHandler ProcessExited;

        private void RaiseProcessExited()
        {
            if (ProcessExited != null)
            {
                ProcessExited(this, EventArgs.Empty);
            }
        }

        private void MonitorProcess(object state)
        {
            try
            {
                string executionString = ExecutionString;
                //log.InfoFormat("Launching {0}", executionString);
                while (_disposed is false)
                {
                    Process p = new Process();
                    p.StartInfo = new ProcessStartInfo(executionString);
                    p.Start();
                    Thread.Sleep(
                        5000); //Wait at least 5 seconds before doing anything else to prevent spiraling out of control

                    //Look for process
                    foreach (Process proc in Process.GetProcesses())
                    {
                        if (proc.ProcessName.StartsWith(ProcessName))
                        {
                            //log.InfoFormat("Found Monitor Process: {0}", proc.ProcessName);
                            lock (_monitoringProcessLock)
                            {
                                _monitoringProcess = proc;
                            }

                            ClearSnapshotHistory();
                            DateTime unresponsiveTime = DateTime.Now;
                            bool exited = proc.WaitForExit((int) ResourceSnapshotInterval.TotalMilliseconds);
                            while (!exited)
                            {
                                //The process is still running.  This is good, just take a snapshot of it
                                lock (_monitoringProcessLock)
                                {
                                    ResourceSnapshotModel snapshotModel = LogResourceSnapshot(_monitoringProcess);
                                    if (snapshotModel.IsNotResponding)
                                    {
                                        if (DateTime.Now >= unresponsiveTime + UnresponsiveTimeout)
                                        {
                                            //log.WarnFormat("Application is not responding.  Will kill.");
                                            Kill();
                                        }
                                    }
                                    else
                                    {
                                        unresponsiveTime = DateTime.Now;
                                    }
                                }

                                exited = proc.WaitForExit((int) ResourceSnapshotInterval.TotalMilliseconds);
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
                //RaiseProcessEvent(string.Format("Error with program: {0}", ex.Message));
                // send a message back to the main class
            }
        }

        private ResourceSnapshotModel LogResourceSnapshot(Process proc)
        {
            proc.Refresh();
            ResourceSnapshotModel snapshotModel = ResourceSnapshotModel.FromProcess(proc);
            //log.DebugFormat("Process Snapshot: {0}", snapshotModel);

            lock (_resourceSnapshots)
            {
                _resourceSnapshots.Enqueue(snapshotModel);
                while (_resourceSnapshots.Count > MaxResourceSnapshots)
                {
                    _resourceSnapshots.Dequeue(); //Just abandon old resource snapshots
                }
            }

            return snapshotModel;
        }

        public IEnumerable<ResourceSnapshotModel> GetSnapshotHistory()
        {
            //Make sure that you do not give access to the underlying queue
            List<ResourceSnapshotModel> snapshots;
            lock (_resourceSnapshots)
            {
                snapshots = _resourceSnapshots.Reverse().ToList();
            }

            foreach (ResourceSnapshotModel s in snapshots)
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
                //log.Error("Cannot kill process: {0}", ex);
            }
        }

        private void LogToAll(Enum log, string message)
        {
            _log.LogInformation(message);

            ProcessMonitorMessages args = new ProcessMonitorMessages();
            args.UILogger = (ProcessMonitorMessages._UiLogger) log;
            args.Message = $"{DateTime.Now:HH:mm:ss.fff} | {message}";
            OnNewMessages(args);
        }

        protected virtual void OnNewMessages(ProcessMonitorMessages e)
        {
            EventHandler<ProcessMonitorMessages> handler = MessageHit;
            if (handler != null)
            {
                handler(this, e);
            }
        }
    }

    public class ProcessMonitorMessages : EventArgs
    {
        public enum _UiLogger
        {
            netLog,
            appLog
        }

        public _UiLogger UILogger { get; set; }
        public string Message { get; set; }
    }
}