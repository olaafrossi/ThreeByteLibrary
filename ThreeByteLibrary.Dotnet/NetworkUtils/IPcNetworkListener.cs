using System;

namespace ThreeByteLibrary.Dotnet
{
    public interface IPcNetworkListener
    {
        event EventHandler<PcNetworkListener.PCNetworkListenerMessages> MessageHit;

        int GetAppSettingsDataUdpPort();
        void ListenLoop(object state);
        void Run();
    }
}