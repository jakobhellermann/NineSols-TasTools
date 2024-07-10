using BepInEx;
using BepInEx.Configuration;
using NineSolsAPI;
using UnityEngine;

namespace ExampleMod;

[BepInDependency(NineSolsAPICore.PluginGUID)]
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class ExampleMod : BaseUnityPlugin {
    // https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/4_configuration.html
    private ConfigEntry<bool> enableSomethingConfig;
    private ConfigEntry<KeyboardShortcut> somethingKeyboardShortcut;

    private void Awake() {
        RCGLifeCycle.DontDestroyForever(gameObject);

        enableSomethingConfig = Config.Bind("General.Something", "Enable", true, "Enable the thing");
        somethingKeyboardShortcut = Config.Bind("General.Something", "Shortcut",
            new KeyboardShortcut(KeyCode.Q, KeyCode.LeftShift), "Shortcut to execute");
        KeybindManager.Add(this, TestMethod, () => somethingKeyboardShortcut.Value);

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }

    private void TestMethod() {
        if (!enableSomethingConfig.Value) return;
        ToastManager.Toast("Shortcut activated");
    }

    private void OnDestroy() {
        // Make sure to clean up resources here to support hot reloading
    }
}