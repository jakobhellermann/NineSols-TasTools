using System;
using System.Collections.Concurrent;
using System.IO;
using JetBrains.Annotations;
using StudioCommunication;
using TAS.Communication;
using TAS.EverestInterop;
using TAS.Input;
using TAS.Utils;
using UnityEngine;

namespace TAS;

internal static class Engine {
    public static int FrameCounter => Time.frameCount;
}

internal static class TasRecorderUtils {
    public static bool Recording = false;
}

internal static class Extensions {
    public static bool Has(this States states, States flag) => (states & flag) == flag;

    public static void Set(ref this States states, States flag) => states |= flag;

    public static void Unset(ref this States states, States flag) => states &= ~flag;

    public static bool Has(this Actions states, Actions flag) => (states & flag) == flag;

    public static bool Has(this FileAttributes states, FileAttributes flag) => (states & flag) == flag;

    public static bool Has(this ExecuteTiming states, ExecuteTiming flag) => (states & flag) == flag;
}

public static class Manager {
    private static readonly ConcurrentQueue<Action> mainThreadActions = new();

    public static bool Running;
    public static bool Recording => TasRecorderUtils.Recording;
    public static readonly InputController Controller = new();
    public static States LastStates, States, NextStates;
    public static float FrameLoops { get; private set; } = 1f;
    public static bool UltraFastForwarding => FrameLoops >= 100 && Running;
    public static bool SlowForwarding => FrameLoops < 1f;
    public static bool AdvanceThroughHiddenFrame;


    private static bool SkipSlowForwardingFrame =>
        FrameLoops < 1f && (int)((Engine.FrameCounter + 1) * FrameLoops) == (int)(Engine.FrameCounter * FrameLoops);

    public static bool SkipFrame =>
        (States.Has(States.FrameStep) || SkipSlowForwardingFrame) && !AdvanceThroughHiddenFrame;

    static Manager() {
        AttributeUtils.CollectMethods<EnableRunAttribute>();
        AttributeUtils.CollectMethods<DisableRunAttribute>();
    }

    private static bool ShouldForceState =>
        NextStates.Has(States.FrameStep) && !Hotkeys.FastForward.OverrideCheck && !Hotkeys.SlowForward.OverrideCheck;

    public static void AddMainThreadAction(Action action) {
        // if (Thread.CurrentThread == MainThreadHelper.MainThread) TODO
        // action();
        // else
        mainThreadActions.Enqueue(action);
    }

    private static void ExecuteMainThreadActions() {
        while (mainThreadActions.TryDequeue(out var action)) action.Invoke();
    }

    public static void PreFrameUpdate() {
    }

    public static void PostFrameUpdate() {
        if (Running && !SkipFrame) {
            Controller.AdvanceFrame(out var canPlayback);

            // stop TAS if breakpoint is not placed at the end
            if (Controller.Break && Controller.CanPlayback && !Recording) {
                Controller.NextCommentFastForward = null;
                NextStates |= States.FrameStep;
                FrameLoops = 1;

                InputHelper.LockTargetFramerate();
            }

            if (!canPlayback)
                DisableRun();
            /* else if (SafeCommand.DisallowUnsafeInput && Controller.CurrentFrameInTas > 1) {
                if (Engine.Scene is not (Level or LevelLoader or LevelExit or Emulator or LevelEnter)) {
                    DisableRun();
                } else if (Engine.Scene is Level level && level.Tracker.GetEntity<TextMenu>() is { } menu) {
                    TextMenu.Item item = menu.Items.FirstOrDefault();
                    if ((item is TextMenu.Header { Title: { } title } &&
                         (title == Dialog.Clean("OPTIONS_TITLE") || title == Dialog.Clean("MENU_VARIANT_TITLE") ||
                          title == Dialog.Clean("MODOPTIONS_EXTENDEDVARIANTS_PAUSEMENU_BUTTON")
                              .ToUpperInvariant())) ||
                        item is TextMenuExt.HeaderImage { Image: "menu/everest" }) {
                        DisableRun();
                    }
                }
            }*/
        }


        LastStates = States;
        ExecuteMainThreadActions();
        Hotkeys.Update();
        // Savestates.HandleSaveStates();
        HandleFrameRates();
        CheckToEnable();
        FrameStepping();


        if (LastStates.Has(States.FrameStep) != States.Has(States.FrameStep)) {
            if (States.Has(States.FrameStep)) FrameStepStart();
            else FrameStepStop();
        }

        Running = States.Has(States.Enable);


        SendStateToStudio();
    }

    private static void HandleFrameRates() {
        FrameLoops = 1;

        // Keep frame rate consistant while recording
        if (Recording) return;

        if (States.Has(States.Enable) && !States.Has(States.FrameStep) && !NextStates.Has(States.FrameStep))
            if (Controller.HasFastForward)
                FrameLoops = Controller.FastForwardSpeed;
        /*if (Hotkeys.FastForward.Check) {
                FrameLoops = TasSettings.FastForwardSpeed;
            } else if (Hotkeys.SlowForward.Check) {
                FrameLoops = TasSettings.SlowForwardSpeed;
            }*/
    }

    private static void FrameStepping() {
        var frameAdvance = Hotkeys.FrameAdvance.Check && !Hotkeys.StartStop.Check;
        var pause = Hotkeys.PauseResume.Check && !Hotkeys.StartStop.Check;

        if (!States.Has(States.Enable)) return;

        if (NextStates.Has(States.FrameStep)) {
            States.Set(States.FrameStep);
            NextStates.Unset(States.FrameStep);
        }

        if (frameAdvance && !Hotkeys.FrameAdvance.LastCheck && !Recording) {
            if (!States.Has(States.FrameStep)) {
                States.Set(States.FrameStep);
                NextStates.Unset(States.FrameStep);
            } else {
                States.Unset(States.FrameStep);
                NextStates.Set(States.FrameStep);
            }
        } else if (pause && !Hotkeys.PauseResume.LastCheck && !Recording) {
            if (!States.Has(States.FrameStep)) {
                States.Set(States.FrameStep);
                NextStates.Unset(States.FrameStep);
            } else {
                States.Unset(States.FrameStep);
                NextStates.Unset(States.FrameStep);
            }
        } else if (LastStates.Has(States.FrameStep) && States.Has(States.FrameStep) &&
                   (Hotkeys.FastForward.Check || (Hotkeys.SlowForward.Check &&
                                                  Engine.FrameCounter %
                                                  Math.Round(4 / TasSettings.SlowForwardSpeed) == 0)) &&
                   !Hotkeys.FastForwardComment.Check) {
            States.Unset(States.FrameStep);
            NextStates.Set(States.FrameStep);
        }
    }

    private static void CheckToEnable() {
        if (Hotkeys.Restart.Released) {
            if (States.Has(States.Enable)) DisableRun();
            EnableRun();
            return;
        }

        if (Hotkeys.StartStop.Check) {
            if (States.Has(States.Enable))
                NextStates |= States.Disable;
            else
                NextStates |= States.Enable;
        } else if (NextStates.Has(States.Enable))
            EnableRun();
        else if (NextStates.Has(States.Disable)) DisableRun();
    }

    private static void FrameStepStart() {
        InputHelper.StopActualTime();
        if (Player.i is { } player) player.enabled = false;
        ConditionTimer.Instance.enabled = false;
    }

    private static void FrameStepStop() {
        InputHelper.WriteActualTime();
        if (Player.i is { } player) player.enabled = true;
        ConditionTimer.Instance.enabled = true;
    }

    public static void EnableRun() {
        if (!GameCore.IsAvailable() || GameCore.Instance.gameLevel is null) {
            Running = false;
            LastStates = States.None;
            States = States.None;
            NextStates = States.None;
            return;
        }

        AttributeUtils.Invoke<EnableRunAttribute>();
        Running = true;
        States |= States.Enable;
        States &= ~States.FrameStep;
        NextStates &= ~States.Enable;
        Controller.RefreshInputs(true);
    }

    public static void DisableRun() {
        if (States.Has(States.FrameStep)) FrameStepStop();

        Running = false;

        LastStates = States.None;
        States = States.None;
        NextStates = States.None;

        AttributeUtils.Invoke<DisableRunAttribute>();
        Controller.Stop();
    }

    public static void DisableRunLater() {
        NextStates |= States.Disable;
    }

    public static void SendStateToStudio() {
        if (UltraFastForwarding && Engine.FrameCounter % 23 > 0) return;

        var remainder = Player.i?.movementCounter;

        var previous = Controller.Previous;
        StudioState state = new() {
            CurrentLine = previous?.Line ?? -1,
            CurrentLineSuffix =
                $"{Controller.CurrentFrameInInput + (previous?.FrameOffset ?? 0)}{previous?.RepeatString ?? ""}",
            CurrentFrameInTas = Controller.CurrentFrameInTas,
            TotalFrames = Controller.Inputs.Count,
            // SaveStateLine = Savestates.StudioHighlightLine,
            SaveStateLine = -1,
            tasStates = States,
            GameInfo = GameInfo.StudioInfo,
            LevelName = GameInfo.LevelName,
            ChapterTime = GameInfo.ChapterTime,
            ShowSubpixelIndicator = TasSettings.InfoSubpixelIndicator && remainder is not null,
            SubpixelRemainder = (remainder?.x ?? 0, remainder?.y ?? 0),
        };
        CommunicationWrapper.SendState(state);
    }

    public static bool IsLoading() => false;
    /*switch (Engine.Scene) {
            case Level level:
                return level.IsAutoSaving() && level.Session.Level == "end-cinematic";
            case SummitVignette summit:
                return !summit.ready;
            case Overworld overworld:
                return (overworld.Current is OuiFileSelect { SlotIndex: >= 0 } slot &&
                        slot.Slots[slot.SlotIndex].StartingGame) ||
                       (overworld.Next is OuiChapterSelect && UserIO.Saving) ||
                       (overworld.Next is OuiMainMenu && (UserIO.Saving || Everest._SavingSettings));
            case Emulator emulator:
                return emulator.game == null;
            default:
                var isLoading = Engine.Scene is LevelExit or LevelLoader or GameLoader ||
                                Engine.Scene.GetType().Name == "LevelExitToLobby";
                return isLoading;
        }*/
}

[AttributeUsage(AttributeTargets.Method)]
[MeansImplicitUse]
internal class EnableRunAttribute : Attribute {
}

[AttributeUsage(AttributeTargets.Method)]
[MeansImplicitUse]
internal class DisableRunAttribute : Attribute {
}