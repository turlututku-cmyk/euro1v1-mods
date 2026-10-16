# Third-party software in the download

The zip in `download/` bundles these projects so it works out of the box. Each keeps its own license:

- **BepInEx** (LGPL-2.1) https://github.com/BepInEx/BepInEx, IL2CPP build `6.0.0-be.755`, includes Il2CppInterop, Cpp2IL, HarmonyX and Doorstop
- **.NET runtime** (MIT) as bundled by BepInEx's IL2CPP build (the `dotnet` folder)
- **NLayer** (MIT) https://github.com/naudio/NLayer, mp3 decoding
- **NVorbis** (MIT) https://github.com/NVorbis/NVorbis, ogg decoding

The mod's own source code is MIT, see `LICENSE`. This is an unofficial fan project and is not affiliated with the game's developers.
