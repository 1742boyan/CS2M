using CS2M.API.Commands;
using CS2M.Commands.Data.Internal;
using CS2M.Networking;

namespace CS2M.Commands.Handler.Internal
{
    public class JoinApprovalHandler : CommandHandler<JoinApprovalCommand>
    {
        public JoinApprovalHandler()
        {
            TransactionCmd = false;
        }

        protected override void Handle(JoinApprovalCommand command)
        {
            if (command.Approved)
            {
                // Note: Actual transition is driven by WorldTransferCommand receipt, 
                // but we can log or trigger UI if we wanted to
                Log.Info("Host approved join request. Waiting for map data...");
            }
            else
            {
                Log.Info("Host denied join request.");
                NetworkInterface.Instance.LocalPlayer.Inactive();
            }
        }
    }
}
