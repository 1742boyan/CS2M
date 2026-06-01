using Unity.Entities;
using Unity.Collections;

namespace CS2M.Networking.Chirper
{
    public struct CS2MCustomChirp : IComponentData
    {
        public FixedString128Bytes m_MessageKey;
        public FixedString64Bytes m_SenderName;
    }
}
