﻿using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using JetBrains.Annotations;
using NineSolsAPI;
using TAS.Communication;
using TAS.Module;
using TAS.Tracer;
using TAS.Utils;
using UnityEngine;

namespace TAS;

[BepInDependency(NineSolsAPICore.PluginGUID)]
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class TasMod : BaseUnityPlugin {
    public static TasMod Instance = null!;
    public CelesteTasSettings TasSettings = null!;

    private Harmony harmony = null!;

    private ConfigEntry<bool> configOpenStudioOnLaunch = null!;
    private ConfigEntry<KeyboardShortcut> configOpenStudioShortcut = null!;

    private static void LaunchStudio() {
        var path = Assembly.GetAssembly(typeof(TasMod)).Location;
        if (path == "") return;

        var studioPath = Path.Join(Path.GetDirectoryName(path) ?? "", "CelesteStudio.exe");
        Log.Info($"Studio path at {studioPath}");

        if (File.Exists(studioPath)) {
            var p = new Process();
            p.StartInfo = new ProcessStartInfo(studioPath) { UseShellExecute = true };
            Log.Info("Trying to start");
            var success = p.Start();
            Log.Info($"Trying to start: {success}");
        }
    }

    private void Awake() {
        Log.Init(Logger);
        Instance = this;

        harmony = Harmony.CreateAndPatchAll(typeof(TasMod).Assembly);

        configOpenStudioOnLaunch = Config.Bind("Studio", "Launch on start", true);
        configOpenStudioShortcut = Config.Bind("Studio", "Launch", new KeyboardShortcut());

        if (configOpenStudioOnLaunch.Value) {
            LaunchStudio();
        }

        KeybindManager.Add(this, LaunchStudio, () => configOpenStudioShortcut.Value);

        TasSettings = new CelesteTasSettings();
        RCGLifeCycle.DontDestroyForever(gameObject);

        AttributeUtils.CollectAllMethods<LoadAttribute>();
        AttributeUtils.CollectAllMethods<UnloadAttribute>();
        AttributeUtils.CollectAllMethods<InitializeAttribute>();
        AttributeUtils.CollectAllMethods<BeforeTasFrame>();
        AttributeUtils.CollectAllMethods<AfterTasFrame>();


        try {
            AttributeUtils.Invoke<LoadAttribute>();
            AttributeUtils.Invoke<InitializeAttribute>();

            if (TasSettings.AttemptConnectStudio) CommunicationWrapper.Start();
        } catch (Exception e) {
            Log.Error($"Failed to load {PluginInfo.PLUGIN_GUID}: {e}");
        }

        // https://giannisakritidis.com/blog/Early-And-Super-Late-Update-In-Unity/
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
        Log.TasTrace("-- FRAME BEGIN --");
        
        TasTracerState.TraceVarsThroughFrame("EarlyUpdate");
    }

    private void PostLateUpdate() {
        TasTracerState.TraceVarsThroughFrame("PostLateUpdate");
        
        Log.TasTrace("-- FRAME END --");
        
        try {
            GameInfo.Update();

            AttributeUtils.Invoke<AfterTasFrame>();
            
            if (Manager.Running) {
                try {
                    if (Manager.CurrState is Manager.State.Running or Manager.State.FrameAdvance) {
                        TasTracer.TraceFrame();
                    } else {
                        if (TasTracer.TracePauseMode == TracePauseMode.Reduced) {
                            TasTracer.TraceFramePause();
                        } else if (TasTracer.TracePauseMode == TracePauseMode.Full) {
                            TasTracer.TraceFrame();
                        }
                    }
                } catch (Exception e) {
                    e.LogException("Error trying to collect trace data");
                }

            }
            
            AttributeUtils.Invoke<BeforeTasFrame>();
                
            TasTracerState.AddFrameHistory("StateBefore", new TracerIrrelevantState($"{Manager.CurrState} -> {Manager.NextState}"));
            TasTracerState.AddFrameHistory("UpdateMeta");
            Manager.UpdateMeta();
            if (Manager.Running) {
                TasTracerState.AddFrameHistory("Update");
                Manager.Update();
            }
            Log.TasTrace($"State: {Manager.CurrState} -> {Manager.NextState}");
            TasTracerState.AddFrameHistory("StateAfter", new TracerIrrelevantState($"{Manager.CurrState} -> {Manager.NextState}"));
            
            TasTracerState.AddFrameHistory("ADM End", $"{Player.i?.AnimationDeltaMove}");

            // TODO: ensure consistent fixedupdate
        } catch (Exception e) {
            e.LogException("");
            Manager.DisableRun();
        }
        
    }

    private void FixedUpdate() {
        Log.TasTrace($"-- FixedUpdate dt={Time.fixedDeltaTime}--");
        
        // TasTracerState.TraceVarsThroughFrame("FixedUpdate");
    }
    private void Update() {
        Log.TasTrace($"-- Update dt={Time.deltaTime}-- ");
        
        TasTracerState.TraceVarsThroughFrame("Update");
    }

    private void LateUpdate() {
        TasTracerState.TraceVarsThroughFrame("LateUpdate");
    }

    private void OnDestroy() {
        AttributeUtils.Invoke<UnloadAttribute>();
        if (Manager.Running) Manager.DisableRun();
        harmony.UnpatchSelf();

        CommunicationWrapper.SendReset();
        CommunicationWrapper.Stop();
    }
}

[AttributeUsage(AttributeTargets.Method)]
[MeansImplicitUse]
internal class LoadAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method)]
[MeansImplicitUse]
internal class UnloadAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method)]
[MeansImplicitUse]
internal class InitializeAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method)]
[MeansImplicitUse]
internal class BeforeTasFrame : Attribute;

[AttributeUsage(AttributeTargets.Method)]
[MeansImplicitUse]
internal class AfterTasFrame : Attribute;
