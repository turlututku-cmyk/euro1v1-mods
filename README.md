# Euro Mods Menu

An in-game **Mods menu** for Euro3v3, built as a [BepInEx 6](https://github.com/BepInEx/BepInEx) IL2CPP plugin.
Open it with **F8** or the **Mods** button that is added to the Play menu and the pause menu.
Mods are installed from the menu's **Featured** tab (everything is bundled, no downloads).

> Unofficial fan project, not affiliated with the game's developers. Everything here is client-side.
> Modified clients can be flagged in online matches, so use at your own risk, and prefer practice and private games.

## Mods

| Area | Mods |
|---|---|
| Crosshair | **Crosshair Changer** (6 styles, colors, outline, rainbow), **Crosshair Profiles** (save and switch presets, F9) |
| Overlays | **FPS Counter**, **Clock**, **FPS Graph**, **Match Stats**, **Input Display**, **Notes** |
| Combat feedback | **Hit Sounds** (built-in or your own files), **Damage Numbers** (one number per shot, healing +N) |
| Visuals | **Better Visuals** (vignette, neon border, particles, cursor trail, UI theme, rainbow guns and text, HUD cleaner, health label), **RTX Mode** (render quality, bloom, tonemapping), **Letterbox**, **Screen Tint** |
| Clips | **Clip Maker**: records match movement and build parts, 3D replay viewer inside the real map, timeline, trim, camera path, **render to video (AVI)** |
| Camera | **Freecam (Team)**: orbit your teammates only, never leaves your team |
| Quality of life | **Pause Helper** (number-key shortcuts), **Menu Music Player** (your own mp3/wav/ogg), **FPS Limiter**, **FOV Changer**, **Volume Control**, **Run in Background**, **Screenshot Key**, **Settings Backup** |

Hotkeys: `F8` menu, `F12` screenshot, `F9` next crosshair profile, `F7` save clip, `Shift+F7` record clip, `F6` team camera, `Backspace` leave the clip viewer.

### Fair-play rules built in
- Freecam only follows living teammates and cannot pass through walls. It refuses to start while you are alive against other players.
- The clip viewer refuses to open while you are alive in a live match with real players. If you are dead it shows your own team only.
- No wallhacks, aim assist or anything that reveals hidden information.

## Install (players)

1. Install Euro3v3 from Steam.
2. Download the zip (BepInEx is already included, see the button at the bottom) and extract it into the game folder (replace files if asked). `winhttp.dll` must end up next to `Euro3v3.exe`.
3. Start the game. The first launch takes 1 to 3 minutes while BepInEx prepares itself.
4. Press **F8** and open **Featured**.

Needs about 2 GB of free RAM on top of the game.

## Build from source

Requirements: .NET 8 SDK, the game with BepInEx 6 (IL2CPP, `6.0.0-be.755` or newer) installed and started once, so `BepInEx/interop` exists.

```powershell
cd src
dotnet build -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Euro3v3\"
```

The plugin DLL (plus `NLayer.dll` and `NVorbis.dll`) is written to `BepInEx/plugins` inside the game folder.

Layout:

- `src/` the plugin (`Cfg.cs` settings, `Window.cs` and `Panes*.cs` the menu, one file per feature)
- `missions/bot_mission_clipview.json` a tiny private mission the clip viewer starts to load the map. Copy it to `Euro3v3_Data/StreamingAssets/Missions/` (the plugin does this itself)
- `tools/MakeClipMission/` generates that mission from the game's own `bot_mission_easy.json`

## Debug tools

Create `BepInEx/debug.trigger` while the game runs and the plugin writes `BepInEx/debug-state.txt` and `debug-shot.png` (state report and a screenshot of the game). `BepInEx/debug-types.trigger` lists the members of named game classes into `debug-types.txt`.

## Credits

[BepInEx](https://github.com/BepInEx/BepInEx), [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop), [NLayer](https://github.com/naudio/NLayer) and [NVorbis](https://github.com/NVorbis/NVorbis) (audio decoding).

## License

MIT, see `LICENSE`. BepInEx and the other libraries keep their own licenses.

---

## Download

BepInEx and everything else is **already inside the zip**, so there is nothing else to install.

[![Download Euro Mods Menu 1.3.5 (with BepInEx)](https://img.shields.io/badge/Download-Euro%20Mods%20Menu%201.3.5%20%C2%B7%20with%20BepInEx-2ea44f?style=for-the-badge&logo=github)](https://github.com/turlututku-cmyk/euro1v1-mods/raw/main/download/Euro3v3_ModsMenu_1.3.5.zip)

- File: `download/Euro3v3_ModsMenu_1.3.5.zip` (33 MB)
- SHA-256: `b7eaae0955453d958469e2f2de312db53078f9e6028a9671980ff91c4636e20f`
- Extract it into the Euro3v3 game folder, start the game and press **F8**.
