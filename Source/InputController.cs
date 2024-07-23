using System.Collections.Generic;
using TAS.Input;

namespace TAS;

public class InputController {
    public static string? StudioTasFilePath;

    public bool Break => false;
    public bool CanPlayback => false;
    public bool HasFastForward => false;
    public float FastForwardSpeed;
    public object? NextCommentFastForward;
    public int CurrentFrameInInput => 0;
    public int CurrentFrameInTas => 0;

    public InputFrame? Previous => null;
    public InputFrame? Current => null;
    public InputFrame? Next => null;
    public readonly List<InputFrame> Inputs = new();

    public void AdvanceFrame(out bool canPlayback) {
        canPlayback = false;
    }

    public void RefreshInputs(bool enableRun) {
    }

    public void Stop() {
    }
}