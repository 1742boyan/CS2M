using CS2M.API.Commands;

namespace CS2M.Commands.Data.Internal
{
    public class PlayerMouseCommand : CommandBase
    {
        public float HitX { get; set; }
        public float HitY { get; set; }
        public float HitZ { get; set; }
        public int EntityIndex { get; set; }
        public int EntityVersion { get; set; }
        public int PlayerId { get; set; }
        public string PlayerName { get; set; }
    }
}
