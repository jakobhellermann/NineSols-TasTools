namespace TAS;

public class GameInfo {
    public static string StudioInfo = "studioinfo";
    public static string LevelName = "levelname";
    public static string ChapterTime = "chaptertime";

    public static void Update(bool updateVel = false) {
        StudioInfo = UpdateInfoText();
    }


    private static string UpdateInfoText() {
        var text = "";

        if (!SingletonBehaviour<GameCore>.IsAvailable()) {
            return "not yet available\n";
        }

        var core = GameCore.Instance;

        var coreState = typeof(GameCore.GameCoreState).GetEnumName(core.currentCoreState);
        text += $"{coreState}\n";

        var player = core.player;
        if (player) {
            text += $"Pos: {player.transform.position}\n";
            text += $"Speed: {player.Velocity}\n";
            text += $"HP: {player.health.CurrentHealthValue} (+{player.health.CurrentInternalInjury})\n";
            var state = typeof(PlayerStateType).GetEnumName(player.fsm.State);
            text += $"{state} {player.playerInput.fsm.State}\n";
        }

        var currentLevel = core.gameLevel;
        if (currentLevel)
            text += $"[{currentLevel.SceneName}] ({currentLevel.BlockCountX}x{currentLevel.BlockCountY})\n";

        text += $"{core.currentCutScene}";

        return text;
    }
}