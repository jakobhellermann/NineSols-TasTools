using System;
using BepInEx;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using JetBrains.Annotations;
using NineSolsAPI;
using TAS.Communication;
using TAS.Module;
using TAS.Utils;

namespace TAS;

[BepInDependency(NineSolsAPICore.PluginGUID)]
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class TasMod : BaseUnityPlugin {
    public static TasMod Instance = null!;
    public CelesteTasSettings TasSettings = null!;

    private Harmony harmony = null!;

    private void Awake() {
        Log.Init(Logger);
        Instance = this;

        harmony = Harmony.CreateAndPatchAll(typeof(TasMod).Assembly);

        TasSettings = new CelesteTasSettings(Config);
        RCGLifeCycle.DontDestroyForever(gameObject);

        AttributeUtils.CollectMethods<LoadAttribute>();
        AttributeUtils.CollectMethods<UnloadAttribute>();
        AttributeUtils.CollectMethods<InitializeAttribute>();


        try {
            AttributeUtils.Invoke<LoadAttribute>();
            AttributeUtils.Invoke<InitializeAttribute>();

            if (TasSettings.AttemptConnectStudio) CommunicationWrapper.Start();
        } catch (Exception e) {
            Log.Error($"Failed to load {PluginInfo.PLUGIN_GUID}: {e}");
        }

        PlayerLoopHelper.AddAction(PlayerLoopTiming.EarlyUpdate, new PlayerLoopItem(this, EarlyUpdate));
        PlayerLoopHelper.AddAction(PlayerLoopTiming.PostLateUpdate, new PlayerLoopItem(this, PostLateUpdate));

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }

    private class PlayerLoopItem(TasMod mb, Action action) : IPlayerLoopItem {
        public bool MoveNext() {
            if (!mb) return false;
            action();
            return true;
        }
    }

    private void EarlyUpdate() {
        // if (Manager.Running && !Manager.SkipFrame) ToastManager.Toast("-- FRAME BEGIN --");
        Manager.PreFrameUpdate();
    }

    private void PostLateUpdate() {
        Manager.PostFrameUpdate();
        // if (Manager.Running && !Manager.SkipFrame) ToastManager.Toast("-- FRAME END --");
    }


    private void LateUpdate() {
        GameInfo.Update();
    }

    private void OnDestroy() {
        AttributeUtils.Invoke<UnloadAttribute>();
        Manager.DisableRun();
        harmony.UnpatchSelf();
    }
}

[AttributeUsage(AttributeTargets.Method)]
[MeansImplicitUse]
internal class LoadAttribute : Attribute {
}

[AttributeUsage(AttributeTargets.Method)]
[MeansImplicitUse]
internal class UnloadAttribute : Attribute {
}

[AttributeUsage(AttributeTargets.Method)]
[MeansImplicitUse]
internal class InitializeAttribute : Attribute {
}