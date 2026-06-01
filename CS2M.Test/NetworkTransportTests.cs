using System;
using CS2M.Networking;
using CS2M.Networking.Transport;
using NUnit.Framework;

namespace CS2M.Test
{
    [TestFixture]
    public class NetworkTransportTests
    {
        [SetUp]
        public void Setup()
        {
            NetworkManager.IsSteamMode = false;
        }

        [Test]
        public void NetworkManager_DefaultMode_IsDirectIP()
        {
            Assert.That(NetworkManager.IsSteamMode, Is.False, "Default mode should not be Steam P2P.");
        }

        [Test]
        public void NetworkManager_SwitchesToSteamMode()
        {
            NetworkManager.IsSteamMode = true;
            Assert.That(NetworkManager.IsSteamMode, Is.True, "NetworkManager should switch to Steam mode.");
        }
    }
}
