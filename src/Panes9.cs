using UnityEngine;

namespace Euro3v3.ModsMenu
{
    public static partial class Window
    {
        // ---------- Match Found Alert ----------
        static void MaPane()
        {
            EnableRow(Cfg.MaOn);
            Tog("Play a chime", Cfg.MaSound);
            Sld("Chime volume", Cfg.MaVol, 0f, 1f, "0.00");
            Tog("Flash the taskbar icon", Cfg.MaFlash);
            Tog("Show an in-queue timer", Cfg.MaTimer);
            var t = Ctl("Test", 34);
            if (SmallBtn(new Rect(t.x, t.y, 150, 30), "TEST ALERT", G.Accent)) Features.MatchAlertNow();
            Note("When the matchmaking queue finds a match, you hear a chime and the game's taskbar icon flashes until you switch back. Turn on Run in Background so the sound plays while the game is not in front.", 60);
        }

        // ---------- Practice Launcher ----------
        static void PlPane()
        {
            EnableRow(Cfg.PlOn);
            Header("START A BOT MATCH");
            string[] desc = { "relaxed bots", "normal bots", "tougher bots", "the real test" };
            Color[] col = { new Color(0.35f, 1f, 0.5f), new Color(0.4f, 0.75f, 1f), new Color(1f, 0.65f, 0.2f), new Color(1f, 0.35f, 0.35f) };
            float gap = 10f, bw = (_rw - 3 * gap) / 4f;
            for (int i = 0; i < 4; i++)
            {
                var r = new Rect(_rx + i * (bw + gap), _py, bw, 78f);
                bool hov = G.Hover(r);
                G.Fill(r, hov ? G.RowHover : G.Row);
                G.Fill(new Rect(r.x, r.y, r.width, 3), col[i]);
                G.Label(new Rect(r.x, r.y + 10, r.width, 30), Features.PracticeNames[i].ToUpperInvariant(), 20, col[i], TextAnchor.MiddleCenter, true);
                G.Label(new Rect(r.x, r.y + 44, r.width, 22), desc[i], 12, G.Dim, TextAnchor.MiddleCenter);
                if (G.Click(r)) Features.PracticeStart(i);
            }
            _py += 96f;
            Note("Starts one of the game's own bot missions with one click, the same as Play > VS Bots. Not available while you are in a live match with other players. After it loads, use Aim Trainer (F2) or Build Trainer (F4) inside it.", 60);
        }

        // ---------- Build Trainer ----------
        static void BtPane()
        {
            EnableRow(Cfg.BtOn);
            var m = Ctl("Drill");
            Cfg.BtMode.Value = G.Segmented(m, Features.BtModes, Cfg.BtMode.Value);
            Sld("Time (s)", Cfg.BtTime, 10f, 120f, "0");
            var b = Ctl("", 36);
            if (SmallBtn(new Rect(b.x, b.y, 200, 32), Features.BtActive ? "STOP  (F4)" : "START  (F4)", G.Accent)) Features.StartBuildTrainer();
            Header("BEST SCORES");
            string line = "";
            for (int i = 0; i < 4; i++) line += Features.BtModes[i] + " " + Cfg.BtBest[i].Value + (i < 3 ? "      " : "");
            G.Label(new Rect(_rx, _py, _rw, 24), line, 14, G.Text); _py += 28;
            if (!string.IsNullOrEmpty(Features.BtResult)) { G.Label(new Rect(_rx, _py, _rw, 24), "Last: " + Features.BtResult, 13, G.Dim); _py += 28; }
            Note("Practice only. Start a bot match (Practice Launcher or Play > VS Bots), press F4 and build as many parts as you can before the timer ends. Only parts that appear during the drill count. It never runs with other players in the match.", 70);
        }

        // ---------- Chat Filter ----------
        static string _cfWordsBox, _cfBlockBox, _cfTestOut = "";
        static void CfPane()
        {
            EnableRow(Cfg.CfOn);
            Tog("Hide spam", Cfg.CfSpam);
            Tog("Filter swearing", Cfg.CfSwear);
            Tog("Hide whole message", Cfg.CfHide);
            var r1 = Ctl("Blocked words");
            var b1 = new Rect(r1.x, r1.y, r1.width, r1.height);
            G.Fill(b1, G.Panel); Outline(b1, Border);
            if (_cfWordsBox == null) _cfWordsBox = Cfg.CfWords.Value;
            var nv1 = G.TextField("cfwords", b1, _cfWordsBox, "word1, word2, word3", 200);
            if (nv1 != _cfWordsBox) { _cfWordsBox = nv1; Cfg.CfWords.Value = nv1; G.Changed = true; }
            var r2 = Ctl("Blocked players");
            var b2 = new Rect(r2.x, r2.y, r2.width, r2.height);
            G.Fill(b2, G.Panel); Outline(b2, Border);
            if (_cfBlockBox == null) _cfBlockBox = Cfg.CfBlock.Value;
            var nv2 = G.TextField("cfblock", b2, _cfBlockBox, "name1, name2", 200);
            if (nv2 != _cfBlockBox) { _cfBlockBox = nv2; Cfg.CfBlock.Value = nv2; G.Changed = true; }
            var t = Ctl("Test", 34);
            if (SmallBtn(new Rect(t.x, t.y, 150, 30), "TEST FILTER", G.Accent)) _cfTestOut = Features.CfTest("Player: you are a shit player, shit player");
            if (!string.IsNullOrEmpty(_cfTestOut)) { G.Label(new Rect(t.x + 160, t.y, t.width - 160, 30), "-> " + _cfTestOut, 12, G.Dim, TextAnchor.MiddleLeft); }
            G.Label(new Rect(_rx, _py, _rw, 22), "Messages hidden or masked this session: " + Features.CfHidden, 13, G.Dim); _py += 26;
            Note("Only changes what you see in the chat, nothing is sent to anyone. Separate words and names with commas. Spam means the same message again within 6 seconds. With 'Hide whole message' off, bad words become ***.", 60);
        }
    }
}
