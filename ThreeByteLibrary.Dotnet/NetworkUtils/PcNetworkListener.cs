using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Serilog;
using Serilog.Core;

namespace ThreeByteLibrary.Dotnet.NetworkUtils
{
    public class PcNetworkListener : IPcNetworkListener
    {
        private readonly int portNum;

        private readonly ILogger log = Log.Logger;

        /// <summary>
        ///     Constructor for the PC listener, injects dependencies
        /// </summary>
        /// <param name="log"></param>
        /// <param name="config"></param>
        public PcNetworkListener(ILogger log, int portNum)
        {
            this.log = log;
            this.portNum = portNum;
            this.log.Information("The PC Network Listener Service has started from the constructor with log and config args", typeof(ILogEventEnricher));
            Run();
        }

        public PcNetworkListener()
        {
            // overloaded the constructor with no params
            // for unit tests and because i can't get DI to work.. bad idea?
            this.log.Information("The PC Network Listener Service has started from the constructor with no args", typeof(ILogEventEnricher));
            Run();
        }

        public event EventHandler<NetworkMessagesEventArgs> MessageHit;

        public int GetAppSettingsDataUdpPort()
        {
            int udpPort = this.portNum;
            int output = 0;

            if (udpPort >= 65535)
            {
                this.log.Warning("UDP port setting is greater than 65535 (this is illegal). Setting to 16009 | your illegal port # was { udpPort }", udpPort);
                output = 16009;
                return output;
            }

            try
            {
                if (udpPort is 16009)
                {
                    output = udpPort;
                    this.log.Information("UDP Port is configured for {udpPort}", udpPort);
                }
                else if (udpPort is 0)
                {
                    this.log.Warning("UDP port setting is 0 (this is illegal). Setting to 16009 | your illegal port # was { udpPort }", udpPort);
                    output = 16009;
                }
                else if (udpPort >= 1)
                {
                    this.log.Information("Parsed a valid (non-standard) UDP listener port from the appsettings.jsonfile | {udpPort}", udpPort);
                    output = udpPort;
                }
            }
            catch
            {
                this.log.Warning("Failed to parse a valid UDP listener port from the appsettings.jsonfile Setting to 16009");
                output = 16009;
            }

            this.MessageHit?.Invoke(
                this,
                new NetworkMessagesEventArgs
                {
                    IncomingMessage = "No Message",
                    OutgoingMessage = "No Message",
                    RemoteIP = "Not Set",
                    RemotePort = "0",
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    UDPPort = output
                });

            return output;
        }

        public void ListenLoop(object state)
        {
            int portNumber = this.GetAppSettingsDataUdpPort();
            var udpClient = this.OpenUdpListener(portNumber);
            var udpEndPoint = this.UdpEndPoint(IPAddress.Any, 0);

            bool listening = true;

            byte[] dataBytes;

            while (listening)
            {
                dataBytes = udpClient.Receive(ref udpEndPoint);
                this.log.Information("Last Remote: {udpEndPoint.Address} on Port: {udpEndPoint.Port}", udpEndPoint.Address, udpEndPoint.Port);

                string stringIn = Encoding.ASCII.GetString(dataBytes); // Incoming commands must be received as a single packet.
                stringIn = stringIn.ToUpper(); // format the string to upper case for matching

                //Parse messages separated by cr
                int delimPos = stringIn.IndexOf("\r");
                while (delimPos >= 0)
                {
                    string message = stringIn.Substring(0, delimPos + 1).Trim();
                    stringIn = stringIn.Remove(0, delimPos + 1); //remove the message
                    delimPos = stringIn.IndexOf("\r");

                    this.log.Information("Incoming Message: {message}", message);

                    if (message == "EXIT")
                    {
                        listening = false;
                        this.log.Warning("Stopping the listen loop by your command");
                        this.MessageHit?.Invoke(
                            this,
                            new NetworkMessagesEventArgs
                            {
                                IncomingMessage = message,
                                UDPPort = portNumber,
                                OutgoingMessage = string.Empty,
                                RemoteIP = udpEndPoint.Address.ToString(),
                                RemotePort = udpEndPoint.Port.ToString(),
                                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                            });
                    }
                    else if (message == "PING")
                    {
                        string responseString = "PONG\r";
                        byte[] sendBytes = Encoding.ASCII.GetBytes(responseString);
                        udpClient.Send(sendBytes, sendBytes.Length, udpEndPoint);
                        this.log.Information("Sent: {responseString}", responseString);
                        this.MessageHit?.Invoke(
                            this,
                            new NetworkMessagesEventArgs
                            {
                                IncomingMessage = message,
                                UDPPort = portNumber,
                                OutgoingMessage = "PONG",
                                RemoteIP = udpEndPoint.Address.ToString(),
                                RemotePort = udpEndPoint.Port.ToString(),
                                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                            });
                    }
                    else if (message == "REBOOT" || message == "RESTART")
                    {
                        this.log.Information("Rebooting PC- in 5 seconds");
                        this.MessageHit?.Invoke(
                            this,
                            new NetworkMessagesEventArgs
                            {
                                IncomingMessage = message,
                                UDPPort = portNumber,
                                OutgoingMessage = string.Empty,
                                RemoteIP = udpEndPoint.Address.ToString(),
                                RemotePort = udpEndPoint.Port.ToString(),
                                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                            });
                        Thread.Sleep(5000);
                        Process.Start("shutdown", "/r /f /t 3 /c \"Reboot Triggered\" /d p:0:0");
                    }
                    else if (message == "SHUTDOWN")
                    {
                        this.log.Information("Shutting Down PC- in 5 seconds");
                        this.MessageHit?.Invoke(
                            this,
                            new NetworkMessagesEventArgs
                            {
                                IncomingMessage = message,
                                UDPPort = portNumber,
                                OutgoingMessage = string.Empty,
                                RemoteIP = udpEndPoint.Address.ToString(),
                                RemotePort = udpEndPoint.Port.ToString(),
                                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                            });
                        Thread.Sleep(5000);
                        Process.Start("shutdown", "/s /f /t 3 /c \"Shutdown Triggered\" /d p:0:0");
                    }
                    else if (message == "SLEEP")
                    {
                        this.log.Information("Sleeping PC- in 5 seconds");
                        this.MessageHit?.Invoke(
                            this,
                            new NetworkMessagesEventArgs
                            {
                                IncomingMessage = message,
                                UDPPort = portNumber,
                                OutgoingMessage = string.Empty,
                                RemoteIP = udpEndPoint.Address.ToString(),
                                RemotePort = udpEndPoint.Port.ToString(),
                                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                            });
                        Thread.Sleep(5000);
                        Process.Start("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
                    }
                }
            }
        }

        public void Run()
        {
            ThreadPool.QueueUserWorkItem(this.ListenLoop);
            this.log.Information("The PC Network Listener Server Run Loop has started");
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
    }

    public class NetworkMessagesEventArgs : EventArgs
    {
        public string IncomingMessage { get; set; }

        public string OutgoingMessage { get; set; }

        public string RemoteIP { get; set; }

        public string RemotePort { get; set; }

        public string Timestamp { get; set; }

        public int UDPPort { get; set; }
    }
}