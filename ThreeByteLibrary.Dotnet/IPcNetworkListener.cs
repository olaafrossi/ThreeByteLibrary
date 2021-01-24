namespace ThreeByteLibrary.Dotnet
{
    public interface IPcNetworkListener
    {
        int GetAppSettingsDataUdpPort();
        void ListenLoop(object state);
        void Run();
    }
}