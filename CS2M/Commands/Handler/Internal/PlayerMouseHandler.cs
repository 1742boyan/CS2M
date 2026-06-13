using CS2M.API.Commands;
using CS2M.Commands.Data.Internal;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Game.Common;

namespace CS2M.Commands.Handler.Internal
{
    public class PlayerMouseState
    {
        public float3 Position;
        public Entity HoveredEntity;
        public string PlayerName;
        public Entity PreviousHoveredEntity;
    }

    public class PlayerMouseHandler : CommandHandler<PlayerMouseCommand>
    {
        public static Dictionary<int, PlayerMouseState> PlayerMouseStates = new Dictionary<int, PlayerMouseState>();

        protected override void Handle(PlayerMouseCommand command)
        {
            if (!PlayerMouseStates.ContainsKey(command.PlayerId))
            {
                PlayerMouseStates[command.PlayerId] = new PlayerMouseState();
            }

            var state = PlayerMouseStates[command.PlayerId];
            state.PlayerName = command.PlayerName;
            state.Position = new float3(command.HitX, command.HitY, command.HitZ);
            
            var newEntity = new Entity { Index = command.EntityIndex, Version = command.EntityVersion };

            if (state.HoveredEntity != newEntity)
            {
                var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                
                if (state.HoveredEntity != Entity.Null && em.Exists(state.HoveredEntity))
                {
                    em.RemoveComponent<Game.Tools.Highlighted>(state.HoveredEntity);
                    em.AddComponentData(state.HoveredEntity, new Game.Common.BatchesUpdated());
                }

                state.PreviousHoveredEntity = state.HoveredEntity;
                state.HoveredEntity = newEntity;
                
                if (newEntity != Entity.Null && em.Exists(newEntity))
                {
                    em.AddComponentData(newEntity, new Game.Tools.Highlighted());
                    em.AddComponentData(newEntity, new Game.Common.BatchesUpdated());
                }
            }

            // Update UI System
            CS2M.UI.UISystem.Instance?.UpdatePlayerMouseData();
        }
    }
}
