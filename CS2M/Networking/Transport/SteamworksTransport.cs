using System;
using System.Collections.Generic;
using System.Net.Sockets;
using CS2M.API.Commands;
using CS2M.API.Networking;
using CS2M.Commands;
using CS2M.Commands.Data.Internal;
using Steamworks;

namespace CS2M.Networking.Transport
{
    public class SteamworksTransport : INetworkTransport
    {
        private HSteamListenSocket _listenSocket;
        private HSteamNetConnection _serverConnection;
        private readonly List<HSteamNetConnection> _connectedClients = new List<HSteamNetConnection>();
        private Callback<SteamNetConnectionStatusChangedCallback_t> _connectionStatusChanged;

        private ConnectionConfig _connectionConfig;

        public event OnNatHolePunchSuccessful NatHolePunchSuccessfulEvent;
        public event OnNatHolePunchFailed NatHolePunchFailedEvent;
        public event OnClientConnectSuccessful ClientConnectSuccessfulEvent;
        public event OnClientConnectFailed ClientConnectFailedEvent;
        public event OnClientDisconnect ClientDisconnectEvent;
        public event OnPeerConnected PeerConnectedEvent;
        public event OnPeerDisconnected PeerDisconnectedEvent;
        public event OnNetworkReceive NetworkReceiveEvent;
        public event OnNetworkError NetworkErrorEvent;

        public SteamworksTransport()
        {
            _connectionStatusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChanged);
        }

        public bool InitConnect(ConnectionConfig connectionConfig)
        {
            _connectionConfig = connectionConfig;
            // Steamworks doesn't require a generic "start" like LiteNetLib.
            return true;
        }

        public bool SetupNatConnect()
        {
            // Trigger hole punch success immediately since Steam handles NAT implicitly.
            NatHolePunchSuccessfulEvent?.Invoke();
            return true;
        }

        public bool Connect()
        {
            if (!ulong.TryParse(_connectionConfig.Token, out ulong steamId))
            {
                Log.Error("Invalid SteamID token.");
                ClientConnectFailedEvent?.Invoke();
                return false;
            }

            SteamNetworkingIdentity identity = new SteamNetworkingIdentity();
            identity.SetSteamID(new CSteamID(steamId));

            _serverConnection = SteamNetworkingSockets.ConnectP2P(ref identity, 0, 0, null);
            if (_serverConnection.m_HSteamNetConnection == 0)
            {
                Log.Error("Failed to initialize Steam P2P connection.");
                ClientConnectFailedEvent?.Invoke();
                return false;
            }

            return true;
        }

        public bool StartServer(ConnectionConfig connectionConfig)
        {
            _connectionConfig = connectionConfig;
            _listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null);

            if (_listenSocket.m_HSteamListenSocket == 0)
            {
                Log.Error("Failed to create Steam P2P listen socket.");
                return false;
            }

            Log.Info("Steam P2P Listen Socket created successfully.");
            return true;
        }

        public void Stop()
        {
            if (_serverConnection.m_HSteamNetConnection != 0)
            {
                SteamNetworkingSockets.CloseConnection(_serverConnection, 0, "Stopping client", false);
                _serverConnection = new HSteamNetConnection();
            }

            foreach (var client in _connectedClients)
            {
                SteamNetworkingSockets.CloseConnection(client, 0, "Stopping server", false);
            }
            _connectedClients.Clear();

            if (_listenSocket.m_HSteamListenSocket != 0)
            {
                SteamNetworkingSockets.CloseListenSocket(_listenSocket);
                _listenSocket = new HSteamListenSocket();
            }
        }

        public void ProcessEvents()
        {
            // SteamAPI.RunCallbacks() is already called by the game engine.
            // We just need to poll messages on our connections.

            if (_serverConnection.m_HSteamNetConnection != 0)
            {
                PollMessages(_serverConnection);
            }

            foreach (var client in _connectedClients)
            {
                PollMessages(client);
            }
        }

        private void PollMessages(HSteamNetConnection connection)
        {
            IntPtr[] messages = new IntPtr[16];
            int msgCount = SteamNetworkingSockets.ReceiveMessagesOnConnection(connection, messages, messages.Length);
            for (int i = 0; i < msgCount; i++)
            {
                SteamNetworkingMessage_t netMessage = (SteamNetworkingMessage_t)System.Runtime.InteropServices.Marshal.PtrToStructure(messages[i], typeof(SteamNetworkingMessage_t));
                byte[] payload = new byte[netMessage.m_cbSize];
                System.Runtime.InteropServices.Marshal.Copy(netMessage.m_pData, payload, 0, netMessage.m_cbSize);

                try
                {
                    CommandBase command = CommandInternal.Instance.Deserialize(payload);
                    NetworkReceiveEvent?.Invoke(new SteamConnection(connection), command);
                }
                catch (Exception e)
                {
                    Log.Error($"Failed to deserialize Steam P2P message: {e}");
                }

                SteamNetworkingMessage_t.Release(messages[i]);
            }
        }

        public void SendToAllClients(CommandBase message)
        {
            byte[] data = CommandInternal.Instance.Serialize(message);
            foreach (var client in _connectedClients)
            {
                SendData(client, data);
            }
        }

        public void SendToClient(INetworkConnection peer, CommandBase message)
        {
            if (peer.NativePeer is HSteamNetConnection connection)
            {
                byte[] data = CommandInternal.Instance.Serialize(message);
                SendData(connection, data);
            }
        }

        public void SendToServer(CommandBase message)
        {
            if (_serverConnection.m_HSteamNetConnection != 0)
            {
                byte[] data = CommandInternal.Instance.Serialize(message);
                SendData(_serverConnection, data);
            }
        }

        private void SendData(HSteamNetConnection connection, byte[] data)
        {
            IntPtr buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(data.Length);
            System.Runtime.InteropServices.Marshal.Copy(data, 0, buffer, data.Length);
            SteamNetworkingSockets.SendMessageToConnection(connection, buffer, (uint)data.Length, Constants.k_nSteamNetworkingSend_Reliable, out _);
            System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
        }

        public string GetConnectionPassword()
        {
            return _connectionConfig.Password;
        }

        private void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t pCallback)
        {
            var info = pCallback.m_info;
            var connection = pCallback.m_hConn;

            switch (info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    if (_listenSocket.m_HSteamListenSocket != 0 && info.m_hListenSocket == _listenSocket)
                    {
                        // Incoming connection request to our listen socket
                        SteamNetworkingSockets.AcceptConnection(connection);
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    if (connection == _serverConnection)
                    {
                        ClientConnectSuccessfulEvent?.Invoke();
                    }
                    else if (info.m_hListenSocket == _listenSocket)
                    {
                        _connectedClients.Add(connection);
                        PeerConnectedEvent?.Invoke(new SteamConnection(connection));
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    SteamNetworkingSockets.CloseConnection(connection, 0, "Closed", false);
                    
                    if (connection == _serverConnection)
                    {
                        ClientDisconnectEvent?.Invoke();
                        _serverConnection = new HSteamNetConnection();
                    }
                    else
                    {
                        _connectedClients.Remove(connection);
                        PeerDisconnectedEvent?.Invoke(new SteamConnection(connection), info.m_szEndDebug);
                    }
                    break;
            }
        }
    }
}
