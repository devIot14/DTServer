using System;
using System.Collections.Generic;
using System.Net;

namespace Dhcp
{
    internal sealed class DhcpReplyOptions
    {
        private const UInt32 TIME = 60 * 5 * 1;

        /// <summary>IP address</summary>
        internal IPAddress SubnetMask = null;
        /// <summary>Next Server IP address (bootp)</summary>
        internal IPAddress ServerIpAddress = null;
        /// <summary>IP address lease time (seconds)</summary>
        internal UInt32 IPAddressLeaseTime = TIME;
        /// <summary>Renewal time (seconds)</summary>
        internal UInt32? RenewalTimeValue_T1 = TIME;
        /// <summary>Rebinding time (seconds)</summary>
        internal UInt32? RebindingTimeValue_T2 = TIME;
        /// <summary>Domain name</summary>
        internal string DomainName = null;
        /// <summary>IP address of DHCP server</summary>
        internal IPAddress ServerIdentifier = null;
        /// <summary>Router (gateway) IP</summary>
        internal IPAddress RouterIP = null;
        /// <summary>Domain name servers (DNS)</summary>
        internal IPAddress[] DomainNameServers = null;
        /// <summary>Log server IP</summary>
        internal IPAddress LogServerIP = null;
        /// <summary>Static routes</summary>
        internal NetworkRoute[] StaticRoutes = null;
        /// <summary>Other options which will be sent on request</summary>
        internal Dictionary<DhcpOption, byte[]> OtherRequestedOptions = new Dictionary<DhcpOption, byte[]>();
    }
}
