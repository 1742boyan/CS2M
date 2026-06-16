using Steamworks;

namespace CS2M.Networking.Transport
{
    public class SteamConnection : INetworkConnection
    {
        private readonly HSteamNetConnection _connection;

        public SteamConnection(HSteamNetConnection connection)
        {
            _connection = connection;
        }

        public long Id => _connection.m_HSteamNetConnection;

        public string Address => _connection.m_HSteamNetConnection.ToString();

        public void Disconnect()
        {
            SteamNetworkingSockets.CloseConnection(_connection, 0, "Disconnect", false);
        }

        public object NativePeer => _connection;

        public int GetMaxSinglePacketSize()
        {
            return 256000;
        }
    }
}
