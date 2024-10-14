using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TAS.Input.Commands;

public static class RepeatCommand {
    private record struct Arguments(int StartLine, int Count, int StartFrame) {
        public readonly int StartLine = StartLine;
        public readonly int Count = Count;
        public readonly int StartFrame = StartFrame;
    }

    private static readonly Dictionary<string, Arguments> RepeatArgs = new();

    [ClearInputs]
    private static void Clear() {
        RepeatArgs.Clear();
    }

    [ParseFileEnd]
    private static void ParseFileEnd() {
        if (!RepeatArgs.Any()) return;

        foreach (var pair in RepeatArgs) {
            var errorText = $"{Path.GetFileName(pair.Key)} line {pair.Value.StartLine - 1}\n";
            AbortTas($"{errorText}Repeat command does not have a paired EndRepeat command");
        }

        Manager.Controller.Clear();
    }

    // "Repeat, Count"
    [TasCommand("Repeat", ExecuteTiming = ExecuteTiming.Parse)]
    private static void Repeat(string[] args, int _, string filePath, int fileLine) {
        var errorText = $"{Path.GetFileName(filePath)} line {fileLine}\n";
        if (!args.Any())
            AbortTas($"{errorText}Repeat command no count given");
        else if (!int.TryParse(args[0], out var count))
            AbortTas($"{errorText}Repeat command's count is not an integer");
        else if (RepeatArgs.ContainsKey(filePath))
            AbortTas($"{errorText}Nesting repeat commands are not supported");
        else {
            if (count < 1) AbortTas($"{errorText}Repeat command's count must be greater than 0");

            RepeatArgs[filePath] = new Arguments(fileLine + 1, count, Manager.Controller.Inputs.Count);
        }
    }

    // "EndRepeat"
    [TasCommand("EndRepeat", ExecuteTiming = ExecuteTiming.Parse)]
    private static void EndRepeat(string[] _, int studioLine, string filePath, int fileLine) {
        var errorText = $"{Path.GetFileName(filePath)} line {fileLine}\n";
        if (!RepeatArgs.Remove(filePath, out var arguments)) {
            AbortTas($"{errorText}EndRepeat command does not have a paired Repeat command");
            return;
        }

        var endLine = fileLine - 1;
        var startLine = arguments.StartLine;
        var count = arguments.Count;
        var repeatStartFrame = arguments.StartFrame;

        if (count <= 1 || endLine < startLine || !File.Exists(filePath)) return;

        var inputController = Manager.Controller;
        var mainFile = filePath == InputController.TasFilePath;

        // first loop needs to set repeat index and repeat count
        if (mainFile)
            for (var i = repeatStartFrame; i < inputController.Inputs.Count; i++) {
                inputController.Inputs[i].RepeatIndex = 1;
                inputController.Inputs[i].RepeatCount = count;
            }

        IEnumerable<string> lines = File.ReadLines(filePath).Take(endLine).ToList();
        for (var i = 2; i <= count; i++)
            inputController.ReadLines(
                lines,
                filePath,
                startLine,
                mainFile ? startLine - 1 : studioLine,
                mainFile ? i : 0,
                mainFile ? count : 0
            );
    }
}