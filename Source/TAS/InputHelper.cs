using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using InControl;
using StudioCommunication;
using TAS.Input;
using UnityEngine;

namespace TAS;

[HarmonyPatch]
public static class InputHelper {
    #region Actual TimeScale Patches

    private static float actualTimeScale = Time.timeScale;
    
    [HarmonyPatch(typeof(InputManager), "UpdateInternal")]
    [HarmonyPrefix]
    public static void InControlManagerUpdateInternal() {
        // Log.TasTrace("-- (update incontrolmanager) --");
    }
    

    [HarmonyPatch(typeof(RCGTime), nameof(RCGTime.timeScale), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool TimeScaleGet(ref float __result) {
        __result = actualTimeScale;
        return false;
    }

    [HarmonyPatch(typeof(RCGTime), nameof(RCGTime.timeScale), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool TimeScaleSet(ref float value) {
        actualTimeScale = value;
        // Time.timeScale = value;
        return false;
    }

    [HarmonyPatch(typeof(RCGTime), nameof(RCGTime.GlobalSimulationSpeed), MethodType.Setter)]
    [HarmonyPrefix]
    public static bool GlobalSimSpeedSet(float value) {
        var field = typeof(RCGTime).GetField("_globalSimulationSpeed", BindingFlags.NonPublic | BindingFlags.Static);
        if (field is null) {
            Log.Error("Could not set _globalSimulationSpeed: field does not exist");
            return true;
        }

        field.SetValue(null, value);
        // Time.timeScale = RCGTime._globalSimulationSpeed * actualTimeScale;

        return false;
    }

    #endregion

    
    [HarmonyPatch(typeof(Actor), nameof(Actor.OnRebindAnimatorMove))]
    [HarmonyPatch(typeof(Actor), nameof(Actor.Move))]
    [HarmonyPrefix]
    public static bool DontRunWhenPaused() => Manager.CurrState != Manager.State.Paused;

    public static void WriteActualTime() {
        Time.timeScale = actualTimeScale;
    }

    public static void StopActualTime() {
        Time.timeScale = 0;
    }


    private static Action inputManagerUpdateInternal =
        AccessTools.MethodDelegate<Action>(AccessTools.Method(typeof(InputManager), "UpdateInternal"));

    private const int DefaultTasFramerate = 60;

    public static void UnlockTargetFramerate() {
        Application.targetFrameRate = 0;
    }

    public static void LockTargetFramerate() {
        Application.targetFrameRate = Time.captureFramerate;
    }


    public static void SetFramerate(int framerate) {
        if (Application.targetFrameRate == Time.captureFramerate) Application.targetFrameRate = framerate;

        Time.captureFramerate = framerate;
    }

    private static int? previousTargetFramerate;

    [EnableRun]
    private static void EnableRun() {
        InputManager.SuspendInBackground = false;
        InputManager.Enabled = true;
        InputManager.ClearInputState();
        // typeof(InputManager).SetFieldValue("initialTime", Time.realtimeSinceStartup);
        // typeof(InputManager).SetFieldValue("currentTick", 0U);
        // typeof(InputManager).SetFieldValue("currentTime", 0f);

        SetFramerate(DefaultTasFramerate);

        if (previousTargetFramerate == null) {
            previousTargetFramerate = Application.targetFrameRate;
            UnlockTargetFramerate();
        }
        
        Log.TasTrace($"Set targetFramerate={Application.targetFrameRate} captureFramerate={Time.captureFramerate}");
    }

    [DisableRun]
    private static void DisableRun() {
        InputManager.SuspendInBackground = true;
        InputManager.ClearInputState();
        // inputManagerUpdateInternal.Invoke();

        Time.captureFramerate = 0;

        if (previousTargetFramerate is { } framerate) {
            Application.targetFrameRate = framerate;
            previousTargetFramerate = null;
        }
        
        Log.TasTrace($"Reset targetFramerate={Application.targetFrameRate} captureFramerate={Time.captureFramerate}");
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
        
        TasTracerState.AddFrameHistory("InputManager.Update"/*, InputManager.CurrentTick, InputManager.CurrentTime*/);
    }


    private static Dictionary<Actions, Key> actionKeyMap = new() {
        { Actions.Jump, Key.Space },
        { Actions.Dash, Key.LeftShift },
        { Actions.Up, Key.W },
        { Actions.Down, Key.S },
        { Actions.Left, Key.A },
        { Actions.Right, Key.D },
        { Actions.Grab, Key.K },
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