using System.Net;
using CarRacer.Core.Discovery;
using Xunit;

namespace CarRacer.Core.Tests
{
    public class HostAddressTests
    {
        [Theory]
        [InlineData("192.168.1.23", "192.168.1.23", DiscoveryConstants.DefaultGamePort)]
        [InlineData("  10.0.0.7  ", "10.0.0.7", DiscoveryConstants.DefaultGamePort)]
        [InlineData("192.168.1.23:47800", "192.168.1.23", 47800)]
        [InlineData("172.20.10.1:1", "172.20.10.1", 1)]
        [InlineData("10.0.0.0", "10.0.0.0", DiscoveryConstants.DefaultGamePort)]
        public void ValidAddressesParse(string text, string address, int port)
        {
            Assert.True(HostAddress.TryParse(text, out string parsed, out ushort parsedPort));
            Assert.Equal(address, parsed);
            Assert.Equal(port, parsedPort);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("192.168.1")]
        [InlineData("192.168.1.256")]
        [InlineData("192.168.1.23.4")]
        [InlineData("192.168..23")]
        [InlineData("a.b.c.d")]
        [InlineData("192.168.1.23:")]
        [InlineData("192.168.1.23:0")]
        [InlineData("192.168.1.23:70000")]
        [InlineData("192.168.1.23:-5")]
        [InlineData("+1.2.3.4")]
        [InlineData("1.2.3.0004")]
        [InlineData("1.2.3.007")]
        [InlineData("01.2.3.4")]
        [InlineData("192.168.001.010")]
        [InlineData("myphone.local")]
        public void InvalidAddressesDoNot(string text)
        {
            Assert.False(HostAddress.TryParse(text, out _, out _));
        }

        [Fact]
        public void LocalAddressesAreIPv4AndNeverLoopback()
        {
            foreach (string address in HostAddress.LocalIPv4Addresses())
            {
                var parsed = IPAddress.Parse(address);
                Assert.Equal(System.Net.Sockets.AddressFamily.InterNetwork, parsed.AddressFamily);
                Assert.False(IPAddress.IsLoopback(parsed), address);
                Assert.True(HostAddress.TryParse(address, out _, out _), address);
            }
        }
    }
}
