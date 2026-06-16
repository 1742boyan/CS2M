using CS2M.API.Commands;

namespace CS2M.Commands.Data.Internal
{
    public class PlayerJoiningStatusCommand : CommandBase
    {
        public bool IsJoining { get; set; }
        public string Username { get; set; }
        public int QueueLength { get; set; }
    }
}
