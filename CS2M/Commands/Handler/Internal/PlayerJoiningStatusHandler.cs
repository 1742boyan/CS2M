using CS2M.API.Commands;
using CS2M.Commands.Data.Internal;
using CS2M.UI;

namespace CS2M.Commands.Handler.Internal
{
    public class PlayerJoiningStatusHandler : CommandHandler<PlayerJoiningStatusCommand>
    {
        public PlayerJoiningStatusHandler()
        {
            TransactionCmd = false;
        }

        protected override void Handle(PlayerJoiningStatusCommand command)
        {
            UISystem.Instance.SetPlayerJoiningStatus(command.IsJoining, command.Username, command.QueueLength);
            
            var simSystem = Unity.Entities.World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<Game.Simulation.SimulationSystem>();
            if (simSystem != null)
            {
                simSystem.selectedSpeed = command.IsJoining ? 0f : 1f;
            }
        }
    }
}
