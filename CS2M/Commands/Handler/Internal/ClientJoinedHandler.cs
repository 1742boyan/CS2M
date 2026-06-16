using CS2M.API.Commands;
using CS2M.Commands.Data.Internal;
using CS2M.Networking;
using CS2M.Networking.Transport;

namespace CS2M.Commands.Handler.Internal
{
    public class ClientJoinedHandler : CommandHandler<ClientJoinedCommand>
    {
        public ClientJoinedHandler()
        {
            TransactionCmd = false;
        }

        protected override void Handle(ClientJoinedCommand command)
        {
        }

        public void HandleOnServer(ClientJoinedCommand command, INetworkConnection peer)
        {
            NetworkInterface.Instance.ClientFinishedJoining(peer);
        }
    }
}
