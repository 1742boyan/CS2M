using CS2M.API.Networking;
using Steamworks;

namespace CS2M.Networking.Steam
{
    public class SteamInviteHandler
    {
        private static SteamInviteHandler _instance;
        private Callback<GameRichPresenceJoinRequested_t> _gameRichPresenceJoinRequested;

        public static SteamInviteHandler Instance => _instance ??= new SteamInviteHandler();

        private SteamInviteHandler()
        {
            // Initialize callback
            _gameRichPresenceJoinRequested = Callback<GameRichPresenceJoinRequested_t>.Create(OnGameRichPresenceJoinRequested);
        }

        public void Initialize()
        {
            // Just accessing the instance is enough to register the callback
            Log.Info("SteamInviteHandler initialized.");
        }

        private void OnGameRichPresenceJoinRequested(GameRichPresenceJoinRequested_t callback)
        {
            CSteamID friendId = callback.m_steamIDFriend;
            string connectString = callback.m_rgchConnect;

            Log.Info($"Steam overlay join requested by friend: {friendId}, Connect string: {connectString}");

            // Tell the user
            UI.UISystem uiSystem = Unity.Entities.World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<UI.UISystem>();
            
            // Wait, we need to trigger the NetworkManager to connect to this friend.
            ConnectionConfig config = new ConnectionConfig(friendId.m_SteamID.ToString());

            // Switch to Steam mode implicitly
            NetworkManager.IsSteamMode = true;

            // Stop any existing connection
            NetworkInterface.Instance.StopServer();

            // Ask LocalPlayer to connect
            NetworkInterface.Instance.Connect(config);
        }

        public void SetRichPresence()
        {
            // Set the connect string so that the "Join Game" button appears
            SteamFriends.SetRichPresence("connect", "steam_p2p");
            Log.Debug("Steam Rich Presence 'connect' set.");
        }

        public void ClearRichPresence()
        {
            SteamFriends.ClearRichPresence();
            Log.Debug("Steam Rich Presence cleared.");
        }
    }
}
