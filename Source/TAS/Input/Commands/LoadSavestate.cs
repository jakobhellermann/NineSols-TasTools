using System.Collections.Generic;
using DebugModPlus;
using NineSolsAPI;
using StudioCommunication;
using TAS.Utils;
using UnityEngine;

namespace TAS.Input.Commands;

public static class LoadSavestateCommand {
    private class LoadSavestateMeta : ITasCommandMeta {
        public string Insert =>
            $"loadsavestate{CommandInfo.Separator}[0;Scene]{CommandInfo.Separator}[1;X]{CommandInfo.Separator}[2;Y]";

        public bool HasArguments => true;

        public IEnumerator<CommandAutoCompleteEntry> GetAutoCompleteEntries(string[] args, string filePath,
            int fileLine) {
            if (!GameCore.IsAvailable()) yield break;

            if (args.Length == 1) {
                GameCore.Instance.FetchScenes();
                foreach (var scene in GameCore.Instance.allScenes)
                    yield return new CommandAutoCompleteEntry { Name = scene, IsDone = true };
            }
        }
    }

    [TasCommand("load_savestate", MetaDataProvider = typeof(LoadSavestateMeta))]
    private static void LoadSavestate(CommandLine commandLine, int studioLine, string filePath, int fileLine) {
        if (commandLine.Arguments.Length != 1)
            AbortTas($"Invalid number of arguments in load command: '{commandLine.OriginalText}'.");

        var fullName = commandLine.Arguments[0];
        
        if (!GameCore.IsAvailable() || GameCore.Instance.gameLevel == null) {
            AbortTas("Attempted to start TAS outside of a level");
            return;
        }

        var task = DebugModPlus.DebugModPlus.Instance.SavestateModule.LoadSavestateAt(fullName);
        if (!task.IsCompleted) {
            ToastManager.Toast("Did not load savestate in a single frame");
        }
        /*var snapshot = new AnimatorSnapshot {
            StateHash = 1432961145,
            NormalizedTime = 0,
            ParamsFloat = new Dictionary<int, float>(),
            ParamsInt = new Dictionary<int, int>(),
            ParamsBool = new Dictionary<int, bool>(),
        };
        snapshot.Restore(Player.i.animator);*/

        // foreach(var condition in ConditionTimer.Instance.AllConditions) {
            // condition.SetFieldValue("_isFalseTimer", float.PositiveInfinity);
        // }
        
        // CameraManager.Instance.ResetCamera2DDockerToPlayer();
        // CameraManager.Instance.camera2D.CenterOnTargets();
        // CameraManager.Instance.dummyOffset = Vector2.SmoothDamp(
        // SingletonBehaviour<CameraManager>.Instance.dummyOffset, direction, ref this.currentV, 0.25f);
    }
}