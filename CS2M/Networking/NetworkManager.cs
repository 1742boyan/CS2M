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
                try
                {
                    ((PreconditionsCheckHandler)handler).HandleOnServer(preconditionsCheckCommand, peer);
                }
                catch (Exception ex)
                {
                    Log.Error($"NetworkManager: Error handling PreconditionsCheckCommand", ex);
                }
                return;
            }

            if (command is ClientJoinedCommand clientJoinedCommand)
            {
                try
                {
                    ((ClientJoinedHandler)handler).HandleOnServer(clientJoinedCommand, peer);
                }
                catch (Exception ex)
                {
                    Log.Error($"NetworkManager: Error handling ClientJoinedCommand", ex);
                }
                return;
            }

            if (NetworkInterface.Instance.LocalPlayer.PlayerType == PlayerType.SERVER)
            {
                bool isConnected = NetworkInterface.Instance.IsPeerConnected(peer);
                if (!isConnected)
                {
                    return;
                }
            }

            try
            {
                handler.Parse(command);

                if (NetworkInterface.Instance.LocalPlayer.PlayerType == PlayerType.SERVER && handler.RelayOnServer)
                {
                    foreach (var p in NetworkInterface.Instance.PlayerListConnected)
                    {
                        if (p is RemotePlayer remotePlayer && remotePlayer.Connection.Id != peer.Id)
                        {
                            CommandInternal.Instance.SendToClient(remotePlayer, command);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"NetworkManager: Error handling command {command.GetType()}", ex);
            }
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
                NetworkInterface.Instance.GetPlayerByPeer(peer)?.HandleDisconnect();
                NetworkInterface.Instance.PlayerDisconnected(peer);
            }
        }

        private void ListenerOnNetworkErrorEvent(string source, SocketError socketError)
        {
            Log.Error($"Received an error from {source}. Code: {socketError}");
            MultiplayerChirpSystem.Instance?.CreateCustomChirp($"Network error: {socketError}");
        }

        public void ProcessEvents()
        {
            try
            {
                _transport?.ProcessEvents();
                _apiServer?.KeepAlive(_connectionConfig);
            }
            catch (Exception ex)
            {
                Log.Error("NetworkManager: Error processing events", ex);
            }
        }

        public void SendToAllClients(CommandBase message)
        {
            try
            {
                _transport?.SendToAllClients(message);
            }
            catch (Exception ex)
            {
                Log.Error("NetworkManager: Failed to send to all clients", ex);
            }
        }

        public void SendToClient(INetworkConnection peer, CommandBase message)
        {
            try
            {
                _transport?.SendToClient(peer, message);
            }
            catch (Exception ex)
            {
                Log.Error($"NetworkManager: Failed to send to client {peer.Id}", ex);
            }
        }

        public void SendToServer(CommandBase message)
        {
            try
            {
                _transport?.SendToServer(message);
            }
            catch (Exception ex)
            {
                Log.Error("NetworkManager: Failed to send to server", ex);
            }
        }

        public void SendToApiServer(ApiCommandBase message)
        {
            try
            {
                _apiServer?.SendCommand(message);
            }
            catch (Exception ex)
            {
                Log.Error("NetworkManager: Failed to send to API server", ex);
            }
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
