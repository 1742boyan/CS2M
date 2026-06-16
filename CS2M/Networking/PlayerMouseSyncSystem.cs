using CS2M.API.Commands;
using CS2M.Commands.Data.Internal;
using Game.Tools;
using Unity.Entities;

namespace CS2M.Networking
{
    public partial class PlayerMouseSyncSystem : SystemBase
    {
        private ToolRaycastSystem _toolRaycastSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            _toolRaycastSystem = World.GetExistingSystemManaged<ToolRaycastSystem>();
        }

        private float _timeSinceLastUpdate = 0f;

        protected override void OnUpdate()
        {
            var localPlayer = CS2M.Networking.NetworkInterface.Instance.LocalPlayer;
            if (localPlayer == null || localPlayer.PlayerType == CS2M.API.Networking.PlayerType.NONE) return;

            _timeSinceLastUpdate += UnityEngine.Time.deltaTime;
            if (_timeSinceLastUpdate < 0.1f) return; // 10 times a second
            _timeSinceLastUpdate = 0f;

            if (_toolRaycastSystem != null)
            {
                if (_toolRaycastSystem.GetRaycastResult(out var result))
                {
                    var cmd = new PlayerMouseCommand();
                    cmd.HitX = result.m_Hit.m_HitPosition.x;
                    cmd.HitY = result.m_Hit.m_HitPosition.y;
                    cmd.HitZ = result.m_Hit.m_HitPosition.z;
                    cmd.EntityIndex = result.m_Owner.Index;
                    cmd.EntityVersion = result.m_Owner.Version;
                    cmd.PlayerId = localPlayer.PlayerId;
                    cmd.PlayerName = localPlayer.Username;

                    CS2M.Commands.CommandInternal.Instance?.SendToClients(cmd);
                    CS2M.Commands.CommandInternal.Instance?.SendToServer(cmd);
                }
            }
        }
    }
}
