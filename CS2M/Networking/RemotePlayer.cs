using CS2M.API.Networking;
using LiteNetLib;
using CS2M.Networking.Transport;

namespace CS2M.Networking
{
    public class RemotePlayer : Player
    {
        public INetworkConnection Connection { get; }

        public RemotePlayer(INetworkConnection peer, string username, PlayerType playerType) : base()
        {
            Connection = peer;
            Username = username;
            PlayerType = playerType;
            PlayerStatusChangedEvent += PlayerStatusChanged;
            PlayerTypeChangedEvent += PlayerTypeChanged;
        }

        public RemotePlayer(string username, PlayerType playerType) : base()
        {
            Connection = null;
            Username = username;
            PlayerType = playerType;
            PlayerStatusChangedEvent += PlayerStatusChanged;
            PlayerTypeChangedEvent += PlayerTypeChanged;
        }

        public void PlayerStatusChanged(PlayerStatus oldPlayerStatus, PlayerStatus newPlayerStatus)
        {
            Log.Debug(
                $"RemotePlayer: {Username} ({PlayerId}) changed player status from {oldPlayerStatus} to {newPlayerStatus}");
        }

        public void PlayerTypeChanged(PlayerType oldPlayerType, PlayerType newPlayerType)
        {
            Log.ErrorWithStackTrace(
                $"RemotePlayer: {Username} ({PlayerId}) Player type for remote player shouldn't be changed after initialization");
        }

        public void HandleConnect()
        {
            Log.Trace($"RemotePlayer: {Username} ({PlayerId}) HandleConnect");
        }

        public void Disconnect()
        {
            Log.Trace($"RemotePlayer: {Username} ({PlayerId}) Disconnect called");
            Connection.Disconnect();
        }

        public void HandleDisconnect()
        {
            Log.Trace($"RemotePlayer: {Username} ({PlayerId}) HandleDisconnect");
        }
    }
}
