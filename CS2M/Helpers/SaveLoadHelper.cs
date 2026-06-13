using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Colossal.IO.AssetDatabase;
using Colossal.Serialization.Entities;
using Game;
using Game.SceneFlow;
using Game.Serialization;
using Game.Settings;
using HarmonyLib;
using Unity.Jobs;
using UnityEngine;
using Hash128 = Colossal.Hash128;
using Version = Game.Version;

namespace CS2M.Helpers
{

    /// <summary>
    ///     Simple wrapper class around SaveGameData to simulate a save game.
    /// </summary>
    internal class SaveWrapper : SaveGameData
    {
        public SaveWrapper()
        {
            id = Identifier.None;
        }

        public override string ToString()
        {
            return "Multiplayer Save Game";
        }
    }

    public partial class SaveLoadHelper : GameSystemBase
    {
        private SaveGameSystem _saveGameSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            _saveGameSystem = World.GetOrCreateSystemManaged<SaveGameSystem>();
            Enabled = false;
        }

        public async Task<System.IO.MemoryStream> SaveGame()
        {
            // See GameManager::Save
            while (_saveGameSystem.Enabled)
            {
                // TODO: Sleep?
            }

            var watch = new Stopwatch();
            watch.Start();

            // Disable auto-save so it doesn't collide with our save process
            bool autoSaveEnabled = SharedSettings.instance.general.autoSave;
            SharedSettings.instance.general.autoSave = false;

            // Cleanup memory
            Resources.UnloadUnusedAssets();
            GC.Collect();

            Log.Debug($"[SaveGame] GC took {watch.ElapsedMilliseconds}ms");
            watch.Restart();

            // Save game to packet stream
            var stream = new System.IO.MemoryStream();
            _saveGameSystem.stream = stream;
            _saveGameSystem.context = new Context(Purpose.SaveGame, Version.current, Hash128.Empty);
            await _saveGameSystem.RunOnce();
            stream.Flush();

            Log.Debug($"[SaveGame] Save took {watch.ElapsedMilliseconds}ms");

            // Re-enable autosave if needed
            SharedSettings.instance.general.autoSave = autoSaveEnabled;
            return stream;
        }

        public static bool IsMultiplayerLoad = false;

        public async Task<bool> LoadGame(System.IO.MemoryStream data)
        {
            data.Position = 0;
            var saveGame = new SaveWrapper();
            ReadSystemPatch.Stream = data;
            AssetDataPatch.OverrideAssetData = true;
            IsMultiplayerLoad = true;
            bool result = await GameManager.instance.Load(GameMode.Game, Purpose.LoadGame, saveGame);
            IsMultiplayerLoad = false;
            AssetDataPatch.OverrideAssetData = false;
            ReadSystemPatch.Stream = null;
            return result;
        }

        protected override void OnUpdate()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    ///     This patch overrides the ReadBytes method that is called while deserializing a save game.
    ///     Instead of loading it from file, the bytes are extracted from our PacketStream.
    /// </summary>
    [HarmonyPatch]
    // ReSharper disable once ClassNeverInstantiated.Global
    internal class ReadSystemPatch
    {
        public static System.IO.MemoryStream Stream;

        public static unsafe bool Prefix(void* data, int bytes)
        {
            if (Stream == null)
            {
                return true;
            }

            int readBytes = 0;
            lock (Stream) 
            {
                byte[] buffer = new byte[bytes];
                readBytes = Stream.Read(buffer, 0, bytes);
                System.Runtime.InteropServices.Marshal.Copy(buffer, 0, new IntPtr(data), readBytes);
            }
            
            if (readBytes != bytes)
            {
                throw new IOException("Failed to read from multiplayer stream!");
            }

            return false;
        }

        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return typeof(StreamBinaryReader).GetMethod("ReadBytes",
                new[] { typeof(void).MakePointerType(), typeof(int) });
            yield return typeof(StreamBinaryReader).GetMethod("ReadBytes",
                new[] { typeof(void).MakePointerType(), typeof(int), typeof(JobHandle).MakeByRefType() });
        }
    }

    /// <summary>
    ///     This patch makes our custom SaveWrapper usable without throwing an exception.
    /// </summary>
    [HarmonyPatch(typeof(AssetData))]
    [HarmonyPatch(nameof(AssetData.GetAsyncReadDescriptor))]
    // ReSharper disable once ClassNeverInstantiated.Global
    internal class AssetDataPatch
    {
        public static bool OverrideAssetData;

        public static bool Prefix(AssetData __instance, ref AsyncReadDescriptor __result)
        {
            if (!OverrideAssetData || __instance is not SaveWrapper)
            {
                return true;
            }

            __result = new AsyncReadDescriptor("Multiplayer", "multiplayer");
            return false;
        }
    }

    /// <summary>
    ///     This patch ensures that the ContextFormat is initialized when loading a multiplayer save,
    ///     preventing a NullReferenceException in RequiredComponentSystem.
    /// </summary>
    [HarmonyPatch(typeof(LoadGameSystem))]
    [HarmonyPatch("OnUpdate")]
    internal class LoadGameSystemPatch
    {
        public static void Prefix(LoadGameSystem __instance)
        {
            if (SaveLoadHelper.IsMultiplayerLoad)
            {
                var ctx = __instance.context;
                // Re-initialize to ensure ContextFormat is properly allocated, preventing NRE in RequiredComponentSystem
                __instance.context = new Colossal.Serialization.Entities.Context(
                    ctx.purpose, 
                    ctx.version, 
                    ctx.instigatorGuid, 
                    0, 
                    Unity.Collections.Allocator.Persistent
                );
            }
        }
    }
}
