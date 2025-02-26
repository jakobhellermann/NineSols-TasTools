using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TAS.Tracer;
using UnityEngine;

namespace TAS;

[HarmonyPatch]
public static class TasTracerState {
    public static void AddFrameHistory(params object[] args) {
        frameHistory.Add(args);
    }
    public static void AddFrameHistoryPaused(params object[] args) {
        FrameHistoryPaused.Add(args);
    }
    
    private static readonly List<(string, Func<object?>)> traceVarsThroughFrame = [
        /*("snapshot", () =>  (
            Player.i.animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
            Player.i.animator.GetCurrentAnimatorStateInfo(0).normalizedTime
            ))*/
        // ("playerPos", () => Player.i?.transform.position),
    ];
    public static void TraceVarsThroughFrame(string phase) {
        var vars = traceVarsThroughFrame
            .ToDictionary(x => x.Item1, x => x.Item2.Invoke());
        if (vars.Count > 0) {
            AddFrameHistory($"ThroughFrame-{phase}", vars);
            AddFrameHistoryPaused($"ThroughFrame-{phase}", vars);
        }
    }


    
    private static List<object[]> frameHistory = [];
    internal static List<object[]> FrameHistoryPaused = [];
    
    [HarmonyPatch(typeof(PhysicsMover), nameof(PhysicsMover.Velocity), MethodType.Setter)]
    [HarmonyPatch(typeof(PhysicsMover), nameof(Actor.MoveH))]
    [HarmonyPatch(typeof(PhysicsMover), nameof(Actor.MoveV))]
    [HarmonyPatch(typeof(Actor), nameof(Actor.Move))]
    [HarmonyPrefix]
    private static void FrameHistory(MethodBase __originalMethod, object[] __args)
        => AddFrameHistory([$"{__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}", ..__args, new StackTrace()]);
    
    [HarmonyPatch(typeof(Player), "Update")]
    [HarmonyPrefix]
    private static void PlayerUpdate() {
        frameHistory.Add(["Player.Update", Time.deltaTime]);
    }
    
    [HarmonyPatch(typeof(PlayerAnimatorEvents), nameof(PlayerAnimatorEvents.AnimationDone))]
    [HarmonyPrefix]
    private static void HistoryAnimationDone(PlayerAnimationEventTag tag) => frameHistory.Add(["PlayerAnimationDone", tag.ToString(), Player.i.AnimationVelocity]);

    [BeforeTasFrame]
    private static void BeforeTasFrame() {
        frameHistory.Clear();
        FrameHistoryPaused.Clear();
    }
    
    [TasTraceAddState]
    private static void ExtendState(TraceData data) {
        var player = Player.i != null ? Player.i : null;
        var playerState =  player?.fsm.FindMappingState(player.fsm.State);
        
         data.Add("Position", player?.transform.position);
         data.Add("Subpixel", player?.movementCounter);
         data.Add("Velocity", player?.Velocity);
         data.Add("VelX", player?.VelX);
         data.Add("VelY", player?.VelY);
         data.Add("AnimationVelocity", player?.AnimationVelocity);
         data.Add("FinalVelocity", player?.FinalVelocity);
         data.Add("JumpFalseTimer", player?.jumpWasPressedCondition.FalseTimer);
         data.Add("CanJump", player?.CanJump);
         data.Add("AnimationTime", player?.animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
         // data.Add("PlayerState", playerState);
         data.Add("Info", GameInfo.StudioInfo);

         data.Add("FrameHistory", new List<object?[]>(frameHistory));
    }
}