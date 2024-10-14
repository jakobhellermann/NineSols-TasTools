using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace TAS;

public class GameInfo {
    public static string StudioInfo = "";
    public static string LevelName = "";
    public static string ChapterTime = "";

    public static void Update(bool updateVel = false) {
        StudioInfo = UpdateInfoText();
        LevelName = (GameCore.IsAvailable() ? GameCore.Instance.gameLevel?.name : null) ?? "";
    }


    private static string UpdateInfoText() {
        var text = "";


        if (!SingletonBehaviour<GameCore>.IsAvailable()) return "not yet available\n";

        var core = GameCore.Instance;

        if (core.currentCoreState != GameCore.GameCoreState.Playing) {
            var coreState = typeof(GameCore.GameCoreState).GetEnumName(core.currentCoreState);
            text += $"{coreState}\n";
        }

        var player = core.player;
        if (player) {
            text += $"Pos: {(Vector2)player.transform.position}\n";
            text += $"Speed: {player.FinalVelocity}\n";
            text += $"HP: {player.health.CurrentHealthValue} (+{player.health.CurrentInternalInjury})\n";
            var state = typeof(PlayerStateType).GetEnumName(player.fsm.State);
            var inputState = player.playerInput.fsm.State;
            text += $"{state} {(inputState == PlayerInputStateType.Action ? "" : inputState.ToString())}\n";

            if (player.jumpState != Player.PlayerJumpState.None) {
                var varJumpTimer = player.currentVarJumpTimer;
                text +=
                    $"JumpState {player.jumpState} {(varJumpTimer > 0 ? varJumpTimer.ToString("0.00") : "")}\n";
            } else text += "\n";

            List<(bool, string)> flags = [
                (player.isOnWall, "isOnWall"),
                (player.isOnLedge, "isOnLedge"),
                (player.isOnRope, "isOnRope"),
                (player.kicked, "kicked"),
                (player.rollCooldownTimer <= 0, "CanDash"),
                (player.onGround, "OnGround"),
            ];

            var flagsStr = flags.Where(x => x.Item1).Join(x => x.Item2, " ");

            text += $"{flagsStr}\n";

            text += $"airjumpcount {player.airJumpCount}\n";
            text += $"dashcd {player.rollCooldownTimer}\n";
            text += $"jumpgracetimer {player.jumpGraceTimer}\n";


            var animInfo = player.animator.GetCurrentAnimatorStateInfo(0);
            var animName = player.animator.ResolveHash(animInfo.m_Name);
            text += $"anim state {animName} {animInfo.normalizedTime % 1 * 100:00.0}%\n";
        }

        var currentLevel = core.gameLevel;
        if (currentLevel)
            text += $"[{currentLevel.SceneName}] ({currentLevel.BlockCountX}x{currentLevel.BlockCountY})\n";

        if (core.currentCutScene is not null) text += $"{core.currentCutScene}";

        if (player.interactableFinder.CurrentInteractableArea is { } current) {
            text += "Interaction:\n";
            foreach (var interaction in current.ValidInteractions)
                text += $"{interaction}";
        }

        return text;
    }

    public static string FormatTime(long time) {
        var timeSpan = TimeSpan.FromTicks(time);
        // return $"{timeSpan.ShortGameplayFormat()}({ConvertMicroSecondToFrames(time)})";
        return "todo time format";
    }
}