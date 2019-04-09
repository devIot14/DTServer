using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;

namespace Dhcp
{
    internal class DhcpRequest
    {
        private readonly Dhcp dhcpServer;
        private readonly DHCPPacket requestData = new DHCPPacket();
        private DatagramSocket requestSocket;
        private const int OPTION_OFFSET = 0xF0; // EDITED:: valore 240 (per uso privato)
        private const int PORT_TO_SEND_TO_CLIENT = 0x44;    // EDITED:: valore 68 (porta del client)
        private const int PORT_TO_SEND_TO_RELAY = 0x43;  // EDITED:: valore 67 (porta del server)

        /// <summary>
        /// Raw DHCP packet
        /// </summary>

        internal DhcpRequest(byte[] data, DatagramSocket socket, Dhcp server)
        {
            dhcpServer = server;
            BinaryReader rdr;
            MemoryStream stm = new MemoryStream(data, 0, data.Length);
            rdr = new BinaryReader(stm);

            // Reading data
            requestData.op = rdr.ReadByte();
            requestData.htype = rdr.ReadByte();
            requestData.hlen = rdr.ReadByte();
            requestData.hops = rdr.ReadByte();
            requestData.xid = rdr.ReadBytes(4);
            requestData.secs = rdr.ReadBytes(2);
            requestData.flags = rdr.ReadBytes(2);
            requestData.ciaddr = rdr.ReadBytes(4);
            requestData.yiaddr = rdr.ReadBytes(4);
            requestData.siaddr = rdr.ReadBytes(4);
            requestData.giaddr = rdr.ReadBytes(4);
            requestData.chaddr = rdr.ReadBytes(16);
            requestData.sname = rdr.ReadBytes(64);
            requestData.file = rdr.ReadBytes(128);
            requestData.mcookie = rdr.ReadBytes(4);
            requestData.options = rdr.ReadBytes(data.Length - OPTION_OFFSET);
            requestSocket = socket;
        }

        /// <summary>
        /// Returns array of requested by client options
        /// </summary>
        /// <returns>Array of requested by client options</returns>
        internal DhcpOption[] GetRequestedOptionsList()
        {
            var reqList = this.GetOptionData(DhcpOption.ParameterRequestList);
            var optList = new List<DhcpOption>();
            if (reqList != null) foreach (var option in reqList) optList.Add((DhcpOption)option); else return null;
            return optList.ToArray();
        }

        private byte[] CreateOptionStruct(DhcpMsgType msgType, DhcpReplyOptions replyOptions, Dictionary<DhcpOption, byte[]> otherForceOptions, IEnumerable<DhcpOption> forceOptions)
        {
            Dictionary<DhcpOption, byte[]> options = new Dictionary<DhcpOption, byte[]>();

            byte[] resultOptions = null;
            // Requested options
            var reqList = GetRequestedOptionsList();
            // Option82?
            var relayInfo = this.GetOptionData(DhcpOption.RelayInfo);
            CreateOptionElement(ref resultOptions, DhcpOption.DHCPMessageTYPE, new byte[] { (byte)msgType });
            // Server identifier - our IP address
            if ((replyOptions != null) && (replyOptions.ServerIdentifier != null))
                options[DhcpOption.ServerIdentifier] = replyOptions.ServerIdentifier.GetAddressBytes();

            if (reqList == null && forceOptions != null)
                reqList = new DhcpOption[0];

            // Requested options
            if ((reqList != null) && (replyOptions != null))
                if (forceOptions == null)
                    forceOptions = new List<DhcpOption>();

            foreach (DhcpOption i in reqList.Union(forceOptions).Distinct().OrderBy(x => (int)x))
            {
                byte[] optionData = null;
                // If it's force option - ignore it. We'll send it later.
                if ((otherForceOptions != null) && (otherForceOptions.TryGetValue(i, out optionData)))
                    continue;
                switch (i)
                {
                    case DhcpOption.SubnetMask:
                        if (replyOptions.SubnetMask != null)
                            optionData = replyOptions.SubnetMask.GetAddressBytes();
                        break;
                    case DhcpOption.Router:
                        if (replyOptions.RouterIP != null)
                            optionData = replyOptions.RouterIP.GetAddressBytes();
                        break;
                    case DhcpOption.DomainNameServers:
                        if (replyOptions.DomainNameServers != null)
                        {
                            optionData = new byte[] { };
                            foreach (var dns in replyOptions.DomainNameServers)
                            {
                                var dnsserv = dns.GetAddressBytes();
                                Array.Resize(ref optionData, optionData.Length + 4);
                                Array.Copy(dnsserv, 0, optionData, optionData.Length - 4, 4);
                            }
                        }
                        break;
                    case DhcpOption.DomainName:
                        if (!string.IsNullOrEmpty(replyOptions.DomainName))
                            optionData = System.Text.Encoding.ASCII.GetBytes(replyOptions.DomainName);
                        break;
                    case DhcpOption.ServerIdentifier:
                        if (replyOptions.ServerIdentifier != null)
                            optionData = replyOptions.ServerIdentifier.GetAddressBytes();
                        break;
                    case DhcpOption.LogServer:
                        if (replyOptions.LogServerIP != null)
                            optionData = replyOptions.LogServerIP.GetAddressBytes();
                        break;
                    case DhcpOption.StaticRoutes:
                    case DhcpOption.StaticRoutesWin:
                        if (replyOptions.StaticRoutes != null)
                        {
                            optionData = new byte[] { };
                            foreach (var route in replyOptions.StaticRoutes)
                            {
                                var routeData = route.BuildRouteData();
                                Array.Resize(ref optionData, optionData.Length + routeData.Length);
                                Array.Copy(routeData, 0, optionData, optionData.Length - routeData.Length, routeData.Length);
                            }
                        }
                        break;
                    default:
                        replyOptions.OtherRequestedOptions.TryGetValue(i, out optionData);
                        break;
                }
                if (optionData != null)
                {
                    options[i] = optionData;
                }
            }

            if (GetMsgType() != DhcpMsgType.DHCPINFORM)
            {
                // Lease time
                if (replyOptions != null)
                {
                    var leaseTime = new byte[4];
                    leaseTime[3] = (byte)(replyOptions.IPAddressLeaseTime);
                    leaseTime[2] = (byte)(replyOptions.IPAddressLeaseTime >> 8);
                    leaseTime[1] = (byte)(replyOptions.IPAddressLeaseTime >> 16);
                    leaseTime[0] = (byte)(replyOptions.IPAddressLeaseTime >> 24);
                    options[DhcpOption.IPAddressLeaseTime] = leaseTime;
                    if (replyOptions.RenewalTimeValue_T1.HasValue)
                    {
                        leaseTime[3] = (byte)(replyOptions.RenewalTimeValue_T1);
                        leaseTime[2] = (byte)(replyOptions.RenewalTimeValue_T1 >> 8);
                        leaseTime[1] = (byte)(replyOptions.RenewalTimeValue_T1 >> 16);
                        leaseTime[0] = (byte)(replyOptions.RenewalTimeValue_T1 >> 24);
                        options[DhcpOption.RenewalTimeValue_T1] = leaseTime;
                    }
                    if (replyOptions.RebindingTimeValue_T2.HasValue)
                    {
                        leaseTime[3] = (byte)(replyOptions.RebindingTimeValue_T2);
                        leaseTime[2] = (byte)(replyOptions.RebindingTimeValue_T2 >> 8);
                        leaseTime[1] = (byte)(replyOptions.RebindingTimeValue_T2 >> 16);
                        leaseTime[0] = (byte)(replyOptions.RebindingTimeValue_T2 >> 24);
                        options[DhcpOption.RebindingTimeValue_T2] = leaseTime;
                    }
                }
            }
            // Other requested options
            if (otherForceOptions != null)
                foreach (var option in otherForceOptions.Keys)
                {
                    options[option] = otherForceOptions[option];
                    if (option == DhcpOption.RelayInfo)
                        relayInfo = null;
                }

            // Option 82? Send it back!
            if (relayInfo != null)
            {
                options[DhcpOption.RelayInfo] = relayInfo;
            }

            foreach (var option in options.OrderBy(x => (int)x.Key))
            {
                CreateOptionElement(ref resultOptions, option.Key, option.Value);
            }

            // Create the end option
            Array.Resize(ref resultOptions, resultOptions.Length + 1);
            Array.Copy(new byte[] { 255 }, 0, resultOptions, resultOptions.Length - 1, 1);
            return resultOptions;
        }

        static private void CreateOptionElement(ref byte[] options, DhcpOption option, byte[] data)
        {
            byte[] optionData;

            optionData = new byte[data.Length + 2];
            optionData[0] = (byte)option;
            optionData[1] = (byte)data.Length;
            Array.Copy(data, 0, optionData, 2, data.Length);
            if (options == null)
                Array.Resize(ref options, (int)optionData.Length);
            else
                Array.Resize(ref options, options.Length + optionData.Length);
            Array.Copy(optionData, 0, options, options.Length - optionData.Length, optionData.Length);
        }

        /// <summary>
        /// Sends DHCP reply
        /// </summary>
        /// <param name="msgType">Type of DHCP message to send</param>
        /// <param name="ip">IP for client</param>
        /// <param name="replyData">Reply options (will be sent if requested)</param>
        internal void SendDHCPReply(DhcpMsgType msgType, IPAddress ip, DhcpReplyOptions replyData)
        {
            SendDHCPReply(msgType, ip, replyData, null, null);
        }

        /// <summary>
        /// Sends DHCP reply
        /// </summary>
        /// <param name="msgType">Type of DHCP message to send</param>
        /// <param name="ip">IP for client</param>
        /// <param name="replyData">Reply options (will be sent if requested)</param>
        /// <param name="otherForceOptions">Force reply options (will be sent anyway)</param>
        internal void SendDHCPReply(DhcpMsgType msgType, IPAddress ip, DhcpReplyOptions replyData,
            Dictionary<DhcpOption, byte[]> otherForceOptions)
        {
            SendDHCPReply(msgType, ip, replyData, otherForceOptions, null);
        }

        /// <summary>
        /// Sends DHCP reply
        /// </summary>
        /// <param name="msgType">Type of DHCP message to send</param>
        /// <param name="ip">IP for client</param>
        /// <param name="replyData">Reply options (will be sent if requested)</param>
        /// <param name="forceOptions">Force reply options (will be sent anyway)</param>
        internal void SendDHCPReply(DhcpMsgType msgType, IPAddress ip, DhcpReplyOptions replyData,
            IEnumerable<DhcpOption> forceOptions)
        {
            SendDHCPReply(msgType, ip, replyData, null, forceOptions);
        }

        /// <summary>
        /// Sends DHCP reply
        /// </summary>
        /// <param name="msgType">Type of DHCP message to send</param>
        /// <param name="ip">IP for client</param>
        /// <param name="replyData">Reply options (will be sent if requested)</param>
        /// <param name="otherForceOptions">Force reply options (will be sent anyway)</param>
        private async void SendDHCPReply(DhcpMsgType msgType, IPAddress ip, DhcpReplyOptions replyData, Dictionary<DhcpOption, byte[]> otherForceOptions, IEnumerable<DhcpOption> forceOptions)
        {
            var replyBuffer = requestData;
            replyBuffer.op = 2; // Reply
            replyBuffer.yiaddr = ip.GetAddressBytes(); // Client's IP
            if (replyData.ServerIpAddress != null)
            {
                replyBuffer.siaddr = replyData.ServerIpAddress.GetAddressBytes();
            }
            replyBuffer.options = CreateOptionStruct(msgType, replyData, otherForceOptions, forceOptions); // Options
            if (!string.IsNullOrEmpty(dhcpServer.ServerName))
            {
                var serverNameBytes = Encoding.ASCII.GetBytes(dhcpServer.ServerName);
                int len = (serverNameBytes.Length > 63) ? 63 : serverNameBytes.Length;
                Array.Copy(serverNameBytes, replyBuffer.sname, len);
                replyBuffer.sname[len] = 0;
            }
            //lock (requestSocket)
            {
                var DataToSend = BuildDataStructure(replyBuffer);
                if (DataToSend.Length < 300)
                {
                    var sendArray = new byte[300];
                    Array.Copy(DataToSend, 0, sendArray, 0, DataToSend.Length);
                    DataToSend = sendArray;
                }

                if ((replyBuffer.giaddr[0] == 0) && (replyBuffer.giaddr[1] == 0) &&
                    (replyBuffer.giaddr[2] == 0) && (replyBuffer.giaddr[3] == 0))
                {
                    //requestSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                    //endPoint = new IPEndPoint(dhcpServer.BroadcastAddress, PORT_TO_SEND_TO_CLIENT);

                    //var udp = new UdpClient();
                    //udp.EnableBroadcast = true;
                    //udp.Send(DataToSend, DataToSend.Length, new IPEndPoint(dhcpServer.BroadcastAddress, 68));
                    //udp.Close();

                    var datagramsocket = new Windows.Networking.Sockets.DatagramSocket();

                    using (var stream = await datagramsocket.GetOutputStreamAsync(new Windows.Networking.HostName(dhcpServer.BroadcastAddress), PORT_TO_SEND_TO_CLIENT.ToString()))
                    {
                        using (var datawriter = new Windows.Storage.Streams.DataWriter(stream))
                        {
                            datawriter.WriteBytes(DataToSend);
                            await datawriter.StoreAsync();
                        }
                    }
                }
                else
                {
                    //requestSocket .SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, false);
                    //endPoint = new IPEndPoint(new IPAddress(replyBuffer.giaddr), PORT_TO_SEND_TO_RELAY);
                    //requestSocket.SendTo(DataToSend, endPoint);

                    using (var stream = await requestSocket.GetOutputStreamAsync(new Windows.Networking.HostName(new IPAddress(replyBuffer.giaddr).ToString()), PORT_TO_SEND_TO_RELAY.ToString()))
                    {
                        using (var datawriter = new Windows.Storage.Streams.DataWriter(stream))
                        {
                            datawriter.WriteBytes(DataToSend);
                            await datawriter.StoreAsync();
                        }
                    }
                }
            }
        }

        private static byte[] BuildDataStructure(DHCPPacket packet)
        {
            byte[] mArray;

            try
            {
                mArray = new byte[0];
                AddOptionElement(new byte[] { packet.op }, ref mArray);
                AddOptionElement(new byte[] { packet.htype }, ref mArray);
                AddOptionElement(new byte[] { packet.hlen }, ref mArray);
                AddOptionElement(new byte[] { packet.hops }, ref mArray);
                AddOptionElement(packet.xid, ref mArray);
                AddOptionElement(packet.secs, ref mArray);
                AddOptionElement(packet.flags, ref mArray);
                AddOptionElement(packet.ciaddr, ref mArray);
                AddOptionElement(packet.yiaddr, ref mArray);
                AddOptionElement(packet.siaddr, ref mArray);
                AddOptionElement(packet.giaddr, ref mArray);
                AddOptionElement(packet.chaddr, ref mArray);
                AddOptionElement(packet.sname, ref mArray);
                AddOptionElement(packet.file, ref mArray);

                AddOptionElement(packet.mcookie, ref mArray);
                AddOptionElement(packet.options, ref mArray);
                return mArray;
            }
            finally
            {
                mArray = null;
            }
        }

        private static void AddOptionElement(byte[] fromValue, ref byte[] targetArray)
        {
            if (targetArray != null)
                Array.Resize(ref targetArray, targetArray.Length + fromValue.Length);
            else
                Array.Resize(ref targetArray, fromValue.Length);
            Array.Copy(fromValue, 0, targetArray, targetArray.Length - fromValue.Length, fromValue.Length);
        }

        /// <summary>
        /// Returns option content
        /// </summary>
        /// <param name="option">Option to retrieve</param>
        /// <returns>Option content</returns>
        internal byte[] GetOptionData(DhcpOption option)
        {
            int DHCPId = 0;
            byte DDataID, DataLength = 0;
            byte[] dumpData;

            DHCPId = (int)option;
            for (int i = 0; i < requestData.options.Length; i++)
            {
                DDataID = requestData.options[i];
                if (DDataID == (byte)DhcpOption.END_Option) break;
                if (DDataID == DHCPId)
                {
                    DataLength = requestData.options[i + 1];
                    dumpData = new byte[DataLength];
                    Array.Copy(requestData.options, i + 2, dumpData, 0, DataLength);
                    return dumpData;
                }
                else if (DDataID == 0)
                {
                }
                else
                {
                    DataLength = requestData.options[i + 1];
                    i += 1 + DataLength;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns all options
        /// </summary>
        /// <returns>Options dictionary</returns>
        internal Dictionary<DhcpOption, byte[]> GetAllOptions()
        {
            var result = new Dictionary<DhcpOption, byte[]>();
            DhcpOption DDataID;
            byte DataLength = 0;

            for (int i = 0; i < requestData.options.Length; i++)
            {
                DDataID = (DhcpOption)requestData.options[i];
                if (DDataID == DhcpOption.END_Option) break;
                DataLength = requestData.options[i + 1];
                byte[] dumpData = new byte[DataLength];
                Array.Copy(requestData.options, i + 2, dumpData, 0, DataLength);
                result[DDataID] = dumpData;

                DataLength = requestData.options[i + 1];
                i += 1 + DataLength;
            }

            return result;
        }
        
        /// <summary>
        /// Returns ciaddr (client IP address)
        /// </summary>
        /// <returns>ciaddr</returns>
        internal IPAddress GetCiaddr()
        {
            if ((requestData.ciaddr[0] == 0) &&
                (requestData.ciaddr[1] == 0) &&
                (requestData.ciaddr[2] == 0) &&
                (requestData.ciaddr[3] == 0)
                ) return null;
            return new IPAddress(requestData.ciaddr);
        }
        /// <summary>
        /// Returns giaddr (gateway IP address switched by relay)
        /// </summary>
        /// <returns>giaddr</returns>
        internal IPAddress GetGiaddr()
        {
            if ((requestData.giaddr[0] == 0) &&
                (requestData.giaddr[1] == 0) &&
                (requestData.giaddr[2] == 0) &&
                (requestData.giaddr[3] == 0)
                ) return null;
            return new IPAddress(requestData.giaddr);
        }
        /// <summary>
        /// Returns chaddr (client hardware address)
        /// </summary>
        /// <returns>chaddr</returns>
        internal byte[] GetChaddr()
        {
            var res = new byte[requestData.hlen];
            Array.Copy(requestData.chaddr, res, requestData.hlen);
            return res;
        }

        /// <summary>
        /// Returns requested IP (option 50)
        /// </summary>
        /// <returns>Requested IP</returns>
        internal IPAddress GetRequestedIP()
        {
            var ipBytes = GetOptionData(DhcpOption.RequestedIPAddress);
            if (ipBytes == null) return null;
            return new IPAddress(ipBytes);
        }

        /// <summary>
        /// Returns type of DHCP request
        /// </summary>
        /// <returns>DHCP message type</returns>
        internal DhcpMsgType GetMsgType()
        {
            byte[] DData;
            DData = GetOptionData(DhcpOption.DHCPMessageTYPE);
            if (DData != null)
                return (DhcpMsgType)DData[0];
            return 0;
        }
        /// <summary>
        /// Returns entire content of DHCP packet
        /// </summary>
        /// <returns>DHCP packet</returns>
        internal DHCPPacket GetRawPacket()
        {
            return requestData;
        }

        /// <summary>
        /// Returns relay info (option 82)
        /// </summary>
        /// <returns>Relay info</returns>
        internal RelayInfo GetRelayInfo()
        {
            var result = new RelayInfo();
            var relayInfo = GetOptionData(DhcpOption.RelayInfo);
            if (relayInfo != null)
            {
                int i = 0;
                while (i < relayInfo.Length)
                {
                    var subOptID = relayInfo[i];
                    if (subOptID == 1)
                    {
                        result.AgentCircuitID = new byte[relayInfo[i + 1]];
                        Array.Copy(relayInfo, i + 2, result.AgentCircuitID, 0, relayInfo[i + 1]);
                    }
                    else if (subOptID == 2)
                    {
                        result.AgentRemoteID = new byte[relayInfo[i + 1]];
                        Array.Copy(relayInfo, i + 2, result.AgentRemoteID, 0, relayInfo[i + 1]);
                    }
                    i += 2 + relayInfo[i + 1];
                }
                return result;
            }
            return null;
        }
    }

    internal sealed class DHCPPacket
    {
        /// <summary>Op code:   1 = boot request, 2 = boot reply</summary>
        internal byte op;
        /// <summary>Hardware address type</summary>
        internal byte htype;
        /// <summary>Hardware address length: length of MACID</summary>
        internal byte hlen;
        /// <summary>Hardware options</summary>
        internal byte hops;
        /// <summary>Transaction id</summary>
        internal byte[] xid;
        /// <summary>Elapsed time from trying to boot</summary>
        internal byte[] secs;
        /// <summary>Flags</summary>
        internal byte[] flags;
        /// <summary>Client IP</summary>
        internal byte[] ciaddr;
        /// <summary>Your client IP</summary>
        internal byte[] yiaddr;
        /// <summary>Server IP</summary>
        internal byte[] siaddr;
        /// <summary>Relay agent IP</summary>
        internal byte[] giaddr;
        /// <summary>Client HW address</summary>
        internal byte[] chaddr;
        /// <summary>Optional server host name</summary>
        internal byte[] sname;
        /// <summary>Boot file name</summary>
        internal byte[] file;
        /// <summary>Magic cookie</summary>
        internal byte[] mcookie;
        /// <summary>Options (rest)</summary>
        internal byte[] options;
    }
}
