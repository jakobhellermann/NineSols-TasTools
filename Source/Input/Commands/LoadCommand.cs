using System.Collections.Generic;
using NineSolsAPI;
using StudioCommunication;
using UnityEngine;

namespace TAS.Input.Commands;

public static class LoadCommand {
    private class LoadMeta : ITasCommandMeta {
        public string Insert =>
            $"load{CommandInfo.Separator}[0;Scene]{CommandInfo.Separator}[1;X]{CommandInfo.Separator}[2;Y]";

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

    [TasCommand("load", MetaDataProvider = typeof(LoadMeta))]
    private static void Load(CommandLine commandLine, int studioLine, string filePath, int fileLine) {
        if (commandLine.Arguments.Length != 3)
            AbortTas($"Invalid number of arguments in load command: '{commandLine.OriginalText}'.");

        var scene = commandLine.Arguments[0];
        var xString = commandLine.Arguments[1];
        var yString = commandLine.Arguments[2];

        if (!float.TryParse(xString, out var x)) {
            AbortTas($"Not a valid float: '{xString}'.");
            return;
        }

        if (!float.TryParse(yString, out var y)) {
            AbortTas($"Not a valid float: '{yString}'.");
            return;
        }

        if (!GameCore.IsAvailable()) return;
        var gameCore = GameCore.Instance;

        if (gameCore.gameLevel.SceneName != scene)
            gameCore.ChangeScene(new SceneConnectionPoint.ChangeSceneData {
                sceneName = scene,
                playerSpawnPosition = () => new Vector3(x, y, 0),
                changeSceneMode = SceneConnectionPoint.ChangeSceneMode.Teleport,
                findMode = SceneConnectionPoint.FindConnectionMode.ID,
            });

        gameCore.ResetLevel();

        if (Player.i is not { } player) return;
        player.transform.position = player.transform.position with { x = x, y = y };

        player.ChangeState(PlayerStateType.Normal);
        player.animator.Rebind();
        player.animator.Update(0f);
        player.Velocity = Vector2.zero;
        player.AnimationVelocity = Vector3.zero;
        player.Facing = Facings.Right;
        player.jumpState = Player.PlayerJumpState.None;
        player.varJumpSpeed = 0;
        player.GroundCheck();
        Physics2D.SyncTransforms();

        // CameraManager.Instance.ResetCamera2DDockerToPlayer();
        // CameraManager.Instance.camera2D.CenterOnTargets();
        // CameraManager.Instance.dummyOffset = Vector2.SmoothDamp(
        // SingletonBehaviour<CameraManager>.Instance.dummyOffset, direction, ref this.currentV, 0.25f);
    }
}