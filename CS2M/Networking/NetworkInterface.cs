using System.Collections.Generic;
using CS2M.Networking.Transport;
using System.Diagnostics;
using System.Linq;
using Colossal;
using CS2M.API.Commands;
using CS2M.API.Networking;
using CS2M.Commands;
using CS2M.Commands.ApiServer;
using CS2M.Commands.Data.Internal;
using CS2M.Helpers;
using LiteNetLib;
using Unity.Entities;
using Game.Simulation;

namespace CS2M.Networking
{
    public class NetworkInterface
    {
        public delegate void OnPlayerConnected(Player player);

        public delegate void OnPlayerDisconnected(Player player);

        public delegate void OnPlayerJoined(Player player);

        public delegate void OnPlayerLeft(Player player);

        private static NetworkInterface _instance;

        public readonly LocalPlayer LocalPlayer = new();

        /// <summary>
        ///     List of all players, which are connected on network level
        /// </summary>
        public List<Player> PlayerListConnected = new();

        /// <summary>
        ///     List of all players, which are connected on game level
        /// </summary>
        public List<Player> PlayerListJoined = new();

        public Queue<RemotePlayer> JoinQueue = new();
        public bool AutoApproveJoins = true;
        public RemotePlayer JoiningPlayer;
        private float _prePauseSpeed = 1f;

        public NetworkInterface()
        {
            PlayerListConnected.Add(LocalPlayer);
            PlayerListJoined.Add(LocalPlayer);
        }

        public static NetworkInterface Instance => _instance ??= new NetworkInterface();

        /// <summary>
        ///     Event is triggered, when a player is connected on the network level
        /// </summary>
        public event OnPlayerConnected PlayerConnectedEvent;

        /// <summary>
        ///     Event is triggered, when a player disconnects on the network level
        /// </summary>
        public event OnPlayerDisconnected PlayerDisconnectedEvent;

        /// <summary>
        ///     Event is triggered, when a player joins on the game level
        /// </summary>
        public event OnPlayerJoined PlayerJoinedEvent;

        /// <summary>
        ///     Event is triggered, when a player leaves on the game level
        /// </summary>
        public event OnPlayerLeft PlayerLeftEvent;

        public void OnUpdate()
        {
            LocalPlayer.OnUpdate();
        }

        public void Connect(ConnectionConfig connectionConfig)
        {
            LocalPlayer.GetServerInfo(connectionConfig);
        }

        public void UpdateLocalPlayerUsername(string username)
        {
            LocalPlayer.UpdateUsername(username);
        }

        public void StartServer(ConnectionConfig connectionConfig)
        {
            LocalPlayer.Playing(connectionConfig);
        }

        public void StopServer()
        {
            LocalPlayer.Inactive();
        }

        public void SendToAll(CommandBase message)
        {
            LocalPlayer.SendToAll(message);
        }

        public void SendToClient(Player player, CommandBase message)
        {
            if (player is RemotePlayer remotePlayer)
            {
                LocalPlayer.SendToClient(remotePlayer.Connection, message);
            }
            else
            {
                Log.Warn("Trying to send packet to non-csm player, ignoring.");
            }
        }

        public void SendToServer(CommandBase message)
        {
            LocalPlayer.SendToServer(message);
        }

        public void SendToApiServer(ApiCommandBase message)
        {
            LocalPlayer.SendToApiServer(message);
        }

        public void SendToClients(CommandBase message)
        {
            LocalPlayer.SendToClients(message);
        }

        public RemotePlayer GetPlayerByPeer(INetworkConnection peer)
        {
            return PlayerListConnected
                .Where(p => p is RemotePlayer)
                .Cast<RemotePlayer>()
                .FirstOrDefault(p => p.Connection.Id == peer.Id);
        }

        public bool IsPeerConnected(INetworkConnection peer)
        {
            return PlayerListConnected
                .Where(p => p is RemotePlayer)
                .Cast<RemotePlayer>()
                .Any(p => p.Connection.Id == peer.Id);
        }

        public bool IsPeerJoined(INetworkConnection peer)
        {
            return PlayerListJoined
                .Where(p => p is RemotePlayer)
                .Cast<RemotePlayer>()
                .Any(p => p.Connection.Id == peer.Id);
        }

        public void PlayerConnected(RemotePlayer player)
        {
            Log.Debug($"RemotePlayer '{player.Username}' connected. Adding to Join Queue.");
            PlayerListConnected.Add(player);
            PlayerConnectedEvent?.Invoke(player);

            JoinQueue.Enqueue(player);
            CS2M.UI.UISystem.Instance?.RefreshJoinQueue();
            ProcessQueue();
        }

        public void PlayerDisconnected(INetworkConnection peer)
        {
            var player = GetPlayerByPeer(peer);
            if (player != null)
            {
                PlayerListConnected.Remove(player);
                PlayerListJoined.Remove(player);
                PlayerDisconnectedEvent?.Invoke(player);

                if (JoinQueue.Contains(player))
                {
                    JoinQueue = new Queue<RemotePlayer>(JoinQueue.Where(p => p.Connection.Id != peer.Id));
                    CS2M.UI.UISystem.Instance?.RefreshJoinQueue();
                }
                
                if (JoiningPlayer != null && JoiningPlayer.Connection.Id == peer.Id)
                {
                    AbortJoining();
                }
            }
        }

        public void ProcessQueue()
        {
            if (JoiningPlayer != null)
                return; // Wait until current joining player finishes

            if (JoinQueue.Count > 0 && AutoApproveJoins)
            {
                var player = JoinQueue.Dequeue();
                CS2M.UI.UISystem.Instance?.RefreshJoinQueue();
                ApprovePlayer(player);
            }
        }

        public void ApprovePlayer(int peerId)
        {
            var player = JoinQueue.FirstOrDefault(p => p.Connection.Id == peerId);
            if (player != null)
            {
                // Remove from queue in case it wasn't dequeued yet
                var newQueue = new Queue<RemotePlayer>(JoinQueue.Where(p => p.Connection.Id != peerId));
                JoinQueue = newQueue;
                CS2M.UI.UISystem.Instance?.RefreshJoinQueue();
                ApprovePlayer(player);
            }
        }

        public void DenyPlayer(int peerId)
        {
            var player = JoinQueue.FirstOrDefault(p => p.Connection.Id == peerId);
            if (player != null)
            {
                var newQueue = new Queue<RemotePlayer>(JoinQueue.Where(p => p.Connection.Id != peerId));
                JoinQueue = newQueue;
                CS2M.UI.UISystem.Instance?.RefreshJoinQueue();
                SendToClient(player, new JoinApprovalCommand { Approved = false });
                player.Connection.Disconnect();
            }
        }

        private void ApprovePlayer(RemotePlayer player)
        {
            JoiningPlayer = player;
            SendToClient(player, new JoinApprovalCommand { Approved = true });

            var simSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<SimulationSystem>();
            if (simSystem != null)
            {
                _prePauseSpeed = simSystem.selectedSpeed;
                simSystem.selectedSpeed = 0f;
            }

            SendToClients(new PlayerJoiningStatusCommand
            {
                IsJoining = true,
                Username = player.Username,
                QueueLength = JoinQueue.Count
            });

            // Get max packet size from MTU discovery
            int maxPacketSize = player.Connection.GetMaxSinglePacketSize();
            maxPacketSize -= 25; // Maximum packet overhead as computed and tested in `PacketSizeOverhead` unit test

            // Send world
            TaskManager.instance.EnqueueTask("LoadMap", async () =>
            {
                SaveLoadHelper saveLoadHelper =
                    World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SaveLoadHelper>();
                SlicedPacketStream stream = await saveLoadHelper.SaveGame(maxPacketSize);
                int remainingBytes = (int)stream.Length;
                bool newTransfer = true;

                var watch = new Stopwatch();
                watch.Start();

                Log.Debug($"Sending world with size of {stream.Length} bytes. Slice size: {maxPacketSize}");
                foreach (byte[] slice in stream.GetSlices())
                {
                    remainingBytes -= slice.Length;
                    var cmd = new WorldTransferCommand
                    {
                        WorldSlice = slice,
                        RemainingBytes = remainingBytes,
                        NewTransfer = newTransfer,
                    };

                    CommandInternal.Instance.SendToClient(player, cmd);

                    newTransfer = false;
                }

                Log.Debug($"[SaveGame] Save game packaging took {watch.ElapsedMilliseconds}ms");
            });
        }

        public void KickJoiningPlayer()
        {
            if (JoiningPlayer != null)
            {
                JoiningPlayer.Connection.Disconnect();
                AbortJoining();
            }
        }

        public void ClientFinishedJoining(INetworkConnection peer)
        {
            if (JoiningPlayer != null && JoiningPlayer.Connection.Id == peer.Id)
            {
                PlayerListJoined.Add(JoiningPlayer);
                PlayerJoinedEvent?.Invoke(JoiningPlayer);
                
                JoiningPlayer = null;
                SendToClients(new PlayerJoiningStatusCommand { IsJoining = false });

                var simSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<SimulationSystem>();
                if (simSystem != null)
                {
                    simSystem.selectedSpeed = _prePauseSpeed;
                }

                ProcessQueue();
            }
        }

        private void AbortJoining()
        {
            JoiningPlayer = null;
            SendToClients(new PlayerJoiningStatusCommand { IsJoining = false });

            var simSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<SimulationSystem>();
            if (simSystem != null)
            {
                simSystem.selectedSpeed = _prePauseSpeed;
            }

            ProcessQueue();
        }
    }
}
