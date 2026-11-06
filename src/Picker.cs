// Keyboard Overlay Studio - selector de color propio (estilo del programa) + cuentagotas de pantalla
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace KOStudio
{
    public class ColorPicker : Form
    {
        double H, S, V; int A;
        Color orig;
        public Color Result;
        Action<Color> live;
        Surface sv; Strip hueBar, alphaBar; Swatch prev; Btn dropper, ok, cancel;
        TextField fHex, fR, fG, fB, fA;
        bool syncing;

        public static readonly string[] Basic = {
            "#ffffff","#d9d9d9","#a6a6a6","#737373","#404040","#1b1e22","#000000","#3a3f46",
            "#ff5a5a","#ff9a3d","#f0a24a","#ffd84a","#9dff8a","#22b455","#2bd6c0","#8fdcff",
            "#2b8fd6","#3b5bff","#7c3aed","#cfa6ff","#db2777","#ffa6d8","#ce5b29","#963e12" };

        public static Color Pick(IWin32Window owner, Color start, IEnumerable<Color> styleColors, Action<Color> liveCb, out bool accepted)
        {
            using (var d = new ColorPicker(start, styleColors, liveCb))
            {
                accepted = d.ShowDialog(owner) == DialogResult.OK;
                if (accepted) AddRecent(d.Result);
                return accepted ? d.Result : start;
            }
        }
        static void AddRecent(Color c)
        {
            string h = G.Hex(c);
            App.P.recentColors.RemoveAll(x => x == h);
            App.P.recentColors.Insert(0, h);
            if (App.P.recentColors.Count > 16) App.P.recentColors.RemoveRange(16, App.P.recentColors.Count - 16);
            App.SavePrefs();
        }

        ColorPicker(Color start, IEnumerable<Color> styleColors, Action<Color> liveCb)
        {
            orig = start; Result = start; live = liveCb;
            Text = "Color"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent; BackColor = App.T.Panel; ForeColor = App.T.Text; KeyPreview = true;
            Font = App.F(10);
            FromColor(start);

            int pad = App.Dp(22), y = pad;
            var title = new Para("Color", 15, true, false) { BackColor = BackColor };
            title.SetBounds(pad, y, App.Dp(300), title.Measure(App.Dp(300))); Controls.Add(title);

            dropper = new Btn("💧  Cuentagotas", BtnKind.Normal, null) { BackColor = BackColor };
            dropper.Click += (s, e) => EyeDrop();
            App.Tips.SetToolTip(dropper, "Toma un color de cualquier parte de la pantalla (también de otras ventanas). Clic para elegir, Esc para cancelar.");
            y += App.Dp(44);

            int svS = App.Dp(250);
            sv = new Surface(this) { BackColor = BackColor }; sv.SetBounds(pad, y, svS, svS); Controls.Add(sv);
            hueBar = new Strip(this, true) { BackColor = BackColor }; hueBar.SetBounds(pad + svS + App.Dp(14), y, App.Dp(26), svS); Controls.Add(hueBar);
            alphaBar = new Strip(this, false) { BackColor = BackColor }; alphaBar.SetBounds(pad + svS + App.Dp(50), y, App.Dp(26), svS); Controls.Add(alphaBar);

            int rx = pad + svS + App.Dp(96), rw = App.Dp(250);
            prev = new Swatch(this) { BackColor = BackColor }; prev.SetBounds(rx, y, rw, App.Dp(64)); Controls.Add(prev);
            dropper.SetBounds(rx, y + App.Dp(76), rw, App.Hit(38)); Controls.Add(dropper);

            int fy = y + App.Dp(76) + App.Hit(38) + App.Dp(12);
            fHex = Field("HEX", rx, fy, rw); fy += fHex.Height + App.Dp(6);
            int cw = (rw - App.Dp(18)) / 4;
            fR = Field("R", rx, fy, cw); fG = Field("G", rx + cw + App.Dp(6), fy, cw); fB = Field("B", rx + 2 * (cw + App.Dp(6)), fy, cw); fA = Field("A %", rx + 3 * (cw + App.Dp(6)), fy, cw);
            fHex.Commit = t => ParseHex(t);
            foreach (var f in new[] { fR, fG, fB, fA }) f.Commit = t => ParseRgb();
            fHex.Box.TextChanged += (s, e) => { if (!syncing && fHex.Box.Focused) { var t = fHex.Box.Text.Trim().TrimStart('#'); if (t.Length == 6 || t.Length == 8) ParseHex(t); } };

            y += svS + App.Dp(18);
            int gw = svS + App.Dp(96) + rw;
            Grid("Colores", Basic.Select(G.Col), pad, ref y, gw);
            if (styleColors != null)
            {
                var sc = styleColors.Select(c => Color.FromArgb(255, c)).Distinct().Take(16).ToList();
                if (sc.Count > 0) Grid("Colores de tu estilo", sc, pad, ref y, gw);
            }
            if (App.P.recentColors.Count > 0) Grid("Usados hace poco", App.P.recentColors.Select(G.Col), pad, ref y, gw);

            y += App.Dp(6);
            ok = new Btn("Aceptar", BtnKind.Primary, null) { BackColor = BackColor };
            cancel = new Btn("Cancelar", BtnKind.Normal, null) { BackColor = BackColor };
            int bw = App.Dp(130);
            ok.SetBounds(pad + gw - bw, y, bw, App.Hit(40)); cancel.SetBounds(pad + gw - 2 * bw - App.Dp(10), y, bw, App.Hit(40));
            ok.Click += (s, e) => { Result = ToColor(); DialogResult = DialogResult.OK; Close(); };
            cancel.Click += (s, e) => { if (live != null) live(orig); DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(ok); Controls.Add(cancel);
            ClientSize = new Size(pad * 2 + gw, y + App.Hit(40) + pad);
            AcceptButton = null;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { cancel.PerformClickSafe(); e.Handled = true; }
                else if (e.KeyCode == Keys.Enter && !(ActiveControl is TextField)) { ok.PerformClickSafe(); e.Handled = true; }
            };
            FormClosing += (s, e) => { if (DialogResult != DialogResult.OK && live != null) live(orig); };
            Shown += (s, e) => { App.DarkTitle(this); SyncFields(); };
        }

        TextField Field(string label, int x, int y, int w)
        {
            var f = new TextField(label, "", null, null) { BackColor = BackColor };
            f.SetBounds(x, y, w, f.Measure(w)); Controls.Add(f);
            return f;
        }
        SwatchGrid Grid(string label, IEnumerable<Color> colors, int x, ref int y, int w)
        {
            var t = new Title(label) { BackColor = BackColor }; t.SetBounds(x, y, w, t.Measure(w)); Controls.Add(t); y += t.Height + App.Dp(4);
            var g = new SwatchGrid(colors.ToList(), c => { var keepA = A; FromColor(Color.FromArgb(255, c)); A = keepA; Changed(); }) { BackColor = BackColor };
            g.SetBounds(x, y, w, g.Measure(w)); Controls.Add(g); y += g.Height + App.Dp(10);
            return g;
        }

        // ---------- conversiones ----------
        void FromColor(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)), d = mx - mn;
            double h = 0;
            if (d > 0) { if (mx == r) h = ((g - b) / d) % 6; else if (mx == g) h = (b - r) / d + 2; else h = (r - g) / d + 4; h *= 60; if (h < 0) h += 360; }
            else h = H; // gris: conserva el tono anterior
            H = h; S = mx == 0 ? 0 : d / mx; V = mx; A = c.A;
        }
        public static Color Hsv(double h, double s, double v, int a)
        {
            h = ((h % 360) + 360) % 360;
            double c = v * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = v - c, r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; } else if (h < 120) { r = x; g = c; } else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; } else if (h < 300) { r = x; b = c; } else { r = c; b = x; }
            return Color.FromArgb(Math.Max(0, Math.Min(255, a)), (int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
        }
        public Color ToColor() { return Hsv(H, S, V, A); }
        void Changed()
        {
            sv.Invalidate(); hueBar.Invalidate(); alphaBar.Invalidate(); prev.Invalidate();
            SyncFields();
            if (live != null) live(ToColor());
        }
        void SyncFields()
        {
            if (fHex == null) return;
            syncing = true;
            var c = ToColor();
            if (!fHex.Box.Focused) fHex.Box.Text = G.Hex(c).Substring(0, 7).ToUpper() + (c.A < 255 ? G.Hex(c).Substring(7).ToUpper() : "");
            if (!fR.Box.Focused) fR.Box.Text = c.R.ToString();
            if (!fG.Box.Focused) fG.Box.Text = c.G.ToString();
            if (!fB.Box.Focused) fB.Box.Text = c.B.ToString();
            if (!fA.Box.Focused) fA.Box.Text = Math.Round(c.A / 2.55).ToString();
            syncing = false;
        }
        void ParseHex(string t)
        {
            t = (t ?? "").Trim().TrimStart('#');
            if (t.Length == 3) t = "" + t[0] + t[0] + t[1] + t[1] + t[2] + t[2];
            if (t.Length != 6 && t.Length != 8) { SyncFields(); return; }
            try
            {
                var c = G.Col("#" + t + (t.Length == 6 ? A.ToString("x2") : ""));
                FromColor(c); Changed();
            }
            catch { SyncFields(); }
        }
        void ParseRgb()
        {
            int r, g, b, a;
            if (int.TryParse(fR.Box.Text, out r) && int.TryParse(fG.Box.Text, out g) && int.TryParse(fB.Box.Text, out b) && int.TryParse(fA.Box.Text, out a))
            {
                FromColor(Color.FromArgb((int)Math.Round(Math.Max(0, Math.Min(100, a)) * 2.55), Math.Max(0, Math.Min(255, r)), Math.Max(0, Math.Min(255, g)), Math.Max(0, Math.Min(255, b))));
                Changed();
            }
            else SyncFields();
        }

        // ---------- cuentagotas: captura toda la pantalla y deja elegir un pixel con lupa ----------
        void EyeDrop()
        {
            var vs = SystemInformation.VirtualScreen;
            double op = Opacity;
            Opacity = 0; Application.DoEvents(); System.Threading.Thread.Sleep(120);
            Bitmap shot = new Bitmap(vs.Width, vs.Height);
            try { using (var g = Graphics.FromImage(shot)) g.CopyFromScreen(vs.Location, Point.Empty, vs.Size); }
            catch { Opacity = op; shot.Dispose(); return; }
            Color? got = null;
            using (var sp = new ScreenPicker(shot, vs)) { sp.ShowDialog(); got = sp.Picked; }
            shot.Dispose();
            Opacity = op; Activate();
            if (got.HasValue) { var keepA = A; FromColor(Color.FromArgb(255, got.Value)); A = keepA; Changed(); }
        }

        // ---------- piezas ----------
        class Surface : Ctl
        {
            ColorPicker p; bool drag;
            public Surface(ColorPicker p) { this.p = p; TabStop = true; AccessibleName = "Saturación y brillo"; Cursor = Cursors.Cross; }
            protected override bool IsInputKey(Keys k) { var kc = k & Keys.KeyCode; return kc == Keys.Left || kc == Keys.Right || kc == Keys.Up || kc == Keys.Down || base.IsInputKey(k); }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                double st = e.Shift ? 0.1 : 0.01;
                if (e.KeyCode == Keys.Left) p.S = Math.Max(0, p.S - st); else if (e.KeyCode == Keys.Right) p.S = Math.Min(1, p.S + st);
                else if (e.KeyCode == Keys.Up) p.V = Math.Min(1, p.V + st); else if (e.KeyCode == Keys.Down) p.V = Math.Max(0, p.V - st);
                else { base.OnKeyDown(e); return; }
                e.Handled = true; p.Changed();
            }
            void At(Point pt) { p.S = Math.Max(0, Math.Min(1, pt.X / (double)(Width - 1))); p.V = Math.Max(0, Math.Min(1, 1 - pt.Y / (double)(Height - 1))); p.Changed(); }
            protected override void OnMouseDown(MouseEventArgs e) { Focus(); drag = true; Capture = true; At(e.Location); }
            protected override void OnMouseMove(MouseEventArgs e) { if (drag) At(e.Location); }
            protected override void OnMouseUp(MouseEventArgs e) { drag = false; Capture = false; }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = Prep(e);
                var r = new Rectangle(0, 0, Width, Height);
                using (var path = G.RR(new RectangleF(0, 0, Width, Height), App.Dp(10)))
                {
                    var st = g.Save(); g.SetClip(path);
                    using (var b = new SolidBrush(Hsv(p.H, 1, 1, 255))) g.FillRectangle(b, r);
                    using (var b = new LinearGradientBrush(new Rectangle(-1, 0, Width + 2, Height), Color.White, Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Horizontal)) g.FillRectangle(b, r);
                    using (var b = new LinearGradientBrush(new Rectangle(0, -1, Width, Height + 2), Color.FromArgb(0, 0, 0, 0), Color.Black, LinearGradientMode.Vertical)) g.FillRectangle(b, r);
                    g.Restore(st);
                }
                G.Stroke(g, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), App.Dp(10), App.T.Line, 1);
                float x = (float)(p.S * (Width - 1)), y = (float)((1 - p.V) * (Height - 1)), rr = App.Dp(8);
                using (var pen = new Pen(Color.Black, App.Dp(3))) g.DrawEllipse(pen, x - rr, y - rr, rr * 2, rr * 2);
                using (var pen = new Pen(Color.White, App.Dp(2))) g.DrawEllipse(pen, x - rr, y - rr, rr * 2, rr * 2);
                if (Focused) G.Ring(g, ClientRectangle, App.Dp(10));
            }
        }
        class Strip : Ctl
        {
            ColorPicker p; bool hue, drag;
            public Strip(ColorPicker p, bool hue) { this.p = p; this.hue = hue; TabStop = true; AccessibleName = hue ? "Tono" : "Opacidad"; Cursor = Cursors.Hand; }
            protected override bool IsInputKey(Keys k) { var kc = k & Keys.KeyCode; return kc == Keys.Up || kc == Keys.Down || base.IsInputKey(k); }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                int st = e.Shift ? 10 : 1;
                if (e.KeyCode == Keys.Up) { if (hue) p.H = (p.H - st + 360) % 360; else p.A = Math.Min(255, p.A + st * 3); }
                else if (e.KeyCode == Keys.Down) { if (hue) p.H = (p.H + st) % 360; else p.A = Math.Max(0, p.A - st * 3); }
                else { base.OnKeyDown(e); return; }
                e.Handled = true; p.Changed();
            }
            void At(int y)
            {
                double t = Math.Max(0, Math.Min(1, y / (double)(Height - 1)));
                if (hue) p.H = t * 359.9; else p.A = (int)Math.Round((1 - t) * 255);
                p.Changed();
            }
            protected override void OnMouseDown(MouseEventArgs e) { Focus(); drag = true; Capture = true; At(e.Y); }
            protected override void OnMouseMove(MouseEventArgs e) { if (drag) At(e.Y); }
            protected override void OnMouseUp(MouseEventArgs e) { drag = false; Capture = false; }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = Prep(e);
                var rf = new RectangleF(App.Dp(4), 0, Width - App.Dp(8), Height);
                using (var path = G.RR(rf, App.Dp(7)))
                {
                    var st = g.Save(); g.SetClip(path);
                    if (hue)
                    {
                        using (var b = new LinearGradientBrush(new RectangleF(rf.X, rf.Y - 1, rf.Width, rf.Height + 2), Color.Red, Color.Red, LinearGradientMode.Vertical))
                        {
                            var cb = new ColorBlend(7);
                            for (int i = 0; i < 7; i++) { cb.Colors[i] = Hsv(i * 60, 1, 1, 255); cb.Positions[i] = i / 6f; }
                            b.InterpolationColors = cb; g.FillRectangle(b, rf);
                        }
                    }
                    else
                    {
                        G.Checker(g, rf, App.Dp(6), Color.FromArgb(200, 200, 200), Color.FromArgb(130, 130, 130));
                        var c = Hsv(p.H, p.S, p.V, 255);
                        using (var b = new LinearGradientBrush(new RectangleF(rf.X, rf.Y - 1, rf.Width, rf.Height + 2), c, Color.FromArgb(0, c), LinearGradientMode.Vertical)) g.FillRectangle(b, rf);
                    }
                    g.Restore(st);
                }
                G.Stroke(g, rf, App.Dp(7), App.T.Line, 1);
                float y = hue ? (float)(p.H / 360 * (Height - 1)) : (float)((1 - p.A / 255.0) * (Height - 1));
                var kr = new RectangleF(1, y - App.Dp(4), Width - 2, App.Dp(8));
                G.Fill(g, kr, App.Dp(4), Color.White);
                G.Stroke(g, kr, App.Dp(4), Color.Black, 1.5f);
                if (Focused) G.Ring(g, ClientRectangle, App.Dp(8));
            }
        }
        class Swatch : Ctl
        {
            ColorPicker p;
            public Swatch(ColorPicker p) { this.p = p; TabStop = false; SetStyle(ControlStyles.Selectable, false); Cursor = Cursors.Hand; }
            protected override void OnMouseClick(MouseEventArgs e) { if (e.X < Width / 2) { p.FromColor(p.orig); p.Changed(); } }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = Prep(e); var T = App.T;
                var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
                using (var path = G.RR(r, App.Dp(10)))
                {
                    var st = g.Save(); g.SetClip(path);
                    G.Checker(g, r, App.Dp(8), Color.FromArgb(200, 200, 200), Color.FromArgb(130, 130, 130));
                    using (var b = new SolidBrush(p.orig)) g.FillRectangle(b, 0, 0, Width / 2f, Height);
                    using (var b = new SolidBrush(p.ToColor())) g.FillRectangle(b, Width / 2f, 0, Width / 2f, Height);
                    g.Restore(st);
                }
                G.Stroke(g, r, App.Dp(10), T.Line, 1);
                TextRenderer.DrawText(g, "Antes", App.F(8, true), new Rectangle(App.Dp(8), Height - App.Dp(22), Width / 2, App.Dp(20)), Contrast(p.orig), G.Line | TextFormatFlags.Left);
                TextRenderer.DrawText(g, "Nuevo", App.F(8, true), new Rectangle(Width / 2 + App.Dp(8), Height - App.Dp(22), Width / 2, App.Dp(20)), Contrast(p.ToColor()), G.Line | TextFormatFlags.Left);
            }
            static Color Contrast(Color c) { return c.A < 110 || (c.R * 0.3 + c.G * 0.59 + c.B * 0.11) > 150 ? Color.Black : Color.White; }
        }
        class SwatchGrid : Ctl
        {
            List<Color> colors; Action<Color> pick; int focusIdx;
            public SwatchGrid(List<Color> colors, Action<Color> pick) { this.colors = colors; this.pick = pick; TabStop = true; AccessibleName = "Muestras de color"; }
            int S { get { return App.Dp(26); } }
            int Per(int w) { return Math.Max(1, (w + App.Dp(6)) / (S + App.Dp(6))); }
            Rectangle R(int i) { int per = Per(Width); return new Rectangle((i % per) * (S + App.Dp(6)), (i / per) * (S + App.Dp(6)), S, S); }
            public override int Measure(int w) { int rows = (colors.Count + Per(w) - 1) / Per(w); return rows * (S + App.Dp(6)); }
            protected override bool IsInputKey(Keys k) { var kc = k & Keys.KeyCode; return kc == Keys.Left || kc == Keys.Right || kc == Keys.Enter || kc == Keys.Space || base.IsInputKey(k); }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Left && focusIdx > 0) focusIdx--;
                else if (e.KeyCode == Keys.Right && focusIdx < colors.Count - 1) focusIdx++;
                else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) pick(colors[focusIdx]);
                else { base.OnKeyDown(e); return; }
                e.Handled = true; Invalidate();
            }
            protected override void OnMouseMove(MouseEventArgs e)
            {
                for (int i = 0; i < colors.Count; i++) if (R(i).Contains(e.Location)) { Cursor = Cursors.Hand; if (App.Tips != null) App.Tips.SetToolTip(this, G.Hex(colors[i]).Substring(0, 7).ToUpper()); return; }
                Cursor = Cursors.Default;
            }
            protected override void OnMouseClick(MouseEventArgs e) { Focus(); for (int i = 0; i < colors.Count; i++) if (R(i).Contains(e.Location)) { focusIdx = i; Invalidate(); pick(colors[i]); } }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = Prep(e);
                for (int i = 0; i < colors.Count; i++)
                {
                    var r = R(i); var rf = new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
                    G.Fill(g, rf, App.Dp(6), colors[i]);
                    G.Stroke(g, rf, App.Dp(6), App.T.Line, 1);
                    if (Focused && i == focusIdx) G.Ring(g, r, App.Dp(6));
                }
            }
        }
    }

    // pantalla completa congelada con lupa: clic = elegir pixel, Esc = cancelar
    public class ScreenPicker : Form
    {
        Bitmap shot; Point mouse; public Color? Picked;
        public ScreenPicker(Bitmap shot, Rectangle vs)
        {
            this.shot = shot;
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual; Bounds = vs;
            TopMost = true; ShowInTaskbar = false; Cursor = Cursors.Cross; KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            MouseMove += (s, e) => { var old = mouse; mouse = e.Location; Invalidate(Loupe(old)); Invalidate(Loupe(mouse)); };
            MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left && mouse.X >= 0 && mouse.Y >= 0 && mouse.X < shot.Width && mouse.Y < shot.Height) Picked = shot.GetPixel(mouse.X, mouse.Y);
                Close();
            };
            mouse = PointToClient(MousePosition);
        }
        Rectangle Loupe(Point p)
        {
            int sz = App.Dp(150), off = App.Dp(24);
            int x = p.X + off, y = p.Y + off;
            if (x + sz > Width) x = p.X - off - sz;
            if (y + sz + App.Dp(30) > Height) y = p.Y - off - sz - App.Dp(30);
            return new Rectangle(x - 4, y - 4, sz + 8, sz + App.Dp(30) + 8);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.DrawImageUnscaled(shot, 0, 0);
            var lr = Loupe(mouse);
            var box = new Rectangle(lr.X + 4, lr.Y + 4, lr.Width - 8, lr.Width - 8);
            int n = 15;
            g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(shot, box, new Rectangle(mouse.X - n / 2, mouse.Y - n / 2, n, n), GraphicsUnit.Pixel);
            g.PixelOffsetMode = PixelOffsetMode.Default;
            float cell = box.Width / (float)n;
            using (var pen = new Pen(Color.White, 2)) g.DrawRectangle(pen, box.X + cell * (n / 2), box.Y + cell * (n / 2), cell, cell);
            using (var pen = new Pen(App.T.Accent, 3)) g.DrawRectangle(pen, box);
            Color c = (mouse.X >= 0 && mouse.Y >= 0 && mouse.X < shot.Width && mouse.Y < shot.Height) ? shot.GetPixel(mouse.X, mouse.Y) : Color.Black;
            var tr = new Rectangle(box.X, box.Bottom + 2, box.Width, App.Dp(28));
            using (var b = new SolidBrush(Color.FromArgb(235, 20, 22, 26))) g.FillRectangle(b, tr);
            using (var b = new SolidBrush(c)) g.FillRectangle(b, tr.X + 6, tr.Y + 6, tr.Height - 12, tr.Height - 12);
            TextRenderer.DrawText(g, G.Hex(c).Substring(0, 7).ToUpper() + "   clic = elegir · Esc = cancelar", App.F(8.5f, true), new Rectangle(tr.X + tr.Height, tr.Y, tr.Width - tr.Height, tr.Height), Color.White, G.Line | TextFormatFlags.Left);
        }
    }

    public static class BtnExt
    {
        public static void PerformClickSafe(this Btn b) { b.Focus(); b.InvokeClick(); }
    }
}
