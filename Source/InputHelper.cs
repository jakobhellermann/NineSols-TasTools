using System;
using System.Collections.Generic;
using HarmonyLib;
using InControl;
using NineSolsAPI;
using StudioCommunication;
using UnityEngine;

namespace TAS;

[HarmonyPatch]
public static class InputHelper {
    #region Actual TimeScale Patches

    private static float actualTimeScale = Time.timeScale;

    [HarmonyPatch(typeof(RCGTime), nameof(RCGTime.timeScale), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool TimeScaleGet(ref float __result) {
        __result = actualTimeScale;
        return false;
    }

    [HarmonyPatch(typeof(RCGTime), nameof(RCGTime.timeScale), MethodType.Setter)]
    [HarmonyPrefix]
    public static bool TimeScaleSet(ref float value) {
        actualTimeScale = value;
        // Time.timeScale = value;
        return false;
    }

    [HarmonyPatch(typeof(RCGTime), nameof(RCGTime.GlobalSimulationSpeed), MethodType.Setter)]
    [HarmonyPrefix]
    public static bool GlobalSimSpeedSet(float value) {
        RCGTime._globalSimulationSpeed = value;
        // Time.timeScale = RCGTime._globalSimulationSpeed * actualTimeScale;

        return false;
    }

    #endregion

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

        ToastManager.Toast(Application.targetFrameRate);
        ToastManager.Toast(Time.captureFramerate);
    }

    private static int? previousTargetFramerate;

    [EnableRun]
    private static void EnableRun() {
        InputManager.SuspendInBackground = false;
        InputManager.Enabled = true;
        InputManager.ClearInputState();

        SetFramerate(DefaultTasFramerate);

        if (previousTargetFramerate == null) {
            previousTargetFramerate = Application.targetFrameRate;
            UnlockTargetFramerate();
        }
    }

    [DisableRun]
    private static void DisableRun() {
        InputManager.SuspendInBackground = true;
        InputManager.ClearInputState();
        inputManagerUpdateInternal.Invoke();

        Time.captureFramerate = 0;

        if (previousTargetFramerate is { } framerate) {
            Application.targetFrameRate = framerate;
            previousTargetFramerate = null;
        }
    }


    [HarmonyPatch(typeof(InputManager), "UpdateInternal")]
    [HarmonyPrefix]
    public static bool InputManagerUpdate() {
        if (!Manager.Running) return true;

        if (Manager.SkipFrame) return false;
        // ToastManager.Toast(
        // $"imupdate  {Manager.Controller.CurrentFrameInTas} {Manager.Controller.Current} with dt {Time.deltaTime}");

        return true;
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
        if (Manager.Controller.Current is not { } current) return true;
        if (!Manager.Running) return true;

        foreach (var (action, actionKey) in actionKeyMap)
            if (current.Actions.Has(action) && actionKey == control)
                // ToastManager.Toast($"yes for {action}");
                __result = true;

        // ToastManager.Toast($"here with {control}");
        return false;
    }
}