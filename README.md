# Nine Sols TAS Tools

## Installing

## Building

```shell
git clone --recurse-submodules https://github.com/jakobhellermann/NineSols-TasTools
cd NineSols-TasTools

dotnet publish
```

1. [Install BepInEx](https://docs.bepinex.dev/articles/user_guide/installation/index.html)
2. clone this
   repo ([generate from this template](https://github.com/new?template_name=NineSols-TAS&template_owner=jakobhellermann),
   then update the `.csproj`
    1. Change `<AssemblyName>` to your mod name
    2. Make sure the `<NineSolsPath>` points to the installed game
3. Download the `NineSolsAPI.dll` from https://github.com/nine-sols-modding/NineSolsAPI/releases/ and put it into
   `BepInEx/plugins`. The API provides utilities like toast messages, preloading objects etc.

When you build the project in your IDE, the resulting mod should automatically be copied into the `BepInEx/scripts`
folder. Mods are usually loaded from the `plugins` directory, but with `ScriptEngine` you can reload a new version of
the mod without restarting the game:

4. download `ScriptEngine` from [BepInEx.Debug](https://github.com/BepInEx/BepInEx.Debug/releases/tag/r10)
   and place it in `BepInEx/plugins` to enable hot reloading
    1. set `LoadOnStart=true` and `EnableFileSystemWatcher=true` in `BepInEx/config/com.bepis.bepinex.scriptengine.cfg`
       to hot reload without pressing F6

When you build the project in your IDE (e.g. [Rider](https://www.jetbrains.com/de-de/rider/)) the mod should be built
and automatically copied to `path/to/game/BepInEx/scripts/YourMod.dll`, triggering a reload from the scriptengine.

For the final distribution without `ScriptEngine` place the `.dll` in `BepInEx/plugins/` instead.

The log file can be viewed in `BepInEx/LogOutput.log`.
