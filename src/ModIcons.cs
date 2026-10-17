using System;
using System.Collections.Generic;
using UnityEngine;

namespace Euro3v3.ModsMenu
{
    // A tiny anti-aliased vector canvas. Icons are authored on a 64 x 64 grid (y points down) and rasterised into a texture,
    // so every mod gets its own picture without shipping any image files.
    internal class IconCanvas
    {
        public readonly int N;
        readonly float[] _p;
        readonly float _k;

        public IconCanvas(int n) { N = n; _p = new float[n * n * 4]; _k = n / 64f; }

        void Blend(int x, int y, Color c, float cov)
        {
            if (cov <= 0.002f) return;
            int i = (y * N + x) * 4;
            float sa = c.a * Mathf.Clamp01(cov), da = _p[i + 3], oa = sa + da * (1f - sa);
            if (oa <= 0f) return;
            _p[i] = (c.r * sa + _p[i] * da * (1f - sa)) / oa;
            _p[i + 1] = (c.g * sa + _p[i + 1] * da * (1f - sa)) / oa;
            _p[i + 2] = (c.b * sa + _p[i + 2] * da * (1f - sa)) / oa;
            _p[i + 3] = oa;
        }

        delegate float Sd(float px, float py);

        void Fill(float x0, float y0, float x1, float y1, Sd sd, Color c)
        {
            int ax = Mathf.Max(0, Mathf.FloorToInt(x0) - 2), ay = Mathf.Max(0, Mathf.FloorToInt(y0) - 2);
            int bx = Mathf.Min(N - 1, Mathf.CeilToInt(x1) + 2), by = Mathf.Min(N - 1, Mathf.CeilToInt(y1) + 2);
            for (int y = ay; y <= by; y++)
                for (int x = ax; x <= bx; x++)
                    Blend(x, y, c, 0.5f - sd(x + 0.5f, y + 0.5f));
        }

        public void Circle(float cx, float cy, float r, Color c)
        {
            cx *= _k; cy *= _k; r *= _k;
            Fill(cx - r, cy - r, cx + r, cy + r, (px, py) => Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy)) - r, c);
        }

        public void Ring(float cx, float cy, float r, float w, Color c)
        {
            cx *= _k; cy *= _k; r *= _k; w *= _k;
            Fill(cx - r - w, cy - r - w, cx + r + w, cy + r + w, (px, py) => Mathf.Abs(Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy)) - r) - w / 2f, c);
        }

        static float SegDist(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
            float t = l2 > 0f ? Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / l2) : 0f;
            float qx = ax + t * dx - px, qy = ay + t * dy - py;
            return Mathf.Sqrt(qx * qx + qy * qy);
        }

        public void Line(float x0, float y0, float x1, float y1, float w, Color c)
        {
            x0 *= _k; y0 *= _k; x1 *= _k; y1 *= _k; w *= _k;
            Fill(Mathf.Min(x0, x1) - w, Mathf.Min(y0, y1) - w, Mathf.Max(x0, x1) + w, Mathf.Max(y0, y1) + w, (px, py) => SegDist(px, py, x0, y0, x1, y1) - w / 2f, c);
        }

        float RectSd(float px, float py, float cx, float cy, float hw, float hh, float rad)
        {
            float qx = Mathf.Abs(px - cx) - (hw - rad), qy = Mathf.Abs(py - cy) - (hh - rad);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - rad;
        }

        public void Rect(float x, float y, float w, float h, float rad, Color c)
        {
            x *= _k; y *= _k; w *= _k; h *= _k; rad = Mathf.Min(rad * _k, Mathf.Min(w, h) / 2f);
            float cx = x + w / 2f, cy = y + h / 2f;
            Fill(x, y, x + w, y + h, (px, py) => RectSd(px, py, cx, cy, w / 2f, h / 2f, rad), c);
        }

        public void RectS(float x, float y, float w, float h, float rad, float sw, Color c)
        {
            x *= _k; y *= _k; w *= _k; h *= _k; sw *= _k; rad = Mathf.Min(rad * _k, Mathf.Min(w, h) / 2f);
            float cx = x + w / 2f, cy = y + h / 2f;
            Fill(x - sw, y - sw, x + w + sw, y + h + sw, (px, py) => Mathf.Abs(RectSd(px, py, cx, cy, w / 2f, h / 2f, rad)) - sw / 2f, c);
        }

        public void Poly(float[] pts, Color c)
        {
            int n = pts.Length / 2;
            var p = new float[pts.Length];
            float minx = 1e9f, miny = 1e9f, maxx = -1e9f, maxy = -1e9f;
            for (int i = 0; i < pts.Length; i++) p[i] = pts[i] * _k;
            for (int i = 0; i < n; i++) { minx = Mathf.Min(minx, p[2 * i]); maxx = Mathf.Max(maxx, p[2 * i]); miny = Mathf.Min(miny, p[2 * i + 1]); maxy = Mathf.Max(maxy, p[2 * i + 1]); }
            Fill(minx, miny, maxx, maxy, (px, py) =>
            {
                float dmin = 1e9f; bool inside = false;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    float ax = p[2 * i], ay = p[2 * i + 1], bx = p[2 * j], by = p[2 * j + 1];
                    dmin = Mathf.Min(dmin, SegDist(px, py, ax, ay, bx, by));
                    if ((ay > py) != (by > py) && px < (bx - ax) * (py - ay) / (by - ay) + ax) inside = !inside;
                }
                return inside ? -dmin : dmin;
            }, c);
        }

        // Stroked arc: angles in degrees, 0 = right, 90 = down, increasing clockwise on screen.
        public void Arc(float cx, float cy, float r, float a0, float a1, float w, Color c)
        {
            cx *= _k; cy *= _k; r *= _k; w *= _k;
            float span = a1 - a0;
            float e0x = cx + r * Mathf.Cos(a0 * Mathf.Deg2Rad), e0y = cy + r * Mathf.Sin(a0 * Mathf.Deg2Rad);
            float e1x = cx + r * Mathf.Cos(a1 * Mathf.Deg2Rad), e1y = cy + r * Mathf.Sin(a1 * Mathf.Deg2Rad);
            Fill(cx - r - w, cy - r - w, cx + r + w, cy + r + w, (px, py) =>
            {
                float dx = px - cx, dy = py - cy, dist = Mathf.Sqrt(dx * dx + dy * dy);
                float rel = Mathf.Repeat(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg - a0, 360f);
                if (rel <= span) return Mathf.Abs(dist - r) - w / 2f;
                float d0 = Mathf.Sqrt((px - e0x) * (px - e0x) + (py - e0y) * (py - e0y)), d1 = Mathf.Sqrt((px - e1x) * (px - e1x) + (py - e1y) * (py - e1y));
                return Mathf.Min(d0, d1) - w / 2f;
            }, c);
        }

        // rotated rectangle, handy for confetti and small details
        public void Quad(float cx, float cy, float w, float h, float angDeg, Color c)
        {
            float a = angDeg * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            float[] lx = { -w / 2f, w / 2f, w / 2f, -w / 2f }, ly = { -h / 2f, -h / 2f, h / 2f, h / 2f };
            var pts = new float[8];
            for (int i = 0; i < 4; i++) { pts[2 * i] = cx + lx[i] * ca - ly[i] * sa; pts[2 * i + 1] = cy + lx[i] * sa + ly[i] * ca; }
            Poly(pts, c);
        }

        public void Star(float cx, float cy, float R, float r, int points, Color c)
        {
            var pts = new float[points * 4];
            for (int i = 0; i < points * 2; i++)
            {
                float ang = (-90f + i * 180f / points) * Mathf.Deg2Rad, rad = (i % 2 == 0) ? R : r;
                pts[2 * i] = cx + rad * Mathf.Cos(ang); pts[2 * i + 1] = cy + rad * Mathf.Sin(ang);
            }
            Poly(pts, c);
        }

        static readonly int[] Seg = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };   // a b c d e f g bits for 0-9

        public void Digit(int d, float x, float y, float w, float h, float t, Color c)
        {
            int m = Seg[Mathf.Clamp(d, 0, 9)]; float r = t / 2f + 0.4f, hm = h / 2f;
            if ((m & 1) != 0) Line(x + r, y, x + w - r, y, t, c);                       // a
            if ((m & 2) != 0) Line(x + w, y + r, x + w, y + hm - r, t, c);              // b
            if ((m & 4) != 0) Line(x + w, y + hm + r, x + w, y + h - r, t, c);          // c
            if ((m & 8) != 0) Line(x + r, y + h, x + w - r, y + h, t, c);               // d
            if ((m & 16) != 0) Line(x, y + hm + r, x, y + h - r, t, c);                 // e
            if ((m & 32) != 0) Line(x, y + r, x, y + hm - r, t, c);                     // f
            if ((m & 64) != 0) Line(x + r, y + hm, x + w - r, y + hm, t, c);            // g
        }

        public Texture2D ToTexture()
        {
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var cols = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int i = (y * N + x) * 4;
                    cols[(N - 1 - y) * N + x] = new Color(_p[i], _p[i + 1], _p[i + 2], _p[i + 3]);
                }
            tex.SetPixels(cols);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }
    }

    internal static class ModIconArt
    {
        static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);
        static Color Lt(Color c, float t) => new Color(Mathf.Lerp(c.r, 1f, t), Mathf.Lerp(c.g, 1f, t), Mathf.Lerp(c.b, 1f, t), 1f);
        static float Cs(float deg) => Mathf.Cos(deg * Mathf.Deg2Rad);
        static float Sn(float deg) => Mathf.Sin(deg * Mathf.Deg2Rad);

        // returns false when the name has no dedicated icon (a generic plug is drawn instead)
        public static bool Draw(string name, IconCanvas c, Color t)
        {
            Color W = new Color(0.97f, 0.98f, 1f, 1f), L = Lt(t, 0.55f), K = Mul(t, 0.28f), R = new Color(1f, 0.38f, 0.34f, 1f), Gn = new Color(0.4f, 0.95f, 0.5f, 1f), Yl = new Color(1f, 0.84f, 0.25f, 1f), Bl = new Color(0.45f, 0.78f, 1f, 1f);
            switch (name)
            {
                case "Crosshair Changer":
                    c.Ring(32, 32, 17, 3.2f, t); c.Line(32, 6, 32, 21, 3.2f, W); c.Line(32, 43, 32, 58, 3.2f, W); c.Line(6, 32, 21, 32, 3.2f, W); c.Line(43, 32, 58, 32, 3.2f, W); c.Circle(32, 32, 2.8f, W); return true;
                case "FPS Counter":
                    c.Arc(32, 38, 21, 150, 390, 4.5f, t); c.Line(32, 38, 32 + 15 * Cs(300), 38 + 15 * Sn(300), 3.4f, W); c.Circle(32, 38, 4f, W);
                    for (int i = 0; i < 5; i++) { float a = 150 + i * 60; c.Line(32 + 15 * Cs(a), 38 + 15 * Sn(a), 32 + 18.5f * Cs(a), 38 + 18.5f * Sn(a), 2f, L); }
                    return true;
                case "Clock":
                    c.Circle(32, 32, 24, K); c.Ring(32, 32, 22, 3.6f, t); c.Line(32, 32, 32, 16, 3.4f, W); c.Line(32, 32, 43, 39, 3.4f, W); c.Circle(32, 32, 3f, W);
                    c.Line(32, 12, 32, 15, 2.4f, L); c.Line(32, 49, 32, 52, 2.4f, L); c.Line(12, 32, 15, 32, 2.4f, L); c.Line(49, 32, 52, 32, 2.4f, L); return true;
                case "Screenshot Key":
                    c.Rect(8, 20, 48, 34, 7, t); c.Rect(22, 13, 18, 9, 3, t); c.Circle(32, 37, 12, K); c.Ring(32, 37, 10, 3.6f, W); c.Circle(32, 37, 4.5f, L); c.Rect(45, 25, 7, 4, 2, W); return true;
                case "FPS Limiter":
                    c.Rect(6, 26, 52, 13, 6.5f, K); c.Rect(6, 26, 32, 13, 6.5f, t); c.Line(44, 16, 44, 49, 4, R); c.Rect(40, 12, 8, 6, 2, R); return true;
                case "FOV Changer":
                    c.Poly(new float[] { 32, 50, 6, 14, 58, 14 }, K); c.Line(32, 50, 6, 14, 3.4f, t); c.Line(32, 50, 58, 14, 3.4f, t); c.Arc(32, 50, 37, -128, -52, 3.4f, W); c.Circle(32, 50, 4, W); return true;
                case "Volume Control":
                    c.Poly(new float[] { 8, 25, 19, 25, 31, 14, 31, 50, 19, 39, 8, 39 }, t); c.Arc(33, 32, 10, -52, 52, 3.6f, W); c.Arc(33, 32, 19, -54, 54, 3.6f, L); return true;
                case "Run in Background":
                    c.Rect(7, 11, 50, 42, 7, K); c.Rect(7, 11, 50, 11, 6, t); c.Circle(14, 16.5f, 2, W); c.Circle(21, 16.5f, 2, L); c.Poly(new float[] { 26, 29, 26, 47, 43, 38 }, W); return true;
                case "Better Visuals":
                    c.Star(27, 29, 22, 5, 4, t); c.Star(48, 14, 9, 2.4f, 4, W); c.Star(47, 47, 8, 2, 4, L); return true;
                case "Crosshair Profiles":
                    c.Rect(18, 12, 38, 32, 6, Mul(t, 0.55f)); c.Rect(8, 24, 38, 32, 6, t); c.Ring(27, 40, 8, 2.6f, W); c.Circle(27, 40, 1.8f, W); c.Line(27, 29, 27, 33, 2.4f, W); c.Line(27, 47, 27, 51, 2.4f, W); return true;
                case "Pause Helper":
                    c.Circle(32, 32, 24, K); c.Ring(32, 32, 22, 4, t); c.Rect(22, 21, 7, 22, 2.5f, W); c.Rect(35, 21, 7, 22, 2.5f, W); return true;
                case "Menu Music Player":
                    c.Circle(21, 47, 9, t); c.Rect(27, 10, 4.5f, 37, 2.2f, t); c.Poly(new float[] { 31, 10, 49, 17, 49, 29, 31, 22 }, W); c.Circle(48, 41, 7, L); c.Rect(52, 22, 4, 20, 2, L); return true;
                case "RTX Mode":
                    c.Circle(32, 32, 11, t); c.Circle(32, 32, 6, W);
                    for (int i = 0; i < 8; i++) { float a = i * 45; c.Line(32 + 17 * Cs(a), 32 + 17 * Sn(a), 32 + 26 * Cs(a), 32 + 26 * Sn(a), 3.4f, i % 2 == 0 ? W : L); }
                    return true;
                case "Freecam (Team)":
                    c.Rect(6, 24, 32, 24, 5, t); c.Poly(new float[] { 38, 31, 57, 22, 57, 50, 38, 41 }, L); c.Circle(16, 31, 3, W); c.Arc(32, 34, 27, 195, 345, 3, W); c.Poly(new float[] { 59, 36, 52, 38, 56, 28 }, W); return true;
                case "Input Display":
                    c.Rect(9, 9, 46, 46, 10, K); c.RectS(9, 9, 46, 46, 10, 3.4f, t); c.Line(21, 37, 32, 25, 4.4f, W); c.Line(32, 25, 43, 37, 4.4f, W); return true;
                case "Hit Sounds":
                    c.Line(16, 16, 27, 27, 4.4f, t); c.Line(48, 16, 37, 27, 4.4f, t); c.Line(16, 48, 27, 37, 4.4f, t); c.Line(48, 48, 37, 37, 4.4f, t); c.Circle(32, 32, 3.2f, W);
                    c.Arc(32, 32, 27, -32, 32, 3, L); c.Arc(32, 32, 27, 148, 212, 3, L); return true;
                case "Damage Numbers":
                    c.Digit(4, 11, 15, 16, 30, 5.2f, R); c.Digit(7, 37, 15, 16, 30, 5.2f, W); c.Line(9, 56, 55, 56, 2.6f, L); return true;
                case "Clip Maker":
                    c.Rect(6, 14, 52, 38, 7, t); for (int i = 0; i < 4; i++) { c.Rect(11 + i * 12, 18, 6, 5, 1.5f, K); c.Rect(11 + i * 12, 43, 6, 5, 1.5f, K); }
                    c.Circle(32, 33, 9.5f, K); c.Circle(32, 33, 7, R); return true;
                case "Match Stats":
                    c.Rect(9, 35, 11, 19, 2.5f, L); c.Rect(26, 22, 11, 32, 2.5f, t); c.Rect(43, 11, 11, 43, 2.5f, W); c.Line(5, 57, 59, 57, 2.6f, L); return true;
                case "Letterbox":
                    c.Rect(5, 12, 54, 40, 4, t); c.Rect(5, 12, 54, 9, 3, new Color(0.04f, 0.04f, 0.06f, 1f)); c.Rect(5, 43, 54, 9, 3, new Color(0.04f, 0.04f, 0.06f, 1f)); c.Circle(32, 32, 5, W); return true;
                case "Screen Tint":
                    c.Poly(new float[] { 32, 6, 15, 35, 49, 35 }, t); c.Circle(32, 39, 17, t); c.Circle(26, 41, 5, L); c.Arc(32, 39, 11, 20, 90, 3, W); return true;
                case "FPS Graph":
                    c.Line(10, 8, 10, 54, 2.6f, L); c.Line(10, 54, 58, 54, 2.6f, L);
                    c.Line(13, 44, 23, 29, 3.4f, t); c.Line(23, 29, 31, 38, 3.4f, t); c.Line(31, 38, 41, 17, 3.4f, t); c.Line(41, 17, 49, 27, 3.4f, t); c.Line(49, 27, 57, 12, 3.4f, t); c.Circle(57, 12, 3.4f, W); return true;
                case "Notes":
                    c.Poly(new float[] { 11, 7, 53, 7, 53, 39, 39, 57, 11, 57 }, t); c.Poly(new float[] { 39, 57, 39, 40, 53, 39 }, L);
                    c.Line(18, 19, 45, 19, 3.2f, K); c.Line(18, 29, 45, 29, 3.2f, K); c.Line(18, 39, 32, 39, 3.2f, K); return true;
                case "Settings Backup":
                    c.Rect(8, 8, 48, 48, 6, t); c.Rect(19, 8, 26, 17, 2.5f, L); c.Rect(36, 11, 5, 11, 1.2f, K); c.Rect(15, 34, 34, 22, 3, W); c.Line(21, 41, 43, 41, 2.4f, K); c.Line(21, 48, 37, 48, 2.4f, K); return true;
                case "Kill Messages":
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 9 + i * 16; Color a = i == 1 ? Bl : R;
                        c.Rect(7, y, 50, 12, 3, K); c.Rect(7, y, 5, 12, 1.5f, a); c.Line(17, y + 6, 17 + (i == 0 ? 24 : i == 1 ? 32 : 18), y + 6, 3.2f, W); c.Circle(50, y + 6, 2.6f, a);
                    }
                    return true;
                case "Announcer":
                    c.Poly(new float[] { 8, 26, 25, 26, 45, 11, 45, 53, 25, 38, 8, 38 }, t); c.Rect(12, 37, 9, 17, 2.5f, L); c.Arc(47, 32, 8, -52, 52, 3.4f, W); c.Arc(47, 32, 16, -52, 52, 3.4f, L); return true;
                case "Kill Confetti":
                    c.Quad(14, 16, 9, 5, 30, Yl); c.Quad(30, 9, 8, 5, -20, R); c.Quad(48, 15, 9, 5, 50, Bl); c.Quad(9, 34, 8, 5, -40, Gn); c.Quad(54, 33, 8, 5, 20, t);
                    c.Circle(22, 26, 3, R); c.Circle(41, 25, 3, Yl); c.Circle(33, 19, 2.6f, W); c.Circle(19, 45, 2.6f, Bl); c.Circle(47, 46, 3, Gn);
                    c.Poly(new float[] { 32, 58, 24, 44, 40, 44 }, t); c.Line(32, 44, 32, 36, 2.6f, L); c.Line(25, 46, 18, 40, 2.6f, L); c.Line(39, 46, 46, 40, 2.6f, L); return true;
                case "Session Stats":
                    c.Rect(8, 34, 15, 21, 2.5f, L); c.Rect(24.5f, 25, 15, 30, 2.5f, t); c.Rect(41, 40, 15, 15, 2.5f, L); c.Star(32, 14, 8, 3.4f, 5, Yl); return true;
                case "Aim Trainer":
                    c.Ring(32, 32, 24, 3.6f, t); c.Ring(32, 32, 15, 3.6f, W); c.Circle(32, 32, 6, R); return true;
                case "Low Health Warning":
                    c.Circle(23, 25, 12, R); c.Circle(41, 25, 12, R); c.Poly(new float[] { 11.5f, 31, 52.5f, 31, 32, 55 }, R);
                    c.Line(5, 33, 22, 33, 3.2f, W); c.Line(22, 33, 27, 21, 3.2f, W); c.Line(27, 21, 34, 46, 3.2f, W); c.Line(34, 46, 39, 33, 3.2f, W); c.Line(39, 33, 59, 33, 3.2f, W); return true;
                case "Death Taunts":
                    c.Circle(32, 27, 20, W); c.Rect(21, 38, 22, 17, 5, W); c.Circle(24, 29, 5.5f, K); c.Circle(40, 29, 5.5f, K); c.Poly(new float[] { 32, 35, 28.5f, 42, 35.5f, 42 }, K);
                    c.Line(27, 46, 27, 54, 2f, K); c.Line(32, 46, 32, 54, 2f, K); c.Line(37, 46, 37, 54, 2f, K); return true;
                case "Sad Trombone":
                    c.Line(14, 22, 42, 22, 4.4f, t); c.Line(14, 40, 42, 40, 4.4f, t); c.Arc(14, 31, 9, 90, 270, 4.4f, t); c.Poly(new float[] { 42, 18, 58, 8, 58, 54, 42, 44 }, L);
                    c.Line(24, 40, 24, 54, 3.2f, W); c.Circle(11, 54, 3, Bl); c.Poly(new float[] { 11, 46, 8, 51, 14, 51 }, Bl); return true;
                case "Scoreboard Screenshot":
                    c.Rect(6, 8, 52, 46, 6, K); c.Rect(12, 14, 40, 7, 2, t); c.Rect(12, 25, 28, 7, 2, L); c.Rect(12, 36, 20, 7, 2, L);
                    c.Rect(36, 38, 25, 19, 5, W); c.Rect(43, 35, 11, 6, 2, W); c.Circle(48.5f, 48, 6, K); c.Circle(48.5f, 48, 3.2f, t); return true;
                case "Chat Macros":
                    c.Rect(6, 9, 52, 36, 10, t); c.Poly(new float[] { 15, 43, 15, 58, 31, 43 }, t); c.Circle(20, 27, 3.6f, W); c.Circle(32, 27, 3.6f, W); c.Circle(44, 27, 3.6f, W); return true;
                case "Performance Coach":
                    c.Line(8, 49, 23, 33, 4.6f, L); c.Line(23, 33, 33, 40, 4.6f, L); c.Line(33, 40, 51, 17, 4.6f, t); c.Poly(new float[] { 58, 9, 55, 27, 41, 18 }, W); c.Star(14, 17, 9, 3.6f, 5, Yl); return true;
                case "Chat Filter":
                    c.Rect(5, 8, 42, 32, 9, t); c.Poly(new float[] { 13, 38, 13, 52, 27, 38 }, t); c.Circle(42, 41, 17, K); c.Ring(42, 41, 14, 4.4f, R); c.Line(32, 31, 52, 51, 4.4f, R); return true;
                case "Build Trainer":
                    c.Rect(6, 8, 23, 12, 2.5f, t); c.Rect(31, 8, 27, 12, 2.5f, L); c.Rect(6, 22, 12, 12, 2.5f, L); c.Rect(20, 22, 24, 12, 2.5f, t); c.Rect(46, 22, 12, 12, 2.5f, L); c.Rect(6, 36, 23, 12, 2.5f, t);
                    c.Circle(45, 44, 14, K); c.Ring(45, 44, 12, 3.8f, W); c.Rect(42, 28, 6, 4, 1.5f, W); c.Line(45, 44, 45, 36, 3, W); c.Line(45, 44, 50, 47, 3, Yl); return true;
                case "Practice Launcher":
                    c.Circle(32, 32, 25, K); c.Ring(32, 32, 23, 3.8f, t); c.Poly(new float[] { 26, 19, 26, 45, 46, 32 }, W); return true;
                case "Match Found Alert":
                    c.Circle(32, 26, 16, t); c.Poly(new float[] { 16, 26, 48, 26, 54, 46, 10, 46 }, t); c.Rect(8, 44, 48, 7, 3.5f, L); c.Circle(32, 56, 4.4f, W); c.Circle(32, 9, 3.2f, W);
                    c.Arc(32, 27, 28, 200, 232, 3.2f, Yl); c.Arc(32, 27, 28, 308, 340, 3.2f, Yl); return true;
                case "Team Panel":
                    c.Circle(13, 22, 6.5f, t); c.Circle(32, 17, 8, W); c.Circle(51, 22, 6.5f, t);
                    c.Rect(6, 31, 14, 19, 6.5f, t); c.Rect(23, 28, 18, 23, 8, W); c.Rect(44, 31, 14, 19, 6.5f, t);
                    c.Rect(7, 54, 50, 6, 3, K); c.Rect(7, 54, 34, 6, 3, Gn); return true;
                case "Combat Log":
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 8 + i * 17; Color a = i == 0 ? R : i == 1 ? Bl : Gn;
                        c.Rect(7, y, 16, 14, 3.5f, K); c.Line(11.5f, y + 7, 18.5f, y + 7, 3, a); if (i != 0) c.Line(15, y + 3.5f, 15, y + 10.5f, 3, a);
                        c.Line(29, y + 7, i == 0 ? 52 : i == 1 ? 56 : 46, y + 7, 4, i == 2 ? L : W);
                    }
                    return true;
            }
            // generic plug for external plugins and anything without its own art
            c.Rect(16, 26, 32, 22, 7, L); c.Rect(22, 10, 7, 18, 2.5f, W); c.Rect(35, 10, 7, 18, 2.5f, W); c.Line(32, 48, 32, 58, 4.4f, L);
            return false;
        }
    }

    public static partial class Window
    {
        static readonly Dictionary<string, Texture2D> _modIcons = new Dictionary<string, Texture2D>();
        static bool _iconSheet;

        public static void DebugIconSheet(bool on) { _iconSheet = on; _view = -1; }

        static Texture2D GetModIcon(ModInfo m)
        {
            string key = m.Name + "|" + ((int)(m.Tint.r * 255)) + (int)(m.Tint.g * 255) + (int)(m.Tint.b * 255);
            if (_modIcons.TryGetValue(key, out var tex) && tex != null) return tex;
            try
            {
                var cv = new IconCanvas(96);
                ModIconArt.Draw(m.Name, cv, m.Tint);
                tex = cv.ToTexture();
            }
            catch (Exception e) { Plugin.Instance.Log.LogWarning("Icon failed for " + m.Name + ": " + e.Message); tex = null; }
            _modIcons[key] = tex;
            return tex;
        }

        // draws the mod's picture inside the tile rect (the tile background is drawn by the caller)
        static void ModIcon(Rect r, ModInfo m)
        {
            var tex = GetModIcon(m);
            if (tex == null) { G.Label(r, m.Glyph, 22, m.Tint, TextAnchor.MiddleCenter, true); return; }
            float pad = r.width * 0.08f;
            GUI.DrawTexture(new Rect(r.x + pad, r.y + pad, r.width - 2 * pad, r.height - 2 * pad), tex, ScaleMode.ScaleToFit, true);
        }

        static void DrawIconSheet()
        {
            G.Fill(new Rect(20, 86, W - 40, H - FootH - 96), G.Panel);
            const int cols = 9; float cell = (W - 80f) / cols;
            for (int i = 0; i < _mods.Count; i++)
            {
                var m = _mods[i];
                float x = 40f + (i % cols) * cell, y = 98f + (i / cols) * 112f;
                var tile = new Rect(x, y, cell - 8f, cell - 8f);
                G.Fill(tile, new Color(m.Tint.r * 0.22f, m.Tint.g * 0.22f, m.Tint.b * 0.22f, 1f));
                G.Fill(new Rect(tile.x, tile.y, tile.width, 2), m.Tint);
                ModIcon(tile, m);
                G.Label(new Rect(x - 6f, y + tile.height + 2f, cell + 4f, 28f), m.Name, 9, G.Dim, TextAnchor.UpperCenter, false, true);
            }
        }
    }
}
