// Created by Three Byte Intemedia, Inc. | project: PCController |
// Created: 2021 03 17
// by Olaaf Rossi

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using Serilog;

namespace ThreeByteLibrary.Dotnet.NetworkUtils
{
    public class AsyncUdpLink : IDisposable, INotifyPropertyChanged, IAsyncUdpLink
    {
        private const int MAX_DATA_SIZE = 100;
        private readonly object _clientLock = new();
        private readonly List<byte[]> _incomingData;
        private readonly ILogger log = Log.Logger; //TODO switch to Mvx logger
        private bool _disposed;
        private IAsyncResult _receiveResult;
        private IAsyncResult _sendResult;
        private UdpClient _udpClient;

        //Assume local and remote port should be the same
        public AsyncUdpLink(string address, int remotePort, int localPort = 0, bool enabled = true)
        {
            Address = address;
            Port = remotePort;
            LocalPort = localPort;

            _incomingData = new List<byte[]>();
            _udpClient = new UdpClient(localPort); //Typically don't bind to the same port that you send to

            Enabled = enabled; //Default is true

            ReceiveData(); //Start the listening process
        }


        /// <summary>
        ///     Asynchronously sends the udp message
        /// </summary>
        /// <param name="message">binary message to be sent</param>
        public void SendMessage(byte[] message)
        {
            if (Enabled)
            {
                lock (_clientLock)
                {
                    try
                    {
                        _sendResult = _udpClient.BeginSend(message, message.Length, Address, Port, SendCallback, null);
                    }
                    catch (Exception ex)
                    {
                        log.Error("Cannot Send", ex);
                        Error = ex;
                    }
                }
            }
        }

        /// <summary>
        ///     Fetches and removes (pops) the next available group of bytes as received on this link in order (FIFO)
        /// </summary>
        /// <returns>null if the link is not Enabled or there is no data currently queued to return, an array of bytes otherwise.</returns>
        public byte[] GetMessage()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Cannot get message from disposed AsyncUdpLink");
            }

            //Return null if the link is not enabled
            if (!Enabled)
            {
                return null;
            }

            byte[] newMessage = null;
            lock (_incomingData)
            {
                if (HasData)
                {
                    newMessage = _incomingData[0];
                    _incomingData.RemoveAt(0);
                }
            }

            return newMessage;
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

            _disposed = true;
            log.Information("Cleaning up network resources");

            SafeClose();
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

        private void ReceiveCallback(IAsyncResult asyncResult)
        {
            bool hasNewData = false;
            lock (_clientLock)
            {
                try
                {
                    if (Enabled)
                    {
                        // TODO olaaf moved tese two statements so that the UDP link could be disposed. Was thowing an exception. need to test regression
                        IPEndPoint remoteEndpoint = new(IPAddress.Any, Port);
                        byte[] bytesRead = _udpClient.EndReceive(asyncResult, ref remoteEndpoint);

                        if (bytesRead.Length > 0)
                        {
                            _incomingData.Add(bytesRead);
                            hasNewData = true;
                            while (_incomingData.Count > MAX_DATA_SIZE)
                            {
                                //Purge messages from the end of the list to prevent overflow
                                log.Error("Too many incoming messages to handle: " + _incomingData.Count);
                                _incomingData.RemoveAt(_incomingData.Count - 1);
                            }
                        }
                    } //If not enabled, these bytes just get lost
                }
                catch (Exception ex)
                {
                    log.Error("Error receiving from stream", ex);
                    Error = ex;
                }

                if (_receiveResult == asyncResult)
                {
                    //log.Debug("Clearing receive Result");
                    _receiveResult = null;
                }
            }

            if (hasNewData && DataReceived != null && !_disposed)
            {
                DataReceived(this, new EventArgs());
            }

            if (Enabled)
            {
                ReceiveData();
            }
        }


        private void ReceiveData()
        {
            if (Enabled)
            {
                lock (_clientLock)
                {
                    try
                    {
                        _receiveResult = _udpClient.BeginReceive(ReceiveCallback, null);
                        Error = null;
                    }
                    catch (Exception ex)
                    {
                        log.Error("Cannot receive", ex);
                        Error = ex;
                    }
                }
            }
        }


        /// <summary>
        ///     Very carefully checks and shuts down the tcpClient and sets it to null
        /// </summary>
        private void SafeClose()
        {
            log.Debug("Safe Close");
            lock (_clientLock)
            {
                if (_receiveResult != null)
                {
                    //End the read process
                    _receiveResult = null;
                }

                if (_sendResult != null)
                {
                    //End the write process
                    _sendResult = null;
                }

                if (_udpClient != null)
                {
                    if (_udpClient.Client != null)
                    {
                        _udpClient.Client.Close();
                    }

                    _udpClient.Close();
                }

                _udpClient = null;

                lock (_incomingData)
                {
                    _incomingData.Clear();
                }
            }
        }


        private void SendCallback(IAsyncResult asyncResult)
        {
            lock (_clientLock)
            {
                try
                {
                    _udpClient.EndSend(asyncResult);
                    Error = null;
                }
                catch (Exception ex)
                {
                    log.Error("Error sending message", ex);
                    Error = ex;
                }

                if (_sendResult == asyncResult)
                {
                    //log.Debug("Clearing send Result");
                    _sendResult = null;
                }
            }
        }

        #region Public Properties

        public string Address { get; }
        public int Port { get; }

        public int LocalPort { get; set; }

        public bool HasData
        {
            get { return _incomingData.Count > 0; }
        }


        private bool _enabled;

        /// <summary>
        ///     Gets or sets a value indicating whether messages should be propogated to the network or not
        /// </summary>
        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                _enabled = value;
                if (_enabled)
                {
                    //initiate receive data
                    lock (_clientLock)
                    {
                        if (_receiveResult == null)
                        {
                            ReceiveData(); //Initiate Receive data because no result is pending
                        }
                    }
                }

                NotifyPropertyChanged("Enabled");
            }
        }

        public event EventHandler DataReceived;

        private Exception _error;

        public Exception Error
        {
            get { return _error; }
            set
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
}