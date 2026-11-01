using System.Net;
using System.Net.Sockets;

namespace HC.Business.Geo;

/// <summary>
/// Reading an address the way a server has to read one: as text that may carry a port or a shape no lookup
/// can use, and with the question "is this address even a place in the world?" answered before anything is
/// called.
///
/// An address written by a webserver can arrive as '1.2.3.4', '[2001:db8::1]:443', '::ffff:1.2.3.4' or not at
/// all, and none of those are what a lookup takes. And the addresses a shop sees from its own machine or its
/// own network - the developer's localhost, the office's 192.168.x.x, the load balancer's 10.x.x.x - are not
/// places: asking a lookup about them spends a call to learn nothing, so they are recognised here and the
/// answer is simply "no location".
/// </summary>
public static class IpAddresses
{
    /// <summary>
    /// The address in the one form the tracker stores: no port, no brackets, IPv4 in its own shape, and
    /// 'unknown' when nothing readable is there (the column is not nullable, and a hit with no address is
    /// still a hit at a known time).
    /// </summary>
    public static string Normalize(string? ip)
    {
        var address = Parse(ip);
        return address?.ToString() ?? AccessTrackingRules.UnknownIp;
    }

    /// <summary>
    /// Whether this address is a place the world can be asked about: not loopback, not a private network, not
    /// carrier-grade NAT, not a documentation address and not a multicast or reserved one. Anything unreadable
    /// is not a place either.
    /// </summary>
    public static bool IsPublic(string? ip)
    {
        var address = Parse(ip);

        if (address is null)
            return false;

        if (address.AddressFamily == AddressFamily.InterNetwork)
            return IsPublicV4(address);

        return address.AddressFamily == AddressFamily.InterNetworkV6 && IsPublicV6(address);
    }

    /// <summary>The address as the runtime reads it, or null when it is not an address at all.</summary>
    private static IPAddress? Parse(string? value)
    {
        var text = (value ?? string.Empty).Trim();

        if (text.Length == 0)
            return null;

        if (text[0] == '[')
        {
            // '[2001:db8::1]:443' - the brackets delimit the address, the port is not part of it.
            var close = text.IndexOf(']');
            if (close > 0)
                text = text[1..close];
        }
        else
        {
            // '1.2.3.4:5678' - one colon after a dotted address is a port. An IPv6 address has several colons,
            // which is why only the dotted form is cut here.
            var colon = text.IndexOf(':');
            if (colon > 0 && text.IndexOf(':', colon + 1) < 0 && text[..colon].Contains('.'))
                text = text[..colon];
        }

        if (!IPAddress.TryParse(text, out var address))
            return null;

        // '::ffff:1.2.3.4' is an IPv4 address written the long way; counting it as IPv6 would read one visitor
        // as two.
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }

    private static bool IsPublicV4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();

        // 0.0.0.0/8 'this network', 10/8 private, 127/8 loopback, 169.254/16 link local, 172.16/12 private,
        // 192.168/16 private, 100.64/10 carrier-grade NAT (the address a mobile network hands out).
        if (bytes[0] is 0 or 10 or 127)
            return false;

        if (bytes[0] == 169 && bytes[1] == 254)
            return false;

        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            return false;

        if (bytes[0] == 192 && bytes[1] == 168)
            return false;

        if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
            return false;

        // Documentation and benchmarking ranges (192.0.2/24, 198.51.100/24, 203.0.113/24, 198.18/15) are
        // never a customer, and 224/4 upward is multicast and reserved.
        if (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2)
            return false;

        if (bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19 || (bytes[1] == 51 && bytes[2] == 100)))
            return false;

        if (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113)
            return false;

        return bytes[0] < 224;
    }

    private static bool IsPublicV6(IPAddress address)
    {
        if (address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.IPv6None))
            return false;

        if (IPAddress.IsLoopback(address))
            return false;

        var bytes = address.GetAddressBytes();

        // fe80::/10 link local, fec0::/10 site local, ff00::/8 multicast.
        if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80)
            return false;

        if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0xC0)
            return false;

        if (bytes[0] == 0xFF)
            return false;

        // fc00::/7 unique local - the IPv6 counterpart of a private network.
        return (bytes[0] & 0xFE) != 0xFC;
    }
}
