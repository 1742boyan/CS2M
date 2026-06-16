using CS2M.API.Commands;
using CS2M.Commands.Data.Internal;
using CS2M.Networking;

namespace CS2M.Commands.Handler.Internal
{
    public class WorldTransferHandler : CommandHandler<WorldTransferCommand>
    {
        protected override void Handle(WorldTransferCommand command)
        {
            Log.Trace($"WorldTransferHandler: Received slice, NewTransfer={command.NewTransfer}, RemainingBytes={command.RemainingBytes}");
            NetworkInterface.Instance.LocalPlayer.SliceReceived(command);
        }
    }
}
