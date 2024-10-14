using System.Collections.Generic;
using NineSolsAPI;
using StudioCommunication;

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
        ToastManager.Toast("load");
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

        if (Player.i is not { } player) return;
        player.transform.position = player.transform.position with { x = x, y = y };
    }
}