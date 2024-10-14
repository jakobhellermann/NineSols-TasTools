using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TAS.Utils;

internal delegate TReturn GetDelegate<in TInstance, out TReturn>(TInstance instance);

internal static class FastReflection {
    // ReSharper disable UnusedMember.Local
    private record struct DelegateKey(Type Type, string Name, Type InstanceType, Type ReturnType) {
        public readonly Type Type = Type;
        public readonly string Name = Name;
        public readonly Type InstanceType = InstanceType;
        public readonly Type ReturnType = ReturnType;
    }
    // ReSharper restore UnusedMember.Local

    private static readonly ConcurrentDictionary<DelegateKey, Delegate> CachedFieldGetDelegates = new();

    private static GetDelegate<TInstance, TReturn> CreateGetDelegateImpl<TInstance, TReturn>(Type type, string name) {
        var field = type.GetFieldInfo(name);
        if (field == null) return null;

        var returnType = typeof(TReturn);
        var fieldType = field.FieldType;
        if (!returnType.IsAssignableFrom(fieldType))
            throw new InvalidCastException(
                $"{field.Name} is of type {fieldType}, it cannot be assigned to the type {returnType}.");

        var key = new DelegateKey(type, name, typeof(TInstance), typeof(TReturn));
        if (CachedFieldGetDelegates.TryGetValue(key, out var result)) return (GetDelegate<TInstance, TReturn>)result;

        if (field.IsConst()) {
            var value = field.GetValue(null);
            var returnValue = value == null ? default : (TReturn)value;
            Func<TInstance, TReturn> func = _ => returnValue;

            var getDelegate =
                (GetDelegate<TInstance, TReturn>)func.Method.CreateDelegate(typeof(GetDelegate<TInstance, TReturn>),
                    func.Target);
            CachedFieldGetDelegates[key] = getDelegate;
            return getDelegate;
        }

        var method = new DynamicMethod($"{field} Getter", returnType, new[] { typeof(TInstance) }, field.DeclaringType,
            true);
        var il = method.GetILGenerator();

        if (field.IsStatic)
            il.Emit(OpCodes.Ldsfld, field);
        else {
            il.Emit(OpCodes.Ldarg_0);
            if (field.DeclaringType.IsValueType && !typeof(TInstance).IsValueType)
                il.Emit(OpCodes.Unbox_Any, field.DeclaringType);

            il.Emit(OpCodes.Ldfld, field);
        }

        if (fieldType.IsValueType && !returnType.IsValueType) il.Emit(OpCodes.Box, fieldType);

        il.Emit(OpCodes.Ret);

        result = CachedFieldGetDelegates[key] = method.CreateDelegate(typeof(GetDelegate<TInstance, TReturn>));
        return (GetDelegate<TInstance, TReturn>)result;
    }

    public static GetDelegate<TInstance, TResult> CreateGetDelegate<TInstance, TResult>(this Type type,
        string fieldName) => CreateGetDelegateImpl<TInstance, TResult>(type, fieldName);

    public static GetDelegate<TInstance, TResult> CreateGetDelegate<TInstance, TResult>(string fieldName) =>
        CreateGetDelegate<TInstance, TResult>(typeof(TInstance), fieldName);
}

internal static class ReflectionExtensions {
    internal const BindingFlags InstanceAnyVisibility =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    internal const BindingFlags StaticAnyVisibility =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    internal const BindingFlags StaticInstanceAnyVisibility =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    internal const BindingFlags InstanceAnyVisibilityDeclaredOnly =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static readonly object[] NullArgs = { null };

    // ReSharper disable UnusedMember.Local
    private record struct MemberKey(Type Type, string Name) {
        public readonly Type Type = Type;
        public readonly string Name = Name;
    }

    private record struct AllMemberKey(Type Type, BindingFlags BindingFlags) {
        public readonly Type Type = Type;
        public readonly BindingFlags BindingFlags = BindingFlags;
    }

    private record struct MethodKey(Type Type, string Name, long Types) {
        public readonly Type Type = Type;
        public readonly string Name = Name;
        public readonly long Types = Types;
    }
    // ReSharper restore UnusedMember.Local

    private static readonly ConcurrentDictionary<MemberKey, MemberInfo> CachedMemberInfos = new();
    private static readonly ConcurrentDictionary<MemberKey, FieldInfo> CachedFieldInfos = new();
    private static readonly ConcurrentDictionary<MemberKey, PropertyInfo> CachedPropertyInfos = new();
    private static readonly ConcurrentDictionary<MethodKey, MethodInfo> CachedMethodInfos = new();
    private static readonly ConcurrentDictionary<MemberKey, MethodInfo> CachedGetMethodInfos = new();
    private static readonly ConcurrentDictionary<MemberKey, MethodInfo> CachedSetMethodInfos = new();
    private static readonly ConcurrentDictionary<AllMemberKey, IEnumerable<FieldInfo>> CachedAllFieldInfos = new();

    private static readonly ConcurrentDictionary<AllMemberKey, IEnumerable<PropertyInfo>>
        CachedAllPropertyInfos = new();

    public static MemberInfo GetMemberInfo(this Type type, string name,
        BindingFlags bindingAttr = StaticInstanceAnyVisibility) {
        var key = new MemberKey(type, name);
        if (CachedMemberInfos.TryGetValue(key, out var result)) return result;

        do {
            result = type.GetMember(name, bindingAttr).FirstOrDefault();
        } while (result == null && (type = type.BaseType) != null);

        return CachedMemberInfos[key] = result;
    }

    public static FieldInfo GetFieldInfo(this Type type, string name,
        BindingFlags bindingAttr = StaticInstanceAnyVisibility) {
        var key = new MemberKey(type, name);
        if (CachedFieldInfos.TryGetValue(key, out var result)) return result;

        do {
            result = type.GetField(name, bindingAttr);
        } while (result == null && (type = type.BaseType) != null);

        return CachedFieldInfos[key] = result;
    }

    public static PropertyInfo GetPropertyInfo(this Type type, string name,
        BindingFlags bindingAttr = StaticInstanceAnyVisibility) {
        var key = new MemberKey(type, name);
        if (CachedPropertyInfos.TryGetValue(key, out var result)) return result;

        do {
            result = type.GetProperty(name, bindingAttr);
        } while (result == null && (type = type.BaseType) != null);

        return CachedPropertyInfos[key] = result;
    }

    public static MethodInfo GetMethodInfo(this Type type, string name, Type[] types = null,
        BindingFlags bindingAttr = StaticInstanceAnyVisibility) {
        var key = new MethodKey(type, name, types.GetCustomHashCode());
        if (CachedMethodInfos.TryGetValue(key, out var result)) return result;

        do {
            var methodInfos = type.GetMethods(bindingAttr);
            result = methodInfos.FirstOrDefault(info =>
                info.Name == name && types?.SequenceEqual(info.GetParameters().Select(i => i.ParameterType)) != false);
        } while (result == null && (type = type.BaseType) != null);

        return CachedMethodInfos[key] = result;
    }

    public static MethodInfo GetGetMethod(this Type type, string propertyName,
        BindingFlags bindingAttr = StaticInstanceAnyVisibility) {
        var key = new MemberKey(type, propertyName);
        if (CachedGetMethodInfos.TryGetValue(key, out var result)) return result;

        do {
            result = type.GetPropertyInfo(propertyName, bindingAttr)?.GetGetMethod(true);
        } while (result == null && (type = type.BaseType) != null);

        return CachedGetMethodInfos[key] = result;
    }

    public static MethodInfo GetSetMethod(this Type type, string propertyName,
        BindingFlags bindingAttr = StaticInstanceAnyVisibility) {
        var key = new MemberKey(type, propertyName);
        if (CachedSetMethodInfos.TryGetValue(key, out var result)) return result;

        do {
            result = type.GetPropertyInfo(propertyName, bindingAttr)?.GetSetMethod(true);
        } while (result == null && (type = type.BaseType) != null);

        return CachedSetMethodInfos[key] = result;
    }

    public static IEnumerable<FieldInfo> GetAllFieldInfos(this Type type, bool includeStatic = false) {
        var bindingFlags = InstanceAnyVisibilityDeclaredOnly;
        if (includeStatic) bindingFlags |= BindingFlags.Static;

        var key = new AllMemberKey(type, bindingFlags);
        if (CachedAllFieldInfos.TryGetValue(key, out var result)) return result;

        HashSet<FieldInfo> hashSet = new();
        while (type != null && type.IsSubclassOf(typeof(object))) {
            IEnumerable<FieldInfo> fieldInfos = type.GetFields(bindingFlags);

            foreach (var fieldInfo in fieldInfos) hashSet.Add(fieldInfo);

            type = type.BaseType;
        }

        CachedAllFieldInfos[key] = hashSet;
        return hashSet;
    }

    public static IEnumerable<PropertyInfo> GetAllProperties(this Type type, bool includeStatic = false) {
        var bindingFlags = InstanceAnyVisibilityDeclaredOnly;
        if (includeStatic) bindingFlags |= BindingFlags.Static;

        var key = new AllMemberKey(type, bindingFlags);
        if (CachedAllPropertyInfos.TryGetValue(key, out var result)) return result;

        HashSet<PropertyInfo> hashSet = new();
        while (type != null && type.IsSubclassOf(typeof(object))) {
            IEnumerable<PropertyInfo> properties = type.GetProperties(bindingFlags);
            foreach (var fieldInfo in properties) hashSet.Add(fieldInfo);

            type = type.BaseType;
        }

        CachedAllPropertyInfos[key] = hashSet;
        return hashSet;
    }

    public static T GetFieldValue<T>(this object obj, string name) {
        var result = obj.GetType().GetFieldInfo(name)?.GetValue(obj);
        if (result == null)
            return default;
        else
            return (T)result;
    }

    public static T GetFieldValue<T>(this Type type, string name) {
        var result = type.GetFieldInfo(name)?.GetValue(null);
        if (result == null)
            return default;
        else
            return (T)result;
    }

    public static void SetFieldValue(this object obj, string name, object value) {
        obj.GetType().GetFieldInfo(name)?.SetValue(obj, value);
    }

    public static void SetFieldValue(this Type type, string name, object value) {
        type.GetFieldInfo(name)?.SetValue(null, value);
    }

    public static T GetPropertyValue<T>(this object obj, string name) {
        var result = obj.GetType().GetPropertyInfo(name)?.GetValue(obj, null);
        if (result == null)
            return default;
        else
            return (T)result;
    }

    public static T GetPropertyValue<T>(Type type, string name) {
        var result = type.GetPropertyInfo(name)?.GetValue(null, null);
        if (result == null)
            return default;
        else
            return (T)result;
    }

    public static void SetPropertyValue(this object obj, string name, object value) {
        if (obj.GetType().GetPropertyInfo(name) is { CanWrite: true } propertyInfo)
            propertyInfo.SetValue(obj, value, null);
    }

    public static void SetPropertyValue(this Type type, string name, object value) {
        if (type.GetPropertyInfo(name) is { CanWrite: true } propertyInfo) propertyInfo.SetValue(null, value, null);
    }

    private static T InvokeMethod<T>(object obj, Type type, string name, params object[] parameters) {
        parameters ??= NullArgs;
        var result = type.GetMethodInfo(name)?.Invoke(obj, parameters);
        if (result == null)
            return default;
        else
            return (T)result;
    }

    public static T InvokeMethod<T>(this object obj, string name, params object[] parameters) =>
        InvokeMethod<T>(obj, obj.GetType(), name, parameters);

    public static T InvokeMethod<T>(this Type type, string name, params object[] parameters) =>
        InvokeMethod<T>(null, type, name, parameters);

    public static void InvokeMethod(this object obj, string name, params object[] parameters) {
        InvokeMethod<object>(obj, obj.GetType(), name, parameters);
    }

    public static void InvokeMethod(this Type type, string name, params object[] parameters) {
        InvokeMethod<object>(null, type, name, parameters);
    }
}

internal static class HashCodeExtensions {
    public static long GetCustomHashCode<T>(this IEnumerable<T> enumerable) {
        if (enumerable == null) return 0;

        unchecked {
            long hash = 17;
            foreach (var item in enumerable) hash = hash * -1521134295 + EqualityComparer<T>.Default.GetHashCode(item);

            return hash;
        }
    }
}

internal static class TypeExtensions {
    public static bool IsSameOrSubclassOf(this Type potentialDescendant, Type potentialBase) =>
        potentialDescendant == potentialBase || potentialDescendant.IsSubclassOf(potentialBase);

    public static bool IsSameOrSubclassOf(this Type potentialDescendant, params Type[] potentialBases) =>
        potentialBases.Any(potentialDescendant.IsSameOrSubclassOf);

    public static bool IsSimpleType(this Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string) ||
                                                       type == typeof(decimal) || type == typeof(Vector2) ||
                                                       type == typeof(Vector3);

    public static bool IsStructType(this Type type) => type.IsValueType && !type.IsEnum && !type.IsPrimitive &&
                                                       !type.IsEquivalentTo(typeof(decimal));

    public static bool IsConst(this FieldInfo fieldInfo) => fieldInfo.IsLiteral && !fieldInfo.IsInitOnly;
}

internal static class PropertyInfoExtensions {
    public static bool IsStatic(this PropertyInfo source, bool nonPublic = true)
        => source.GetAccessors(nonPublic).Any(x => x.IsStatic);
}

internal static class CommonExtensions {
    public static T Apply<T>(this T obj, Action<T> action) {
        action(obj);
        return obj;
    }
}

// https://github.com/NoelFB/Foster/blob/main/Framework/Extensions/EnumExt.cs
internal static class EnumExtensions {
    /// Enum.HasFlag boxes the value, whereas this method does not
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool Has<TEnum>(this TEnum lhs, TEnum rhs) where TEnum : unmanaged, Enum {
        return sizeof(TEnum) switch {
            1 => (*(byte*)&lhs & *(byte*)&rhs) > 0,
            2 => (*(ushort*)&lhs & *(ushort*)&rhs) > 0,
            4 => (*(uint*)&lhs & *(uint*)&rhs) > 0,
            8 => (*(ulong*)&lhs & *(ulong*)&rhs) > 0,
            _ => throw new Exception("Size does not match a known Enum backing type."),
        };
    }
}

internal static class StringExtensions {
    private static readonly Regex LineBreakRegex = new(@"\r\n?|\n", RegexOptions.Compiled);

    public static string ReplaceLineBreak(this string text, string replacement) =>
        LineBreakRegex.Replace(text, replacement);

    public static bool IsNullOrEmpty(this string text) => string.IsNullOrEmpty(text);

    public static bool IsNotNullOrEmpty(this string text) => !string.IsNullOrEmpty(text);

    public static bool IsNullOrWhiteSpace(this string text) => string.IsNullOrWhiteSpace(text);

    public static bool IsNotNullOrWhiteSpace(this string text) => !string.IsNullOrWhiteSpace(text);
}

internal static class EnumerableExtensions {
    public static bool IsEmpty<T>(this IEnumerable<T> enumerable) => !enumerable.Any();

    public static bool IsNullOrEmpty<T>(this IEnumerable<T> enumerable) => enumerable == null || !enumerable.Any();

    public static bool IsNotEmpty<T>(this IEnumerable<T> enumerable) => !enumerable.IsEmpty();

    public static bool IsNotNullOrEmpty<T>(this IEnumerable<T> enumerable) => !enumerable.IsNullOrEmpty();

    // public static IEnumerable<T> SkipLast<T>(this IEnumerable<T> source, int n = 1) {
    //     var it = source.GetEnumerator();
    //     bool hasRemainingItems = false;
    //     var cache = new Queue<T>(n + 1);
    //
    //     do {
    //         if (hasRemainingItems = it.MoveNext()) {
    //             cache.Enqueue(it.Current);
    //             if (cache.Count > n)
    //                 yield return cache.Dequeue();
    //         }
    //     } while (hasRemainingItems);
    // }
}

internal static class ListExtensions {
    public static T GetValueOrDefault<T>(this IList<T> list, int index, T defaultValue = default) =>
        index >= 0 && index < list.Count ? list[index] : defaultValue;
}

internal static class DictionaryExtensions {
    public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key,
        TValue defaultValue = default) => dict.TryGetValue(key, out var value) ? value : defaultValue;

    public static TKey LastKeyOrDefault<TKey, TValue>(this SortedDictionary<TKey, TValue> dict) =>
        dict.Count > 0 ? dict.Last().Key : default;

    public static TValue LastValueOrDefault<TKey, TValue>(this SortedDictionary<TKey, TValue> dict) =>
        dict.Count > 0 ? dict.Last().Value : default;
}

/*internal static class DynamicDataExtensions {
    private static readonly ConditionalWeakTable<object, DynamicData> cached = new();

    public static DynamicData GetDynamicDataInstance(this object obj) {
        return cached.GetValue(obj, key => new DynamicData(key));
    }
}*/

internal static class NumberExtensions {
    private static readonly string format = "0.".PadRight(339, '#');

    public static string ToFormattedString(this float value, int decimals) {
        if (decimals == 0)
            return value.ToString(format);
        else
            return ((double)value).ToFormattedString(decimals);
    }

    public static string ToFormattedString(this double value, int decimals) {
        if (decimals == 0)
            return value.ToString(format);
        else
            return value.ToString($"F{decimals}");
    }

    public static long SecondsToTicks(this float seconds) {
        // .NET Framework rounded TimeSpan.FromSeconds to the nearest millisecond.
        // See: https://github.com/EverestAPI/Everest/blob/dev/NETCoreifier/Patches/TimeSpan.cs
        var millis = seconds * 1000 + (seconds >= 0 ? +0.5 : -0.5);
        return (long)millis * TimeSpan.TicksPerMillisecond;
    }
}

internal static class CloneUtil<T> {
    private static readonly Func<T, object> Clone;

    static CloneUtil() {
        var cloneMethod = typeof(T).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        Clone = (Func<T, object>)cloneMethod.CreateDelegate(typeof(Func<T, object>));
    }

    public static T ShallowClone(T obj) => (T)Clone(obj);
}

internal static class CloneUtil {
    public static T ShallowClone<T>(this T obj) => CloneUtil<T>.ShallowClone(obj);

    public static void CopyAllFields(this object to, object from, bool onlyDifferent = false) {
        if (to.GetType() != from.GetType()) throw new ArgumentException("object to and from must be the same type");

        foreach (var fieldInfo in to.GetType().GetAllFieldInfos()) {
            var fromValue = fieldInfo.GetValue(from);
            if (onlyDifferent && fromValue == fieldInfo.GetValue(to)) continue;

            fieldInfo.SetValue(to, fromValue);
        }
    }

    public static void CopyAllProperties(this object to, object from, bool onlyDifferent = false) {
        if (to.GetType() != from.GetType()) throw new ArgumentException("object to and from must be the same type");

        foreach (var propertyInfo in to.GetType().GetAllProperties()) {
            if (propertyInfo.GetGetMethod(true) == null || propertyInfo.GetSetMethod(true) == null) continue;

            var fromValue = propertyInfo.GetValue(from);
            if (onlyDifferent && fromValue == propertyInfo.GetValue(to)) continue;

            propertyInfo.SetValue(to, fromValue);
        }
    }
}