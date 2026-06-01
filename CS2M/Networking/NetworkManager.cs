using CS2M.API;
using CS2M.API.Commands;
using CS2M.API.Networking;
using CS2M.Commands;
using CS2M.Commands.ApiServer;
using CS2M.Util;
using LiteNetLib;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using CS2M.Commands.Data.Internal;
using CS2M.Commands.Handler.Internal;
using CS2M.Networking.Transport;
using CS2M.Networking.Chirper;

namespace CS2M.Networking
{
    public class NetworkManager
    {
        private INetworkTransport _transport;
        private ApiServer _apiServer;
        private ConnectionConfig _connectionConfig;

        public event OnNatHolePunchSuccessful NatHolePunchSuccessfulEvent;
        public event OnNatHolePunchFailed NatHolePunchFailedEvent;
        public event OnClientConnectSuccessful ClientConnectSuccessfulEvent;
        public event OnClientConnectFailed ClientConnectFailedEvent;
        public event OnClientDisconnect ClientDisconnectEvent;

        public static bool IsSteamMode { get; set; } = false;

        public NetworkManager()
        {
        }

        public bool InitConnect(ConnectionConfig connectionConfig)
        {
            Log.Trace("NetworkManager: InitConnect");
            _connectionConfig = connectionConfig;

            if (IsSteamMode)
            {
                _transport = new SteamworksTransport();
            }
            else
            {
                _transport = new LiteNetLibTransport();
            }
            
            RegisterTransportEvents();

            if (_transport is LiteNetLibTransport lnlTransport)
            {
                _apiServer = new ApiServer(lnlTransport.GetNetManager());
            }

            return _transport.InitConnect(connectionConfig);
        }

        private void RegisterTransportEvents()
        {
            _transport.NatHolePunchSuccessfulEvent += () => { return NatHolePunchSuccessfulEvent?.Invoke() ?? false; };
            _transport.NatHolePunchFailedEvent += () => { return NatHolePunchFailedEvent?.Invoke() ?? false; };
            _transport.ClientConnectSuccessfulEvent += () => { return ClientConnectSuccessfulEvent?.Invoke() ?? false; };
            _transport.ClientConnectFailedEvent += () => { return ClientConnectFailedEvent?.Invoke() ?? false; };
            _transport.ClientDisconnectEvent += () => { return ClientDisconnectEvent?.Invoke() ?? false; };
            
            _transport.NetworkReceiveEvent += ListenerOnNetworkReceiveEvent;
            _transport.NetworkErrorEvent += ListenerOnNetworkErrorEvent;
            _transport.PeerConnectedEvent += ListenerOnPeerConnectedEvent;
            _transport.PeerDisconnectedEvent += ListenerOnPeerDisconnectedEvent;
        }

        public bool SetupNatConnect()
        {
            return _transport?.SetupNatConnect() ?? false;
        }

        public bool Connect()
        {
            return _transport?.Connect() ?? false;
        }

        public string GetConnectionPassword()
        {
            return _transport?.GetConnectionPassword() ?? "";
        }

        private void ListenerOnNetworkReceiveEvent(INetworkConnection peer, CommandBase command)
        {
            CommandHandler handler = CommandInternal.Instance.GetCommandHandler(command.GetType());
            Log.Trace($"NetworkManager: OnNetworkReceiveEvent [PeerId: {peer.Id}] {command.GetType()}");
            
            if (command is PreconditionsCheckCommand preconditionsCheckCommand)
            {
                if (peer.NativePeer is NetPeer netPeer)
                {
                    ((PreconditionsCheckHandler)handler).HandleOnServer(preconditionsCheckCommand, netPeer);
                }
                // TODO: Handle precondition check on server for Steamworks
                return;
            }

            if (NetworkInterface.Instance.LocalPlayer.PlayerType == PlayerType.SERVER)
            {
                bool isConnected = false;
                if (peer.NativePeer is NetPeer netPeer)
                {
                    isConnected = NetworkInterface.Instance.IsPeerConnected(netPeer);
                }
                
                if (!isConnected)
                {
                    return;
                }
            }

            handler.Parse(command);
        }

        private void ListenerOnPeerConnectedEvent(INetworkConnection peer)
        {
            Log.Trace($"NetworkManager: OnPeerConnectedEvent [PeerId: {peer.Id}]");
            if (NetworkInterface.Instance.LocalPlayer.PlayerType == PlayerType.CLIENT)
            {
                MultiplayerChirpSystem.Instance?.CreateCustomChirp("Successfully connected to server.");
                ClientConnectSuccessfulEvent?.Invoke();
            }
            else if (NetworkInterface.Instance.LocalPlayer.PlayerType == PlayerType.SERVER)
            {
                MultiplayerChirpSystem.Instance?.CreateCustomChirp("A player joined the game.");
                // TODO: timeout logic for new connections
            }
        }

        private void ListenerOnPeerDisconnectedEvent(INetworkConnection peer, string disconnectInfo)
        {
            Log.Trace($"NetworkManager: OnPeerDisconnectedEvent [PeerId: {peer.Id}]");
            if (NetworkInterface.Instance.LocalPlayer.PlayerType == PlayerType.CLIENT)
            {
                MultiplayerChirpSystem.Instance?.CreateCustomChirp("Disconnected from server.");
                ClientDisconnectEvent?.Invoke();
            }
            else if (NetworkInterface.Instance.LocalPlayer.PlayerType == PlayerType.SERVER)
            {
                MultiplayerChirpSystem.Instance?.CreateCustomChirp("A player disconnected.");
                if (peer.NativePeer is NetPeer netPeer)
                {
                    NetworkInterface.Instance.GetPlayerByPeer(netPeer)?.HandleDisconnect();
                }
            }
        }

        private void ListenerOnNetworkErrorEvent(string source, SocketError socketError)
        {
            Log.Error($"Received an error from {source}. Code: {socketError}");
            MultiplayerChirpSystem.Instance?.CreateCustomChirp($"Network error: {socketError}");
        }

        public void ProcessEvents()
        {
            _transport?.ProcessEvents();
            _apiServer?.KeepAlive(_connectionConfig);
        }

        public void SendToAllClients(CommandBase message)
        {
            _transport?.SendToAllClients(message);
        }

        public void SendToClient(NetPeer peer, CommandBase message)
        {
            // Backward compatibility for LiteNetLib peers
            _transport?.SendToClient(new LiteNetConnection(peer), message);
        }

        public void SendToServer(CommandBase message)
        {
            _transport?.SendToServer(message);
        }

        public void SendToApiServer(ApiCommandBase message)
        {
            _apiServer?.SendCommand(message);
        }

        public bool StartServer(ConnectionConfig connectionConfig)
        {
            MultiplayerChirpSystem.Instance?.CreateCustomChirp("Multiplayer session started.");
            _connectionConfig = connectionConfig;
            
            if (IsSteamMode)
            {
                _transport = new SteamworksTransport();
                Steam.SteamInviteHandler.Instance.SetRichPresence();
            }
            else
            {
                _transport = new LiteNetLibTransport();
            }
            
            RegisterTransportEvents();

            if (_transport is LiteNetLibTransport lnlTransport)
            {
                _apiServer = new ApiServer(lnlTransport.GetNetManager());
            }

            return _transport.StartServer(connectionConfig);
        }

        public void Stop()
        {
            MultiplayerChirpSystem.Instance?.CreateCustomChirp("Multiplayer session stopped.");
            _transport?.Stop();
            _transport = null;
            if (IsSteamMode)
            {
                Steam.SteamInviteHandler.Instance.ClearRichPresence();
            }
        }
    }
}
