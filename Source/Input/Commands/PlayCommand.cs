using System.Collections.Generic;
using System.IO;
using System.Linq;
using StudioCommunication;
using StudioCommunication.Util;

namespace TAS.Input.Commands;

public static class PlayCommand {
    private class PlayMeta : ITasCommandMeta {
        public string Insert => $"Play{CommandInfo.Separator}[0;Starting Label]";
        public bool HasArguments => true;

        public int GetHash(string[] args, string filePath, int fileLine) =>
            // Only file contents and line matters
            31 * File.ReadAllText(filePath).GetStableHashCode() + 17 * fileLine;

        public IEnumerator<CommandAutoCompleteEntry> GetAutoCompleteEntries(string[] args, string filePath,
            int fileLine) {
            if (args.Length != 1) yield break;

            // Don't include labels before the current line
            // TODO ReplaceLineEndings
            foreach (var line in File.ReadAllText(filePath).Split('\n').Skip(fileLine)) {
                if (!CommentLine.IsLabel(line)) continue;

                var label = line[1..]; // Remove the #
                yield return new CommandAutoCompleteEntry { Name = label, IsDone = true, HasNext = false };
            }
        }
    }

    // "Play, StartLabel",
    // "Play, StartLabel, FramesToWait"
    [TasCommand("Play", ExecuteTiming = ExecuteTiming.Parse, MetaDataProvider = typeof(PlayMeta))]
    private static void Play(CommandLine commandLine, int studioLine, string filePath, int fileLine) {
        var args = commandLine.Arguments;
        if (!ReadCommand.TryGetLine(args[0], InputController.TasFilePath, out var startLine)) {
            AbortTas($"\"Play, {string.Join(", ", args)}\" failed\n{args[0]} is invalid", true);
            return;
        }

        if (args.Length > 1 && int.TryParse(args[1], out _)) Manager.Controller.AddFrames(args[1], studioLine);

        if (startLine <= studioLine + 1) {
            Log.Warn("Play command does not allow playback from before the current line");
            return;
        }

        Manager.Controller.ReadFile(InputController.TasFilePath, startLine, int.MaxValue, startLine - 1);
    }
}