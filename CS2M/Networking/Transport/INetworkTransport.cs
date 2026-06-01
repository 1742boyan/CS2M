using CS2M.API.Commands;
using System.Net;
using System.Net.Sockets;

namespace CS2M.Networking.Transport
{
    public interface INetworkTransport
    {
        bool InitConnect(ConnectionConfig connectionConfig);
        bool SetupNatConnect();
        bool Connect();
        bool StartServer(ConnectionConfig connectionConfig);
        void Stop();
        void ProcessEvents();
        void SendToAllClients(CommandBase message);
        void SendToClient(INetworkConnection peer, CommandBase message);
        void SendToServer(CommandBase message);
        string GetConnectionPassword();
        
        event OnNatHolePunchSuccessful NatHolePunchSuccessfulEvent;
        event OnNatHolePunchFailed NatHolePunchFailedEvent;
        event OnClientConnectSuccessful ClientConnectSuccessfulEvent;
        event OnClientConnectFailed ClientConnectFailedEvent;
        event OnClientDisconnect ClientDisconnectEvent;

        event OnPeerConnected PeerConnectedEvent;
        event OnPeerDisconnected PeerDisconnectedEvent;
        event OnNetworkReceive NetworkReceiveEvent;
        event OnNetworkError NetworkErrorEvent;
    }

    public delegate bool OnNatHolePunchSuccessful();
    public delegate bool OnNatHolePunchFailed();
    public delegate bool OnClientConnectSuccessful();
    public delegate bool OnClientConnectFailed();
    public delegate bool OnClientDisconnect();

    public delegate void OnPeerConnected(INetworkConnection peer);
    public delegate void OnPeerDisconnected(INetworkConnection peer, string disconnectInfo);
    public delegate void OnNetworkReceive(INetworkConnection peer, CommandBase command);
    public delegate void OnNetworkError(string endpointAddress, SocketError socketError);
}
