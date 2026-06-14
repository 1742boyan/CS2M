using CS2M.API.Commands;
using CS2M.API.Networking;
using CS2M.Commands;
using CS2M.Commands.Data.Internal;
using CS2M.Util;
using LiteNetLib;
using System;
using System.Net;
using System.Net.Sockets;
using Timer = System.Timers.Timer;

namespace CS2M.Networking.Transport
{
    public class LiteNetLibTransport : INetworkTransport
    {
        private const string ConnectionKey = "CSM";

        private readonly NetManager _netManager;
        private readonly EventBasedNetListener _listener;
        private ConnectionConfig _connectionConfig;
        private IPEndPoint _connectEndpoint;
        private Timer _timeout;
        private bool _pollNatEvent = false;

        public event OnNatHolePunchSuccessful NatHolePunchSuccessfulEvent;
        public event OnNatHolePunchFailed NatHolePunchFailedEvent;
        public event OnClientConnectSuccessful ClientConnectSuccessfulEvent;
        public event OnClientConnectFailed ClientConnectFailedEvent;
        public event OnClientDisconnect ClientDisconnectEvent;

        public event OnPeerConnected PeerConnectedEvent;
        public event OnPeerDisconnected PeerDisconnectedEvent;
        public event OnNetworkReceive NetworkReceiveEvent;
        public event OnNetworkError NetworkErrorEvent;

        public LiteNetLibTransport()
        {
            _listener = new EventBasedNetListener();
            _netManager = new NetManager(_listener)
            {
                NatPunchEnabled = true,
                UnconnectedMessagesEnabled = true,
                MtuDiscovery = true,
            };

            _listener.NetworkReceiveEvent += ListenerOnNetworkReceiveEvent;
            _listener.NetworkErrorEvent += ListenerOnNetworkErrorEvent;
            _listener.PeerConnectedEvent += ListenerOnPeerConnectedEvent;
            _listener.PeerDisconnectedEvent += ListenerOnPeerDisconnectedEvent;
            _listener.ConnectionRequestEvent += ListenerOnConnectionRequestEvent;
        }

        public bool InitConnect(ConnectionConfig connectionConfig)
        {
            _connectionConfig = connectionConfig;
            return _netManager.Start();
        }

        public bool SetupNatConnect()
        {
            IPEndPoint directEndpoint = null;
            if (!_connectionConfig.IsTokenBased())
            {
                try
                {
                    directEndpoint = IPUtil.CreateIPEndPoint(_connectionConfig.HostAddress, _connectionConfig.Port);
                }
                catch
                {
                    return false;
                }
            }

            _pollNatEvent = true;

            EventBasedNatPunchListener natPunchListener = new EventBasedNatPunchListener();
            _timeout = new Timer
            {
                Interval = 5000,
                AutoReset = false
            };
            _timeout.Elapsed += (sender, args) =>
            {
                _pollNatEvent = false;
                _connectEndpoint = directEndpoint;
                NatHolePunchFailedEvent?.Invoke();
            };

            natPunchListener.NatIntroductionSuccess += (point, type, token) =>
            {
                _pollNatEvent = false;
                _connectEndpoint = point;
                bool? eventResult = NatHolePunchSuccessfulEvent?.Invoke();
                if (eventResult != null && eventResult.Value)
                {
                    _timeout.Enabled = false;
                }
            };

            string connect = "";
            if (_connectionConfig.IsTokenBased())
            {
                connect = "token:" + _connectionConfig.Token;
            }
            else if (directEndpoint != null)
            {
                connect = "ip:" + directEndpoint.Address;
            }

            _netManager.NatPunchModule.Init(natPunchListener);
            try
            {
                _netManager.NatPunchModule.SendNatIntroduceRequest(
                    IPUtil.CreateIP4EndPoint(Mod.Instance.Settings.ApiServer, Mod.Instance.Settings.GetApiServerPort()),
                    connect);
                _timeout.Start();
            }
            catch (Exception) { }

            return true;
        }

        public bool Connect()
        {
            if (_connectEndpoint == null) return false;

            try
            {
                _timeout = new Timer
                {
                    Interval = 5000,
                    AutoReset = false
                };
                _timeout.Elapsed += (sender, args) =>
                {
                    ClientConnectFailedEvent?.Invoke();
                };

                _netManager.Connect(_connectEndpoint, ConnectionKey);
                _timeout.Start();
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public bool StartServer(ConnectionConfig connectionConfig)
        {
            _connectionConfig = connectionConfig;
            bool result = _netManager.Start(_connectionConfig.Port);
            if (!result)
            {
                Stop();
                return false;
            }
            return true;
        }

        public void Stop()
        {
            _netManager.Stop();
        }

        public void ProcessEvents()
        {
            if (_pollNatEvent)
            {
                _netManager.NatPunchModule.PollEvents();
            }

            _netManager.PollEvents();
        }

        public void SendToAllClients(CommandBase message)
        {
            _netManager.SendToAll(CommandInternal.Instance.Serialize(message), DeliveryMethod.ReliableOrdered);
        }

        public void SendToClient(INetworkConnection peer, CommandBase message)
        {
            if (peer.NativePeer is NetPeer netPeer)
            {
                netPeer.Send(CommandInternal.Instance.Serialize(message), DeliveryMethod.ReliableOrdered);
            }
        }

        public void SendToServer(CommandBase message)
        {
            if (_netManager.ConnectedPeerList.Count > 0)
            {
                NetPeer server = _netManager.ConnectedPeerList[0];
                server.Send(CommandInternal.Instance.Serialize(message), DeliveryMethod.ReliableOrdered);
            }
        }

        public string GetConnectionPassword()
        {
            return _connectionConfig.Password;
        }

        private void ListenerOnNetworkReceiveEvent(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            try
            {
                CommandBase command = CommandInternal.Instance.Deserialize(reader.GetRemainingBytes());
                NetworkReceiveEvent?.Invoke(new LiteNetConnection(peer), command);
            }
            catch (Exception ex)
            {
                Log.Error($"LiteNetLibTransport: Failed to deserialize or process packet from {peer.Id}", ex);
            }
        }

        private void ListenerOnNetworkErrorEvent(IPEndPoint endpoint, SocketError socketError)
        {
            string source = endpoint != null ? $"{endpoint.Address}:{endpoint.Port}" : "<Unconnected>";
            NetworkErrorEvent?.Invoke(source, socketError);
        }

        private void ListenerOnPeerConnectedEvent(NetPeer peer)
        {
            Log.Debug($"LiteNetLibTransport: Peer connected {peer.Id}");
            if (_timeout != null)
            {
                _timeout.Enabled = false;
            }
            PeerConnectedEvent?.Invoke(new LiteNetConnection(peer));
        }

        private void ListenerOnPeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            Log.Warn($"LiteNetLibTransport: Peer disconnected {peer.Id}. Reason: {disconnectInfo.Reason}");
            PeerDisconnectedEvent?.Invoke(new LiteNetConnection(peer), disconnectInfo.Reason.ToString());
        }

        private void ListenerOnConnectionRequestEvent(ConnectionRequest request)
        {
            request.AcceptIfKey(ConnectionKey);
        }
        
        public NetManager GetNetManager()
        {
            return _netManager;
        }
    }
}
