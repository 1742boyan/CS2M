using HarmonyLib;
using Game.UI.InGame;
using Unity.Entities;
using CS2M.Networking.Chirper;
using Colossal.UI.Binding;

namespace CS2M.Patches
{
    [HarmonyPatch(typeof(ChirperUISystem))]
    public static class ChirperUISystemPatch
    {
        [HarmonyPatch("GetMessageID")]
        [HarmonyPrefix]
        public static bool GetMessageID_Prefix(Entity chirp, ref string __result, ChirperUISystem __instance)
        {
            var entityManager = __instance.EntityManager;
            if (entityManager.HasComponent<CS2MCustomChirp>(chirp))
            {
                var customChirp = entityManager.GetComponentData<CS2MCustomChirp>(chirp);
                __result = customChirp.m_MessageKey.ToString();
                return false; // Skip original
            }
            return true;
        }

        [HarmonyPatch("BindChirpSender")]
        [HarmonyPrefix]
        public static bool BindChirpSender_Prefix(IJsonWriter binder, Entity entity, ChirperUISystem __instance)
        {
            var entityManager = __instance.EntityManager;
            if (entityManager.HasComponent<CS2MCustomChirp>(entity))
            {
                var customChirp = entityManager.GetComponentData<CS2MCustomChirp>(entity);
                binder.TypeBegin("chirper.ChirpSender");
                binder.PropertyName("entity");
                binder.Write(Entity.Null);
                binder.PropertyName("link");
                
                // Call public BindChirpLink with CustomName
                __instance.BindChirpLink(binder, Entity.Null, Game.UI.NameSystem.Name.CustomName(customChirp.m_SenderName.ToString()));
                
                binder.TypeEnd();
                return false; // Skip original
            }
            return true;
        }
    }
}
