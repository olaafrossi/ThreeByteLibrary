// Created by Three Byte Intemedia, Inc. | project: PCController |
// Created: 2021 03 14
// by Olaaf Rossi

using System;

namespace ThreeByteLibrary.Dotnet.NetworkUtils
{
    public interface IPcNetworkListener
    {
        event EventHandler<NetworkMessagesEventArgs> MessageHit;
        int GetAppSettingsDataUdpPort();
        void ListenLoop(object state);
        void Run();
    }
}