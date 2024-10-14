using System.Collections.Generic;
using StudioCommunication;

namespace TAS.Input.Commands;

public static class LoadCommand {
    private class LoadMeta : ITasCommandMeta {
        public string Insert => $"Read{CommandInfo.Separator}[0;Scene]{CommandInfo.Separator}[1;Position]";
        public bool HasArguments => true;

        public IEnumerator<CommandAutoCompleteEntry> GetAutoCompleteEntries(string[] args, string filePath,
            int fileLine) {
            if (!GameCore.IsAvailable()) yield break;

            foreach (var scene in GameCore.Instance.allScenes)
                yield return new CommandAutoCompleteEntry { Name = scene, IsDone = true };
        }
    }

    [TasCommand("load")]
    private static void Load(CommandLine commandLine, int studioLine, string filePath, int fileLine) {
        var commandName = commandLine.Arguments[0].ToLowerInvariant();
        var commandArgs = commandLine.Arguments[1..];
    }
}