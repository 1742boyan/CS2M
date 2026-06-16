using LiteNetLib;

namespace CS2M.Networking.Transport
{
    public class LiteNetConnection : INetworkConnection
    {
        private readonly NetPeer _peer;

        public LiteNetConnection(NetPeer peer)
        {
            _peer = peer;
        }

        public long Id => _peer.Id;
        
        public string Address => $"{_peer.Address}:{_peer.Port}";
        
        public void Disconnect()
        {
            _peer.Disconnect();
        }

        public object NativePeer => _peer;

        public int GetMaxSinglePacketSize()
        {
            return _peer.GetMaxSinglePacketSize(DeliveryMethod.ReliableOrdered);
        }
    }
}
