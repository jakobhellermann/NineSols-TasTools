using System;
using BepInEx;
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

    private void Awake() {
        Log.Init(Logger);
        Instance = this;

        TasSettings = new CelesteTasSettings(Config);
        RCGLifeCycle.DontDestroyForever(gameObject);

        AttributeUtils.CollectMethods<LoadAttribute>();
        AttributeUtils.CollectMethods<UnloadAttribute>();


        try {
            AttributeUtils.Invoke<LoadAttribute>();

            if (TasSettings.AttemptConnectStudio) {
                CommunicationWrapper.Start();
            }
        } catch (Exception e) {
            Log.Error($"Failed to load {PluginInfo.PLUGIN_GUID}: {e}");
        }

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }

    private void LateUpdate() {
        GameInfo.Update();
        ManagerOld.Update();
    }

    private void OnDestroy() {
        AttributeUtils.Invoke<UnloadAttribute>();
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal class LoadAttribute : Attribute {
}

[AttributeUsage(AttributeTargets.Method)]
internal class UnloadAttribute : Attribute {
}