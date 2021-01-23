using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ThreeByteLibrary.Dotnet.Standard
{
    public class PcNetworkListener : IPcNetworkListener
    {
        private readonly IConfiguration _config;
        private readonly ILogger<PcNetworkListener> _log;

        //private CrestronAppMessages messages = new();

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

        private Dictionary<string, string> LogToAll(string log, string message)
        {
            var output = new Dictionary<string, string>();

            // this should go to the UI one day
            output.Add($"{log}", $" | {DateTime.Now:HH:mm:ss.fff} | {message}");

            // write to Serilog one day
            _log.LogInformation($"{log} {message}");

            // some event handler here to message the UI

            return output;
        }

        public int GetAppSettingsDataUdpPort()
        {
            int output = 0;
            int udpPort = 0;

            try
            {
                udpPort = _config.GetValue<int>("UdpPort");
                LogToAll("appLog", $"Parsed a valid UDP listener port from the appsettings.jsonfile | {udpPort}");
            }
            catch
            {
                LogToAll("appLog",
                    $"Failed to parse a valid UDP listener port from the appsettings.jsonfile | {udpPort}");
            }

            if (udpPort is 16009)
            {
                output = udpPort;
            }

            if (udpPort is 0)
            {
                output = 16009;
                LogToAll("appLog", $"UDP listener port was set to zero (invalid) changing to 16009 | {udpPort}");
                return output;
            }

            if (udpPort is > 0 and not 16009)
            {
                output = udpPort;
                LogToAll("appLog",
                    $"Parsed a valid/custom UDP listener port from the appsettings.jsonfile | {udpPort}");
                return output;
            }

            LogToAll("appLog",
                $"Something is wrong with UDP port settings, and using a hardcodeded value of 16009 | {udpPort}");
            output = 16009;
            return output;
        }

        public void ListenLoop(object state)
        {
            int portNumber = GetAppSettingsDataUdpPort();
            var udpClient = OpenUdpListener(portNumber);
            var udpEndPoint = UdpEndPoint(IPAddress.Any, 0);

            bool listening = true;

            byte[] dataBytes;

            LogToAll("netLog", $"UDP listener started on port {portNumber}");

            while (listening)
            {
                dataBytes = udpClient.Receive(ref udpEndPoint);
                LogToAll("netLog", $"| Last Remote: {udpEndPoint.Address} on Port: {udpEndPoint.Port}");

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

                    LogToAll("netLog", $"| Incoming Message: {message}");

                    if (message == "EXIT")
                    {
                        listening = false;
                    }
                    else if (message == "PING")
                    {
                        string responseString = "PONG\r";
                        byte[] sendBytes = Encoding.ASCII.GetBytes(responseString);
                        udpClient.Send(sendBytes, sendBytes.Length, udpEndPoint);
                        LogToAll("appLog", $"| Sent: {responseString}");
                    }
                    else if (message == "REBOOT" || message == "RESTART")
                    {
                        LogToAll("appLog", "| Reboot Triggered");
                        Process.Start("shutdown", "/r /f /t 3 /c \"Reboot Triggered\" /d p:0:0");
                    }
                    else if (message == "SHUTDOWN")
                    {
                        LogToAll("appLog", "| Shutting Down PC");
                        Process.Start("shutdown", "/s /f /t 3 /c \"Shutdown Triggered\" /d p:0:0");
                    }
                    else if (message == "SLEEP")
                    {
                        LogToAll("appLog", "| Sleeping PC");
                        Process.Start("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
                    }
                }
            }
        }

        // probably not needed, but maybe to cast the Log Dictionary into an object for easier event handling?
        public class CrestronAppMessages
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