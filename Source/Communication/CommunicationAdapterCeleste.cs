using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NineSolsAPI;
using StudioCommunication;
using StudioCommunication.Util;
using TAS.EverestInterop;
using TAS.Module;

namespace TAS.Communication;

public sealed class CommunicationAdapterCeleste() : CommunicationAdapterBase(Location.Celeste) {
    protected override void FullReset() {
        CommunicationWrapper.Stop();
        CommunicationWrapper.Start();
    }

    protected override void OnConnectionChanged() {
        ToastManager.Toast($"On connection changed: {Connected}");
        if (Connected) {
            // Stall until input initialized to avoid sending invalid hotkey data
            while (Hotkeys.KeysDict == null) {
                Thread.Sleep(UpdateRate);
            }

            CommunicationWrapper.SendCurrentBindings();
        }
    }

    protected override void HandleMessage(MessageID messageId, BinaryReader reader) {
        ToastManager.Toast($"got {messageId}");
        switch (messageId) {
            case MessageID.FilePath:
                var path = reader.ReadString();
                LogVerbose($"Received message FilePath: '{path}'");

                InputController.StudioTasFilePath = path;
                break;

            case MessageID.Hotkey:
                var hotkey = (HotkeyID)reader.ReadByte();
                var released = reader.ReadBoolean();
                LogVerbose($"Received message Hotkey: {hotkey} ({(released ? "released" : "pressed")})");

                Hotkeys.KeysDict[hotkey].OverrideCheck = !released;
                break;

            case MessageID.SetSetting:
                var settingName = reader.ReadString();
                LogVerbose($"Received message Hotkey: '{settingName}'");

                if (typeof(CelesteTasSettings).GetProperty(settingName) is { } property) {
                    if (property.GetSetMethod(true) == null) {
                        break;
                    }

                    var value = property.GetValue(TasSettings)!;
                    var modified = false;

                    if (value is bool boolValue) {
                        property.SetValue(TasSettings, !boolValue);
                        modified = true;
                    } else if (value is int) {
                        property.SetValue(TasSettings, reader.ReadInt32());
                        modified = true;
                    } else if (value is float) {
                        property.SetValue(TasSettings, reader.ReadSingle());
                        modified = true;
                        /*} else if (value is HudOptions hudOptions) {
                            property.SetValue(TasSettings,
                                hudOptions.Has(HudOptions.StudioOnly) ? HudOptions.Off : HudOptions.Both);
                            modified = true;*/
                    } else if (value is Enum) {
                        property.SetValue(TasSettings, ((int)value + 1) % Enum.GetValues(property.PropertyType).Length);
                        modified = true;
                    }

                    if (modified) {
                        /* CelesteTasModule.Instance.SaveSettings();*/
                    }
                }

                break;

            /*case MessageID.SetCustomInfoTemplate:
                var customInfoTemplate = reader.ReadString();
                LogVerbose($"Received message SetCustomInfoTemplate: '{customInfoTemplate}'");

                TasSettings.InfoCustomTemplate = customInfoTemplate;
                GameInfo.Update();
                break;

            case MessageID.ClearWatchEntityInfo:
                LogVerbose("Received message ClearWatchEntityInfo");

                InfoWatchEntity.ClearWatchEntities();
                GameInfo.Update();
                break;*/

            case MessageID.RecordTAS:
                var fileName = reader.ReadString();
                LogVerbose($"Received message RecordTAS: '{fileName}'");

                ProcessRecordTAS(fileName);
                break;

            case MessageID.RequestGameData:
                var gameDataType = (GameDataType)reader.ReadByte();
                object? arg = gameDataType switch {
                    GameDataType.ConsoleCommand => reader.ReadBoolean(),
                    GameDataType.SettingValue => reader.ReadString(),
                    GameDataType.SetCommandAutoCompleteEntries or
                        GameDataType.InvokeCommandAutoCompleteEntries => reader.ReadObject<(string, int)>(),
                    GameDataType.RawInfo => reader.ReadObject<(string, bool)>(),
                    _ => null,
                };
                LogVerbose($"Received message RequestGameData: '{gameDataType}' ('{arg ?? "<null>"}')");

                // Gathering data from the game can sometimes take a while (and cause a timeout)
                Task.Run(() => {
                    try {
                        object? gameData = gameDataType switch {
                            // GameDataType.ConsoleCommand => GameData.GetConsoleCommand((bool)arg!),
                            // GameDataType.ModInfo => GameData.GetModInfo(),
                            // GameDataType.ExactGameInfo => GameInfo.ExactStudioInfo,
                            // GameDataType.SettingValue => GameData.GetSettingValue((string)arg!),
                            // GameDataType.CompleteInfoCommand => AreaCompleteInfo.CreateCommand(),
                            // GameDataType.ModUrl => GameData.GetModUrl(),
                            // GameDataType.CustomInfoTemplate => !string.IsNullOrWhiteSpace(
                            //     TasSettings.InfoCustomTemplate)
                            //     ? TasSettings.InfoCustomTemplate
                            //     : string.Empty,
                            // GameDataType.SetCommandAutoCompleteEntries => GameData
                            //     .GetSetCommandAutoCompleteEntries((((string, int))arg!).Item1,
                            //         (((string, int))arg!).Item2).ToArray(),
                            // GameDataType.InvokeCommandAutoCompleteEntries => GameData
                            //     .GetInvokeCommandAutoCompleteEntries((((string, int))arg!).Item1,
                            //         (((string, int))arg!).Item2).ToArray(),
                            // GameDataType.RawInfo => InfoCustom.GetRawInfo(((string, bool))arg!),
                            // GameDataType.GameState => GameData.GetGameState(),
                            _ => null,
                        };

                        QueueMessage(MessageID.GameDataResponse,
                            writer => {
                                writer.Write((byte)gameDataType);

                                switch (gameDataType) {
                                    case GameDataType.ConsoleCommand:
                                    case GameDataType.ModInfo:
                                    case GameDataType.ExactGameInfo:
                                    case GameDataType.SettingValue:
                                    case GameDataType.CompleteInfoCommand:
                                    case GameDataType.ModUrl:
                                    case GameDataType.CustomInfoTemplate:
                                        writer.Write((string?)gameData ?? string.Empty);
                                        break;

                                    case GameDataType.SetCommandAutoCompleteEntries:
                                    case GameDataType.InvokeCommandAutoCompleteEntries:
                                        writer.WriteObject((CommandAutoCompleteEntry[]?)gameData ?? []);
                                        break;

                                    case GameDataType.RawInfo:
                                        writer.WriteObject(gameData);
                                        break;

                                    case GameDataType.GameState:
                                        writer.WriteObject((GameState?)gameData);
                                        break;
                                }

                                LogVerbose($"Sent message GameDataResponse: {gameDataType} = '{gameData}'");
                            });
                    } catch (Exception ex) {
                        Console.WriteLine(ex);
                    }
                });
                break;

            default:
                LogError($"Received unknown message ID: {messageId}");
                break;
        }
    }

    public void WriteState(StudioState state) {
        QueueMessage(MessageID.State, writer => writer.WriteObject(state));
        // LogVerbose("Sent message State");
    }

    public void WriteUpdateLines(Dictionary<int, string> updateLines) {
        QueueMessage(MessageID.UpdateLines, writer => writer.WriteObject(updateLines));
        LogVerbose($"Sent message UpdateLines: {updateLines.Count}");
    }

    public void WriteCurrentBindings(Dictionary<int, List<int>> nativeBindings) {
        QueueMessage(MessageID.CurrentBindings, writer => writer.WriteObject(nativeBindings));
        LogVerbose($"Sent message CurrentBindings: {nativeBindings.Count}");
    }

    public void WriteRecordingFailed(RecordingFailedReason reason) {
        QueueMessage(MessageID.RecordingFailed, writer => writer.Write((byte)reason));
        LogVerbose($"Sent message RecordingFailed: {reason}");
    }

    private void ProcessRecordTAS(string fileName) {
        /*if (!TASRecorderUtils.Installed) {
            WriteRecordingFailed(RecordingFailedReason.TASRecorderNotInstalled);
            return;
        }

        if (!TASRecorderUtils.FFmpegInstalled) {
            WriteRecordingFailed(RecordingFailedReason.FFmpegNotInstalled);
            return;
        }

        Manager.Controller.RefreshInputs(enableRun: true);
        if (RecordingCommand.RecordingTimes.IsNotEmpty()) {
            AbortTas("Can't use StartRecording/StopRecording with \"Record TAS\"");
            return;
        }

        Manager.NextStates |= States.Enable;

        int totalFrames = Manager.Controller.Inputs.Count;
        if (totalFrames <= 0) return;

        TASRecorderUtils.StartRecording(fileName);
        TASRecorderUtils.SetDurationEstimate(totalFrames);

        if (!Manager.Controller.Commands.TryGetValue(0, out var commands)) return;

        bool startsWithConsoleLoad = commands.Any(c =>
            c.Attribute.Name.Equals("Console", StringComparison.OrdinalIgnoreCase) &&
            c.Args.Length >= 1 &&
            ConsoleCommand.LoadCommandRegex.Match(c.Args[0].ToLower()) is { Success: true });

        if (startsWithConsoleLoad) {
            // Restart the music when we enter the level
            Audio.SetMusic(null, startPlaying: false, allowFadeOut: false);
            Audio.SetAmbience(null, startPlaying: false);
            Audio.BusStopAll(Buses.GAMEPLAY, immediate: true);
        }*/
    }

    protected override void LogInfo(string message) => Log.Info($"CelesteTAS/StudioCom {message}");
    protected override void LogVerbose(string message) => Log.Info($"CelesteTAS/StudioCom {message}");
    protected override void LogError(string message) => Log.Error($"CelesteTAS/StudioCom {message}");
}