using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using MonoMod.Utils;
using StudioCommunication;
using TAS.EverestInterop;
using TAS.Input.Commands;
using TAS.Utils;

namespace TAS.Input;

public class InputController {
    static InputController() {
        AttributeUtils.CollectMethods<ClearInputsAttribute>();
        AttributeUtils.CollectMethods<ParseFileEndAttribute>();
    }

    private static readonly Dictionary<string, FileSystemWatcher> watchers = new();
    private static string studioTasFilePath = string.Empty;

    public readonly SortedDictionary<int, List<Command>> Commands = new();
    public readonly SortedDictionary<int, FastForward> FastForwards = new();
    public readonly SortedDictionary<int, FastForward> FastForwardComments = new();
    public readonly Dictionary<string, List<Comment>> Comments = new();
    public readonly List<InputFrame> Inputs = new();
    private readonly Dictionary<string, byte> UsedFiles = new();

    public bool NeedsReload = true;
    public FastForward? NextCommentFastForward;

    private string? checksum;
    private string? savestateChecksum;

    public int CurrentParsingFrame { get; private set; }

    private static readonly string DefaultTasFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Celeste.tas");

    public static string StudioTasFilePath {
        get => studioTasFilePath;
        set {
            if (studioTasFilePath == value /*|| PlayTasAtLaunch.WaitToPlayTas*/) return;

            Manager.AddMainThreadAction(() => {
                studioTasFilePath = string.IsNullOrEmpty(value) ? value : Path.GetFullPath(value);

                var path = string.IsNullOrEmpty(value) ? DefaultTasFilePath : value;
                try {
                    if (!File.Exists(path)) File.WriteAllText(path, string.Empty);
                } catch {
                    studioTasFilePath = DefaultTasFilePath;
                }

                if (Manager.Running) Manager.DisableRunLater();

                Manager.Controller.Clear();

                // preload tas file
                Manager.Controller.RefreshInputs(true);
            });
        }
    }

    public static string TasFilePath =>
        string.IsNullOrEmpty(StudioTasFilePath) ? DefaultTasFilePath : StudioTasFilePath;

    // start from 1
    public int CurrentFrameInInput { get; private set; }

    // start from 1
    public int CurrentFrameInInputForHud { get; private set; }

    // start from 0
    public int CurrentFrameInTas { get; private set; }

    public InputFrame? Previous => Inputs.GetValueOrDefault(CurrentFrameInTas - 1);
    public InputFrame? Current => Inputs.GetValueOrDefault(CurrentFrameInTas);
    public InputFrame? Next => Inputs.GetValueOrDefault(CurrentFrameInTas + 1);
    public List<Command> CurrentCommands => Commands.GetValueOrDefault(CurrentFrameInTas);
    public bool CanPlayback => CurrentFrameInTas < Inputs.Count;
    public bool NeedsToWait => Manager.IsLoading();

    private FastForward CurrentFastForward => NextCommentFastForward ??
                                              FastForwards.FirstOrDefault(pair => pair.Key > CurrentFrameInTas).Value ??
                                              FastForwards.LastOrDefault().Value;

    public bool HasFastForward => CurrentFastForward is { } forward && forward.Frame > CurrentFrameInTas;

    public float FastForwardSpeed => /*RecordingCommand.StopFastForward
        ? 1
        : */CurrentFastForward is { } forward && forward.Frame > CurrentFrameInTas
        ? Math.Min(forward.Frame - CurrentFrameInTas, forward.Speed)
        : 1f;

    public bool Break => CurrentFastForward?.Frame == CurrentFrameInTas;
    private string Checksum => string.IsNullOrEmpty(checksum) ? checksum = CalcChecksum(Inputs.Count - 1) : checksum;

    public string SavestateChecksum {
        get => string.IsNullOrEmpty(savestateChecksum)
            ? savestateChecksum = CalcChecksum(CurrentFrameInTas)
            : savestateChecksum;
        private set => savestateChecksum = value;
    }

    public void RefreshInputs(bool enableRun) {
        if (enableRun) Stop();

        var lastChecksum = Checksum;
        var firstRun = UsedFiles.Count == 0;
        if (NeedsReload) {
            Clear();
            var tryCount = 5;
            while (tryCount > 0)
                if (ReadFile(TasFilePath)) {
                    if (Manager.NextStates.Has(States.Disable)) {
                        Clear();
                        Manager.DisableRun();
                    } else {
                        NeedsReload = false;
                        ParseFileEnd();
                        if (!firstRun && lastChecksum != Checksum) MetadataCommands.UpdateRecordCount(this);
                    }

                    break;
                } else {
                    Thread.Sleep(50);
                    tryCount--;
                    Clear();
                }

            CurrentFrameInTas = Math.Min(Inputs.Count, CurrentFrameInTas);
        }
    }

    public void Stop() {
        CurrentFrameInInput = 0;
        CurrentFrameInInputForHud = 0;
        CurrentFrameInTas = 0;
        NextCommentFastForward = null;
    }

    public void Clear() {
        CurrentParsingFrame = 0;
        checksum = string.Empty;
        savestateChecksum = string.Empty;
        Inputs.Clear();
        Commands.Clear();
        FastForwards.Clear();
        FastForwardComments.Clear();
        Comments.Clear();
        UsedFiles.Clear();
        NeedsReload = true;
        StopWatchers();
        AttributeUtils.Invoke<ClearInputsAttribute>();
    }

    private void StartWatchers() {
        foreach (var pair in UsedFiles) {
            var filePath = Path.GetFullPath(pair.Key);
            // watch tas file
            CreateWatcher(filePath);

            // watch parent folder, since watched folder's change is not detected
            while (filePath != null && Directory.GetParent(filePath) != null) {
                CreateWatcher(Path.GetDirectoryName(filePath));
                filePath = Directory.GetParent(filePath)?.FullName;
            }
        }

        void CreateWatcher(string filePath) {
            if (watchers.ContainsKey(filePath)) return;

            FileSystemWatcher watcher;
            if (File.GetAttributes(filePath).Has(FileAttributes.Directory)) {
                if (Directory.GetParent(filePath) is { } parentDir) {
                    watcher = new FileSystemWatcher();
                    watcher.Path = parentDir.FullName;
                    watcher.Filter = new DirectoryInfo(filePath).Name;
                    watcher.NotifyFilter = NotifyFilters.DirectoryName;
                } else
                    return;
            } else {
                watcher = new FileSystemWatcher();
                watcher.Path = Path.GetDirectoryName(filePath);
                watcher.Filter = Path.GetFileName(filePath);
            }

            watcher.Changed += OnTasFileChanged;
            watcher.Created += OnTasFileChanged;
            watcher.Deleted += OnTasFileChanged;
            watcher.Renamed += OnTasFileChanged;

            try {
                watcher.EnableRaisingEvents = true;
            } catch (Exception e) {
                Log.Error($"Failed watching folder: {watcher.Path}, filter: {watcher.Filter}: {e}");
                watcher.Dispose();
                return;
            }

            watchers[filePath] = watcher;
        }

        void OnTasFileChanged(object sender, FileSystemEventArgs e) {
            NeedsReload = true;
        }
    }

    private void StopWatchers() {
        foreach (var fileSystemWatcher in watchers.Values) fileSystemWatcher.Dispose();

        watchers.Clear();
    }

    private void ParseFileEnd() {
        StartWatchers();
        AttributeUtils.Invoke<ParseFileEndAttribute>();
    }

    public void AdvanceFrame(out bool canPlayback) {
        RefreshInputs(false);
        canPlayback = CanPlayback;

        if (NeedsToWait) return;

        if (CurrentCommands != null)
            foreach (var command in CurrentCommands) {
                if (command.Attribute.ExecuteTiming.Has(ExecuteTiming.Runtime) /*&&
                    (!EnforceLegalCommand.EnabledWhenRunning || command.Attribute.LegalInMainGame)*/)
                    command.Invoke();

                // SaveAndQuitReenter inserts inputs, so we can't continue executing the commands
                // It already handles the moving of all following commands
                if (command.Attribute.Name == "SaveAndQuitReenter") break;
            }

        if (!CanPlayback) return;

        // ExportGameInfo.ExportInfo();
        // StunPauseCommand.UpdateSimulateSkipInput();
        // InputHelper.FeedInputs(Current);

        if (CurrentFrameInInput == 0 || (Current.Line == Previous.Line && Current.RepeatIndex == Previous.RepeatIndex &&
                                         Current.FrameOffset == Previous.FrameOffset))
            CurrentFrameInInput++;
        else
            CurrentFrameInInput = 1;

        if (CurrentFrameInInputForHud == 0 || Current == Previous)
            CurrentFrameInInputForHud++;
        else
            CurrentFrameInInputForHud = 1;

        CurrentFrameInTas++;
    }

    // studioLine start from 0, startLine start from 1;
    public bool ReadFile(string filePath, int startLine = 0, int endLine = int.MaxValue, int studioLine = 0,
        int repeatIndex = 0,
        int repeatCount = 0) {
        try {
            if (!File.Exists(filePath)) return false;

            UsedFiles[filePath] = default;
            var lines = File.ReadLines(filePath).Take(endLine);
            ReadLines(lines, filePath, startLine, studioLine, repeatIndex, repeatCount);
            return true;
        } catch (Exception e) {
            Log.Warn(e);
            return false;
        }
    }

    public void ReadLines(IEnumerable<string> lines, string filePath, int startLine, int studioLine, int repeatIndex,
        int repeatCount,
        bool lockStudioLine = false) {
        var subLine = 0;
        foreach (var readLine in lines) {
            subLine++;
            if (subLine < startLine) continue;

            var lineText = readLine.Trim();

            if (Command.TryParse(this, filePath, subLine, lineText, CurrentParsingFrame, studioLine, out var command) &&
                command.Is("Play"))
                // workaround for the play command
                // the play command needs to stop reading the current file when it's done to prevent recursion
                return;

            if (lineText.StartsWith("***")) {
                FastForward fastForward = new(CurrentParsingFrame, lineText.Substring(3), studioLine);
                if (FastForwards.TryGetValue(CurrentParsingFrame, out var oldFastForward) && oldFastForward.SaveState &&
                    !fastForward.SaveState) {
                    // ignore
                } else
                    FastForwards[CurrentParsingFrame] = fastForward;
            } else if (lineText.StartsWith("#")) {
                FastForwardComments[CurrentParsingFrame] = new FastForward(CurrentParsingFrame, "", studioLine);
                if (!Comments.TryGetValue(filePath, out var comments))
                    Comments[filePath] = comments = new List<Comment>();

                comments.Add(new Comment(filePath, CurrentParsingFrame, subLine, lineText));
            } else /*if (!AutoInputCommand.TryInsert(filePath, lineText, studioLine, repeatIndex, repeatCount))*/
                AddFrames(lineText, studioLine, repeatIndex, repeatCount);

            if (filePath == TasFilePath && !lockStudioLine) studioLine++;
        }

        if (filePath == TasFilePath)
            FastForwardComments[CurrentParsingFrame] = new FastForward(CurrentParsingFrame, "", studioLine);
    }

    public void AddFrames(string line, int studioLine, int repeatIndex = 0, int repeatCount = 0, int frameOffset = 0) {
        if (!InputFrame.TryParse(line, studioLine, Inputs.LastOrDefault(), out var inputFrame, repeatIndex, repeatCount,
                frameOffset)) return;

        for (var i = 0; i < inputFrame.Frames; i++) Inputs.Add(inputFrame);

        // LibTasHelper.WriteLibTasFrame(inputFrame);
        CurrentParsingFrame += inputFrame.Frames;
    }

    public InputController Clone() {
        InputController clone = new();

        clone.Inputs.AddRange(Inputs);
        clone.FastForwards.AddRange((IDictionary)FastForwards);
        clone.FastForwardComments.AddRange((IDictionary)FastForwardComments);

        foreach (var filePath in Comments.Keys) clone.Comments[filePath] = new List<Comment>(Comments[filePath]);

        foreach (var frame in Commands.Keys) clone.Commands[frame] = new List<Command>(Commands[frame]);

        clone.NeedsReload = NeedsReload;
        clone.UsedFiles.AddRange((IDictionary)UsedFiles);
        clone.CurrentFrameInTas = CurrentFrameInTas;
        clone.CurrentFrameInInput = CurrentFrameInInput;
        clone.CurrentFrameInInputForHud = CurrentFrameInInputForHud;
        clone.SavestateChecksum = clone.CalcChecksum(CurrentFrameInTas);

        clone.checksum = checksum;
        clone.CurrentParsingFrame = CurrentParsingFrame;

        return clone;
    }

    public void CopyFrom(InputController controller) {
        Inputs.Clear();
        Inputs.AddRange(controller.Inputs);

        FastForwards.Clear();
        FastForwards.AddRange((IDictionary)controller.FastForwards);
        FastForwardComments.Clear();
        FastForwardComments.AddRange((IDictionary)controller.FastForwardComments);

        Comments.Clear();
        foreach (var filePath in controller.Comments.Keys)
            Comments[filePath] = new List<Comment>(controller.Comments[filePath]);

        Comments.Clear();
        foreach (var frame in controller.Commands.Keys) Commands[frame] = new List<Command>(controller.Commands[frame]);

        UsedFiles.Clear();
        UsedFiles.AddRange((IDictionary)controller.UsedFiles);

        NeedsReload = controller.NeedsReload;
        CurrentFrameInTas = controller.CurrentFrameInTas;
        CurrentFrameInInput = controller.CurrentFrameInInput;
        CurrentFrameInInputForHud = controller.CurrentFrameInInputForHud;

        checksum = controller.checksum;
        CurrentParsingFrame = controller.CurrentParsingFrame;
        savestateChecksum = controller.savestateChecksum;
    }

    public void CopyProgressFrom(InputController controller) {
        CurrentFrameInInput = controller.CurrentFrameInInput;
        CurrentFrameInInputForHud = controller.CurrentFrameInInputForHud;
        CurrentFrameInTas = controller.CurrentFrameInTas;
    }

    public void FastForwardToNextComment() {
        if (Manager.Running && Hotkeys.FastForwardComment.Pressed) {
            NextCommentFastForward = null;
            RefreshInputs(false);
            var next = FastForwardComments.FirstOrDefault(pair => pair.Key > CurrentFrameInTas).Value;
            if (next != null && HasFastForward && CurrentFastForward is { } last && next.Frame > last.Frame) {
                // NextCommentFastForward = last;
            } else
                NextCommentFastForward = next;

            Manager.States &= ~States.FrameStep;
            Manager.NextStates &= ~States.FrameStep;
        }
    }

    private string CalcChecksum(int toInputFrame) {
        StringBuilder result = new(TasFilePath);
        result.AppendLine();

        var checkInputFrame = 0;

        while (checkInputFrame < toInputFrame) {
            var currentInput = Inputs[checkInputFrame];
            result.AppendLine(currentInput.ToActionsString());

            if (Commands.GetValueOrDefault(checkInputFrame) is { } commands)
                foreach (var command in commands.Where(command => command.Attribute.CalcChecksum))
                    result.AppendLine(command.LineText);

            checkInputFrame++;
        }

        return HashHelper.ComputeHash(result.ToString());
    }

    public string CalcChecksum(InputController controller) => CalcChecksum(controller.CurrentFrameInTas);

#if DEBUG
    // ReSharper disable once UnusedMember.Local
    [Load]
    private static void RestoreStudioTasFilePath() {
        // studioTasFilePath = Engine.Instance.GetDynamicDataInstance().Get<string>(nameof(studioTasFilePath));
    }

    // for hot loading
    // ReSharper disable once UnusedMember.Local
    [Unload]
    private static void SaveStudioTasFilePath() {
        // Engine.Instance.GetDynamicDataInstance().Set(nameof(studioTasFilePath), studioTasFilePath);
        Manager.Controller.StopWatchers();
    }
#endif
}

[AttributeUsage(AttributeTargets.Method)]
internal class ClearInputsAttribute : Attribute {
}

[AttributeUsage(AttributeTargets.Method)]
internal class ParseFileEndAttribute : Attribute {
}

internal static class ListExtensions {
    public static T? GetValueOrDefault<T>(this IList<T> list, int index, T? defaultValue = default) =>
        index >= 0 && index < list.Count ? list[index] : defaultValue;
}

internal static class HashHelper {
    public static string ComputeHash(string text) =>
        SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(text)).ToHexadecimalString();
}