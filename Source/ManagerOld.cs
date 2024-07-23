using StudioCommunication;
using TAS.Communication;

namespace TAS;

public static class ManagerOld {
    public static void Update() {
        SendStateToStudio();
    }

    public static void SendStateToStudio() {
        // if ultrafastforwarding && fraemcounter % then return

        StudioState state = new() {
            CurrentLine = 0,
            CurrentLineSuffix = "",
            CurrentFrameInTas = 0,
            TotalFrames = 0,
            SaveStateLine = 0,
            tasStates = States.None,
            GameInfo = GameInfo.StudioInfo,
            LevelName = GameInfo.LevelName,
            ChapterTime = GameInfo.ChapterTime,
            ShowSubpixelIndicator = false,
            SubpixelRemainder = (0, 0),
        };
        CommunicationWrapper.SendState(state);
    }
}