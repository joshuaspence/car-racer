using CarRacer.Core.Discovery;
using CarRacer.Core.Session;
using Xunit;

namespace CarRacer.Core.Tests
{
    public class ServerOptionsTests
    {
        [Fact]
        public void APlainLaunchIsNotAServer()
        {
            var options = ServerOptions.Parse(new[] { "-batchmode", "-nographics" });
            Assert.False(options.IsServer);
            Assert.Equal(DiscoveryConstants.DefaultGamePort, options.Port);
            Assert.Equal(ServerOptions.DefaultName, options.Name);
            Assert.Null(options.Error);
        }

        [Fact]
        public void ServerPortAndNameAreRead_AmongUnitysOwnArguments()
        {
            var options = ServerOptions.Parse(new[] { "-batchmode", "-SERVER", "-logFile", "-", "-port", "47900", "-name", "  Lounge PC  " });
            Assert.True(options.IsServer);
            Assert.Equal(47900, options.Port);
            Assert.Equal("Lounge PC", options.Name);
            Assert.Null(options.Error);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("70000")]
        [InlineData("abc")]
        [InlineData("47777")]
        public void ABadPortIsAnError_AndTheDefaultStays(string port)
        {
            var options = ServerOptions.Parse(new[] { "-server", "-port", port });
            Assert.NotNull(options.Error);
            Assert.Equal(DiscoveryConstants.DefaultGamePort, options.Port);
        }

        [Fact]
        public void AMissingValueIsHandled()
        {
            Assert.NotNull(ServerOptions.Parse(new[] { "-port" }).Error);
            Assert.Equal("Player", ServerOptions.Parse(new[] { "-name" }).Name);
        }
    }
}
