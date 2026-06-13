using CS2M.API.Commands;
using LiteNetLib.Utils;

namespace CS2M.Commands.Data.Internal
{
    public class JoinApprovalCommand : CommandBase
    {
        public bool Approved { get; set; }

        public override void Serialize(NetDataWriter writer)
        {
            writer.Put(Approved);
        }

        public override void Deserialize(NetDataReader reader)
        {
            Approved = reader.GetBool();
        }
    }
}
