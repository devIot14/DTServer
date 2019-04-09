using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;

namespace Dhcp
{
    #region Dhcp
    public sealed class Dhcp
    {
        /// <summary>Will be called on any DHCP message</summary>
        internal event EventHandler<DhcpRequest> OnDataReceived; // = delegate { };
        /// <summary>Will be called on any DISCOVER message</summary>
        internal event EventHandler<DhcpRequest> OnDiscover; // = delegate { };
        /// <summary>Will be called on any REQUEST message</summary>
        internal event EventHandler<DhcpRequest> OnRequest; // = delegate { };
        /// <summary>Will be called on any DECLINE message</summary>
        internal event EventHandler<DhcpRequest> OnDecline; // = delegate { };
        /// <summary>Will be called on any DECLINE released</summary>
        internal event EventHandler<DhcpRequest> OnReleased; // = delegate { };
        /// <summary>Will be called on any DECLINE inform</summary>
        internal event EventHandler<DhcpRequest> OnInform; // = delegate { };

        /// <summary>Server name (optional)</summary>
        internal string ServerName { get; set; }

        private DatagramSocket Socket = null; // EDITED:: modificato da socket a Socket
        private const int PORT_TO_LISTEN_TO = 0x43; // EDITED:: Porta 67 (porta del server. Il client utilizza la porta 68)
        private IPAddress _bindIp;

        internal event EventHandler<Exception> UnhandledException;

        internal string BroadcastAddress { get; set; }

        internal DatagramSocket DataSocket;

        /// <summary>Creates DHCP server, it will be started instantly</summary>
        public Dhcp() : this(IPAddress.Any)
        {
            BroadcastAddress = IPAddress.Broadcast.ToString();
        }

        /// <summary>
        /// Creates DHCP server, it will be started instantly
        /// </summary>
        /// <param name="bindIp">IP address to bind</param>
        internal Dhcp(IPAddress bindIp)
        {
            _bindIp = bindIp;
        }

        internal async void Start()
        {
            try
            {
                DataSocket = new DatagramSocket();

                DataSocket.MessageReceived += DataSocket_MessageReceived;

                //await DataSocket.BindEndpointAsync(new HostName(_bindIp.ToString()), PORT_TO_LISTEN_TO.ToString());
                await DataSocket.BindServiceNameAsync(PORT_TO_LISTEN_TO.ToString());

            }
            catch (Exception ex)
            {
                UnhandledException?.Invoke(this, ex); // EDITED:: invoke viene eseguito se e soltanto se UnhandledException non è null
            }
        }

        /// <summary>Disposes DHCP server</summary>
        internal void Dispose()
        {
            if (Socket != null)
            {
                //socket.Close();
                Socket.Dispose();
                Socket = null;
            }
        }

        //private void DataReceived(object o)
        private void DataSocket_MessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            try
            {
                var reader = args.GetDataReader();

                byte[] data = new byte[reader.UnconsumedBufferLength];
                args.GetDataReader().ReadBytes(data);

                this.Socket = sender;

                var dhcpRequest = new DhcpRequest(data, Socket, this);
                //ccDHCP = new clsDHCP();


                //data is now in the structure
                //get the msg type
                OnDataReceived(this, dhcpRequest);
                var msgType = dhcpRequest.GetMsgType();
                switch (msgType)
                {
                    case DhcpMsgType.DHCPDISCOVER:
                        OnDiscover(this, dhcpRequest);
                        break;
                    case DhcpMsgType.DHCPREQUEST:
                        OnRequest(this, dhcpRequest);
                        break;
                    case DhcpMsgType.DHCPDECLINE:
                        OnDecline(this, dhcpRequest);
                        break;
                    case DhcpMsgType.DHCPRELEASE:
                        OnReleased(this, dhcpRequest);
                        break;
                    case DhcpMsgType.DHCPINFORM:
                        OnInform(this, dhcpRequest);
                        break;
                        //default:
                        //    Console.WriteLine("Unknown DHCP message: " + (int)MsgTyp + " (" + MsgTyp.ToString() + ")");
                        //    break;
                }
            }
            catch (Exception ex)
            {
                UnhandledException?.Invoke(this, ex);   // EDITED:: invoke viene eseguito se e soltanto se UnhandledException non è null
            }
        }
    } 
    #endregion

    #region DhcpServer
    /// <summary>
    /// DHCP Server.
    /// </summary>
    public class DhcpServer
    {
        public List<string> Status { get; internal set; }
        public bool Readed = false;

        private const ushort DELAY = 0x1388;    // 5 s

        public void Run(IPAddress iPAddress, string dns, string subnetMask, string serverIdentifier, string routerIP)
        {
            UpdateStatus($"IP address:\t{iPAddress}");
            UpdateStatus($"DNS:\t{dns}");
            UpdateStatus($"SUBMASK:\t{subnetMask}");
            UpdateStatus($"SERVER IDENTIFIER:\t{serverIdentifier}");
            UpdateStatus($"ROUTER IP:\t{routerIP}");

            var server = new Dhcp(iPAddress);
            UpdateStatus("SETTATO L'IP DEL SERVER");
            server.ServerName = dns;
            UpdateStatus("SETTATO IL DNS DEL SERVER");
            server.BroadcastAddress = IPAddress.Broadcast.ToString();
            UpdateStatus("SETTATO IN BROADCAST IL SERVER");
            server.OnDataReceived += (sender, dhcpRequest) =>
            {
                UpdateStatus("GENERATO NUOVO EVENTO NEL SERVER");
                try
                {
                    var type = dhcpRequest.GetMsgType();
                    UpdateStatus($"TIPO DI RICHIESTA RICEVUTA DAL SERVER:\t{type}");
                    var ip = iPAddress;

                    var replyOptions = new DhcpReplyOptions();
                    UpdateStatus("SETTAGGIO DELLA RISPOSTA DAL SERVER IN CORSO...");
                    replyOptions.SubnetMask = IPAddress.Parse(subnetMask);
                    UpdateStatus($"SUBNETMASK:\t{replyOptions.SubnetMask}");
                    replyOptions.DomainName = server.ServerName;
                    UpdateStatus($"DOMAIN NAME:\t{replyOptions.DomainName}");
                    replyOptions.ServerIdentifier = IPAddress.Parse(serverIdentifier);
                    UpdateStatus($"SERVER IDENTIFIER:\t{replyOptions.ServerIdentifier}");
                    replyOptions.RouterIP = IPAddress.Parse(routerIP);
                    UpdateStatus($"IP ROUTER:\t{replyOptions.RouterIP}");
                    replyOptions.DomainNameServers = new IPAddress[]
                    {IPAddress.Parse("8.8.8.8"), IPAddress.Parse("8.8.4.4")};
                    UpdateStatus("CONFIGURAZIONE DEL DOMINIO DEL SERVER");

                    if (type == DhcpMsgType.DHCPDISCOVER)
                    {
                        UpdateStatus($"RISPOSTA\t->\tTIPO MESSAGGIO:{DhcpMsgType.DHCPOFFER}");
                        UpdateStatus($"RISPOSTA\t->\tIP:{ip}");
                        dhcpRequest.SendDHCPReply(DhcpMsgType.DHCPOFFER, ip, replyOptions);
                    }
                    if (type == DhcpMsgType.DHCPREQUEST)
                    {
                        UpdateStatus($"RISPOSTA\t->\tTIPO MESSAGGIO:{DhcpMsgType.DHCPACK}");
                        UpdateStatus($"RISPOSTA\t->\tIP:{ip}");
                        dhcpRequest.SendDHCPReply(DhcpMsgType.DHCPACK, ip, replyOptions);
                    }

                }
                catch (Exception e)
                {
                    UpdateStatus($"EXCEPTION IN SERVER:\t{e.Message}");
                }

            };
            UpdateStatus("ESECUZIONE DEL SERVER DHCP IN CORSO...");
            server.Start();
        }

        private void UpdateStatus(string status)
        {
            Status = Status ?? new List<string>();
            Status.Add(status);
        }

        public void UpdateReaded()
        {
            if (Readed)
            {
                Status = new List<string>();
                Readed = !Readed;
            }
        }
    } 
    #endregion
}
