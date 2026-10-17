# Euro Mods Menu

An in-game **Mods menu** for Euro3v3, built as a [BepInEx 6](https://github.com/BepInEx/BepInEx) IL2CPP plugin.
Open it with **F8** or the **Mods** button that is added to the Play menu and the pause menu.
Every mod is optional: nothing is switched on until you press **GET** on it.

> Unofficial fan project, not affiliated with the game's developers. Everything here is client-side.
> Modified clients can be flagged in online matches, so use at your own risk, and prefer practice and private games.

## The menu

| Tab | What it shows |
|---|---|
| **Installed** | The mods you have, each with an on/off switch |
| **Featured** (yellow) | Popular picks that are worth downloading. Damage Numbers is the first one |
| **New** | The most recently added mods, tagged NEW. A laughing face marks the joke mods |
| **Download** | The whole list in one place, with search, A-Z and an "on" filter |

## Mods

| Area | Mods |
|---|---|
| Crosshair | **Crosshair Changer** (6 styles, colors, outline, rainbow), **Crosshair Profiles** (save and switch presets, F9) |
| Overlays | **FPS Counter**, **Clock**, **FPS Graph**, **Match Stats**, **Input Display**, **Notes** |
| Combat feedback | **Hit Sounds**, **Damage Numbers** (one number per shot, healing +N), **Kill Messages** ("You killed X", "You assisted Y for X", "You were sniped by Z"), **Announcer** (double/triple kills, sprees), **Kill Confetti**, **Low Health Warning** (pulsing red edges, heartbeat) |
| Get better | **Performance Coach** (tips after every round and match from your own stats), **Aim Trainer** (F2), **Build Trainer** (F4), **Practice Launcher** (one-click bot matches), **Session Stats** (leaderboard that updates at the end of each round, F3) |
| Match tools | **Scoreboard Screenshot** (result card and PNG when a match ends), **Chat Macros** (Alt + 1-6 sends quick messages), **Chat Filter** (spam, swearing and blocked players), **Match Found Alert** (chime and taskbar flash, queue timer) |
| Jokes | **Death Taunts**, **Sad Trombone**, Announcer and Kill Confetti |
| Visuals | **Better Visuals** (vignette, neon border, particles, cursor trail, UI theme, rainbow guns and text, HUD cleaner, health label), **RTX Mode**, **Letterbox**, **Screen Tint** |
| Clips | **Clip Maker**: records match movement and build parts, 3D replay viewer inside the real map, timeline, trim, camera path, render to video (AVI) |
| Camera | **Freecam (Team)**: orbit your teammates only, never leaves your team |
| Quality of life | **Pause Helper**, **Menu Music Player** (your own mp3/wav/ogg, streamed so it stays light on memory), **FPS Limiter**, **FOV Changer**, **Volume Control**, **Run in Background**, **Screenshot Key**, **Settings Backup** |

Hotkeys: `F8` menu, `F12` screenshot, `F9` next crosshair profile, `F7` save clip, `Shift+F7` record clip, `F6` team camera,
`F2` Aim Trainer, `F3` session leaderboard, `F4` Build Trainer, `Alt+1..6` Chat Macros (the modifier key is configurable), `Backspace` leave the clip viewer.

### Fair-play rules built in
- Freecam only follows living teammates and cannot pass through walls. It refuses to start while you are alive against other players.
- The clip viewer refuses to open while you are alive in a live match with real players. If you are dead it shows your own team only.
- Aim Trainer, Build Trainer and Practice Launcher refuse to run in a live match with other players.
- The Coach, kill tracking and round tracking use only your own stats and what the game already shows on its scoreboard.
- No wallhacks, aim assist or anything that reveals hidden information.

## Install (players)

1. Install Euro3v3 from Steam.
2. Download the zip (BepInEx is already included, see the button at the bottom) and extract it into the game folder (replace files if asked). `winhttp.dll` must end up next to `Euro3v3.exe`.
3. Start the game. The first launch takes 1 to 3 minutes while BepInEx prepares itself.
4. Press **F8**, open **Featured**, **New** or **Download** and press **GET** on the mods you want.

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

Create `BepInEx/debug.trigger` while the game runs and the plugin writes `BepInEx/debug-state.txt` and `debug-shot.png` (state report and a screenshot of the game).
`BepInEx/debug-types.trigger` lists the members of named game classes into `debug-types.txt` (a line starting with `*` searches type names, `~` searches member names).
`BepInEx/debug-menu.trigger` opens the Mods menu on a tab (`0`-`3`) or on a mod page (`view:Mod Name`) and saves a screenshot.

## Credits

[BepInEx](https://github.com/BepInEx/BepInEx), [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop), [NLayer](https://github.com/naudio/NLayer) and [NVorbis](https://github.com/NVorbis/NVorbis) (audio decoding).

## License

MIT, see `LICENSE`. BepInEx and the other libraries keep their own licenses.

---

## Download

BepInEx and everything else is **already inside the zip**, so there is nothing else to install.

[![Download Euro Mods Menu 1.0.2 (with BepInEx)](https://img.shields.io/badge/Download-Euro%20Mods%20Menu%201.0.2%20%C2%B7%20with%20BepInEx-2ea44f?style=for-the-badge&logo=github)](https://github.com/turlututku-cmyk/euro1v1-mods/raw/main/download/Euro3v3_ModsMenu_1.0.2.zip)

- Version: **1.0.2** (see `CHANGELOG.md`)
- File: `download/Euro3v3_ModsMenu_1.0.2.zip` (33 MB)
- SHA-256: `4498c364eb6e8b117d5f9f32bde74fc31ab12c2a25ee8a21a1ffacf3f0db214e`
- Extract it into the Euro3v3 game folder, start the game, press **F8** and GET the mods you want.
- Updating: extract a newer zip over the old one. Your settings are kept.
