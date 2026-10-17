using BepInEx.Configuration;

namespace Euro3v3.ModsMenu
{
    public static class Cfg
    {
        public static ConfigEntry<bool> CrossOn, CrossOutline, CrossDot, CrossAlways, CrossHideDefault;
        public static ConfigEntry<int> CrossStyle;
        public static ConfigEntry<float> CrossSize, CrossThick, CrossGap, CrossAlpha, CrossR, CrossG, CrossB;
        public static ConfigEntry<bool> FpsOn, ClockOn, Clock24, ShotOn, LimitOn, Vsync, FovOn, VolOn, BgOn;
        public static ConfigEntry<int> FpsCorner, ClockCorner;
        public static ConfigEntry<string> Recent, Installed;
        public static ConfigEntry<bool> BvOn, GunOn, TextOn, TextMenusOnly, FreeOn;
        public static ConfigEntry<bool> InputOn, InputMouse, HitOn, HitMuteGame, HitVary, DmgOn, DmgKillTag, ClipOn, DmgHeal, ClipAutoMap;
        public static ConfigEntry<float> ClipSeconds;
        public static ConfigEntry<bool> StatsOn, BarsOn, TintOn, GraphOn, NotesOn, BackupOn;
        public static ConfigEntry<int> StatsCorner, TintIdx, GraphCorner, NotesCorner;
        public static ConfigEntry<float> BarsSize, TintStrength, GraphScale, NotesSize;
        public static ConfigEntry<bool> HpLabelOn;
        public static ConfigEntry<bool> KmOn, KmOthers, KmHead, AnnOn, AnnSound, ConfOn, ConfHead, ConfAssist, SsOn, SsOverlay, SsRoundEnd, SsPopup, AimOn, LhOn, LhPulse, LhSound, LhText;
        public static ConfigEntry<float> LhThreshold, LhStrength, LhVol;
        public static ConfigEntry<bool> DtOn, DtKiller, StOn, StText, SbOn, SbCard, CmOn;
        public static ConfigEntry<int> DtStyle, CmMod;
        public static ConfigEntry<float> DtSize, StMisses, StVol, SbDelay, CmCooldown;
        public static ConfigEntry<string>[] CmMsgs = new ConfigEntry<string>[6];
        public static ConfigEntry<bool> CoachOn, CoachRound, CoachMatch, CoachLive;
        public static ConfigEntry<bool> AimSound, AimCard;
        public static ConfigEntry<string> KbMenu, KbShot, KbAim, KbSession, KbBuild, KbFreecam, KbFcNext, KbFcPrev, KbClipSave, KbClipRec, KbProfile, KbViewerExit;
        public static ConfigEntry<string>[] CmKeys = new ConfigEntry<string>[6];
        public static ConfigEntry<string>[] PhKeys = new ConfigEntry<string>[5];
        public static ConfigEntry<bool> TpOn, TpDist, TpDead, ClOn, ClDealt, ClSummary;
        public static ConfigEntry<int> TpCorner, ClCorner;
        public static ConfigEntry<float> ClTime;
        public static ConfigEntry<int> AimDist;
        public static ConfigEntry<int>[] AimBests = new ConfigEntry<int>[6];
        public static ConfigEntry<bool> CfOn, CfSpam, CfSwear, CfHide, MaOn, MaSound, MaFlash, MaTimer, PlOn, BtOn;
        public static ConfigEntry<string> CfWords, CfBlock;
        public static ConfigEntry<float> MaVol, BtTime;
        public static ConfigEntry<int> BtMode;
        public static ConfigEntry<int>[] BtBest = new ConfigEntry<int>[4];
        public static ConfigEntry<int> CoachCorner;
        public static ConfigEntry<int> KmCorner, SsCorner, AimMode, AimBest0, AimBest1;
        public static ConfigEntry<float> KmTime, AnnSize, AnnVol, ConfAmount, AimTime, AimSize;
        public static ConfigEntry<string> NotesText;
        public static ConfigEntry<int> InputCorner, HitStyle, KillStyle, DmgPlace;
        public static ConfigEntry<float> InputScale, InputOpacity, HitVol, DmgSize, DmgTime;
        public static ConfigEntry<float> FreeSens;
        public static ConfigEntry<bool> HideOn, HideAds, HideNews, HideFeedback, HideVersion, HideTasks, HideQuest, HideLevel, HideParty, HideDials, HideAtm, HideIngameLevel;
        public static ConfigEntry<bool> ProfOn, PauseOn, PauseHints, MusicOn, MusicShuffle, MusicMenusOnly, MusicMuteGame, MusicAuto;
        public static ConfigEntry<bool> RtxOn, RtxShadows, RtxHdr, RtxAniso, RtxTonemap;
        public static ConfigEntry<string> ProfList, ProfActive;
        public static ConfigEntry<int> RtxMsaa;
        public static ConfigEntry<float> MusicVol, RtxScale, RtxShadowDist, RtxLod, RtxBloom, RtxSat, RtxContrast, RtxExposure;
        public static ConfigEntry<int> GunScope, TextMode;
        public static ConfigEntry<float> GunSpeed, GunSat, TextSpeed;
        public static ConfigEntry<bool> CrossRainbow, VigOn, NeonOn, NeonRainbow, TrailOn, TrailRainbow, PartOn, PartMenusOnly, TrailMenusOnly, ThemeOn;
        public static ConfigEntry<int> VigColor, NeonColor, PartStyle, ThemeIdx;
        public static ConfigEntry<float> VigStrength, NeonThick, PartDensity, PartSpeed, TrailLife;
        public static ConfigEntry<float> LimitFps, Fov, Vol;

        static ConfigEntry<T> B<T>(string section, string key, T def) =>
            Plugin.Instance.Config.Bind(section, key, def);

        public static void Init()
        {
            Plugin.Instance.Config.SaveOnConfigSet = false;
            CrossOn = B("Crosshair", "Enabled", false);
            CrossStyle = B("Crosshair", "Style", 0);
            CrossSize = B("Crosshair", "Size", 10f);
            CrossThick = B("Crosshair", "Thickness", 2f);
            CrossGap = B("Crosshair", "Gap", 4f);
            CrossAlpha = B("Crosshair", "Opacity", 1f);
            CrossR = B("Crosshair", "Red", 0.2f);
            CrossG = B("Crosshair", "Green", 1f);
            CrossB = B("Crosshair", "Blue", 0.2f);
            CrossOutline = B("Crosshair", "Outline", true);
            CrossDot = B("Crosshair", "CenterDot", false);
            CrossAlways = B("Crosshair", "ShowInMenus", false);
            CrossHideDefault = B("Crosshair", "HideDefaultCrosshair", false);

            FpsOn = B("FpsCounter", "Enabled", false);
            FpsCorner = B("FpsCounter", "Corner", 0);
            ClockOn = B("Clock", "Enabled", false);
            Clock24 = B("Clock", "Use24Hour", true);
            ClockCorner = B("Clock", "Corner", 1);
            ShotOn = B("Screenshot", "Enabled", true);
            LimitOn = B("FpsLimit", "Enabled", false);
            LimitFps = B("FpsLimit", "Limit", 144f);
            Vsync = B("FpsLimit", "VSync", false);
            FovOn = B("Fov", "Enabled", false);
            Fov = B("Fov", "Value", 90f);
            VolOn = B("Volume", "Enabled", false);
            Vol = B("Volume", "Master", 1f);
            BgOn = B("Background", "RunInBackground", true);
            Recent = B("Menu", "RecentMods", "");
            Installed = B("Menu", "Installed", "");
            CrossRainbow = B("Crosshair", "Rainbow", false);
            VigOn = B("Vignette", "Enabled", false); VigStrength = B("Vignette", "Strength", 0.6f); VigColor = B("Vignette", "Color", 0);
            NeonOn = B("NeonBorder", "Enabled", false); NeonThick = B("NeonBorder", "Thickness", 6f); NeonRainbow = B("NeonBorder", "Rainbow", true); NeonColor = B("NeonBorder", "Color", 0);
            TrailOn = B("CursorTrail", "Enabled", false); TrailRainbow = B("CursorTrail", "Rainbow", true); TrailLife = B("CursorTrail", "Length", 0.6f); TrailMenusOnly = B("CursorTrail", "MenusOnly", true);
            PartOn = B("Particles", "Enabled", false); PartStyle = B("Particles", "Style", 0); PartDensity = B("Particles", "Density", 120f); PartSpeed = B("Particles", "Speed", 1f); PartMenusOnly = B("Particles", "MenusOnly", true);
            ThemeOn = B("Theme", "Enabled", false); ThemeIdx = B("Theme", "Color", 1);
            BvOn = B("BetterVisuals", "Enabled", true);
            FreeOn = B("Freecam", "Enabled", false); FreeSens = B("Freecam", "Sensitivity", 0.12f);
            InputOn = B("InputDisplay", "Enabled", false); InputCorner = B("InputDisplay", "Corner", 3); InputScale = B("InputDisplay", "Scale", 1f); InputOpacity = B("InputDisplay", "Opacity", 0.9f); InputMouse = B("InputDisplay", "ShowMouse", true);
            HitOn = B("HitSounds", "Enabled", false); HitVol = B("HitSounds", "Volume", 0.6f); HitStyle = B("HitSounds", "HitStyle", 0); KillStyle = B("HitSounds", "KillStyle", 3); HitVary = B("HitSounds", "PitchVariation", true); HitMuteGame = B("HitSounds", "MuteGameSound", false);
            DmgOn = B("DamageNumbers", "Enabled", false); DmgSize = B("DamageNumbers", "Size", 26f); DmgTime = B("DamageNumbers", "Duration", 1.1f); DmgKillTag = B("DamageNumbers", "KillTag", true); DmgPlace = B("DamageNumbers", "Placement", 0); DmgHeal = B("DamageNumbers", "Healing", true);
            ClipOn = B("ClipMaker", "Enabled", false); ClipSeconds = B("ClipMaker", "KeepSeconds", 120f);
            ClipAutoMap = B("ClipMaker", "OpenInRealMap", true);
            StatsOn = B("MatchStats", "Enabled", false); StatsCorner = B("MatchStats", "Corner", 2);
            BarsOn = B("Letterbox", "Enabled", false); BarsSize = B("Letterbox", "BarHeight", 0.11f);
            TintOn = B("ScreenTint", "Enabled", false); TintIdx = B("ScreenTint", "Color", 0); TintStrength = B("ScreenTint", "Strength", 0.18f);
            GraphOn = B("FpsGraph", "Enabled", false); GraphCorner = B("FpsGraph", "Corner", 1); GraphScale = B("FpsGraph", "Size", 1f);
            NotesOn = B("Notes", "Enabled", false); NotesCorner = B("Notes", "Corner", 0); NotesSize = B("Notes", "TextSize", 16f); NotesText = B("Notes", "Text", "warm up | aim for the head");
            BackupOn = B("SettingsBackup", "Installed", false);
            HpLabelOn = B("HideGui", "HealthLabel", true);
            KmOn = B("KillMessages", "Enabled", false); KmCorner = B("KillMessages", "Corner", 0); KmTime = B("KillMessages", "Seconds", 5f); KmOthers = B("KillMessages", "ShowOthers", false); KmHead = B("KillMessages", "TagHeadshots", true);
            AnnOn = B("Announcer", "Enabled", false); AnnSize = B("Announcer", "Size", 52f); AnnSound = B("Announcer", "Sound", true); AnnVol = B("Announcer", "Volume", 0.7f);
            ConfOn = B("KillConfetti", "Enabled", false); ConfAmount = B("KillConfetti", "Amount", 140f); ConfHead = B("KillConfetti", "DoubleOnHeadshot", true); ConfAssist = B("KillConfetti", "OnAssists", false);
            SsOn = B("SessionStats", "Enabled", false); SsOverlay = B("SessionStats", "Overlay", true); SsCorner = B("SessionStats", "Corner", 1); SsRoundEnd = B("SessionStats", "FreezeUntilRoundEnd", false); SsPopup = B("SessionStats", "Popup", true);
            DtOn = B("DeathTaunts", "Enabled", false); DtStyle = B("DeathTaunts", "Style", 0); DtSize = B("DeathTaunts", "TextSize", 46f); DtKiller = B("DeathTaunts", "UseKillerName", true);
            StOn = B("SadTrombone", "Enabled", false); StMisses = B("SadTrombone", "MissesInARow", 5f); StVol = B("SadTrombone", "Volume", 0.7f); StText = B("SadTrombone", "Text", true);
            SbOn = B("ScoreboardShot", "Enabled", false); SbCard = B("ScoreboardShot", "DrawResultCard", true); SbDelay = B("ScoreboardShot", "DelaySeconds", 2f);
            CmOn = B("ChatMacros", "Enabled", false); CmMod = B("ChatMacros", "Modifier", 0); CmCooldown = B("ChatMacros", "CooldownSeconds", 2f);
            { var defs = new[] { "gg", "nice shot!", "need help!", "watch out!", "good game, well played", "thanks!" }; for (int i = 0; i < 6; i++) CmMsgs[i] = B("ChatMacros", "Message" + (i + 1), defs[i]); }
            CoachOn = B("Coach", "Enabled", false); CoachRound = B("Coach", "RoundTips", true); CoachMatch = B("Coach", "MatchSummary", true); CoachLive = B("Coach", "LiveStrip", true); CoachCorner = B("Coach", "StripCorner", 1);
            CfOn = B("ChatFilter", "Enabled", false); CfSpam = B("ChatFilter", "HideSpam", true); CfSwear = B("ChatFilter", "FilterSwearing", true); CfHide = B("ChatFilter", "HideWholeMessage", false); CfWords = B("ChatFilter", "BlockedWords", ""); CfBlock = B("ChatFilter", "BlockedPlayers", "");
            MaOn = B("MatchAlert", "Enabled", false); MaSound = B("MatchAlert", "Chime", true); MaFlash = B("MatchAlert", "FlashTaskbar", true); MaTimer = B("MatchAlert", "QueueTimer", true); MaVol = B("MatchAlert", "Volume", 0.7f);
            PlOn = B("Practice", "Enabled", false);
            BtOn = B("BuildTrainer", "Enabled", false); BtMode = B("BuildTrainer", "Drill", 0); BtTime = B("BuildTrainer", "Seconds", 30f);
            { var bn = new[] { "BestAny", "BestWalls", "BestFloors", "BestStairs" }; for (int i = 0; i < 4; i++) BtBest[i] = B("BuildTrainer", bn[i], 0); }
            LhOn = B("LowHealth", "Enabled", false); LhPulse = B("LowHealth", "Pulse", true); LhSound = B("LowHealth", "Heartbeat", true); LhText = B("LowHealth", "Text", true);
            LhThreshold = B("LowHealth", "BelowPercent", 30f); LhStrength = B("LowHealth", "Strength", 0.7f); LhVol = B("LowHealth", "Volume", 0.6f);
            AimOn = B("AimTrainer", "Enabled", false); AimMode = B("AimTrainer", "Mode", 0); AimTime = B("AimTrainer", "Seconds", 60f); AimSize = B("AimTrainer", "TargetSize", 0.7f); AimBest0 = B("AimTrainer", "BestGridshot", 0); AimBest1 = B("AimTrainer", "BestFlick", 0);
            TpOn = B("TeamPanel", "Enabled", false); TpCorner = B("TeamPanel", "Corner", 0); TpDist = B("TeamPanel", "DistanceArrow", true); TpDead = B("TeamPanel", "ShowDead", true);
            ClOn = B("CombatLog", "Enabled", false); ClCorner = B("CombatLog", "Corner", 3); ClTime = B("CombatLog", "LineSeconds", 4f); ClDealt = B("CombatLog", "ShowDealt", true); ClSummary = B("CombatLog", "FightSummaries", true);
            AimSound = B("AimTrainer", "HitSounds", true); AimCard = B("AimTrainer", "ResultsCard", true); AimDist = B("AimTrainer", "Distance", 1);
            AimBests[0] = AimBest0; AimBests[1] = AimBest1;
            { var an = new[] { "BestMoving", "BestTracking", "BestHeadLevel", "BestReaction" }; for (int i = 0; i < 4; i++) AimBests[i + 2] = B("AimTrainer", an[i], 0); }
            KbMenu = B("Keys", "Menu", "F8"); KbShot = B("Keys", "Screenshot", "F12"); KbAim = B("Keys", "AimTrainer", "F2"); KbSession = B("Keys", "SessionOverlay", "F3"); KbBuild = B("Keys", "BuildTrainer", "F4");
            KbFreecam = B("Keys", "Freecam", "F6"); KbFcNext = B("Keys", "FreecamNextTarget", "E"); KbFcPrev = B("Keys", "FreecamPreviousTarget", "Q");
            KbClipSave = B("Keys", "SaveClip", "F7"); KbClipRec = B("Keys", "RecordClip", "Shift+F7"); KbProfile = B("Keys", "NextCrosshairProfile", "F9"); KbViewerExit = B("Keys", "LeaveClipViewer", "Backspace");
            { string cmod = CmMod.Value == 1 ? "Ctrl+" : CmMod.Value == 2 ? "Shift+" : "Alt+"; for (int i = 0; i < 6; i++) CmKeys[i] = B("Keys", "ChatMacro" + (i + 1), cmod + "Digit" + (i + 1)); for (int i = 0; i < 5; i++) PhKeys[i] = B("Keys", "PauseShortcut" + (i + 1), "Digit" + (i + 1)); }
            HideOn = B("HideGui", "Enabled", true); HideAds = B("HideGui", "Ads", true); HideNews = B("HideGui", "News", true); HideFeedback = B("HideGui", "FeedbackButton", true); HideVersion = B("HideGui", "VersionText", true);
            HideTasks = B("HideGui", "Tasks", false); HideQuest = B("HideGui", "Quest", false); HideLevel = B("HideGui", "LevelBar", false); HideParty = B("HideGui", "Party", false);
            HideDials = B("HideGui", "CompassAndDials", true); HideAtm = B("HideGui", "AtmInfo", false); HideIngameLevel = B("HideGui", "InGameLevel", false);
            ProfOn = B("CrosshairProfiles", "Enabled", false); ProfList = B("CrosshairProfiles", "List", ""); ProfActive = B("CrosshairProfiles", "Active", "");
            PauseOn = B("PauseHelper", "Enabled", false); PauseHints = B("PauseHelper", "ShowHints", true);
            MusicOn = B("MusicPlayer", "Enabled", false); MusicVol = B("MusicPlayer", "Volume", 0.6f); MusicShuffle = B("MusicPlayer", "Shuffle", false); MusicMenusOnly = B("MusicPlayer", "MenusOnly", true); MusicMuteGame = B("MusicPlayer", "MuteGameMusic", false); MusicAuto = B("MusicPlayer", "AutoStart", true);
            RtxOn = B("RtxMode", "Enabled", false); RtxScale = B("RtxMode", "RenderScale", 1.25f); RtxMsaa = B("RtxMode", "Msaa", 2); RtxShadows = B("RtxMode", "Shadows", true); RtxShadowDist = B("RtxMode", "ShadowDistance", 150f);
            RtxHdr = B("RtxMode", "Hdr", true); RtxAniso = B("RtxMode", "SharpTextures", true); RtxLod = B("RtxMode", "DetailDistance", 1.5f);
            RtxBloom = B("RtxMode", "Bloom", 0.6f); RtxSat = B("RtxMode", "Saturation", 12f); RtxContrast = B("RtxMode", "Contrast", 10f); RtxExposure = B("RtxMode", "Exposure", 0f); RtxTonemap = B("RtxMode", "Tonemap", true);
            GunOn = B("RainbowGuns", "Enabled", false); GunSpeed = B("RainbowGuns", "Speed", 0.25f); GunSat = B("RainbowGuns", "Saturation", 0.65f); GunScope = B("RainbowGuns", "Scope", 0);
            TextOn = B("RainbowText", "Enabled", false); TextSpeed = B("RainbowText", "Speed", 0.25f); TextMode = B("RainbowText", "Mode", 0); TextMenusOnly = B("RainbowText", "MenusOnly", false);
        }
    }
}
