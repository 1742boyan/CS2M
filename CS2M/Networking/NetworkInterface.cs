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
        public bool AutoApproveJoins = false;
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
            UpdateJoiningStatus();
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

        public void ApprovePlayer(long peerId)
        {
            var player = JoinQueue.FirstOrDefault(p => p.Connection.Id == peerId);
            if (player != null)
            {
                // Remove from queue in case it wasn't dequeued yet
                var newQueue = new Queue<RemotePlayer>(JoinQueue.Where(p => p.Connection.Id != peerId));
                JoinQueue = newQueue;
                UpdateJoiningStatus();
                ApprovePlayer(player);
            }
        }

        public void DenyPlayer(long peerId)
        {
            var player = JoinQueue.FirstOrDefault(p => p.Connection.Id == peerId);
            if (player != null)
            {
                var newQueue = new Queue<RemotePlayer>(JoinQueue.Where(p => p.Connection.Id != peerId));
                JoinQueue = newQueue;
                UpdateJoiningStatus();
                SendToClient(player, new JoinApprovalCommand { Approved = false });
                player.Connection.Disconnect();
            }
        }

        private void ApprovePlayer(RemotePlayer player)
        {
            JoiningPlayer = player;
            SendToClient(player, new JoinApprovalCommand { Approved = true });
            UpdateJoiningStatus();

            // Use 500KB chunk size (well within Steam's 512KB reliable limit)
            // This drastically reduces packet count and prevents buffer exhaustion.
            int maxPacketSize = 512000;
            maxPacketSize -= 25; // Maximum packet overhead as computed and tested in `PacketSizeOverhead` unit test

            // Send world
            TaskManager.instance.EnqueueTask("LoadMap", async () =>
            {
                SaveLoadHelper saveLoadHelper =
                    World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<SaveLoadHelper>();
                System.IO.MemoryStream stream = await saveLoadHelper.SaveGame();
                byte[] fullData = stream.ToArray();
                int remainingBytes = fullData.Length;
                bool newTransfer = true;

                var watch = new Stopwatch();
                watch.Start();

                Log.Debug($"Sending world with size of {fullData.Length} bytes. Slice size: {maxPacketSize}");
                for (int i = 0; i < fullData.Length; i += maxPacketSize)
                {
                    int sliceLen = System.Math.Min(maxPacketSize, fullData.Length - i);
                    byte[] slice = new byte[sliceLen];
                    System.Array.Copy(fullData, i, slice, 0, sliceLen);
                    
                    remainingBytes -= slice.Length;
                    var cmd = new WorldTransferCommand
                    {
                        WorldSlice = slice,
                        RemainingBytes = remainingBytes,
                        NewTransfer = newTransfer,
                    };

                    CommandInternal.Instance.SendToClient(player, cmd);

                    newTransfer = false;

                    // Throttle to avoid flooding the Steam buffer and blocking the main thread
                    if (i > 0 && (i / maxPacketSize) % 5 == 0)
                    {
                        await System.Threading.Tasks.Task.Delay(1);
                    }
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
                UpdateJoiningStatus();
                ProcessQueue();
            }
        }

        private void AbortJoining()
        {
            JoiningPlayer = null;
            UpdateJoiningStatus();
            ProcessQueue();
        }

        public void UpdateJoiningStatus()
        {
            CS2M.UI.UISystem.Instance?.RefreshJoinQueue();
            
            bool isJoining = JoiningPlayer != null || JoinQueue.Count > 0;
            string username = JoiningPlayer?.Username ?? (JoinQueue.Count > 0 ? JoinQueue.Peek().Username : "");
            int queueLength = JoinQueue.Count;

            var simSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<SimulationSystem>();
            if (simSystem != null)
            {
                if (isJoining && simSystem.selectedSpeed > 0f)
                {
                    _prePauseSpeed = simSystem.selectedSpeed;
                    simSystem.selectedSpeed = 0f;
                }
                else if (!isJoining && simSystem.selectedSpeed == 0f)
                {
                    simSystem.selectedSpeed = _prePauseSpeed > 0f ? _prePauseSpeed : 1f;
                }
            }

            CS2M.UI.UISystem.Instance?.SetPlayerJoiningStatus(isJoining, username, queueLength);
            
            SendToClients(new PlayerJoiningStatusCommand
            {
                IsJoining = isJoining,
                Username = username,
                QueueLength = queueLength
            });
        }
    }
}
