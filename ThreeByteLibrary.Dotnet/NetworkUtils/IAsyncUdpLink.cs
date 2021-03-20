// Created by Three Byte Intemedia, Inc. | project: PCController |
// Created: 2021 03 18
// by Olaaf Rossi

using System;
using System.ComponentModel;

namespace ThreeByteLibrary.Dotnet.NetworkUtils
{
    public interface IAsyncUdpLink
    {
        /// <summary>
        ///     Implementation of IDisposable interface.  Cancels the thread and releases resources.
        ///     Clients of this class are responsible for calling it.
        /// </summary>
        void Dispose();

        event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        ///     Asynchronously sends the udp message
        /// </summary>
        /// <param name="message">binary message to be sent</param>
        void SendMessage(byte[] message);

        /// <summary>
        ///     Fetches and removes (pops) the next available group of bytes as received on this link in order (FIFO)
        /// </summary>
        /// <returns>null if the link is not Enabled or there is no data currently queued to return, an array of bytes otherwise.</returns>
        byte[] GetMessage();

        string Address { get; }
        int Port { get; }
        int LocalPort { get; set; }
        bool HasData { get; }

        /// <summary>
        ///     Gets or sets a value indicating whether messages should be propogated to the network or not
        /// </summary>
        bool Enabled { get; set; }

        Exception Error { get; set; }

        event EventHandler DataReceived;
    }
}