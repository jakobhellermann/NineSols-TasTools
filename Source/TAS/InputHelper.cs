using System.Collections.Generic;
using HarmonyLib;
using InControl;
using StudioCommunication;
using TAS.Input;
using UnityEngine;

namespace TAS;

[HarmonyPatch]
public static class InputHelper {
    [HarmonyPatch(typeof(Actor), nameof(Actor.OnRebindAnimatorMove))]
    [HarmonyPatch(typeof(Actor), nameof(Actor.Move))]
    [HarmonyPatch(typeof(Player), "Update")]
    [HarmonyPrefix]
    public static bool DontRunWhenPaused() => Manager.CurrState != Manager.State.Paused;

    /*
    private static Action inputManagerUpdateInternal =
        AccessTools.MethodDelegate<Action>(AccessTools.Method(typeof(InputManager), "UpdateInternal"));
    */

    private const int DefaultTasFramerate = 60;

    private record FramerateState(int TargetFramerate, int VsyncCount) {
        public static FramerateState Save() => new(Application.targetFrameRate, QualitySettings.vSyncCount);

        public void Restore() {
            Application.targetFrameRate = TargetFramerate;
            QualitySettings.vSyncCount = VsyncCount;
        }
    }

    public static void SetTasFramerate(int framerate) {
        // If we have 1:1 tas playback, keep that
        if (Application.targetFrameRate == Time.captureFramerate) {
            Application.targetFrameRate = framerate;
        }

        Time.captureFramerate = framerate;
    }


    private static FramerateState framerateState = FramerateState.Save();

    [EnableRun]
    private static void EnableRun() {
        InputManager.SuspendInBackground = false;
        InputManager.Enabled = true;
        InputManager.ClearInputState();
        // typeof(InputManager).SetFieldValue("initialTime", Time.realtimeSinceStartup);
        // typeof(InputManager).SetFieldValue("currentTick", 0U);
        // typeof(InputManager).SetFieldValue("currentTime", 0f);

        framerateState = FramerateState.Save();
        Time.captureFramerate = DefaultTasFramerate;
        Application.targetFrameRate = DefaultTasFramerate;
        QualitySettings.vSyncCount = 0;
    }

    [DisableRun]
    private static void DisableRun() {
        InputManager.SuspendInBackground = true;
        InputManager.ClearInputState();
        // inputManagerUpdateInternal.Invoke();

        framerateState.Restore();
        Time.captureFramerate = 0;
    }

    private static InputFrame? currentFeed = null;

    public static void FeedInputs(InputFrame inputFrame) {
        Log.TasTrace($"Feeding {inputFrame}");
        currentFeed = inputFrame;
    }


    [HarmonyPatch(typeof(InputManager), "UpdateInternal")]
    [HarmonyPostfix]
    public static void InputManagerUpdate() {
        if (!Manager.Running) return;

        // TasTracerState.AddFrameHistory("InputManager.Update" /*, InputManager.CurrentTick, InputManager.CurrentTime*/);
    }

    private static Dictionary<Actions, Key> actionKeyMap = new() {
        { Actions.Up, Key.W },
        { Actions.Down, Key.S },
        { Actions.Left, Key.A },
        { Actions.Right, Key.D },
        
        { Actions.Jump, Key.Space },
        { Actions.Dash, Key.LeftShift },
        
        { Actions.Interact, Key.E },
        { Actions.Attack, Key.J },
        { Actions.Shoot, Key.C },
        { Actions.Parry, Key.K },
        
        { Actions.Talisman, Key.F },
        { Actions.Nymph, Key.Q },
        { Actions.Heal, Key.R },
    };

    [HarmonyPatch(typeof(UnityKeyboardProvider), nameof(UnityKeyboardProvider.GetKeyIsPressed))]
    [HarmonyPrefix]
    public static bool GetKeyIsPressed(Key control, ref bool __result) {
        if (!Manager.Running || currentFeed is null) return true;

        foreach (var (action, actionKey) in actionKeyMap) {
            if ((currentFeed.Actions & action) != 0 && actionKey == control) {
                __result = true;
            }
        }

        return false;
    }
}