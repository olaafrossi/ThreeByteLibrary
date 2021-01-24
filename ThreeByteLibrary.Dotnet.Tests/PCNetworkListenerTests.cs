using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Xunit;
using Newtonsoft.Json;

namespace ThreeByteLibrary.Dotnet.Tests
{
    public class PCNetworkListenerTests
    {

        public class UdpJsonData
        {
            public int UdpPortValue { get; set; }
        }

        [Fact]
        public void GetAppSettingsDataUdpPortShouldReturnAValueGreaterThanZero()
        {
            // Arrange
            var _pcNetworkListener= new PcNetworkListener();
            UdpJsonData data = JsonConvert.DeserializeObject<UdpJsonData>(File.ReadAllText(@"testudpdata.json"));

            int expected = 16009;

            // Act
            int actual = _pcNetworkListener.GetAppSettingsDataUdpPort();

            // Assert
            Assert.Equal(expected,actual);
        }
    }

}