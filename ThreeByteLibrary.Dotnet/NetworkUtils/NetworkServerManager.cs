// Created by Three Byte Intemedia, Inc. | project: ThreeByteLibrary |
// Created: 2021 03 17
// by Olaaf Rossi

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Serilog;

namespace ThreeByteLibrary.Dotnet.NetworkUtils
{
    public class NetworkServerManager : IDisposable, INotifyPropertyChanged
    {
        private readonly ILogger log = Log.Logger;

        private IAsyncResult _acceptResult;


        private readonly bool _disposed = false;
        private readonly Timer _purgeTimer;
        private object _serverLock = new();


        private bool _stopped = true;

        private readonly TcpListener _tcpListener;

        public NetworkServerManager(int port)
        {
            Port = port;
            CurrentClients = new ObservableCollection<TcpClient>();

            _tcpListener = new TcpListener(IPAddress.Any, Port);
            _purgeTimer = new Timer(PurgeTimer_Tick, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        /// <summary>
        ///     Implementation of IDisposable interface.  Cancels the thread and releases resources.
        ///     Clients of this class are responsible for calling it.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return; //Dispose has already been called
            }

            log.Information("Cleaning up network resources");

            _purgeTimer.Change(TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
            _purgeTimer.Dispose();
            Stop();
        }

        //Observable Interface
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        public event EventHandler<TcpClientEventArgs> ClientConnected;
        public event EventHandler<TcpClientEventArgs> ClientPurged;

        public void Start()
        {
            log.Debug("Listener Start: " + Port);
            _stopped = false;
            try
            {
                _tcpListener.Start();
            }
            catch (Exception ex)
            {
                log.Error("Error starting listener", ex);
                Error = ex;
            }

            _acceptResult = _tcpListener.BeginAcceptTcpClient(AcceptCallback, _tcpListener);
        }

        private void AcceptCallback(IAsyncResult asyncResult)
        {
            log.Debug("Accept Callback");
            try
            {
                TcpListener listener = (TcpListener) asyncResult.AsyncState;
                TcpClient newClient = listener.EndAcceptTcpClient(asyncResult);
                lock (_clientLock)
                {
                    CurrentClients.Add(newClient);
                }

                if (ClientConnected != null)
                {
                    ClientConnected(this, new TcpClientEventArgs(newClient));
                }
            }
            catch (Exception ex)
            {
                log.Error("Error accepting client", ex);
                Error = ex;
            }

            if (!_stopped)
            {
                _acceptResult = _tcpListener.BeginAcceptTcpClient(AcceptCallback, _tcpListener);
            }
        }

        public void Stop()
        {
            _stopped = true;
            try
            {
                //if(_acceptResult != null) {
                //    _tcpListener.EndAcceptTcpClient(_acceptResult);
                //}
                //_acceptResult = null;

                _tcpListener.Stop();
                lock (_clientLock)
                {
                    foreach (TcpClient c in CurrentClients)
                    {
                        c.Client.Close();
                    }

                    CurrentClients.Clear();
                }
            }
            catch (Exception ex)
            {
                log.Error("Error stopping listener", ex);
                Error = ex;
            }
        }

        public void PurgeTimer_Tick(object state)
        {
            PurgeDisconnectedClients();
        }

        public void PurgeDisconnectedClients()
        {
            Collection<TcpClient> clientsToRemove = new Collection<TcpClient>();

            lock (_clientLock)
            {
                foreach (TcpClient c in CurrentClients)
                {
                    if (!c.Connected)
                    {
                        clientsToRemove.Add(c);
                    }
                }

                foreach (TcpClient c in clientsToRemove)
                {
                    CurrentClients.Remove(c);
                }
            }

            foreach (TcpClient c in clientsToRemove)
            {
                if (ClientPurged != null)
                {
                    ClientPurged(this, new TcpClientEventArgs(c));
                }
            }
        }

        public List<TcpClient> GetCurrentClientsList()
        {
            lock (_clientLock)
            {
                return CurrentClients.ToList();
            }
        }

        #region Public Properties

        public int Port { get; }

        public ObservableCollection<TcpClient> CurrentClients { get; }

        private readonly object _clientLock = new(); //Ensures serialized modification to CurrentClients collection

        private Exception _error;

        public Exception Error
        {
            get { return _error; }
            private set
            {
                Exception oldError = _error;
                _error = value;
                if (oldError != _error)
                {
                    NotifyPropertyChanged("Error");
                }
            }
        }

        #endregion //Public Properties
    }

    public class TcpClientEventArgs : EventArgs
    {
        public TcpClientEventArgs(TcpClient client)
        {
            Client = client;
        }

        public TcpClient Client { get; }
    }
}