# Changelog

## 1.0.4
- **Round and match detection finally works.** The game's phase (build, attack, ending ...) was being read with the wrong name, so every phase looked the same and round ends, match ends and
  "in a match" checks never fired. It now reads the real phase name. This fixes the Session Stats round snapshot, Scoreboard Screenshot, Performance Coach round and match summaries, Team Panel (match only)
  and the clip viewer's "match finished" rule.
- **Session Stats leaderboard updates live every time** (every kill and hit). Freezing it until a round ends is now an optional switch (off by default).
- **Combat Log damage matches Damage Numbers.** "Dealt" lines now use the same per-shot total that appears at your crosshair, instead of a separate scoreboard counter.
- **Combat Log shows healing.** The healing you give a teammate (the same number Damage Numbers shows over them) and the health you regain, with one switch to turn it off.
- The overlay title and the footer show your current keys.

## 1.0.3
- **Every hotkey can be changed.** Click the key box on a mod's page and press the new key (Esc cancels, Delete unbinds, RESET restores the default).
  Covers the menu key, screenshot, Aim Trainer, Session Stats overlay, Build Trainer, Freecam (toggle and teammate keys), Clip Maker (save, record, leave viewer),
  Crosshair Profiles, all six Chat Macros and the five Pause Helper shortcuts.
- **Versions are fix counts.** A mod's patch number is how many fixes it has had (Clip Maker v1.0.10, Damage Numbers v1.0.3, ...). The page shows an "N FIXES" tag and
  hovering the title lists what changed.
- **Custom icons for every mod**, drawn in code and tinted per mod, so there are no image files to ship.
- **Aim Trainer v1.1:** six drills (Gridshot, Flick, Moving, Tracking, Head level, Reaction), a 3-minute warm-up routine, combos and hit sounds,
  a results card with coaching tips (including where your misses land), best scores per minute and a saved run history.
- **New mods:** Team Panel (your teammates' health, weapon, distance and a direction arrow, your team only) and Combat Log (damage taken, dealt, healing and fight summaries).
- The NEW tab now holds only the latest additions.
- Footer shows the current menu and screenshot keys, plus a Menu key button.

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
