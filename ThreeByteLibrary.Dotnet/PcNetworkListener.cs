using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ThreeByteLibrary.Dotnet
{
    public class PcNetworkListener : IPcNetworkListener
    {
        private readonly IConfiguration _config;
        private readonly ILogger<PcNetworkListener> _log;

        //private PCNetworkListenerMessages messages = new();

        /// <summary>
        ///     Constructor for the PC listener, injects dependencies
        /// </summary>
        /// <param name="log"></param>
        /// <param name="config"></param>
        public PcNetworkListener(ILogger<PcNetworkListener> log, IConfiguration config)
        {
            _log = log;
            _config = config;
        }

        public PcNetworkListener()
        {
            // overloaded the constructor with no params
            // for unit tests and because i can't get DI to work.. bad idea?
        }

        public void Run()
        {
            ThreadPool.QueueUserWorkItem(ListenLoop);
        }

        public event EventHandler<PCNetworkListenerMessages> MessageHit;

        public int GetAppSettingsDataUdpPort()
        {
            int udpPort = _config.GetValue<int>("UdpPort");
            int output = 0;

            if (udpPort >= 65535)
            {
                LogToAll(PCNetworkListenerMessages._UiLogger.appLog,
                    $"Port setting in appsettings.jsonfile is greater than 65535 (illegal). Setting to 16009 | your setting is {udpPort}");
                output = 16009;
                return output;
            }

            try
            {
                if (udpPort is 16009)
                {
                    output = udpPort;
                }
                else if (udpPort is 0)
                {
                    LogToAll(PCNetworkListenerMessages._UiLogger.appLog,
                        $"Invalid UDP listener port found. Setting to 16009 | {udpPort}");
                    output = 16009;
                }
                else if (udpPort >= 1)
                {
                    LogToAll(PCNetworkListenerMessages._UiLogger.appLog,
                        $"Parsed a valid (non-standard) UDP listener port from the appsettings.jsonfile | {udpPort}");
                    output = udpPort;
                }
            }
            catch
            {
                LogToAll(PCNetworkListenerMessages._UiLogger.appLog,
                    $"Failed to parse a valid UDP listener port from the appsettings.jsonfile Setting to 16009 | {udpPort}");
                output = 16009;
            }

            return output;
        }

        public void ListenLoop(object state)
        {
            int portNumber = GetAppSettingsDataUdpPort();
            var udpClient = OpenUdpListener(portNumber);
            var udpEndPoint = UdpEndPoint(IPAddress.Any, 0);

            bool listening = true;

            byte[] dataBytes;

            LogToAll(PCNetworkListenerMessages._UiLogger.netLog, $"UDP listener started on port {portNumber}");

            while (listening)
            {
                dataBytes = udpClient.Receive(ref udpEndPoint);
                LogToAll(PCNetworkListenerMessages._UiLogger.netLog,
                    $"| Last Remote: {udpEndPoint.Address} on Port: {udpEndPoint.Port}");

                string stringIn =
                    Encoding.ASCII.GetString(dataBytes); // Incoming commands must be received as a single packet.
                stringIn = stringIn.ToUpper(); // format the string to upper case for matching

                //Parse messages separated by cr
                int delimPos = stringIn.IndexOf("\r");
                while (delimPos >= 0)
                {
                    string message = stringIn.Substring(0, delimPos + 1).Trim();
                    stringIn = stringIn.Remove(0, delimPos + 1); //remove the message
                    delimPos = stringIn.IndexOf("\r");

                    LogToAll(PCNetworkListenerMessages._UiLogger.netLog, $"| Incoming Message: {message}");

                    if (message == "EXIT")
                    {
                        listening = false;
                    }
                    else if (message == "PING")
                    {
                        string responseString = "PONG\r";
                        byte[] sendBytes = Encoding.ASCII.GetBytes(responseString);
                        udpClient.Send(sendBytes, sendBytes.Length, udpEndPoint);
                        LogToAll(PCNetworkListenerMessages._UiLogger.netLog, $"| Sent: {responseString}");
                    }
                    else if (message == "REBOOT" || message == "RESTART")
                    {
                        LogToAll(PCNetworkListenerMessages._UiLogger.appLog, "| Rebooting PC- in 5 seconds");
                        Thread.Sleep(5000);
                        Process.Start("shutdown", "/r /f /t 3 /c \"Reboot Triggered\" /d p:0:0");
                    }
                    else if (message == "SHUTDOWN")
                    {
                        LogToAll(PCNetworkListenerMessages._UiLogger.appLog, "| Shutting Down PC- in 5 seconds");
                        Thread.Sleep(5000);
                        Process.Start("shutdown", "/s /f /t 3 /c \"Shutdown Triggered\" /d p:0:0");
                    }
                    else if (message == "SLEEP")
                    {
                        LogToAll(PCNetworkListenerMessages._UiLogger.appLog, "| Sleeping PC- in 5 seconds");
                        Thread.Sleep(5000);
                        Process.Start("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
                    }
                }
            }
        }

        private UdpClient OpenUdpListener(int port)
        {
            UdpClient udpSender = new UdpClient(port);
            return udpSender;
        }

        private IPEndPoint UdpEndPoint(IPAddress address, int port)
        {
            IPEndPoint udpEndPoint = new IPEndPoint(address, port);
            return udpEndPoint;
        }

        private void LogToAll(Enum log, string message)
        {
            _log.LogInformation(message);

            PCNetworkListenerMessages args = new PCNetworkListenerMessages();
            args.UILogger = (PCNetworkListenerMessages._UiLogger) log;
            args.Message = $"{DateTime.Now:HH:mm:ss.fff} | {message}";
            OnNewMessages(args);
        }

        protected virtual void OnNewMessages(PCNetworkListenerMessages e)
        {
            EventHandler<PCNetworkListenerMessages> handler = MessageHit;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public class PCNetworkListenerMessages : EventArgs
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
}