using BepInEx;
using NineSolsAPI;

namespace ExampleMod;

[BepInDependency(NineSolsAPICore.PluginGUID)]
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class ExampleMod : BaseUnityPlugin {
    private void Awake() {
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        
        ToastManager.Toast("Example Toast Message");
    }

    private void OnDestroy() {
        // Make sure to clean up resources here to enable hot reloading
    }
}