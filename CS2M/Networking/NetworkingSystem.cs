using Unity.Entities;

namespace CS2M.Networking
{
    public partial class NetworkingSystem : SystemBase
    {

        private ConnectionConfig _config;
        private int _maxPlayers;

        protected override void OnCreate()
        {
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            NetworkInterface.Instance.OnUpdate();

            if (NetworkInterface.Instance.JoiningPlayer != null)
            {
                var toolSystem = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<Game.Tools.ToolSystem>();
                var defaultTool = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<Game.Tools.DefaultToolSystem>();
                if (toolSystem != null && defaultTool != null && toolSystem.activeTool != defaultTool)
                {
                    toolSystem.activeTool = defaultTool;
                }
            }

            CS2M.UI.UISystem.Instance?.UpdatePlayerMouseData();
        }

        protected override void OnDestroy()
        {
            NetworkInterface.Instance.LocalPlayer?.Inactive();
            base.OnDestroy();
        }
    }
}