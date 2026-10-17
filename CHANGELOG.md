# Changelog

## 1.0.2
- **New menu layout.** Four tabs: Installed, Featured (yellow, popular picks only), New (just added, tagged NEW) and Download (the whole list with search).
  Joke mods get a laughing face next to their name. Rows and detail pages were polished and long descriptions now wrap.
- **New mods:** Kill Messages, Announcer, Kill Confetti, Session Stats (leaderboard that updates at the end of each round), Aim Trainer,
  Low Health Warning, Death Taunts, Sad Trombone, Scoreboard Screenshot, Chat Macros, Performance Coach (tips from your own stats after every round
  and match, saved history and trends), Chat Filter, Build Trainer, Practice Launcher and Match Found Alert.
- **Menu Music Player now streams.** It decodes about 10 seconds at a time instead of one huge array, which cuts its peak memory from roughly 170 MB
  to about 10 MB for a four minute song.
- Round tracking from the game's own state machine (used by Session Stats, Scoreboard Screenshot and the Coach).
- Debug tools: member-name search for game types and a trigger that opens a menu tab or a mod page and saves a screenshot.

## 1.0.1
- **Featured now offers only Damage Numbers.** The other mods stay in the source but are not offered in the menu yet.
- **Nothing is pre-installed.** Every mod, including the ones that used to be on from the start, needs a GET press. Existing settings are kept.
- Rebuilt download zip with BepInEx included, plus a download button at the bottom of the README.
- Third-party notices added for the bundled BepInEx, .NET runtime, NLayer and NVorbis.

## 1.0.0
- First public release: in-game Mods menu (F8, or the Mods button in the Play and pause menus) with crosshair, overlays, hit sounds,
  damage numbers, visual mods, the clip maker with 3D replay viewer and video render, team freecam and more.
