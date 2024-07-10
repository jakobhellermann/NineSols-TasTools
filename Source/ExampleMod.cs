using BepInEx;
using BepInEx.Configuration;
using NineSolsAPI;

namespace ExampleMod;

[BepInDependency(NineSolsAPICore.PluginGUID)]
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class ExampleMod : BaseUnityPlugin  {
    // https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/4_configuration.html
    private ConfigEntry<bool> enableSomethingConfig;

    private void Awake() {
        RCGLifeCycle.DontDestroyForever(gameObject);
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

        enableSomethingConfig = Config.Bind("General.Something", "Enable", true, "Enable the thing");
        
        ToastManager.Toast($"Something enable: {enableSomethingConfig.Value}");
    }

    private void OnDestroy() {
        // Make sure to clean up resources here to enable hot reloading
    }
}