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
            
            // Uncap Steam Datagram Relay (SDR) limit from 1Mbps to 2Gbps to allow instant map transfers
            int sendRateMax = 2000000000;
            IntPtr pSendRateMax = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(int));
            System.Runtime.InteropServices.Marshal.WriteInt32(pSendRateMax, sendRateMax);
            SteamNetworkingUtils.SetConfigValue(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMax, ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global, IntPtr.Zero, ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32, pSendRateMax);
            System.Runtime.InteropServices.Marshal.FreeHGlobal(pSendRateMax);

            // Expand SDR buffer to 50MB to prevent k_EResultLimitExceeded and game freezing
            int sendBufferSize = 52428800;
            IntPtr pSendBufferSize = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(int));
            System.Runtime.InteropServices.Marshal.WriteInt32(pSendBufferSize, sendBufferSize);
            SteamNetworkingUtils.SetConfigValue(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize, ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global, IntPtr.Zero, ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32, pSendBufferSize);
            System.Runtime.InteropServices.Marshal.FreeHGlobal(pSendBufferSize);

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
            try
            {
                byte[] data = CommandInternal.Instance.Serialize(message);
                foreach (var client in _connectedClients)
                {
                    SendData(client, data);
                }
            }
            catch (Exception ex)
            {
                Log.Error("SteamworksTransport: Failed to SendToAllClients", ex);
            }
        }

        public void SendToClient(INetworkConnection peer, CommandBase message)
        {
            try
            {
                if (peer.NativePeer is HSteamNetConnection connection)
                {
                    byte[] data = CommandInternal.Instance.Serialize(message);
                    SendData(connection, data);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"SteamworksTransport: Failed to SendToClient {peer.Id}", ex);
            }
        }

        public void SendToServer(CommandBase message)
        {
            try
            {
                if (_serverConnection.m_HSteamNetConnection != 0)
                {
                    byte[] data = CommandInternal.Instance.Serialize(message);
                    SendData(_serverConnection, data);
                }
            }
            catch (Exception ex)
            {
                Log.Error("SteamworksTransport: Failed to SendToServer", ex);
            }
        }

        private void SendData(HSteamNetConnection connection, byte[] data)
        {
            IntPtr buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(data.Length);
            System.Runtime.InteropServices.Marshal.Copy(data, 0, buffer, data.Length);
            
            EResult result;
            do
            {
                result = SteamNetworkingSockets.SendMessageToConnection(connection, buffer, (uint)data.Length, Constants.k_nSteamNetworkingSend_Reliable, out _);
                if (result == EResult.k_EResultLimitExceeded)
                {
                    System.Threading.Thread.Sleep(1);
                }
                else if (result != EResult.k_EResultOK)
                {
                    Log.Error($"Failed to send Steam P2P message: {result}");
                    break;
                }
            } while (result == EResult.k_EResultLimitExceeded);

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
                    Log.Debug($"SteamworksTransport: Connection established {connection.m_HSteamNetConnection}");
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
                    Log.Warn($"SteamworksTransport: Connection closed or problem detected for {connection.m_HSteamNetConnection}. Info: {info.m_szEndDebug}");
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
