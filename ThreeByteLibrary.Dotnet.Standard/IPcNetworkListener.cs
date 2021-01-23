namespace ThreeByteLibrary.Dotnet.Standard
{
    public interface IPcNetworkListener
    {
        int GetAppSettingsDataUdpPort();
        void ListenLoop(object state);
        void Run();
    }
}