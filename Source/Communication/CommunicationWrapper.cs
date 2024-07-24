using System.Collections.Generic;
using System.Linq;
using StudioCommunication;
using TAS.EverestInterop;

namespace TAS.Communication;

public static class CommunicationWrapper {
    public static bool Connected => comm is { Connected: true };
    private static CommunicationAdapterCeleste? comm;

    [Unload]
    private static void Unload() {
        Stop();
    }

    public static void Start() {
        if (comm != null) {
            Log.Warn("Tried to start the communication adapter while already running!");
            return;
        }

        comm = new CommunicationAdapterCeleste();
    }

    public static void Stop() {
        if (comm == null) {
            Log.Warn("Tried to stop the communication adapter while not running!");
            return;
        }

        SendReset();

        comm.Dispose();
        comm = null;
    }

    public static void ChangeStatus() {
        if (TasSettings.AttemptConnectStudio && comm == null)
            Start();
        else if (comm != null) Stop();
    }

    #region Actions

    public static void SendReset() {
        if (!Connected) return;

        comm.WriteReset();
    }

    public static void SendState(StudioState state) {
        if (!Connected) return;

        comm.WriteState(state);
    }

    public static void SendUpdateLines(Dictionary<int, string> updateLines) {
        if (!Connected) return;

        comm.WriteUpdateLines(updateLines);
    }

    public static void SendCurrentBindings() {
        if (!Connected) return;

        var nativeBindings =
            Hotkeys.KeysInteractWithStudio.ToDictionary(pair => (int)pair.Key,
                pair => {
                    var keys = pair.Value.Select(key => (int)UnityToXna.MapKeyCodeToXna(key)).ToList();
                    return keys;
                });
        comm.WriteCurrentBindings(nativeBindings);
    }

    public static void SendRecordingFailed(RecordingFailedReason reason) {
        if (!Connected) return;

        comm.WriteRecordingFailed(reason);
    }

    #endregion
}