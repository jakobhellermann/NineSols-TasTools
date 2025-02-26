using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NineSolsAPI;
using NineSolsAPI.Utils;
using TAS.Input;
using TAS.Utils;
using UnityEngine;

namespace TAS.Tracer;

internal record TasTrace {
    public List<TraceData> Trace = [];
    public int Checksum;
    public string? FilePath;

    public override string ToString() => Trace.Select((x, n) => $"{x} {n}").Join(delimiter: "\n");
}

[AttributeUsage(AttributeTargets.Method), MeansImplicitUse]
public class TasTraceAddState : Attribute;

public class TraceData {
    public readonly Dictionary<string, object?> Data = [];

    public override string ToString() => $"TraceData {{ {Data.Select(kv => $"{kv.Key}: {kv.Value}").Join()} }}";

    public void Add(string name, object? value) {
        Data[name] = value;
    }

    public override bool Equals(object? obj) {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not TraceData b) return false;
        if (!EqualsHelper.CompareDeep(Data, b.Data, out var failurePath, out var left, out var right)) {
            ToastManager.Toast($"{left} != {right} at TraceData{failurePath}");
            return false;
        }

        return true;
    }

    public override int GetHashCode() => Data.GetHashCode();

    public static bool operator ==(TraceData a, TraceData b) => Equals(a, b);

    public static bool operator !=(TraceData a, TraceData b) => !(a == b);
}

file class WritableOnlyResolver : DefaultContractResolver {
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
        var property = base.CreateProperty(member, memberSerialization);
        property.ShouldSerialize = _ => property.Writable;
        return property;
    }
}

public class TracerIrrelevantState(object? data): IComparable {
    public object? Data = data;

    public override bool Equals(object? obj) => true;

    protected bool Equals(TracerIrrelevantState other) => true;

    public override int GetHashCode() => 0;

    public int CompareTo(object obj) => 0;
}

enum TracePauseMode {
    None,
    Reduced,
    Full,
}

internal static class TasTracer {
    private static string traceDirRoot = Path.Combine(Path.GetTempPath(), "TAS-Traces");
    public const TracePauseMode TracePauseMode = Tracer.TracePauseMode.None;
    private const bool CheckMismatches = true;

    [Initialize]
    private static void Initialize() {
        AttributeUtils.CollectAllMethods<TasTraceAddState>();

        ClearOldTraces();
    }


    private static void ClearOldTraces() {
        DeleteDirectoryChildren(traceDirRoot);
    }


    private static TasTrace trace = new();

    private static Dictionary<int, List<TasTrace>> traceCache = new();

    [EnableRun]
    private static void BeginTrace() {
        trace.Trace.Clear();
        trace.Checksum = Manager.Controller.Checksum;
        trace.FilePath = Manager.Controller.FilePath.Replace(@"\", "/");
    }

    [DisableRun]
    private static void EndTrace() {
        if (!Manager.DidComplete) {
            trace.Trace.Clear();
            trace.Checksum = 0;
            trace.FilePath = null;
            return;
        }
        
        if (!traceCache.ContainsKey(trace.Checksum)) traceCache[trace.Checksum] = [];
        var checksumTraces = traceCache[trace.Checksum];

        if (checksumTraces.Count > 0 && CheckMismatches && TracePauseMode == TracePauseMode.None) {
            var previousTrace = checksumTraces[^1];
            var len = Math.Min(previousTrace.Trace.Count, trace.Trace.Count);

            bool hasMismatch = false;

            if (previousTrace.Trace.Count != trace.Trace.Count) {
                ToastManager.Toast($"Length mismatch: {previousTrace.Trace.Count} != {trace.Trace.Count}");
                hasMismatch = true;
            } else
                for (var i = 0; i < len; i++) {
                    if (!EqualsHelper.CompareDeep(previousTrace.Trace[i], trace.Trace[i], out var failurePath,
                            out var left, out var right)) {
                        ToastManager.Toast($"{left} != {right} at frame {i}: TraceData{failurePath}");
                        hasMismatch = true;
                    }
                }

            if (hasMismatch) {
                ToastManager.Toast("TAS nondeterminism detected!");
                Log.Info($"Check traces at {traceDirRoot}");
            }
        }

        checksumTraces.Add(trace with { Trace = [..trace.Trace] });
        SaveTrace(trace);
    }

    public static void TraceFrame() {
        Log.TasTrace("Collect trace data");

        var data = new TraceData();
        var inputFrame = Manager.Controller.Previous!;
        data.Add("Frame", inputFrame.Actions.ToString());
        data.Add("FrameOffset", Manager.Controller.CurrentFrameInTas);
        AttributeUtils.Invoke<TasTraceAddState>([data]);

        trace.Trace.Add(data);
    }

    public static void TraceFramePause() {
        var data = new TraceData();
        if (TasTracerState.FrameHistoryPaused.Count > 0) {
            data.Add("FrameHistory", new List<object?[]>(new List<object[]>(TasTracerState.FrameHistoryPaused)));
        }

        trace.Trace.Add(data);
    }

    private static void SaveTrace(TasTrace newTrace) {
        var json = JsonConvert.SerializeObject(newTrace, Formatting.Indented, new JsonSerializerSettings {
            ContractResolver = new WritableOnlyResolver(),
            Converters = new List<JsonConverter> {
                new FuncConverter<Vector2>(vec => $"({vec.x:0.0000}, {vec.y:0.0000})"),
                new FuncConverter<Vector3>(vec =>
                    $"({vec.x:0.0000}, {vec.y:0.0000}" + (vec.z != 0 ? $",{vec.z:0.0000} " : "") + ")"),
                new FuncConverter<TraceData>(data => data.Data),
                new FuncConverter<TracerIrrelevantState>(data => data.Data),
                new FuncConverter<StackTrace>(st => {
                    var frames = new List<string>(st.FrameCount - 1);
                    for (var i = 1; i < st.FrameCount; i++) {
                        var frame = st.GetFrame(i);
                        var method = frame.GetMethod();
                        var name = method.Name.TrimStartMatches("DMD<").TrimEndMatches(">").ToString();
                        frames.Add($"{method.DeclaringType}.{name}");
                    }

                    return frames;
                }),
                new ToStringConverter<InputFrame>(),
            },
        });


        var name = Path.GetFileNameWithoutExtension(newTrace.FilePath);
        var traceDir = Path.Combine(traceDirRoot, name);
        Directory.CreateDirectory(traceDir);
        var datetime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var tracePath = Path.Combine(traceDir, $"{datetime}.json");
        File.WriteAllText(tracePath, json);


        var latest = Path.Combine(traceDir, "latest");
        Directory.CreateDirectory(latest);
        var latestChecksum = Path.Combine(latest, "checksum.txt");

        using var file = File.Open(latestChecksum, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        var reader = new StreamReader(file);
        if (reader.ReadToEnd() != newTrace.Checksum.ToString()) {
            DeleteFileChildren(latest, "*.json");
            file.SetLength(0);
            file.Write(Encoding.UTF8.GetBytes(newTrace.Checksum.ToString()));
        }

        File.Copy(tracePath, Path.Combine(latest, $"{datetime}.json"), true);
    }

    private static void DeleteDirectoryChildren(string path) {
        if (!Directory.Exists(path)) return;

        try {
            foreach (var dir in Directory.GetDirectories(path)) {
                Directory.Delete(dir, true);
            }
        } catch (Exception e) {
            Log.Debug($"Failed to delete old traces: {e}");
        }
    }

    private static void DeleteFileChildren(string path, string searchPattern) {
        foreach (var dir in Directory.GetFiles(path, searchPattern)) {
            File.Delete(dir);
        }
    }
}

file class ToStringConverter<T> : JsonConverter {
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
        if (value == null) {
            writer.WriteNull();
            return;
        }

        writer.WriteValue(value.ToString());
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue,
        JsonSerializer serializer) => throw new NotImplementedException();

    public override bool CanConvert(Type objectType) {
        var underlying = Nullable.GetUnderlyingType(objectType) ?? objectType;
        return typeof(T).IsAssignableFrom(underlying);
    }
}

file class FuncConverter<T>(Func<T, object?> func) : JsonConverter {
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
        if (value == null) {
            writer.WriteNull();
            return;
        }

        serializer.Serialize(writer, func((T)value));
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue,
        JsonSerializer serializer) => throw new NotImplementedException();

    public override bool CanConvert(Type objectType) {
        var underlying = Nullable.GetUnderlyingType(objectType) ?? objectType;
        return typeof(T).IsAssignableFrom(underlying);
    }
}

internal static class EqualsHelper {
    public static bool CompareDeep(
        object? obj1,
        object? obj2,
        [NotNullWhen(true)] out string? failurePath,
        [NotNullWhen(true)] out object? left,
        [NotNullWhen(true)] out object? right
    ) {
        string? fp = "";
        var ret = CompareDeepInner(obj1, obj2, ref fp, out left, out right);
        failurePath = fp;
        return ret;
    }

    private static bool CompareDeepInner(object? obj1, object? obj2, ref string? failurePath, out object? left,
        out object? right) {
        if (obj1 == null) {
            if (obj2 == null) {
                left = right = null;
                return true;
            }

            left = obj1;
            right = obj2;
            return false;
        }

        if (obj2 == null) {
            left = obj1;
            right = obj2;
            return false;
        }

        var type1 = obj1.GetType();
        var type2 = obj2.GetType();
        
        if (type1 != type2) {
            left = obj1;
            right = obj2;
            return false;
        }

        if (type1.IsPrimitive || obj1 is string) {
            var nativeEq = obj1.Equals(obj2);
            if (nativeEq) {
                left = right = null;
                return true;
            }

            left = obj1;
            right = obj2;
            return false;
        }

        if (type1.IsArray) {
            var first = (obj1 as Array)!;
            var second = (obj2 as Array)!;

            var en = first.GetEnumerator();
            var i = 0;
            while (en.MoveNext()) {
                if (!CompareDeep(en.Current, second.GetValue(i), out failurePath, out left, out right)) {
                    failurePath = $"[{i}]" + failurePath;
                    return false;
                }

                i++;
            }
            
            if (first.Length != second.Length) {
                failurePath = "<array_length_mismatch>" + failurePath;
                left = first.Length;
                right = second.Length;
                return false;
            }


            left = right = null;
            return true;
        }

        if (typeof(System.Collections.IDictionary).IsAssignableFrom(type1)) {
            var dict1 = (System.Collections.IDictionary)obj1;
            var dict2 = (System.Collections.IDictionary)obj2;

            if (dict1.Count != dict2.Count) {
                failurePath = "<dict_count>" + failurePath;
                left = dict1.Count;
                right = dict2.Count;
                return false;
            }

            foreach (var key in dict1.Keys) {
                if (!dict2.Contains(key)) {
                    failurePath = $"<missing_key:{key}>" + failurePath;
                    left = key;
                    right = null;
                    return false;
                }

                if (!CompareDeep(dict1[key], dict2[key], out failurePath, out left, out right)) {
                    failurePath = $".{key}" + failurePath;
                    return false;
                }
            }

            left = right = null;
            return true;
        }
        
        if (obj1 is IComparable comparable) {
            if (comparable.CompareTo(obj2) == 0) {
                left = right = null;
                return true;
            } else {
                left = obj1;
                right = obj2;
                return false;
            }
        }

        /*foreach (var pi in type.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public))
        {
            var val = pi.GetValue(obj1);
            var tval = pi.GetValue(obj2);
            if (!CompareDeep(val, tval))
                return false;
        }*/

        foreach (var fi in type1.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)) {
            var val = fi.GetValue(obj1);
            var tval = fi.GetValue(obj2);

            if (!CompareDeep(val, tval, out failurePath, out left, out right)) {
                failurePath = $".{fi.Name}" + failurePath;
                return false;
            }
        }

        left = right = null;
        return true;
    }
}