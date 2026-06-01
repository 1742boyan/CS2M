using System;
using Unity.Entities;
using Unity.Collections;
using Game.Common;
using Game.Triggers;
using Game.Prefabs;
using Game.UI.Localization;
using Colossal.Entities;
using Game.SceneFlow;
using HarmonyLib;
using Game.Simulation;

namespace CS2M.Networking.Chirper
{
    public partial class MultiplayerChirpSystem : Game.UpdateSystem
    {
        public static MultiplayerChirpSystem Instance { get; private set; }

        private EntityQuery m_TimeDataQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            Instance = this;
            m_TimeDataQuery = GetEntityQuery(ComponentType.ReadOnly<TimeData>());
        }

        protected override void OnUpdate()
        {
            // Empty update
        }

        public void CreateCustomChirp(string message)
        {
            CS2M.Log.Info($"Creating custom chirp: {message}");

            if (GameManager.instance == null || GameManager.instance.localizationManager == null)
            {
                CS2M.Log.Warn("Cannot create custom chirp: GameManager or localizationManager is null!");
                return; // Localization not ready
            }

            string uuid = Guid.NewGuid().ToString("N");
            string messageKey = $"CS2M_Chirp_{uuid}";

            // Register localization key at runtime
            GameManager.instance.localizationManager.activeDictionary.Add(messageKey, message, true);

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            Entity entity = entityManager.CreateEntity(
                typeof(Game.Triggers.Chirp),
                typeof(PrefabRef),
                typeof(Created),
                typeof(CS2MCustomChirp)
            );

            uint creationFrame = 0;
            if (!m_TimeDataQuery.IsEmptyIgnoreFilter)
            {
                creationFrame = m_TimeDataQuery.GetSingleton<TimeData>().m_FirstFrame; 
                
                var simSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<SimulationSystem>();
                if (simSystem != null)
                {
                    creationFrame = simSystem.frameIndex;
                }
            }

            // Find a valid Chirp prefab to satisfy ChirperUISystem's checks
            Entity prefabEntity = Entity.Null;
            var prefabQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Game.Prefabs.ChirpData>(), ComponentType.ReadOnly<Game.Prefabs.PrefabData>());
            if (!prefabQuery.IsEmptyIgnoreFilter)
            {
                var prefabs = prefabQuery.ToEntityArray(Allocator.Temp);
                if (prefabs.Length > 0)
                {
                    prefabEntity = prefabs[0];
                }
                prefabs.Dispose();
            }

            if (prefabEntity != Entity.Null)
            {
                entityManager.SetComponentData(entity, new PrefabRef { m_Prefab = prefabEntity });
            }
            else
            {
                CS2M.Log.Warn("Failed to find a valid Chirp prefab! Chirp might not show up.");
            }

            // Populate components
            // Chirp has likes, sender, etc.
            entityManager.SetComponentData(entity, new Game.Triggers.Chirp
            {
                m_CreationFrame = creationFrame,
                m_Likes = 0,
                m_Flags = 0
            });

            // Note: ChirperUISystem requires RandomLocalizationIndex as a buffer!
            // In the decompiled ChirperUISystem, GetMessageID checks: base.EntityManager.TryGetBuffer(chirp, true, out DynamicBuffer<RandomLocalizationIndex> buffer)
            // Wait! RandomLocalizationIndex is a buffer element?
            // "TryGetBuffer(chirp, true, out DynamicBuffer<RandomLocalizationIndex> buffer)"
            // Oh! Then RandomLocalizationIndex is a buffer component!
            // I should use AddBuffer instead of typeof() in CreateEntity.
            entityManager.AddBuffer<RandomLocalizationIndex>(entity).Add(new RandomLocalizationIndex { m_Index = 0 });

            entityManager.SetComponentData(entity, new CS2MCustomChirp
            {
                m_MessageKey = messageKey,
                m_SenderName = "CS2M"
            });
        }
    }
}
