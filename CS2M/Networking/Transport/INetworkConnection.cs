namespace CS2M.Networking.Transport
{
    public interface INetworkConnection
    {
        long Id { get; }
        string Address { get; }
        void Disconnect();
        
        /// <summary>
        /// Returns the underlying native connection object (e.g. NetPeer or HSteamNetConnection)
        /// </summary>
        object NativePeer { get; }
    }
}
