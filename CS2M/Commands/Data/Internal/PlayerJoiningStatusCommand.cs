using CS2M.API.Commands;
using LiteNetLib.Utils;

namespace CS2M.Commands.Data.Internal
{
    public class PlayerJoiningStatusCommand : CommandBase
    {
        public bool IsJoining { get; set; }
        public string Username { get; set; }
        public int QueueLength { get; set; }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(IsJoining);
            writer.Put(Username ?? string.Empty);
            writer.Put(QueueLength);
        }

        public override void Deserialize(NetDataReader reader)
        {
            IsJoining = reader.GetBool();
            Username = reader.GetString();
            QueueLength = reader.GetInt();
        }
    }
}
