namespace Dhcp
{
    internal enum DhcpMsgType
    {
        /// <summary>DHCP DISCOVER message</summary>
        DHCPDISCOVER = 0x1,
        /// <summary>DHCP OFFER message</summary>
        DHCPOFFER = 0x2,
        /// <summary>DHCP REQUEST message</summary>
        DHCPREQUEST = 0x3,
        /// <summary>DHCP DECLINE message</summary>
        DHCPDECLINE = 0x4,
        /// <summary>DHCP ACK message</summary>
        DHCPACK = 0x5,
        /// <summary>DHCP NAK message</summary>
        DHCPNAK = 0x6,
        /// <summary>DHCP RELEASE message</summary>
        DHCPRELEASE = 0x7,
        /// <summary>DHCP INFORM message</summary>
        DHCPINFORM = 0x8
    }
}
