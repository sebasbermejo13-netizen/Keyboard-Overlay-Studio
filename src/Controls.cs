// Keyboard Overlay Studio - controles propios (tema oscuro, accesibles con teclado y lector)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace KOStudio
{
    public class Ctl : Control
    {
        public string HelpText;
        protected bool hover, down;
        public Ctl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = App.T.Panel; ForeColor = App.T.Text; Font = App.F(10);
        }
        public virtual int Measure(int w) { return Height; }
        public virtual int PrefWidth() { return Width; }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); App.Speak(SpeakText()); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected virtual string SpeakText() { return AccessibleName + (string.IsNullOrEmpty(AccessibleDescription) ? "" : ". " + AccessibleDescription); }
        protected Graphics Prep(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(BackColor);
            return g;
        }
        protected int HelpH(int w)
        {
            if (App.P.hideHelp || string.IsNullOrEmpty(HelpText)) return 0;
            return TextRenderer.MeasureText(HelpText, App.F(8.75f), new Size(w, 10000), G.Wrap).Height + App.Dp(4);
        }
        protected void DrawHelp(Graphics g, int y, int w)
        {
            if (App.P.hideHelp || string.IsNullOrEmpty(HelpText)) return;
            TextRenderer.DrawText(g, HelpText, App.F(8.75f), new Rectangle(0, y + App.Dp(4), w, 10000), App.T.Muted, G.Wrap);
        }
        protected void SetHelpAccessible() { AccessibleDescription = HelpText; if (App.Tips != null && App.P.tips && !string.IsNullOrEmpty(HelpText)) App.Tips.SetToolTip(this, HelpText); }
    }

    // ---------------- texto ----------------
    public class Para : Ctl
    {
        bool muted;
        public Para(string text, float pt, bool bold, bool muted)
        {
            Text = text; this.muted = muted;
            Font = App.F(pt, bold); TabStop = false; SetStyle(ControlStyles.Selectable, false);
            AccessibleRole = AccessibleRole.StaticText; AccessibleName = text;
        }
        public override int Measure(int w) { return TextRenderer.MeasureText(Text, Font, new Size(w, 10000), G.Wrap).Height + 2; }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(BackColor);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, muted ? App.T.Muted : App.T.Text, G.Wrap);
        }
    }

    public class Title : Ctl
    {
        public Title(string t) { Text = t; TabStop = false; SetStyle(ControlStyles.Selectable, false); Font = App.F(8.5f, true); AccessibleRole = AccessibleRole.StaticText; AccessibleName = t; }
        public override int Measure(int w) { return App.Dp(30); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(BackColor);
            var r = new Rectangle(0, App.Dp(8), Width, Height - App.Dp(8));
            TextRenderer.DrawText(g, Text.ToUpper(), Font, r, App.T.Accent2, G.Line | TextFormatFlags.Left);
            int tw = TextRenderer.MeasureText(Text.ToUpper(), Font).Width + App.Dp(10);
            using (var p = new Pen(App.T.Line)) g.DrawLine(p, tw, r.Y + r.Height / 2, Width, r.Y + r.Height / 2);
        }
    }

    // ---------------- boton ----------------
    public enum BtnKind { Normal, Primary, Danger, Ghost }
    public class Btn : Ctl
    {
        public BtnKind Kind; public string Glyph; public bool Small, Wide;
        public Btn(string text, BtnKind kind, string glyph)
        {
            Text = text; Kind = kind; Glyph = glyph; TabStop = true;
            AccessibleRole = AccessibleRole.PushButton; AccessibleName = text;
            Font = App.F(9.75f, kind == BtnKind.Primary);
            Cursor = Cursors.Hand;
        }
        public override int Measure(int w) { return Small ? App.Hit(32) : App.Hit(40); }
        public override int PrefWidth()
        {
            int w = string.IsNullOrEmpty(Text) ? 0 : TextRenderer.MeasureText(Text, Font).Width;
            if (!string.IsNullOrEmpty(Glyph) && App.HasGlyphs) w += App.Dp(22);
            return w + App.Dp(Small ? 18 : 28) + (string.IsNullOrEmpty(Text) ? App.Dp(12) : 0);
        }
        public void InvokeClick() { OnClick(EventArgs.Empty); }
        protected override bool IsInputKey(Keys k) { return k == Keys.Enter || k == Keys.Space || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { e.Handled = true; OnClick(EventArgs.Empty); } base.OnKeyDown(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e); var T = App.T;
            var r = new RectangleF(1, 1 + (down ? 1 : 0), Width - 2, Height - 3);
            Color bg, fg, line;
            switch (Kind)
            {
                case BtnKind.Primary: bg = hover ? G.Mix(T.Accent, T.Accent2, .35f) : T.Accent; fg = T.AccentInk; line = Color.Transparent; break;
                case BtnKind.Danger: bg = hover ? G.Mix(T.Card, T.Danger, .18f) : T.Card; fg = T.Danger; line = G.Mix(T.Line, T.Danger, .4f); break;
                case BtnKind.Ghost: bg = hover ? T.Card2 : BackColor; fg = T.Text; line = Color.Transparent; break;
                default: bg = hover ? T.Card2 : T.Card; fg = T.Text; line = T.Line; break;
            }
            if (!Enabled) { fg = G.Alpha(fg, .4); }
            float rad = App.Dp(9);
            if (Kind == BtnKind.Primary) G.FillV(g, r, rad, G.Mix(T.Accent2, T.Accent, .25f), bg);
            else G.Fill(g, r, rad, bg);
            if (line != Color.Transparent) G.Stroke(g, r, rad, line, 1);
            int x = 0, tw = string.IsNullOrEmpty(Text) ? 0 : TextRenderer.MeasureText(Text, Font).Width;
            bool hasG = !string.IsNullOrEmpty(Glyph) && App.HasGlyphs;
            int gw = hasG ? App.Dp(22) : 0;
            x = (Width - tw - gw) / 2;
            if (hasG) TextRenderer.DrawText(g, Glyph, App.Glyph(11), new Rectangle(x, 0, App.Dp(18), Height), fg, G.Line | TextFormatFlags.HorizontalCenter);
            if (tw > 0) TextRenderer.DrawText(g, Text, Font, new Rectangle(x + gw, 0, tw + 4, Height), fg, G.Line | TextFormatFlags.Left);
            if (Focused) G.Ring(g, ClientRectangle, rad + 2);
        }
    }

    // ---------------- deslizador ----------------
    public class Slider : Ctl
    {
        public string Label, Unit; public double Min, Max, Step, Value;
        public Action<double, bool> Changed;
        bool drag; TextBox edit;
        public Slider(string label, double min, double max, double step, string unit, string help)
        {
            Label = label; Min = min; Max = max; Step = step; Unit = unit ?? ""; HelpText = help;
            TabStop = true; AccessibleRole = AccessibleRole.Slider; AccessibleName = label; SetHelpAccessible();
        }
        int TopH { get { return App.Dp(24); } }
        int TrackH { get { return App.Hit(30); } }
        public override int Measure(int w) { return TopH + TrackH + HelpH(w); }
        public string Fmt()
        {
            if (Unit == "×") return Value.ToString("0.00", CultureInfo.InvariantCulture) + "×";
            if (Unit == "%") return Math.Round(Value) + "%";
            string n = Step < 1 ? Value.ToString("0.0", CultureInfo.InvariantCulture) : Math.Round(Value).ToString();
            return Unit == "" ? n : n + " " + Unit;
        }
        protected override string SpeakText() { return Label + ", " + Fmt() + (string.IsNullOrEmpty(HelpText) ? "" : ". " + HelpText); }
        public void SetValue(double v) { Value = Snap(v); AccessibleDescription = Fmt(); Invalidate(); }
        double Snap(double v) { v = Math.Round(v / Step) * Step; return Math.Max(Min, Math.Min(Max, Math.Round(v, 4))); }
        void Set(double v, bool commit)
        {
            v = Snap(v);
            bool ch = v != Value; Value = v; Invalidate();
            if ((ch || commit) && Changed != null) Changed(v, commit);
        }
        int X0 { get { return App.Dp(10); } }
        int X1 { get { return Width - App.Dp(10); } }
        double FromX(int x) { return Min + (Max - Min) * Math.Max(0, Math.Min(1, (x - X0) / (double)(X1 - X0))); }
        Rectangle ValueRect { get { int w = App.Dp(90); return new Rectangle(Width - w, 0, w, TopH); } }

        protected override bool IsInputKey(Keys k)
        {
            var kc = k & Keys.KeyCode;
            return kc == Keys.Left || kc == Keys.Right || kc == Keys.Up || kc == Keys.Down || kc == Keys.PageUp || kc == Keys.PageDown || kc == Keys.Home || kc == Keys.End || base.IsInputKey(k);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            double st = Step * (e.Shift ? 10 : 1);
            switch (e.KeyCode)
            {
                case Keys.Left: case Keys.Down: Set(Value - st, true); break;
                case Keys.Right: case Keys.Up: Set(Value + st, true); break;
                case Keys.PageDown: Set(Value - Step * 10, true); break;
                case Keys.PageUp: Set(Value + Step * 10, true); break;
                case Keys.Home: Set(Min, true); break;
                case Keys.End: Set(Max, true); break;
                case Keys.Enter: case Keys.F2: StartEdit(); break;
                default: base.OnKeyDown(e); return;
            }
            e.Handled = true;
            if (App.P.speak) App.Speak(Fmt());
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (e.Button != MouseButtons.Left) return;
            if (ValueRect.Contains(e.Location)) { StartEdit(); return; }
            if (e.Y >= TopH - App.Dp(4)) { drag = true; Capture = true; Set(FromX(e.X), false); }
        }
        protected override void OnMouseMove(MouseEventArgs e) { if (drag) Set(FromX(e.X), false); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { if (drag) { drag = false; Capture = false; Set(Value, true); } base.OnMouseUp(e); }

        void StartEdit()
        {
            if (edit != null) return;
            edit = new TextBox { Text = Fmt().Split(' ')[0].TrimEnd('%', '×'), BorderStyle = BorderStyle.FixedSingle, BackColor = App.T.Input, ForeColor = App.T.Text, Font = App.F(10), TextAlign = HorizontalAlignment.Right };
            var vr = ValueRect;
            edit.SetBounds(vr.X + App.Dp(10), 0, vr.Width - App.Dp(10), vr.Height);
            Controls.Add(edit); edit.Focus(); edit.SelectAll();
            edit.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; EndEdit(true); }
                else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; EndEdit(false); }
            };
            edit.LostFocus += (s, e) => EndEdit(true);
        }
        void EndEdit(bool apply)
        {
            if (edit == null) return;
            var t = edit; edit = null;
            double v;
            if (apply && double.TryParse(t.Text.Replace(',', '.').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) Set(v, true);
            Controls.Remove(t); t.Dispose(); Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e); var T = App.T;
            TextRenderer.DrawText(g, Label, App.F(9.75f, true), new Rectangle(0, 0, Width - App.Dp(90), TopH), T.Text, G.Line | TextFormatFlags.Left);
            if (edit == null) TextRenderer.DrawText(g, Fmt(), App.F(9.75f, true), ValueRect, T.Accent2, G.Line | TextFormatFlags.Right);
            int cy = TopH + TrackH / 2;
            float th = App.Dp(6);
            double f = (Value - Min) / (Max - Min);
            float tx = (float)(X0 + (X1 - X0) * f);
            G.Fill(g, new RectangleF(X0, cy - th / 2, X1 - X0, th), th / 2, T.Card2);
            G.Fill(g, new RectangleF(X0, cy - th / 2, tx - X0, th), th / 2, T.Accent);
            float r = App.Dp(drag || hover ? 10 : 9);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, tx - r, cy - r, r * 2, r * 2);
            using (var p = new Pen(T.Accent, App.Dp(3))) g.DrawEllipse(p, tx - r, cy - r, r * 2, r * 2);
            if (Focused) using (var p = new Pen(T.Focus, App.Dp(2.5f))) g.DrawEllipse(p, tx - r - App.Dp(4), cy - r - App.Dp(4), (r + App.Dp(4)) * 2, (r + App.Dp(4)) * 2);
            DrawHelp(g, TopH + TrackH - App.Dp(2), Width);
        }
    }

    // ---------------- interruptor ----------------
    public class Toggle : Ctl
    {
        public bool Checked; public Action<bool> Changed; string title;
        public Toggle(string title, string help, bool value)
        {
            this.title = title; HelpText = help; Checked = value; TabStop = true; Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.CheckButton; AccessibleName = title; SetHelpAccessible();
        }
        int TextW { get { return Width - App.Dp(90); } }
        public override int Measure(int w)
        {
            int h = TextRenderer.MeasureText(title, App.F(9.75f, true), new Size(w - App.Dp(90), 1000), G.Wrap).Height;
            if (!App.P.hideHelp && !string.IsNullOrEmpty(HelpText)) h += TextRenderer.MeasureText(HelpText, App.F(8.75f), new Size(w - App.Dp(90), 1000), G.Wrap).Height + App.Dp(2);
            return Math.Max(App.Hit(52), h + App.Dp(22));
        }
        protected override string SpeakText() { return title + (Checked ? ", activado" : ", desactivado") + (string.IsNullOrEmpty(HelpText) ? "" : ". " + HelpText); }
        protected override AccessibleObject CreateAccessibilityInstance() { return new Acc(this); }
        class Acc : ControlAccessibleObject
        {
            Toggle o; public Acc(Toggle t) : base(t) { o = t; }
            public override AccessibleStates State { get { return base.State | (o.Checked ? AccessibleStates.Checked : AccessibleStates.None); } }
        }
        void Flip() { Checked = !Checked; Invalidate(); if (Changed != null) Changed(Checked); App.Speak(Checked ? "Activado" : "Desactivado"); }
        protected override bool IsInputKey(Keys k) { return k == Keys.Space || k == Keys.Enter || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { e.Handled = true; Flip(); } base.OnKeyDown(e); }
        protected override void OnMouseClick(MouseEventArgs e) { Focus(); Flip(); base.OnMouseClick(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e); var T = App.T;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            G.Fill(g, r, App.Dp(12), hover ? T.Card2 : T.Card);
            G.Stroke(g, r, App.Dp(12), T.Line, 1);
            int pad = App.Dp(14);
            var tf = App.F(9.75f, true);
            int th = TextRenderer.MeasureText(title, tf, new Size(TextW - pad, 1000), G.Wrap).Height;
            int hh = (!App.P.hideHelp && !string.IsNullOrEmpty(HelpText)) ? TextRenderer.MeasureText(HelpText, App.F(8.75f), new Size(TextW - pad, 1000), G.Wrap).Height + App.Dp(2) : 0;
            int y = (Height - th - hh) / 2;
            TextRenderer.DrawText(g, title, tf, new Rectangle(pad, y, TextW - pad, th), T.Text, G.Wrap);
            if (hh > 0) TextRenderer.DrawText(g, HelpText, App.F(8.75f), new Rectangle(pad, y + th + App.Dp(2), TextW - pad, hh), T.Muted, G.Wrap);
            float sw = App.Dp(48), shh = App.Dp(27);
            var s = new RectangleF(Width - pad - sw, (Height - shh) / 2, sw, shh);
            G.Fill(g, s, shh / 2, Checked ? T.Accent : T.Card2);
            G.Stroke(g, s, shh / 2, Checked ? T.Accent : T.Line, 1);
            float kd = shh - App.Dp(6);
            float kx = Checked ? s.Right - kd - App.Dp(3) : s.X + App.Dp(3);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, kx, s.Y + App.Dp(3), kd, kd);
            if (Checked) TextRenderer.DrawText(g, "SÍ", App.F(7, true), new Rectangle((int)s.X + App.Dp(4), (int)s.Y, App.Dp(22), (int)shh), T.AccentInk, G.Line);
            if (Focused) G.Ring(g, ClientRectangle, App.Dp(12));
        }
    }

    // ---------------- color con opacidad ----------------
    public class ColorRow : Ctl
    {
        public Color Value; public Action<Color, bool> Changed; string name; bool dragA;
        public ColorRow(string name, string help, Color value)
        {
            this.name = name; HelpText = help; Value = value; TabStop = true;
            AccessibleRole = AccessibleRole.PushButton; AccessibleName = "Color " + name; SetHelpAccessible();
        }
        public override int Measure(int w) { return App.Hit(86) + ((App.P.hideHelp || string.IsNullOrEmpty(HelpText)) ? 0 : App.Dp(16)); }
        Rectangle Swatch { get { int w = App.Dp(64), h = App.Hit(36); return new Rectangle(Width - w - App.Dp(12), App.Dp(10), w, h); } }
        int TrackY { get { return Height - App.Dp(22); } }
        int TX0 { get { return App.Dp(96); } }
        int TX1 { get { return Width - App.Dp(64); } }
        int AlphaPct { get { return (int)Math.Round(Value.A / 2.55); } }
        protected override string SpeakText() { return name + ". Opacidad " + AlphaPct + " por ciento. Enter para elegir el color, flechas para la opacidad."; }
        void SetA(int pct, bool commit)
        {
            pct = Math.Max(0, Math.Min(100, pct));
            Value = Color.FromArgb((int)Math.Round(pct * 2.55), Value.R, Value.G, Value.B); Invalidate();
            if (Changed != null) Changed(Value, commit);
        }
        public static Func<IEnumerable<Color>> StyleColors;
        void Pick()
        {
            var start = Value; bool ok;
            var c = ColorPicker.Pick(FindForm(), start, StyleColors != null ? StyleColors() : null,
                lc => { Value = lc; Invalidate(); if (Changed != null) Changed(lc, false); }, out ok);
            Value = c; Invalidate();
            if (Changed != null) Changed(c, ok);
        }
        protected override bool IsInputKey(Keys k) { var kc = k & Keys.KeyCode; return kc == Keys.Left || kc == Keys.Right || kc == Keys.Enter || kc == Keys.Space || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            int st = e.Shift ? 10 : 1;
            if (e.KeyCode == Keys.Left) { SetA(AlphaPct - st, true); e.Handled = true; App.Speak(AlphaPct + "%"); }
            else if (e.KeyCode == Keys.Right) { SetA(AlphaPct + st, true); e.Handled = true; App.Speak(AlphaPct + "%"); }
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { e.Handled = true; Pick(); }
            base.OnKeyDown(e);
        }
        int PctFromX(int x) { return (int)Math.Round(100.0 * Math.Max(0, Math.Min(1, (x - TX0) / (double)(TX1 - TX0)))); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (Swatch.Contains(e.Location)) { Pick(); return; }
            if (e.Y > TrackY - App.Dp(14)) { dragA = true; Capture = true; SetA(PctFromX(e.X), false); }
        }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragA) SetA(PctFromX(e.X), false); Cursor = Swatch.Contains(e.Location) ? Cursors.Hand : Cursors.Default; base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { if (dragA) { dragA = false; Capture = false; SetA(AlphaPct, true); } base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e); var T = App.T;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            G.Fill(g, r, App.Dp(12), T.Card); G.Stroke(g, r, App.Dp(12), T.Line, 1);
            int pad = App.Dp(14);
            var sw = Swatch;
            TextRenderer.DrawText(g, name, App.F(9.75f, true), new Rectangle(pad, App.Dp(8), sw.X - pad * 2, App.Dp(22)), T.Text, G.Line | TextFormatFlags.Left);
            string sub = G.Hex(Value).ToUpper().Substring(0, 7);
            if (!App.P.hideHelp && !string.IsNullOrEmpty(HelpText)) sub = HelpText + "  ·  " + sub;
            TextRenderer.DrawText(g, sub, App.F(8.5f), new Rectangle(pad, App.Dp(30), sw.X - pad * 2, App.Dp(18)), T.Muted, G.Line | TextFormatFlags.Left);
            using (var p = G.RR(sw, App.Dp(8)))
            {
                var st = g.Save(); g.SetClip(p);
                G.Checker(g, sw, App.Dp(7), Color.FromArgb(200, 200, 200), Color.FromArgb(130, 130, 130));
                using (var b = new SolidBrush(Value)) g.FillRectangle(b, sw);
                g.Restore(st);
            }
            G.Stroke(g, sw, App.Dp(8), T.Line, 1);
            int ty = TrackY;
            TextRenderer.DrawText(g, "Opacidad", App.F(8.5f), new Rectangle(pad, ty - App.Dp(10), TX0 - pad, App.Dp(20)), T.Muted, G.Line | TextFormatFlags.Left);
            var tr = new RectangleF(TX0, ty - App.Dp(4), TX1 - TX0, App.Dp(8));
            using (var p = G.RR(tr, App.Dp(4)))
            {
                var st = g.Save(); g.SetClip(p);
                G.Checker(g, tr, App.Dp(4), Color.FromArgb(200, 200, 200), Color.FromArgb(130, 130, 130));
                using (var b = new LinearGradientBrush(new RectangleF(tr.X - 1, tr.Y, tr.Width + 2, tr.Height), Color.FromArgb(0, Value), Color.FromArgb(255, Value), LinearGradientMode.Horizontal)) g.FillRectangle(b, tr);
                g.Restore(st);
            }
            float tx = TX0 + (TX1 - TX0) * AlphaPct / 100f, rr = App.Dp(8);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, tx - rr, ty - rr, rr * 2, rr * 2);
            using (var p = new Pen(T.Accent, App.Dp(2.5f))) g.DrawEllipse(p, tx - rr, ty - rr, rr * 2, rr * 2);
            TextRenderer.DrawText(g, AlphaPct + "%", App.F(9, true), new Rectangle(TX1 + App.Dp(8), ty - App.Dp(10), Width - TX1 - App.Dp(14), App.Dp(20)), T.Text, G.Line | TextFormatFlags.Right);
            if (Focused) G.Ring(g, ClientRectangle, App.Dp(12));
        }
    }

    // ---------------- tarjetas de opcion ----------------
    public class Choice { public string Title, Sub; public Action<Graphics, Rectangle, bool> Paint; }
    public class Choices : Ctl
    {
        public List<Choice> Items = new List<Choice>(); public int Selected = -1, Cols = 2, CardH = 96; public Action<int> Picked;
        int focusIdx; public bool Radio = true; public Action<int> Deleted;   // si se pone: X para borrar en cada tarjeta
        public Choices(string name) { TabStop = true; AccessibleRole = AccessibleRole.List; AccessibleName = name; }
        int Rows { get { return (Items.Count + Cols - 1) / Cols; } }
        public override int Measure(int w) { return Rows * App.Dp(CardH) + (Rows - 1) * App.Dp(10) + 2; }
        Rectangle CardRect(int i)
        {
            int gap = App.Dp(10), cw = (Width - gap * (Cols - 1)) / Cols, ch = App.Dp(CardH);
            return new Rectangle((i % Cols) * (cw + gap), (i / Cols) * (ch + gap), cw, ch);
        }
        protected override string SpeakText()
        {
            if (Items.Count == 0) return AccessibleName;
            var it = Items[Math.Max(0, Math.Min(Items.Count - 1, focusIdx))];
            return AccessibleName + ": " + it.Title + (focusIdx == Selected ? ", elegido" : "") + ". " + (it.Sub ?? "") + ". Flechas para moverte, Enter para elegir.";
        }
        Rectangle XRect(int i) { var r = CardRect(i); int d = App.Dp(24); return new Rectangle(r.Right - d - App.Dp(6), r.Y + App.Dp(6), d, d); }
        void Pick(int i) { if (i < 0 || i >= Items.Count) return; if (Radio) Selected = i; focusIdx = i; Invalidate(); if (Picked != null) Picked(i); }
        protected override bool IsInputKey(Keys k) { var kc = k & Keys.KeyCode; return kc == Keys.Left || kc == Keys.Right || kc == Keys.Up || kc == Keys.Down || kc == Keys.Enter || kc == Keys.Space || kc == Keys.Delete || base.IsInputKey(k); }
        protected override void OnGotFocus(EventArgs e) { if (Selected >= 0) focusIdx = Selected; base.OnGotFocus(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            int n = focusIdx;
            switch (e.KeyCode)
            {
                case Keys.Left: n--; break;
                case Keys.Right: n++; break;
                case Keys.Up: n -= Cols; break;
                case Keys.Down: n += Cols; break;
                case Keys.Enter: case Keys.Space: e.Handled = true; Pick(focusIdx); return;
                case Keys.Delete: if (Deleted != null) { e.Handled = true; Deleted(focusIdx); } return;
                default: base.OnKeyDown(e); return;
            }
            e.Handled = true;
            if (n >= 0 && n < Items.Count) { focusIdx = n; Invalidate(); App.Speak(SpeakText()); }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool over = false; for (int i = 0; i < Items.Count; i++) if (CardRect(i).Contains(e.Location)) over = true;
            Cursor = over ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnMouseMove(e);
        }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            Focus();
            for (int i = 0; i < Items.Count; i++)
                if (CardRect(i).Contains(e.Location))
                {
                    if (Deleted != null && XRect(i).Contains(e.Location)) { focusIdx = i; Invalidate(); Deleted(i); }
                    else Pick(i);
                    break;
                }
            base.OnMouseClick(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e); var T = App.T;
            var mp = PointToClient(MousePosition);
            for (int i = 0; i < Items.Count; i++)
            {
                var r = CardRect(i); var it = Items[i];
                bool sel = i == Selected, hv = hover && r.Contains(mp);
                var rf = new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
                G.Fill(g, rf, App.Dp(12), hv ? T.Card2 : T.Card);
                G.Stroke(g, rf, App.Dp(12), sel ? T.Accent : T.Line, sel ? App.Dp(2.5f) : 1);
                int pad = App.Dp(12);
                int textH = App.Dp(string.IsNullOrEmpty(it.Sub) ? 24 : 40);
                var art = new Rectangle(r.X + pad, r.Y + pad, r.Width - pad * 2, r.Height - pad * 2 - textH);
                if (it.Paint != null && art.Height > 4) { try { it.Paint(g, art, sel); } catch { } }
                TextRenderer.DrawText(g, it.Title, App.F(9.75f, true), new Rectangle(r.X + pad, r.Bottom - pad - textH, r.Width - pad * 2, App.Dp(22)), T.Text, G.Line | TextFormatFlags.Left);
                if (!string.IsNullOrEmpty(it.Sub))
                    TextRenderer.DrawText(g, it.Sub, App.F(8.25f), new Rectangle(r.X + pad, r.Bottom - pad - App.Dp(18), r.Width - pad * 2, App.Dp(18)), T.Muted, G.Line | TextFormatFlags.Left);
                if (sel)
                {
                    int d = App.Dp(20);
                    var cr = new Rectangle(r.Right - d - App.Dp(8), r.Y + App.Dp(8), d, d);
                    using (var b = new SolidBrush(T.Accent)) g.FillEllipse(b, cr);
                    TextRenderer.DrawText(g, "✓", App.F(8, true), cr, T.AccentInk, G.Line | TextFormatFlags.HorizontalCenter);
                }
                if (Deleted != null)
                {
                    var xr = XRect(i); bool xh = hover && xr.Contains(mp);
                    if (hv || xh)
                    {
                        using (var b = new SolidBrush(xh ? T.Danger : Color.FromArgb(200, T.Card2))) g.FillEllipse(b, xr);
                        using (var pen = new Pen(xh ? T.Danger : T.Line, 1)) g.DrawEllipse(pen, xr);
                        TextRenderer.DrawText(g, "✕", App.F(8.5f, true), xr, xh ? Color.White : T.Text, G.Line | TextFormatFlags.HorizontalCenter);
                    }
                }
                if (Focused && i == focusIdx) G.Ring(g, r, App.Dp(12));
            }
        }
    }

    // ---------------- fichas (lista de teclas, simbolos, codigos) ----------------
    public class Chips : Ctl
    {
        static Font symFont; static int symScale;
        static Font SymFont
        {
            get
            {
                if (!App.Installed("Segoe UI Symbol")) return App.F(10, true);
                if (symFont == null || symScale != App.P.uiScale) { if (symFont != null) symFont.Dispose(); symScale = App.P.uiScale; symFont = new Font("Segoe UI Symbol", 12 * symScale / 100f); }
                return symFont;
            }
        }
        public List<string> Items = new List<string>(), Subs = new List<string>();
        public int Selected = -1; public bool Removable, Square; public Action<int> Picked, Removed;
        int focusIdx; List<Rectangle> rects = new List<Rectangle>();
        public string Empty = "";
        public Chips(string name) { TabStop = true; AccessibleRole = AccessibleRole.List; AccessibleName = name; }
        int ChipH { get { return Square ? App.Hit(38) : (Subs.Count > 0 ? App.Hit(50) : App.Hit(34)); } }
        void DoLayout(int w)
        {
            rects.Clear(); int x = 0, y = 0, gap = App.Dp(8), h = ChipH;
            for (int i = 0; i < Items.Count; i++)
            {
                int cw;
                if (Square) cw = h;
                else
                {
                    cw = TextRenderer.MeasureText(Items[i], App.F(10, true)).Width;
                    if (i < Subs.Count) cw = Math.Max(cw, TextRenderer.MeasureText(Subs[i] ?? "", App.F(7.5f)).Width);
                    cw += App.Dp(Removable ? 44 : 26); cw = Math.Max(cw, App.Dp(56));
                }
                if (x > 0 && x + cw > w) { x = 0; y += h + gap; }
                rects.Add(new Rectangle(x, y, cw, h)); x += cw + gap;
            }
        }
        public override int Measure(int w)
        {
            DoLayout(w);
            if (rects.Count == 0) return string.IsNullOrEmpty(Empty) ? 0 : App.Dp(26);
            int b = 0; foreach (var r in rects) b = Math.Max(b, r.Bottom); return b + 2;
        }
        protected override void OnResize(EventArgs e) { DoLayout(Width); base.OnResize(e); }
        protected override string SpeakText()
        {
            if (Items.Count == 0) return AccessibleName + ". " + Empty;
            int i = Math.Max(0, Math.Min(Items.Count - 1, focusIdx));
            return AccessibleName + ": " + Items[i] + (i < Subs.Count ? ", " + Subs[i] : "") + (Removable ? ". Suprimir para quitar." : (i == Selected ? ", elegida" : ""));
        }
        protected override bool IsInputKey(Keys k) { var kc = k & Keys.KeyCode; return kc == Keys.Left || kc == Keys.Right || kc == Keys.Up || kc == Keys.Down || kc == Keys.Enter || kc == Keys.Space || kc == Keys.Delete || kc == Keys.Back || base.IsInputKey(k); }
        protected override void OnGotFocus(EventArgs e) { if (Selected >= 0) focusIdx = Selected; base.OnGotFocus(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Items.Count == 0) { base.OnKeyDown(e); return; }
            int n = focusIdx;
            switch (e.KeyCode)
            {
                case Keys.Left: case Keys.Up: n--; break;
                case Keys.Right: case Keys.Down: n++; break;
                case Keys.Enter: case Keys.Space: e.Handled = true; Act(focusIdx); return;
                case Keys.Delete: case Keys.Back: if (Removable) { e.Handled = true; if (Removed != null) Removed(focusIdx); } return;
                default: base.OnKeyDown(e); return;
            }
            e.Handled = true;
            if (n >= 0 && n < Items.Count) { focusIdx = n; if (!Removable && !Square) Act(n); Invalidate(); App.Speak(SpeakText()); }
        }
        void Act(int i)
        {
            if (i < 0 || i >= Items.Count) return;
            if (Removable) { if (Removed != null) Removed(i); return; }
            if (!Square) Selected = i;
            focusIdx = i; Invalidate();
            if (Picked != null) Picked(i);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool over = false; foreach (var r in rects) if (r.Contains(e.Location)) over = true;
            Cursor = over ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnMouseMove(e);
        }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            Focus();
            for (int i = 0; i < rects.Count; i++) if (rects[i].Contains(e.Location)) { focusIdx = i; Act(i); break; }
            base.OnMouseClick(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e); var T = App.T;
            if (rects.Count != Items.Count) DoLayout(Width);
            if (Items.Count == 0 && !string.IsNullOrEmpty(Empty)) { TextRenderer.DrawText(g, Empty, App.F(9), ClientRectangle, T.Muted, G.Wrap); return; }
            var mp = PointToClient(MousePosition);
            for (int i = 0; i < rects.Count; i++)
            {
                var r = rects[i]; bool sel = i == Selected, hv = hover && r.Contains(mp);
                var rf = new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
                float rad = Removable ? rf.Height / 2 : App.Dp(10);
                G.Fill(g, rf, rad, sel ? T.Card2 : hv ? T.Card2 : T.Card);
                G.Stroke(g, rf, rad, sel ? T.Accent : T.Line, sel ? App.Dp(2.5f) : 1);
                if (Removable)
                {
                    TextRenderer.DrawText(g, Items[i], App.F(9, true), new Rectangle(r.X + App.Dp(12), r.Y, r.Width - App.Dp(40), r.Height), T.Text, G.Line | TextFormatFlags.Left);
                    var xr = new Rectangle(r.Right - App.Dp(30), r.Y + (r.Height - App.Dp(22)) / 2, App.Dp(22), App.Dp(22));
                    if (hv) using (var b = new SolidBrush(T.Danger)) g.FillEllipse(b, xr);
                    TextRenderer.DrawText(g, "✕", App.F(8, true), xr, hv ? Color.White : T.Muted, G.Line | TextFormatFlags.HorizontalCenter);
                }
                else if (i < Subs.Count && !string.IsNullOrEmpty(Subs[i]))
                {
                    TextRenderer.DrawText(g, Items[i], App.F(10, true), new Rectangle(r.X, r.Y + App.Dp(5), r.Width, r.Height / 2), T.Text, G.Line | TextFormatFlags.HorizontalCenter);
                    TextRenderer.DrawText(g, Subs[i], App.F(7.5f), new Rectangle(r.X + 2, r.Y + r.Height / 2, r.Width - 4, r.Height / 2 - App.Dp(5)), T.Muted, G.Line | TextFormatFlags.HorizontalCenter);
                }
                else
                {
                    if (VIcons.Is(Items[i])) VIcons.Draw(g, Items[i], RectangleF.Inflate(r, -App.Dp(11), -App.Dp(11)), T.Text);
                    else TextRenderer.DrawText(g, Items[i], Square ? SymFont : App.F(10, true), r, T.Text, G.Line | TextFormatFlags.HorizontalCenter);
                }
                if (Focused && i == focusIdx) G.Ring(g, r, rad);
            }
        }
    }

    // ---------------- campo de texto ----------------
    public class TextField : Ctl
    {
        public TextBox Box; public Action<string> Live, Commit; string label; public Btn Side;
        public TextField(string label, string value, string help, string sideButton)
        {
            this.label = label; HelpText = help; TabStop = false;
            Box = new TextBox { Text = value ?? "", BorderStyle = BorderStyle.None, BackColor = App.T.Input, ForeColor = App.T.Text, Font = App.F(11), AccessibleName = label, AccessibleDescription = help };
            Box.TextChanged += (s, e) => { if (Live != null) Live(Box.Text); };
            Box.Leave += (s, e) => { if (Commit != null) Commit(Box.Text); };
            Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; if (Commit != null) Commit(Box.Text); } };
            Box.GotFocus += (s, e) => { Invalidate(); App.Speak(label + (string.IsNullOrEmpty(help) ? "" : ". " + help) + ". Campo de texto: " + Box.Text); };
            Box.LostFocus += (s, e) => Invalidate();
            Controls.Add(Box);
            if (!string.IsNullOrEmpty(sideButton)) { Side = new Btn(sideButton, BtnKind.Normal, null); Controls.Add(Side); }
            if (App.Tips != null && App.P.tips && !string.IsNullOrEmpty(help)) App.Tips.SetToolTip(Box, help);
        }
        int BoxH { get { return Math.Max(Box.PreferredHeight + App.Dp(16), App.Hit(40)); } }
        int SideW { get { return Side != null ? Side.PrefWidth() + App.Dp(8) : 0; } }
        public override int Measure(int w) { return App.Dp(26) + BoxH + HelpH(w); }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int top = App.Dp(26), bh = BoxH, pad = App.Dp(12);
            Box.SetBounds(pad, top + (bh - Box.PreferredHeight) / 2, Width - SideW - pad * 2, Box.PreferredHeight);
            if (Side != null) Side.SetBounds(Width - SideW + App.Dp(8), top, SideW - App.Dp(8), bh);
        }
        protected override void OnMouseDown(MouseEventArgs e) { Box.Focus(); base.OnMouseDown(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e); var T = App.T;
            TextRenderer.DrawText(g, label, App.F(9.75f, true), new Rectangle(0, 0, Width, App.Dp(22)), T.Text, G.Line | TextFormatFlags.Left);
            var r = new RectangleF(0.5f, App.Dp(26) + 0.5f, Width - SideW - 1, BoxH - 1);
            G.Fill(g, r, App.Dp(9), T.Input);
            G.Stroke(g, r, App.Dp(9), Box.Focused ? T.Focus : T.Line, Box.Focused ? App.Dp(2) : 1);
            DrawHelp(g, App.Dp(26) + BoxH, Width);
        }
    }

    // ---------------- desplegable ----------------
    public class Drop : Ctl
    {
        public ComboBox Box; public Btn Side; string label;
        public Drop(string label, IEnumerable<object> items, string help, string sideButton)
        {
            this.label = label; HelpText = help; TabStop = false;
            Box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = App.T.Input, ForeColor = App.T.Text, Font = App.F(10.5f), DrawMode = DrawMode.OwnerDrawFixed, AccessibleName = label, MaxDropDownItems = 14 };
            Box.ItemHeight = App.Hit(26);
            foreach (var i in items) Box.Items.Add(i);
            if (Box.Items.Count > 0) Box.SelectedIndex = 0;
            Box.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                bool sel = (e.State & DrawItemState.Selected) != 0;
                using (var b = new SolidBrush(sel ? App.T.Accent : App.T.Input)) e.Graphics.FillRectangle(b, e.Bounds);
                TextRenderer.DrawText(e.Graphics, Box.Items[e.Index].ToString(), Box.Font, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height), sel ? App.T.AccentInk : App.T.Text, G.Line | TextFormatFlags.Left);
            };
            Box.GotFocus += (s, e) => App.Speak(label + ": " + Box.Text);
            Box.SelectedIndexChanged += (s, e) => { if (Box.Focused) App.Speak(Box.Text); };
            Controls.Add(Box);
            if (!string.IsNullOrEmpty(sideButton)) { Side = new Btn(sideButton, BtnKind.Normal, null); Controls.Add(Side); }
        }
        public override int Measure(int w) { return App.Dp(24) + Box.Height + App.Dp(4) + HelpH(w); }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int sw = Side != null ? Side.PrefWidth() + App.Dp(8) : 0;
            Box.SetBounds(0, App.Dp(24), Width - sw, Box.Height);
            if (Side != null) Side.SetBounds(Width - sw + App.Dp(8), App.Dp(24), sw - App.Dp(8), Box.Height);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e);
            TextRenderer.DrawText(g, label, App.F(9.75f, true), new Rectangle(0, 0, Width, App.Dp(22)), App.T.Text, G.Line | TextFormatFlags.Left);
            DrawHelp(g, App.Dp(24) + Box.Height, Width);
        }
    }

    // ---------------- selector de tipografia (lista desplegable) ----------------
    public class FontPicker : Ctl
    {
        class Item { public string Name; public bool Header; public override string ToString() { return Name; } }
        public ComboBox Box; public Action<string> Picked;
        int weight, last = -1; bool loading;
        static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();

        public FontPicker(string current, int weight)
        {
            this.weight = weight; TabStop = false;
            Box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = App.T.Input, ForeColor = App.T.Text,
                DrawMode = DrawMode.OwnerDrawFixed, Font = App.F(11), MaxDropDownItems = 18, AccessibleName = "Tipografía", IntegralHeight = false };
            Box.ItemHeight = App.Hit(30);
            Box.DropDownHeight = App.Dp(520);

            loading = true;
            var rec = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Box.Items.Add(new Item { Name = "RECOMENDADAS", Header = true });
            foreach (var f in Fonts.List) if (f[1] != "") { Box.Items.Add(new Item { Name = f[0] }); rec.Add(f[0]); }
            Box.Items.Add(new Item { Name = "INSTALADAS EN TU ORDENADOR", Header = true });
            var sys = new List<string>();
            try { using (var c = new System.Drawing.Text.InstalledFontCollection()) foreach (var f in c.Families) if (!rec.Contains(f.Name)) sys.Add(f.Name); } catch { }
            sys.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var n in sys) Box.Items.Add(new Item { Name = n });
            bool found = false;
            for (int i = 0; i < Box.Items.Count; i++)
            {
                var it = (Item)Box.Items[i];
                if (!it.Header && string.Equals(it.Name, current, StringComparison.OrdinalIgnoreCase)) { Box.SelectedIndex = i; found = true; break; }
            }
            if (!found && !string.IsNullOrEmpty(current)) { Box.Items.Insert(1, new Item { Name = current }); Box.SelectedIndex = 1; }
            last = Box.SelectedIndex;
            loading = false;

            Box.DrawItem += DrawItem;
            Box.SelectedIndexChanged += (s, e) =>
            {
                if (loading || Box.SelectedIndex < 0) return;
                var it = (Item)Box.Items[Box.SelectedIndex];
                if (it.Header)
                {
                    int dir = Box.SelectedIndex >= last ? 1 : -1, n = Box.SelectedIndex + dir;
                    if (n < 0 || n >= Box.Items.Count) n = last;
                    loading = true; Box.SelectedIndex = n; loading = false;
                    it = (Item)Box.Items[n];
                    if (it.Header) return;
                }
                last = Box.SelectedIndex;
                Invalidate();
                if (Picked != null) Picked(it.Name);
                App.Speak(it.Name);
            };
            Box.GotFocus += (s, e) => App.Speak("Tipografía: " + Box.Text + ". Flechas para cambiar, Alt y flecha abajo para abrir la lista.");
            Controls.Add(Box);
            Fonts.Loaded += OnFontsLoaded;
        }
        void OnFontsLoaded() { if (!IsDisposed) { Invalidate(); Box.Invalidate(); } }
        protected override void Dispose(bool disposing) { Fonts.Loaded -= OnFontsLoaded; base.Dispose(disposing); }

        static Font PreviewFont(string fam, int weight, float px)
        {
            FontFamily ff; FontStyle st;
            Fonts.Get(fam, Fonts.NearestWeight(fam, weight), out ff, out st);
            string key = ff.Name + "|" + st + "|" + px + "|" + fam;
            Font f;
            if (!cache.TryGetValue(key, out f)) { try { f = new Font(ff, px, st, GraphicsUnit.Pixel); } catch { f = App.F(11); } cache[key] = f; }
            return f;
        }
        void DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var it = (Item)Box.Items[e.Index];
            var g = e.Graphics; var T = App.T;
            bool sel = (e.State & DrawItemState.Selected) != 0 && !it.Header && (e.State & DrawItemState.ComboBoxEdit) == 0;
            using (var b = new SolidBrush(it.Header ? T.Card : sel ? T.Accent : T.Input)) g.FillRectangle(b, e.Bounds);
            var r = new Rectangle(e.Bounds.X + App.Dp(10), e.Bounds.Y, e.Bounds.Width - App.Dp(14), e.Bounds.Height);
            if (it.Header) { TextRenderer.DrawText(g, it.Name, App.F(8, true), r, T.Accent2, G.Line | TextFormatFlags.Left); return; }
            Font f = PreviewFont(it.Name, weight, Box.ItemHeight * 0.55f);
            Color c = sel ? T.AccentInk : T.Text;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            try
            {
                using (var b = new SolidBrush(c))
                using (var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString(it.Name, f, b, r, sf);
            }
            catch { TextRenderer.DrawText(g, it.Name, App.F(10), r, c, G.Line | TextFormatFlags.Left); }
        }
        public override int Measure(int w) { return Box.Height + App.Dp(70); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); Box.SetBounds(0, 0, Width, Box.Height); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e); var T = App.T;
            var r = new RectangleF(0.5f, Box.Height + App.Dp(10), Width - 1, App.Dp(58));
            G.Fill(g, r, App.Dp(10), T.Card); G.Stroke(g, r, App.Dp(10), T.Line, 1);
            string fam = Box.Text;
            var f = PreviewFont(fam, weight, App.Dp(30));
            // la muestra se achica si no cabe en el recuadro
            try
            {
                var sz = g.MeasureString("W A S D  Shift  Ctrl", f);
                if (sz.Width > r.Width - App.Dp(20)) f = PreviewFont(fam, weight, App.Dp(30) * (r.Width - App.Dp(20)) / sz.Width);
            }
            catch { }
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            try
            {
                using (var b = new SolidBrush(T.Text))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString("W A S D  Shift  Ctrl", f, b, r, sf);
            }
            catch { }
        }
    }

    // ---------------- paleta ----------------
    public class Palette : Ctl
    {
        public List<Color[]> Colors = new List<Color[]>(); public List<string> Names = new List<string>(); public Action<int> Picked;
        int focusIdx;
        public Palette(string name) { TabStop = true; AccessibleRole = AccessibleRole.List; AccessibleName = name; }
        int S { get { return App.Hit(46); } }
        Rectangle R(int i) { int gap = App.Dp(8), per = Math.Max(1, (Width + gap) / (S + gap)); return new Rectangle((i % per) * (S + gap), (i / per) * (S + gap), S, S); }
        public override int Measure(int w) { int gap = App.Dp(8), per = Math.Max(1, (w + gap) / (S + gap)); int rows = (Colors.Count + per - 1) / per; return rows * (S + gap); }
        protected override string SpeakText() { return AccessibleName + ": " + (Colors.Count > 0 ? Names[focusIdx] : "") + ". Flechas y Enter."; }
        protected override bool IsInputKey(Keys k) { var kc = k & Keys.KeyCode; return kc == Keys.Left || kc == Keys.Right || kc == Keys.Enter || kc == Keys.Space || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left && focusIdx > 0) { focusIdx--; e.Handled = true; }
            else if (e.KeyCode == Keys.Right && focusIdx < Colors.Count - 1) { focusIdx++; e.Handled = true; }
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { e.Handled = true; if (Picked != null) Picked(focusIdx); return; }
            else { base.OnKeyDown(e); return; }
            Invalidate(); App.Speak(Names[focusIdx]);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            for (int i = 0; i < Colors.Count; i++) if (R(i).Contains(e.Location)) { if (App.Tips != null) App.Tips.SetToolTip(this, Names[i]); Cursor = Cursors.Hand; return; }
            Cursor = Cursors.Default; base.OnMouseMove(e);
        }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            Focus();
            for (int i = 0; i < Colors.Count; i++) if (R(i).Contains(e.Location)) { focusIdx = i; Invalidate(); if (Picked != null) Picked(i); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e);
            for (int i = 0; i < Colors.Count; i++)
            {
                var r = R(i); var rf = new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
                using (var p = G.RR(rf, App.Dp(10)))
                {
                    var st = g.Save(); g.SetClip(p);
                    G.Checker(g, rf, App.Dp(6), Color.FromArgb(90, 90, 90), Color.FromArgb(60, 60, 60));
                    g.Restore(st);
                }
                G.FillV(g, rf, App.Dp(10), Colors[i][0], Colors[i][1]);
                G.Stroke(g, rf, App.Dp(10), App.T.Line, 1.5f);
                if (Focused && i == focusIdx) G.Ring(g, r, App.Dp(10));
            }
        }
    }

    // ---------------- esquina de anclaje ----------------
    public class AnchorGrid : Ctl
    {
        static readonly string[] ids = { "top-left", null, "top-right", null, "center", null, "bottom-left", null, "bottom-right" };
        static readonly string[] names = { "Arriba a la izquierda", "", "Arriba a la derecha", "", "Centro", "", "Abajo a la izquierda", "", "Abajo a la derecha" };
        static readonly string[] arrows = { "↖", "", "↗", "", "●", "", "↙", "", "↘" };
        public string Value; public Action<string> Changed;
        public AnchorGrid(string v) { Value = v; TabStop = true; AccessibleRole = AccessibleRole.List; AccessibleName = "Esquina de referencia"; }
        int C { get { return App.Hit(46); } }
        public override int Measure(int w) { return C * 3 + App.Dp(12); }
        Rectangle R(int i) { int g = App.Dp(6); return new Rectangle((i % 3) * (C + g), (i / 3) * (C + g), C, C); }
        protected override string SpeakText() { int i = Array.IndexOf(ids, Value); return "Esquina de referencia: " + (i >= 0 ? names[i] : "") + ". Flechas para cambiar."; }
        protected override bool IsInputKey(Keys k) { var kc = k & Keys.KeyCode; return kc == Keys.Left || kc == Keys.Right || kc == Keys.Up || kc == Keys.Down || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            int i = Math.Max(0, Array.IndexOf(ids, Value)), r = i / 3, c = i % 3;
            if (e.KeyCode == Keys.Left) c = Math.Max(0, c - 2); else if (e.KeyCode == Keys.Right) c = Math.Min(2, c + 2);
            else if (e.KeyCode == Keys.Up) r = Math.Max(0, r - 2); else if (e.KeyCode == Keys.Down) r = Math.Min(2, r + 2);
            else { base.OnKeyDown(e); return; }
            e.Handled = true;
            int n = r * 3 + c; if (ids[n] == null) n = 4;
            Set(ids[n]);
        }
        void Set(string v) { Value = v; Invalidate(); if (Changed != null) Changed(v); App.Speak(names[Array.IndexOf(ids, v)]); }
        protected override void OnMouseClick(MouseEventArgs e) { Focus(); for (int i = 0; i < 9; i++) if (ids[i] != null && R(i).Contains(e.Location)) Set(ids[i]); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e); var T = App.T;
            for (int i = 0; i < 9; i++)
            {
                if (ids[i] == null) continue;
                var r = R(i); var rf = new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
                bool sel = ids[i] == Value;
                G.Fill(g, rf, App.Dp(10), sel ? T.Card2 : T.Card);
                G.Stroke(g, rf, App.Dp(10), sel ? T.Accent : T.Line, sel ? 2.5f : 1);
                TextRenderer.DrawText(g, arrows[i], App.F(13, true), r, sel ? T.Accent2 : T.Text, G.Line | TextFormatFlags.HorizontalCenter);
                if (Focused && sel) G.Ring(g, r, App.Dp(10));
            }
            TextRenderer.DrawText(g, names[Math.Max(0, Array.IndexOf(ids, Value))], App.F(9.5f, true), new Rectangle(C * 3 + App.Dp(30), 0, Width - C * 3 - App.Dp(30), C * 3), T.Muted, G.Wrap | TextFormatFlags.VerticalCenter);
        }
    }

    // ---------------- miniatura de imagen ----------------
    public class ImageBox : Ctl
    {
        string data;
        public ImageBox(string dataUrl) { data = dataUrl; TabStop = false; SetStyle(ControlStyles.Selectable, false); }
        public override int Measure(int w) { return App.Dp(96); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e); var T = App.T;
            var r = new RectangleF(1, 1, App.Dp(94), App.Dp(94));
            var img = Images.Get(data);
            using (var p = G.RR(r, App.Dp(12)))
            {
                var st = g.Save(); g.SetClip(p);
                G.Checker(g, r, App.Dp(8), Color.FromArgb(70, 70, 70), Color.FromArgb(50, 50, 50));
                if (img != null)
                {
                    float sc = Math.Min(r.Width / img.Width, r.Height / img.Height);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(img, r.X + (r.Width - img.Width * sc) / 2, r.Y + (r.Height - img.Height * sc) / 2, img.Width * sc, img.Height * sc);
                }
                g.Restore(st);
            }
            using (var pen = new Pen(T.Line, 1.5f) { DashStyle = img == null ? DashStyle.Dash : DashStyle.Solid }) using (var p = G.RR(r, App.Dp(12))) g.DrawPath(pen, p);
            string t = img == null ? "Sin imagen.\nPNG, JPG, GIF o WEBP. Se guarda dentro del estilo: no hace falta conservar el archivo." :
                "La letra queda siempre por encima de la imagen. Ajusta abajo su opacidad.";
            TextRenderer.DrawText(g, t, App.F(8.75f), new Rectangle(App.Dp(110), 0, Width - App.Dp(110), Height), T.Muted, G.Wrap | TextFormatFlags.VerticalCenter);
        }
    }

    // ---------------- selector segmentado (barra de la vista previa) ----------------
    public class Seg : Ctl
    {
        public string[] Labels, Values; public int Selected; public Action<string> Changed; string name;
        public Seg(string name, string[] labels, string[] values, string value)
        {
            this.name = name; Labels = labels; Values = values; Selected = Math.Max(0, Array.IndexOf(values, value));
            TabStop = true; AccessibleRole = AccessibleRole.PageTabList; AccessibleName = name; BackColor = App.T.Panel;
        }
        int Pad { get { return App.Dp(11); } }
        int W(int i) { return TextRenderer.MeasureText(Labels[i], App.F(9, true)).Width + Pad * 2; }
        public override int PrefWidth() { int w = App.Dp(6); for (int i = 0; i < Labels.Length; i++) w += W(i); return w + TextRenderer.MeasureText(name, App.F(8, true)).Width + App.Dp(10); }
        int LabelW { get { return TextRenderer.MeasureText(name, App.F(8, true)).Width + App.Dp(10); } }
        Rectangle R(int i) { int x = LabelW + App.Dp(3); for (int j = 0; j < i; j++) x += W(j); return new Rectangle(x, App.Dp(3), W(i), Height - App.Dp(6)); }
        protected override string SpeakText() { return name + ": " + Labels[Selected] + ". Flechas para cambiar."; }
        protected override bool IsInputKey(Keys k) { var kc = k & Keys.KeyCode; return kc == Keys.Left || kc == Keys.Right || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left && Selected > 0) Pick(Selected - 1);
            else if (e.KeyCode == Keys.Right && Selected < Labels.Length - 1) Pick(Selected + 1);
            else { base.OnKeyDown(e); return; }
            e.Handled = true;
        }
        public void Pick(int i) { Selected = i; Invalidate(); if (Changed != null) Changed(Values[i]); App.Speak(Labels[i]); }
        protected override void OnMouseMove(MouseEventArgs e) { Invalidate(); base.OnMouseMove(e); }
        protected override void OnMouseClick(MouseEventArgs e) { for (int i = 0; i < Labels.Length; i++) if (R(i).Contains(e.Location)) Pick(i); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e); var T = App.T;
            TextRenderer.DrawText(g, name.ToUpper(), App.F(7.5f, true), new Rectangle(0, 0, LabelW, Height), T.Muted, G.Line | TextFormatFlags.Left);
            var all = new RectangleF(LabelW, 0.5f, PrefWidth() - LabelW - 1, Height - 1);
            G.Fill(g, all, App.Dp(10), T.Card); G.Stroke(g, all, App.Dp(10), T.Line, 1);
            var mp = PointToClient(MousePosition);
            for (int i = 0; i < Labels.Length; i++)
            {
                var r = R(i); bool sel = i == Selected;
                if (sel) { G.Fill(g, r, App.Dp(8), T.Card2); using (var p = new Pen(T.Accent, App.Dp(2.5f))) g.DrawLine(p, r.X + App.Dp(8), r.Bottom - App.Dp(1.5f), r.Right - App.Dp(8), r.Bottom - App.Dp(1.5f)); }
                else if (hover && r.Contains(mp)) G.Fill(g, r, App.Dp(8), G.Mix(T.Card, T.Card2, .5f));
                TextRenderer.DrawText(g, Labels[i], App.F(9, true), r, sel ? T.Text : T.Muted, G.Line | TextFormatFlags.HorizontalCenter);
            }
            if (Focused) G.Ring(g, Rectangle.Round(all), App.Dp(10));
        }
    }

    // ---------------- boton del menu lateral ----------------
    public class NavItem : Ctl
    {
        public string Id, Glyph, Title, Sub; public bool Selected; public Action<string> Picked;
        public NavItem(string id, string glyph, string title, string sub)
        {
            Id = id; Glyph = glyph; Title = title; Sub = sub; TabStop = true; Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PageTab; AccessibleName = title; AccessibleDescription = sub; BackColor = App.T.Side;
        }
        public override int Measure(int w) { return App.Hit(52); }
        protected override bool IsInputKey(Keys k) { var kc = k & Keys.KeyCode; return kc == Keys.Up || kc == Keys.Down || kc == Keys.Enter || kc == Keys.Space || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
            {
                e.Handled = true;
                var p = Parent; var items = new List<NavItem>();
                foreach (Control c in p.Controls) if (c is NavItem) items.Add((NavItem)c);
                items.Sort((a, b) => a.Top.CompareTo(b.Top));
                int i = items.IndexOf(this) + (e.KeyCode == Keys.Down ? 1 : -1);
                if (i >= 0 && i < items.Count) { items[i].Focus(); if (items[i].Picked != null) items[i].Picked(items[i].Id); }
                return;
            }
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { e.Handled = true; if (Picked != null) Picked(Id); return; }
            base.OnKeyDown(e);
        }
        protected override void OnMouseClick(MouseEventArgs e) { if (Picked != null) Picked(Id); base.OnMouseClick(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e); var T = App.T;
            var r = new RectangleF(App.Dp(8), 1, Width - App.Dp(16), Height - 2);
            if (Selected) { G.Fill(g, r, App.Dp(10), T.Card2); G.Fill(g, new RectangleF(r.X, r.Y + App.Dp(10), App.Dp(4), r.Height - App.Dp(20)), App.Dp(2), T.Accent); }
            else if (hover) G.Fill(g, r, App.Dp(10), T.Card);
            int gx = (int)r.X + App.Dp(14);
            Color gc = Selected ? T.Accent2 : T.Muted;
            if (App.HasGlyphs) TextRenderer.DrawText(g, Glyph, App.Glyph(13), new Rectangle(gx, 0, App.Dp(26), Height), gc, G.Line | TextFormatFlags.HorizontalCenter);
            else { using (var b = new SolidBrush(gc)) g.FillEllipse(b, gx + App.Dp(8), Height / 2 - App.Dp(5), App.Dp(10), App.Dp(10)); }
            int tx = gx + App.Dp(38);
            bool sub = !string.IsNullOrEmpty(Sub) && !App.P.hideHelp;
            TextRenderer.DrawText(g, Title, App.F(10, true), new Rectangle(tx, sub ? App.Dp(7) : 0, Width - tx - App.Dp(10), sub ? App.Dp(22) : Height), Selected ? T.Text : G.Mix(T.Text, T.Muted, .25f), G.Line | TextFormatFlags.Left);
            if (sub) TextRenderer.DrawText(g, Sub, App.F(8), new Rectangle(tx, App.Dp(27), Width - tx - App.Dp(10), App.Dp(18)), T.Muted, G.Line | TextFormatFlags.Left);
            if (Focused) G.Ring(g, Rectangle.Round(r), App.Dp(10));
        }
    }

    // ================================================================
    // VISTA PREVIA
    // ================================================================
    public class Preview : Ctl
    {
        Config cfg;
        public Config Cfg { get { return cfg; } set { cfg = value; version++; } }   // asignarlo (en cada cambio) invalida el dibujo guardado
        public string SelId; public bool EditView; public string Bg = "dark"; public string ZoomMode = "fit";
        public Dictionary<string, KeyAnim> Anim = new Dictionary<string, KeyAnim>();
        public Action<string> KeySelected; public Action<bool> KeysMoved; public Action DeleteRequested;
        public Action<string, string> KeyCommand; // (orden, id)
        // modo "Transformar" de la imagen comun
        // seleccion multiple (recuadro, Mayus+clic, Ctrl+A)
        public HashSet<string> Multi = new HashSet<string>(); public Action MultiChanged;
        string pendingToggle; bool marquee; Point mqStart, mqEnd; Dictionary<string, Rectangle> origs, startAll; RectangleF groupStart; HashSet<string> snapSkip; double[] startFrame, startMinv; int startPX, startPY; string lastSig;
        public bool ImgEdit; public string ImgEditKey; public Action<bool> ImageChanged; public Action ImageEditDone;
        string imgDrag; int hu, hv; ImgFrame f0; PointF il0; double rotShown = double.NaN;
        float zoom = 1, zoomMul = 1; Point pan; int cx, cy; double[] M, Minv;
        string toast; bool toastErr; long toastUntil;
        string dragId; PointF dragStart; int dragEdges; bool dragMoved, panning, panMoved; Point panStart, panOrig;
        string hoverId; List<float> guideX = new List<float>(), guideY = new List<float>();
        const int GAP = 6;   // separacion estandar entre teclas
        const int L = 1, R = 2, T_ = 4, B_ = 8;

        public Preview()
        {
            TabStop = true; AccessibleRole = AccessibleRole.Graphic; AccessibleName = "Vista previa del teclado";
            AccessibleDescription = "Clic en una tecla para editarla, arrástrala para moverla o tira de sus bordes para cambiar su tamaño. Control al arrastrar activa el imán, Mayúsculas mueve en línea recta o mantiene la forma al estirar, Alt estira desde el centro. Flechas mueven la tecla elegida, Control más flechas cambian su tamaño, Suprimir la borra, Escape deja de elegirla.";
            BackColor = App.T.Bg;
        }
        public void Toast(string msg, bool err) { toast = msg; toastErr = err; toastUntil = KeyPainter.Now + 3400; Invalidate(); StartAnim(); }
        // devuelve true si el estado de la tecla cambio
        public bool SetPressed(string id, bool on)
        {
            KeyAnim a;
            if (!Anim.TryGetValue(id, out a)) { a = new KeyAnim(); Anim[id] = a; }
            long now = KeyPainter.Now;
            if (on && !a.Active) { a.Active = true; a.PressAt = now; }
            else if (!on && a.Active) { a.Active = false; a.ReleaseAt = now; }
            else return false;
            StartAnim();
            return true;
        }
        // fuerza a volver a dibujar las teclas (p.ej. al terminar de descargar una fuente)
        public void Touch() { version++; Invalidate(); }

        // ---- animaciones: el temporizador solo corre mientras algo se mueve ----
        Timer animTimer;
        void StartAnim()
        {
            if (animTimer == null) { animTimer = new Timer { Interval = 16 }; animTimer.Tick += (s, e) => AnimTick(); }
            if (!animTimer.Enabled) animTimer.Start();
        }
        void AnimTick()
        {
            if (!Busy()) { animTimer.Stop(); Invalidate(); return; }
            var f = FindForm();
            if (f != null && (!f.Visible || f.WindowState == FormWindowState.Minimized)) return;   // ventana oculta: no se pinta
            Invalidate();
        }
        protected override void Dispose(bool disposing) { if (disposing) { if (animTimer != null) animTimer.Dispose(); if (keysBmp != null) keysBmp.Dispose(); } base.Dispose(disposing); }
        bool KeysAnimating()
        {
            long now = KeyPainter.Now;
            int spd = Cfg == null ? 100 : Math.Max(10, Cfg.effects.speed);
            foreach (var a in Anim.Values)
                if ((a.Active && now - a.PressAt < 900 * 100 / spd) || (!a.Active && now - a.ReleaseAt < 300 * 100 / spd)) return true;
            return false;
        }
        public bool Busy()
        {
            if (toast != null && KeyPainter.Now < toastUntil + 300) return true;
            return KeysAnimating();
        }
        public bool MouseBusy { get { return dragId != null || marquee || imgDrag != null || panning; } }
        public void ResetPan() { pan = Point.Empty; zoomMul = 1; Invalidate(); }
        // rueda del raton: acerca / aleja alrededor del puntero
        public void Wheel(int delta, Point mp)
        {
            if (Cfg == null) return;
            float old = zoom;
            float nm = Math.Max(1f, Math.Min(8f, zoomMul * (float)Math.Pow(1.18, delta / 120.0)));
            if (Math.Abs(nm - zoomMul) < 0.0001f) return;
            float fit = old / zoomMul;
            float nz = fit * nm;
            float px = (mp.X - cx) / old, py = (mp.Y - cy) / old;
            zoomMul = nm;
            if (nm <= 1.0001f) { pan = Point.Empty; Invalidate(); return; }
            int W = Cfg.canvas.width, H = Cfg.canvas.height;
            int top = App.Dp(44), padB = App.Dp(28);
            float availH = Height - top - padB;
            int ncw = (int)Math.Round(W * nz), nch = (int)Math.Round(H * nz);
            float baseX = (Width - ncw) / 2f, baseY = top + Math.Max(0, (availH - nch) / 2f);
            pan = new Point((int)Math.Round(mp.X - px * nz - baseX), (int)Math.Round(mp.Y - py * nz - baseY));
            Invalidate();
        }

        protected override bool IsInputKey(Keys k) { var kc = k & Keys.KeyCode; return kc == Keys.Left || kc == Keys.Right || kc == Keys.Up || kc == Keys.Down || kc == Keys.Delete || kc == Keys.Escape || kc == Keys.Enter || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (ImgEdit && (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter)) { e.Handled = true; if (ImageEditDone != null) ImageEditDone(); return; }
            if (e.KeyCode == Keys.Escape) { e.Handled = true; Select(null); return; }
            var sel = SelKeys();
            var k = sel.Count > 0 ? sel[0] : null;
            if (k != null)
            {
                int d = e.Shift ? 10 : 1, dx = 0, dy = 0;
                if (e.KeyCode == Keys.Left) dx = -d; else if (e.KeyCode == Keys.Right) dx = d;
                else if (e.KeyCode == Keys.Up) dy = -d; else if (e.KeyCode == Keys.Down) dy = d;
                if (dx != 0 || dy != 0)
                {
                    if (e.Control) { var f1 = Cfg.Frame(); foreach (var q in sel) { q.w = Math.Max(20, q.w + dx); q.h = Math.Max(20, q.h + dy); } Cfg.Reanchor(f1); App.Speak(k.w + " por " + k.h); }
                    else { var f0 = Cfg.Frame(); foreach (var q in sel) { q.x += dx; q.y += dy; } Cfg.Reanchor(f0); App.Speak("X " + k.x + ", Y " + k.y); }
                    e.Handled = true;
                    if (KeysMoved != null) KeysMoved(true);
                    return;
                }
                if (e.KeyCode == Keys.Delete) { e.Handled = true; if (DeleteRequested != null) DeleteRequested(); return; }
            }
            base.OnKeyDown(e);
        }

        void Select(string id)
        {
            Multi.Clear();
            SelId = id;
            if (KeySelected != null) KeySelected(id);
            Invalidate();
        }
        PointF ToLocal(Point p) { if (Minv == null) return PointF.Empty; return Mx.Ap(Minv, p.X - cx, p.Y - cy); }
        PointF ToScreen(double x, double y) { var p = Mx.Ap(M, x, y); return new PointF(p.X + cx, p.Y + cy); }
        float Px { get { return 1f / Math.Max(0.05f, zoom * (float)Cfg.position.scale); } } // 1 px de pantalla en unidades de tecla
        CKey HitKey(Point p)
        {
            var l = ToLocal(p);
            for (int i = Cfg.keys.Count - 1; i >= 0; i--)
            {
                var k = Cfg.keys[i];
                if (l.X >= k.x && l.X <= k.x + k.w && l.Y >= k.y && l.Y <= k.y + k.h) return k;
            }
            return null;
        }
        // bordes de la tecla elegida bajo el raton (para estirarla como una ventana)
        public List<CKey> SelKeys()
        {
            var l = new List<CKey>();
            if (Cfg == null) return l;
            if (Multi.Count > 0) { foreach (var k in Cfg.keys) if (Multi.Contains(k.id)) l.Add(k); }
            else { var k = Cfg.Find(SelId); if (k != null) l.Add(k); }
            return l;
        }
        static RectangleF Box(IEnumerable<CKey> ks)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var k in ks) { x0 = Math.Min(x0, k.x); y0 = Math.Min(y0, k.y); x1 = Math.Max(x1, k.x + k.w); y1 = Math.Max(y1, k.y + k.h); }
            return x0 == float.MaxValue ? RectangleF.Empty : RectangleF.FromLTRB(x0, y0, x1, y1);
        }
        public void SelectAll()
        {
            Multi.Clear();
            if (Cfg.keys.Count == 1) { Select(Cfg.keys[0].id); return; }
            foreach (var k in Cfg.keys) Multi.Add(k.id);
            SelId = null; if (MultiChanged != null) MultiChanged(); Invalidate();
        }
        int EdgesAt(Point p)
        {
            var ks = SelKeys(); if (ks.Count == 0 || M == null) return 0;
            var b = Box(ks);
            var l = ToLocal(p); float t = 7 * Px;
            if (l.X < b.X - t || l.X > b.Right + t || l.Y < b.Y - t || l.Y > b.Bottom + t) return 0;
            int e = 0;
            if (Math.Abs(l.X - b.X) <= t) e |= L; else if (Math.Abs(l.X - b.Right) <= t) e |= R;
            if (Math.Abs(l.Y - b.Y) <= t) e |= T_; else if (Math.Abs(l.Y - b.Bottom) <= t) e |= B_;
            return e;
        }
        void UpdateMatrix()
        {
            if (Cfg == null) return;
            int W = Cfg.canvas.width, H = Cfg.canvas.height;
            int pad = App.Dp(28), top = App.Dp(44);
            float availW = Width - 2 * pad, availH = Height - top - pad;
            zoom = Math.Max(0.2f, Math.Min(3f, Math.Min(availW / W, availH / H))) * zoomMul;
            int cw = Math.Max(1, (int)Math.Round(W * zoom)), ch = Math.Max(1, (int)Math.Round(H * zoom));
            cx = (int)((Width - cw) / 2) + pan.X;
            cy = (int)(top + Math.Max(0, (availH - ch) / 2)) + pan.Y;
            M = Mx.Mul(Mx.S(zoom), Mx.LocalToCanvas(Cfg, EditView));
            Minv = Mx.Inv(M);
        }
        static Cursor CursorFor(int e)
        {
            if (e == (L | T_) || e == (R | B_)) return Cursors.SizeNWSE;
            if (e == (R | T_) || e == (L | B_)) return Cursors.SizeNESW;
            if ((e & (L | R)) != 0) return Cursors.SizeWE;
            if ((e & (T_ | B_)) != 0) return Cursors.SizeNS;
            return Cursors.Default;
        }

        // ---------- IMAN (Mayus): pega bordes, centros y separacion de 6 px a las teclas cercanas ----------
        float SnapDist { get { return Math.Max(3f, 9 * Px); } }
        int SnapPos(int v, int size, bool horiz, CKey self)
        {
            float best = SnapDist + 1; int res = v; float guide = float.NaN;
            foreach (var o in Cfg.keys)
            {
                if (o == self || (snapSkip != null && snapSkip.Contains(o.id))) continue;
                int op = horiz ? o.x : o.y, os = horiz ? o.w : o.h;
                // (posicion candidata, linea guia)
                int[][] c = {
                    new[]{ op, op },                         // mismo borde inicial
                    new[]{ op + os - size, op + os },        // mismo borde final
                    new[]{ op + os / 2 - size / 2, op + os / 2 }, // centrados
                    new[]{ op + os + GAP, op + os + GAP },   // pegada despues, con 6 px
                    new[]{ op - GAP - size, op - GAP },      // pegada antes, con 6 px
                    new[]{ op + os, op + os },               // tocandose
                    new[]{ op - size, op } };
                foreach (var cc in c)
                {
                    float d = Math.Abs(cc[0] - v);
                    if (d < best) { best = d; res = cc[0]; guide = cc[1]; }
                }
            }
            if (!float.IsNaN(guide)) (horiz ? guideX : guideY).Add(guide);
            return res;
        }
        int SnapEdge(int v, bool horiz, CKey self)
        {
            float best = SnapDist + 1; int res = v;
            foreach (var o in Cfg.keys)
            {
                if (o == self || (snapSkip != null && snapSkip.Contains(o.id))) continue;
                int op = horiz ? o.x : o.y, os = horiz ? o.w : o.h;
                foreach (int c in new[] { op, op + os, op - GAP, op + os + GAP, op + os / 2 })
                {
                    float d = Math.Abs(c - v);
                    if (d < best) { best = d; res = c; }
                }
            }
            if (res != v || best <= SnapDist) (horiz ? guideX : guideY).Add(res);
            return res;
        }
        int SnapSize(int size, bool horiz, CKey self, out bool hit)
        {
            hit = false; float best = SnapDist + 1; int res = size;
            foreach (var o in Cfg.keys)
            {
                if (o == self || (snapSkip != null && snapSkip.Contains(o.id))) continue;
                int os = horiz ? o.w : o.h;
                float d = Math.Abs(os - size);
                if (d < best) { best = d; res = os; hit = true; }
            }
            if (best > SnapDist) hit = false;
            return hit ? res : size;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle) { panning = true; panMoved = false; panStart = e.Location; panOrig = pan; Capture = true; return; }
            if (e.Button != MouseButtons.Left) return;
            // al empezar a editar con el raton, Mayus/Ctrl/Alt pulsados no deben verse como teclas pulsadas
            foreach (var an in Anim.Values) { an.Active = false; an.PressAt = an.ReleaseAt = -100000; }
            if (ImgEdit) { ImgDown(e.Location); return; }
            int edges = EdgesAt(e.Location);
            var hit = HitKey(e.Location);
            bool shift = (ModifierKeys & Keys.Shift) != 0;
            // en la nada: recuadro de seleccion (si apenas se mueve, deselecciona)
            if (edges == 0 && hit == null) { marquee = true; mqStart = mqEnd = e.Location; Capture = true; return; }
            if (edges == 0 && (ModifierKeys & Keys.Alt) != 0 && CycleUnder(e.Location)) return;
            // Mayus + clic (sin arrastrar): anade o quita de la seleccion. Se decide al soltar.
            pendingToggle = null;
            bool inSel = hit != null && SelKeys().Any(q => q.id == hit.id);
            if (edges == 0 && shift) pendingToggle = hit.id;
            else if (edges == 0)
            {
                if (Multi.Count > 0 && !Multi.Contains(hit.id)) Select(hit.id);
                else if (Multi.Count == 0 && hit.id != SelId) Select(hit.id);
            }
            var ks = (edges == 0 && shift && !inSel) ? new List<CKey> { hit } : SelKeys();
            if (ks.Count == 0) return;
            origs = new Dictionary<string, Rectangle>();
            foreach (var kk in ks) origs[kk.id] = new Rectangle(kk.x, kk.y, kk.w, kk.h);
            groupStart = Box(ks);
            // foto del estado al empezar: cada movimiento se calcula desde aqui (asi nada "se pega")
            startAll = new Dictionary<string, Rectangle>();
            foreach (var kk in Cfg.keys) startAll[kk.id] = new Rectangle(kk.x, kk.y, kk.w, kk.h);
            startFrame = Cfg.Frame(); startMinv = Minv; startPX = Cfg.position.x; startPY = Cfg.position.y; lastSig = null;
            dragId = (hit != null && origs.ContainsKey(hit.id) ? hit : ks[0]).id;
            dragEdges = edges; dragStart = ToLocal(e.Location); dragMoved = false; Capture = true;
            Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (panning)
            {
                if (Math.Abs(e.X - panStart.X) + Math.Abs(e.Y - panStart.Y) > 4) { panMoved = true; Cursor = Cursors.SizeAll; }
                if (panMoved) { pan = new Point(panOrig.X + e.X - panStart.X, panOrig.Y + e.Y - panStart.Y); Invalidate(); }
                return;
            }
            if (ImgEdit) { ImgMove(e.Location); return; }
            if (marquee) { mqEnd = e.Location; Invalidate(); return; }
            if (dragId != null && origs != null)
            {
                var l = Mx.Ap(startMinv, e.X - cx, e.Y - cy);   // en el marco del inicio del arrastre
                // vuelve todo al estado inicial y aplica el movimiento completo
                foreach (var kk in Cfg.keys) { Rectangle o0; if (startAll.TryGetValue(kk.id, out o0)) { kk.x = o0.X; kk.y = o0.Y; kk.w = o0.Width; kk.h = o0.Height; } }
                Cfg.position.x = startPX; Cfg.position.y = startPY;
                int dx = (int)Math.Round(l.X - dragStart.X), dy = (int)Math.Round(l.Y - dragStart.Y);
                // Ctrl = iman (mover y estirar) · Mayus = mover en linea recta / mantener la forma al estirar · Alt = desde el centro
                bool magnet = (ModifierKeys & Keys.Control) != 0;
                if (dragEdges == 0 && (ModifierKeys & Keys.Shift) != 0) { if (Math.Abs(dx) >= Math.Abs(dy)) dy = 0; else dx = 0; }
                guideX.Clear(); guideY.Clear();
                snapSkip = new HashSet<string>(origs.Keys);
                var gs = groupStart;
                int gx = (int)gs.X, gy = (int)gs.Y, gw = (int)gs.Width, gh = (int)gs.Height;
                CKey single = origs.Count == 1 ? Cfg.Find(origs.Keys.First()) : null;
                int left, top, right, bottom;
                if (dragEdges == 0)
                {
                    int nx = gx + dx, ny = gy + dy;
                    if (magnet) { nx = SnapPos(nx, gw, true, single); ny = SnapPos(ny, gh, false, single); }
                    left = nx; top = ny; right = nx + gw; bottom = ny + gh;
                }
                else
                {
                    left = gx; top = gy; right = gx + gw; bottom = gy + gh;
                    int mn = 20;
                    // al estirar: Mayus = mantener la forma (proporcion), Alt = desde el centro, Ctrl = iman
                    bool keepRatio = (ModifierKeys & Keys.Shift) != 0, fromCenter = (ModifierKeys & Keys.Alt) != 0;
                    if ((dragEdges & L) != 0) { left = Math.Min(right - mn, gx + dx); if (magnet) left = Math.Min(right - mn, SnapEdge(left, true, single)); }
                    if ((dragEdges & R) != 0) { right = Math.Max(left + mn, gx + gw + dx); if (magnet) right = Math.Max(left + mn, SnapEdge(right, true, single)); }
                    if ((dragEdges & T_) != 0) { top = Math.Min(bottom - mn, gy + dy); if (magnet) top = Math.Min(bottom - mn, SnapEdge(top, false, single)); }
                    if ((dragEdges & B_) != 0) { bottom = Math.Max(top + mn, gy + gh + dy); if (magnet) bottom = Math.Max(top + mn, SnapEdge(bottom, false, single)); }
                    // iman de tamano (una sola tecla): mismo ancho/alto que otra
                    if (magnet && single != null)
                    {
                        bool hit2; int lf = left, rt = right, tp = top, bt = bottom;
                        if ((dragEdges & (L | R)) != 0 && !guideX.Any(q => q == lf || q == rt)) { int sw2 = SnapSize(right - left, true, single, out hit2); if (hit2) { if ((dragEdges & L) != 0) left = right - sw2; else right = left + sw2; } }
                        if ((dragEdges & (T_ | B_)) != 0 && !guideY.Any(q => q == tp || q == bt)) { int sh2 = SnapSize(bottom - top, false, single, out hit2); if (hit2) { if ((dragEdges & T_) != 0) top = bottom - sh2; else bottom = top + sh2; } }
                    }
                    if (fromCenter)
                    {
                        // el lado contrario se mueve lo mismo: crece/encoge desde el centro
                        if ((dragEdges & L) != 0) right = gx + gw + (gx - left); else if ((dragEdges & R) != 0) left = gx - (right - gx - gw);
                        if ((dragEdges & T_) != 0) bottom = gy + gh + (gy - top); else if ((dragEdges & B_) != 0) top = gy - (bottom - gy - gh);
                    }
                    if (keepRatio && gw > 0 && gh > 0)
                    {
                        bool hx = (dragEdges & (L | R)) != 0, hy = (dragEdges & (T_ | B_)) != 0;
                        double s2 = hx && hy ? Math.Max((right - left) / (double)gw, (bottom - top) / (double)gh)
                                  : hx ? (right - left) / (double)gw : (bottom - top) / (double)gh;
                        s2 = Math.Max(s2, Math.Max(mn / (double)gw, mn / (double)gh));
                        int nw = (int)Math.Round(gw * s2), nh = (int)Math.Round(gh * s2);
                        // ancla: la esquina/lado contrario (o el centro con Alt)
                        double ax = fromCenter || !hx ? gx + gw / 2.0 : ((dragEdges & L) != 0 ? gx + gw : gx);
                        double ay = fromCenter || !hy ? gy + gh / 2.0 : ((dragEdges & T_) != 0 ? gy + gh : gy);
                        double fx = fromCenter || !hx ? 0.5 : ((dragEdges & L) != 0 ? 1 : 0);
                        double fy = fromCenter || !hy ? 0.5 : ((dragEdges & T_) != 0 ? 1 : 0);
                        left = (int)Math.Round(ax - nw * fx); right = left + nw;
                        top = (int)Math.Round(ay - nh * fy); bottom = top + nh;
                    }
                }
                snapSkip = null;
                double sx = gw > 0 ? (right - left) / (double)gw : 1, sy = gh > 0 ? (bottom - top) / (double)gh : 1;
                foreach (var kv in origs)
                {
                    var k = Cfg.Find(kv.Key); if (k == null) continue; var o = kv.Value;
                    k.x = (int)Math.Round(left + (o.X - gx) * sx); k.y = (int)Math.Round(top + (o.Y - gy) * sy);
                    k.w = Math.Max(10, (int)Math.Round(o.Width * sx)); k.h = Math.Max(10, (int)Math.Round(o.Height * sy));
                }
                var shf = Cfg.Reanchor(startFrame);
                for (int i = 0; i < guideX.Count; i++) guideX[i] += shf.X;
                for (int i = 0; i < guideY.Count; i++) guideY[i] += shf.Y;
                UpdateMatrix();
                var sb = new System.Text.StringBuilder();
                sb.Append(Cfg.position.x).Append(',').Append(Cfg.position.y);
                foreach (var kk in Cfg.keys) sb.Append('|').Append(kk.x).Append(',').Append(kk.y).Append(',').Append(kk.w).Append(',').Append(kk.h);
                string sig = sb.ToString();
                if (sig != lastSig) { lastSig = sig; dragMoved = dx != 0 || dy != 0 || dragMoved; if (KeysMoved != null) KeysMoved(false); }
                else Invalidate();
                Cursor = dragEdges == 0 ? Cursors.SizeAll : CursorFor(dragEdges);
                return;
            }
            int ed = EdgesAt(e.Location);
            var hk = HitKey(e.Location);
            string nh2 = hk == null ? null : hk.id;
            if (nh2 != hoverId) { hoverId = nh2; Invalidate(); }
            Cursor = ed != 0 ? CursorFor(ed) : hk != null ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { if (hoverId != null) { hoverId = null; Invalidate(); } base.OnMouseLeave(e); }
        // todas las teclas bajo el puntero, de la de arriba a la de abajo
        List<CKey> HitKeys(Point p)
        {
            var l = ToLocal(p); var res = new List<CKey>();
            for (int i = Cfg.keys.Count - 1; i >= 0; i--)
            {
                var k = Cfg.keys[i];
                if (l.X >= k.x && l.X <= k.x + k.w && l.Y >= k.y && l.Y <= k.y + k.h) res.Add(k);
            }
            return res;
        }
        // doble clic (o Alt + clic) sobre teclas encimadas: elige la siguiente de debajo, en ciclo
        bool CycleUnder(Point p)
        {
            var under = HitKeys(p);
            if (under.Count < 2) return false;
            int i = under.FindIndex(k => k.id == SelId);
            var next = under[(i + 1) % under.Count];
            Select(next.id);
            Toast("Elegida «" + (string.IsNullOrEmpty(next.label) ? (next.bar ? "Espacio" : "tecla") : next.label) + "» (" + (under.IndexOf(next) + 1) + " de " + under.Count + " en este punto)", false);
            return true;
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !ImgEdit)
            {
                if (HitKey(e.Location) == null) ResetPan();
                else CycleUnder(e.Location);
            }
            base.OnMouseDoubleClick(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (panning)
            {
                panning = false; Capture = false; Cursor = Cursors.Default;
                if (!panMoved && e.Button == MouseButtons.Right) ShowMenu(e.Location);
                return;
            }
            if (imgDrag != null) { imgDrag = null; Capture = false; rotShown = double.NaN; Invalidate(); if (ImageChanged != null) ImageChanged(true); return; }
            if (marquee)
            {
                marquee = false; Capture = false;
                var rc = Rectangle.FromLTRB(Math.Min(mqStart.X, mqEnd.X), Math.Min(mqStart.Y, mqEnd.Y), Math.Max(mqStart.X, mqEnd.X), Math.Max(mqStart.Y, mqEnd.Y));
                bool add = (ModifierKeys & Keys.Shift) != 0;
                if (rc.Width < 4 && rc.Height < 4) { if (!add) Select(null); Invalidate(); return; }
                var found = new HashSet<string>();
                if (add) { foreach (var k in SelKeys()) found.Add(k.id); }
                foreach (var k in Cfg.keys)
                {
                    var pts = new[] { ToScreen(k.x, k.y), ToScreen(k.x + k.w, k.y), ToScreen(k.x + k.w, k.y + k.h), ToScreen(k.x, k.y + k.h) };
                    var kb = RectangleF.FromLTRB(pts.Min(q => q.X), pts.Min(q => q.Y), pts.Max(q => q.X), pts.Max(q => q.Y));
                    if (kb.IntersectsWith(rc)) found.Add(k.id);
                }
                if (found.Count == 0) Select(null);
                else if (found.Count == 1) Select(found.First());
                else { Multi = found; SelId = null; if (MultiChanged != null) MultiChanged(); }
                Invalidate(); return;
            }
            if (dragId != null && pendingToggle != null && !dragMoved)
            {
                // Mayus + clic sin mover: anadir o quitar de la seleccion
                string id = pendingToggle; pendingToggle = null;
                dragId = null; origs = null; startAll = null; Capture = false;
                if (Multi.Count == 0 && SelId != null) Multi.Add(SelId);
                if (Multi.Contains(id)) Multi.Remove(id); else Multi.Add(id);
                if (Multi.Count == 1) { var only = Multi.First(); Select(only); }
                else if (Multi.Count == 0) Select(null);
                else { SelId = null; if (MultiChanged != null) MultiChanged(); }
                Invalidate(); return;
            }
            if (dragId != null && pendingToggle != null && dragMoved && !SelKeys().Any(q => q.id == pendingToggle))
            {
                string id = pendingToggle; pendingToggle = null;
                dragId = null; origs = null; startAll = null; Capture = false; guideX.Clear(); guideY.Clear();
                Select(id);
                if (KeysMoved != null) KeysMoved(true);
                return;
            }
            pendingToggle = null;
            if (dragId != null)
            {
                dragId = null; origs = null; startAll = null; Capture = false; guideX.Clear(); guideY.Clear(); Invalidate();
                if (dragMoved && KeysMoved != null) KeysMoved(true);
            }
            base.OnMouseUp(e);
        }
        void ShowMenu(Point p)
        {
            var k = HitKey(p); if (k == null || KeyCommand == null) return;
            if (k.id != SelId) Select(k.id);
            var m = new ContextMenuStrip { Font = App.F(10), ShowImageMargin = false, BackColor = App.T.Card, ForeColor = App.T.Text, Renderer = new ToolStripProfessionalRenderer(new DarkColors()) };
            Action<string, string> add = (txt, cmd) => { var it = new ToolStripMenuItem(txt) { ForeColor = App.T.Text }; it.Click += (s, e) => KeyCommand(cmd, k.id); m.Items.Add(it); };
            add("Duplicar      Ctrl+D", "dup");
            add("Copiar           Ctrl+C", "copy");
            m.Items.Add(new ToolStripSeparator());
            add("Mismo tamaño que la tecla de al lado", "same");
            add("Tamaño normal (80 × 94)", "normal");
            m.Items.Add(new ToolStripSeparator());
            add("Traer al frente", "front");
            add("Enviar atrás", "back");
            m.Items.Add(new ToolStripSeparator());
            add("Eliminar          Supr", "del");
            m.Show(this, p);
        }
        class DarkColors : ProfessionalColorTable
        {
            public override Color MenuItemSelected { get { return App.T.Card2; } }
            public override Color MenuItemBorder { get { return App.T.Accent; } }
            public override Color MenuBorder { get { return App.T.Line; } }
            public override Color ToolStripDropDownBackground { get { return App.T.Card; } }
            public override Color ImageMarginGradientBegin { get { return App.T.Card; } }
            public override Color ImageMarginGradientMiddle { get { return App.T.Card; } }
            public override Color ImageMarginGradientEnd { get { return App.T.Card; } }
            public override Color SeparatorDark { get { return App.T.Line; } }
            public override Color SeparatorLight { get { return App.T.Card; } }
        }

        // asas de la tecla elegida + guias del iman + medida en vivo (en pantalla, ya con la inclinacion)
        // ---------- TRANSFORMAR IMAGEN: mover, escalar por las asas y girar con el asa de arriba ----------
        // imagen que se esta transformando: la comun o la de una tecla
        CImage EditImage()
        {
            var key = Cfg.Find(ImgEditKey);
            var im = key != null ? key.img : (ImgEditKey == null ? Cfg.image : null);
            return im != null && Images.Get(im.Shown()) != null ? im : null;
        }
        ImgFrame EditFrame() { var key = Cfg.Find(ImgEditKey); return key != null ? Cfg.KeyImageFrame(key) : Cfg.ImageFrame(); }
        void SetEditFrame(ImgFrame f)
        {
            var key = Cfg.Find(ImgEditKey);
            var im = key != null ? key.img : Cfg.image; if (im == null) return;
            double ox = key != null ? key.x : 0, oy = key != null ? key.y : 0;   // en la tecla se guarda relativo a ella
            im.custom = true; im.cx = Math.Round(f.cx - ox, 2); im.cy = Math.Round(f.cy - oy, 2); im.w = Math.Round(f.w, 2); im.h = Math.Round(f.h, 2); im.rot = f.rot;
        }
        PointF FP(ImgFrame f, double u, double v)
        {
            double a = f.rot * Math.PI / 180;
            return new PointF((float)(f.cx + u * Math.Cos(a) - v * Math.Sin(a)), (float)(f.cy + u * Math.Sin(a) + v * Math.Cos(a)));
        }
        void ToFrame(ImgFrame f, PointF l, out double u, out double v)
        {
            double a = f.rot * Math.PI / 180, dx = l.X - f.cx, dy = l.Y - f.cy;
            u = dx * Math.Cos(a) + dy * Math.Sin(a); v = -dx * Math.Sin(a) + dy * Math.Cos(a);
        }
        string ImgHit(PointF l, out int hx, out int hy)
        {
            hx = hy = 0;
            var f = EditFrame(); double u, v; ToFrame(f, l, out u, out v);
            double t = 8 * Px, rh = 28 * Px;
            if (Math.Abs(u) <= t && Math.Abs(v - (-f.h / 2 - rh)) <= t + 2 * Px) return "rot";
            for (int a = -1; a <= 1; a++) for (int b = -1; b <= 1; b++)
                {
                    if (a == 0 && b == 0) continue;
                    if (Math.Abs(u - a * f.w / 2) <= t && Math.Abs(v - b * f.h / 2) <= t) { hx = a; hy = b; return "scale"; }
                }
            if (Math.Abs(u) <= f.w / 2 && Math.Abs(v) <= f.h / 2) return "move";
            return null;
        }
        void ImgDown(Point p)
        {
            var l = ToLocal(p);
            imgDrag = ImgHit(l, out hu, out hv);
            if (imgDrag == null) { if (ImageEditDone != null) ImageEditDone(); return; }   // clic fuera: termina
            f0 = EditFrame(); il0 = l; Capture = true;
        }
        void ImgMove(Point p)
        {
            var l = ToLocal(p);
            if (imgDrag == null)
            {
                int a, b; var h = ImgHit(l, out a, out b);
                Cursor = h == "move" ? Cursors.SizeAll : h == "rot" ? Cursors.Hand : h == "scale" ? (a == 0 ? Cursors.SizeNS : b == 0 ? Cursors.SizeWE : (a == b ? Cursors.SizeNWSE : Cursors.SizeNESW)) : Cursors.Default;
                return;
            }
            bool shift = (ModifierKeys & Keys.Shift) != 0;
            var f = new ImgFrame { cx = f0.cx, cy = f0.cy, w = f0.w, h = f0.h, rot = f0.rot };
            bool alt = (ModifierKeys & Keys.Alt) != 0;
            if (imgDrag == "move")
            {
                double mdx = l.X - il0.X, mdy = l.Y - il0.Y;
                if (shift) { if (Math.Abs(mdx) >= Math.Abs(mdy)) mdy = 0; else mdx = 0; }   // Mayus: en linea recta
                f.cx = f0.cx + mdx; f.cy = f0.cy + mdy;
            }
            else if (imgDrag == "rot")
            {
                double ang = Math.Atan2(l.Y - f0.cy, l.X - f0.cx) * 180 / Math.PI + 90;
                if (shift) ang = Math.Round(ang / 15) * 15;
                ang = ((ang + 180) % 360 + 360) % 360 - 180;
                f.rot = Math.Round(ang, 1); rotShown = f.rot;
            }
            else
            {
                double u, v; ToFrame(f0, l, out u, out v);
                double nw = f0.w, nh = f0.h;
                // igual que las teclas: Mayus = mantener la forma, Alt = desde el centro
                if (hu != 0) nw = Math.Max(8, alt ? 2 * Math.Abs(u) : hu * u + f0.w / 2);
                if (hv != 0) nh = Math.Max(8, alt ? 2 * Math.Abs(v) : hv * v + f0.h / 2);
                if (shift)
                {
                    double sc = hu != 0 && hv != 0 ? Math.Max(nw / f0.w, nh / f0.h) : hu != 0 ? nw / f0.w : nh / f0.h;
                    nw = f0.w * sc; nh = f0.h * sc;
                }
                double du = alt ? 0 : hu * (nw - f0.w) / 2, dv = alt ? 0 : hv * (nh - f0.h) / 2;
                var c = FP(f0, du, dv);
                f.cx = c.X; f.cy = c.Y; f.w = nw; f.h = nh;
            }
            SetEditFrame(f);
            if (ImageChanged != null) ImageChanged(false);
            Invalidate();
        }
        void DrawImageFrame(Graphics g)
        {
            var T = App.T; var f = EditFrame();
            Func<double, double, PointF> S = (u, v) => { var q = FP(f, u, v); return ToScreen(q.X, q.Y); };
            var poly = new[] { S(-f.w / 2, -f.h / 2), S(f.w / 2, -f.h / 2), S(f.w / 2, f.h / 2), S(-f.w / 2, f.h / 2) };
            using (var pen = new Pen(Color.FromArgb(140, 0, 0, 0), App.Dp(3))) g.DrawPolygon(pen, poly);
            using (var pen = new Pen(T.Accent2, App.Dp(1.5f))) g.DrawPolygon(pen, poly);
            var topMid = S(0, -f.h / 2); var rotP = S(0, -f.h / 2 - 28 * Px);
            using (var pen = new Pen(T.Accent2, App.Dp(1.5f))) g.DrawLine(pen, topMid, rotP);
            float rr = App.Dp(7);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, rotP.X - rr, rotP.Y - rr, rr * 2, rr * 2);
            using (var pen = new Pen(T.Accent, App.Dp(2))) g.DrawEllipse(pen, rotP.X - rr, rotP.Y - rr, rr * 2, rr * 2);
            TextRenderer.DrawText(g, "↻", App.F(8, true), new Rectangle((int)(rotP.X - rr), (int)(rotP.Y - rr), (int)(rr * 2), (int)(rr * 2)), T.Accent, G.Line | TextFormatFlags.HorizontalCenter);
            float hs = App.Dp(5);
            using (var fill = new SolidBrush(Color.White)) using (var pen = new Pen(T.Accent, App.Dp(1.5f)))
                for (int a = -1; a <= 1; a++) for (int b = -1; b <= 1; b++)
                    {
                        if (a == 0 && b == 0) continue;
                        var q = S(a * f.w / 2, b * f.h / 2);
                        g.FillRectangle(fill, q.X - hs, q.Y - hs, hs * 2, hs * 2); g.DrawRectangle(pen, q.X - hs, q.Y - hs, hs * 2, hs * 2);
                    }
            string txt = !double.IsNaN(rotShown) ? Math.Round(rotShown) + "°" : imgDrag == "scale" ? Math.Round(f.w) + " × " + Math.Round(f.h) : null;
            if (txt != null)
            {
                var ft = App.F(9, true); var sz = TextRenderer.MeasureText(txt, ft);
                var rc = new RectangleF(rotP.X - sz.Width / 2f - App.Dp(8), rotP.Y - sz.Height - App.Dp(18), sz.Width + App.Dp(16), sz.Height + App.Dp(8));
                G.Fill(g, rc, App.Dp(6), Color.FromArgb(235, 20, 22, 26));
                TextRenderer.DrawText(g, txt, ft, Rectangle.Round(rc), Color.White, G.Line | TextFormatFlags.HorizontalCenter);
            }
        }

        void DrawOverlay(Graphics g)
        {
            if (ImgEdit && EditImage() != null && (ImgEditKey != null || Cfg.image.mode != "each")) { DrawImageFrame(g); return; }
            var T = App.T;
            var b = Cfg.KeysBounds();
            using (var pen = new Pen(Color.FromArgb(230, 255, 64, 160), 1.5f) { DashStyle = DashStyle.Dash })
            {
                foreach (var gx in guideX.Distinct()) g.DrawLine(pen, ToScreen(gx, -40), ToScreen(gx, b.Height + 40));
                foreach (var gy in guideY.Distinct()) g.DrawLine(pen, ToScreen(-40, gy), ToScreen(b.Width + 40, gy));
            }
            var hk = Cfg.Find(hoverId);
            if (hk != null && hk.id != SelId && dragId == null)
            {
                var poly = new[] { ToScreen(hk.x, hk.y), ToScreen(hk.x + hk.w, hk.y), ToScreen(hk.x + hk.w, hk.y + hk.h), ToScreen(hk.x, hk.y + hk.h) };
                using (var pen = new Pen(Color.FromArgb(70, T.Focus), 1f)) g.DrawPolygon(pen, poly);
            }
            if (marquee)
            {
                var rc = RectangleF.FromLTRB(Math.Min(mqStart.X, mqEnd.X), Math.Min(mqStart.Y, mqEnd.Y), Math.Max(mqStart.X, mqEnd.X), Math.Max(mqStart.Y, mqEnd.Y));
                using (var mb = new SolidBrush(Color.FromArgb(60, T.Focus))) g.FillRectangle(mb, rc);
                using (var pen = new Pen(T.Focus, 1.2f)) g.DrawRectangle(pen, rc.X, rc.Y, rc.Width, rc.Height);
            }
            var selk = SelKeys();
            if (selk.Count == 0) return;
            if (selk.Count > 1)
                foreach (var q in selk)
                {
                    var poly2 = new[] { ToScreen(q.x, q.y), ToScreen(q.x + q.w, q.y), ToScreen(q.x + q.w, q.y + q.h), ToScreen(q.x, q.y + q.h) };
                    using (var pen = new Pen(Color.FromArgb(200, T.Focus), 1.5f)) g.DrawPolygon(pen, poly2);
                }
            var gb = Box(selk);
            var k = new CKey { x = (int)gb.X, y = (int)gb.Y, w = (int)gb.Width, h = (int)gb.Height };
            if (selk.Count > 1)
            {
                var gpoly = new[] { ToScreen(k.x, k.y), ToScreen(k.x + k.w, k.y), ToScreen(k.x + k.w, k.y + k.h), ToScreen(k.x, k.y + k.h) };
                using (var pen = new Pen(Color.FromArgb(150, T.Focus), 1f) { DashStyle = DashStyle.Dash }) g.DrawPolygon(pen, gpoly);
            }
            float hs = App.Dp(4.5f);
            var pts = new[] {
                ToScreen(k.x, k.y), ToScreen(k.x + k.w / 2.0, k.y), ToScreen(k.x + k.w, k.y),
                ToScreen(k.x + k.w, k.y + k.h / 2.0), ToScreen(k.x + k.w, k.y + k.h),
                ToScreen(k.x + k.w / 2.0, k.y + k.h), ToScreen(k.x, k.y + k.h), ToScreen(k.x, k.y + k.h / 2.0) };
            using (var fill = new SolidBrush(Color.White))
            using (var pen = new Pen(T.Focus, App.Dp(1.5f)))
                foreach (var p in pts) { g.FillRectangle(fill, p.X - hs, p.Y - hs, hs * 2, hs * 2); g.DrawRectangle(pen, p.X - hs, p.Y - hs, hs * 2, hs * 2); }
            if (dragId != null && dragMoved)
            {
                string txt = (selk.Count > 1 ? selk.Count + " teclas  ·  " : "") + (dragEdges == 0 ? "X " + k.x + "  ·  Y " + k.y : k.w + " × " + k.h);
                var mk = ModifierKeys; if ((mk & Keys.Control) != 0) txt += "   imán"; if ((mk & Keys.Shift) != 0) txt += dragEdges == 0 ? "   recto" : "   proporción"; if (dragEdges != 0 && (mk & Keys.Alt) != 0) txt += "   desde el centro";
                var top = ToScreen(k.x + k.w / 2.0, k.y);
                var f = App.F(9, true); var sz = TextRenderer.MeasureText(txt, f);
                var r = new RectangleF(top.X - sz.Width / 2f - App.Dp(8), top.Y - sz.Height - App.Dp(20), sz.Width + App.Dp(16), sz.Height + App.Dp(8));
                G.Fill(g, r, App.Dp(6), Color.FromArgb(235, 20, 22, 26));
                TextRenderer.DrawText(g, txt, f, Rectangle.Round(r), Color.White, G.Line | TextFormatFlags.HorizontalCenter);
            }
        }

        internal static unsafe void Warp(Bitmap src, Bitmap dst, double[] inv, Rectangle box)
        {
            var sd = src.LockBits(new Rectangle(0, 0, src.Width, src.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            var dd = dst.LockBits(new Rectangle(0, 0, dst.Width, dst.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                int sw = src.Width, sh = src.Height, ss = sd.Stride / 4, ds = dd.Stride / 4;
                uint* sp = (uint*)sd.Scan0, dp = (uint*)dd.Scan0;
                for (int y = box.Top; y < box.Bottom; y++)
                {
                    uint* row = dp + y * ds;
                    for (int x = box.Left; x < box.Right; x++)
                    {
                        double X = x + 0.5, Y = y + 0.5;
                        double w = inv[6] * X + inv[7] * Y + inv[8];
                        if (w <= 1e-9) continue;
                        double fx = (inv[0] * X + inv[1] * Y + inv[2]) / w - 0.5;
                        double fy = (inv[3] * X + inv[4] * Y + inv[5]) / w - 0.5;
                        if (fx < -1 || fy < -1 || fx >= sw || fy >= sh) continue;
                        int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
                        double ax = fx - x0, ay = fy - y0;
                        uint p00 = (x0 >= 0 && y0 >= 0 && x0 < sw && y0 < sh) ? sp[y0 * ss + x0] : 0;
                        uint p10 = (x0 + 1 >= 0 && y0 >= 0 && x0 + 1 < sw && y0 < sh) ? sp[y0 * ss + x0 + 1] : 0;
                        uint p01 = (x0 >= 0 && y0 + 1 >= 0 && x0 < sw && y0 + 1 < sh) ? sp[(y0 + 1) * ss + x0] : 0;
                        uint p11 = (x0 + 1 >= 0 && y0 + 1 >= 0 && x0 + 1 < sw && y0 + 1 < sh) ? sp[(y0 + 1) * ss + x0 + 1] : 0;
                        if ((p00 | p10 | p01 | p11) == 0) continue;
                        double w00 = (1 - ax) * (1 - ay), w10 = ax * (1 - ay), w01 = (1 - ax) * ay, w11 = ax * ay;
                        uint o = 0;
                        for (int sh2 = 0; sh2 < 32; sh2 += 8)
                        {
                            double v = ((p00 >> sh2) & 255) * w00 + ((p10 >> sh2) & 255) * w10 + ((p01 >> sh2) & 255) * w01 + ((p11 >> sh2) & 255) * w11;
                            uint iv = (uint)(v + 0.5); if (iv > 255) iv = 255;
                            o |= iv << sh2;
                        }
                        row[x] = o;
                    }
                }
            }
            finally { src.UnlockBits(sd); dst.UnlockBits(dd); }
        }

        void DrawBackground(Graphics g, Rectangle r)
        {
            switch (Bg)
            {
                case "dark": using (var b = new SolidBrush(Color.FromArgb(28, 31, 36))) g.FillRectangle(b, r); break;
                case "light": using (var b = new SolidBrush(Color.FromArgb(236, 232, 223))) g.FillRectangle(b, r); break;
                default:
                    using (var b = new SolidBrush(Color.FromArgb(28, 31, 36))) g.FillRectangle(b, r); break;
                case "game":
                    int hz = r.Y + (int)(r.Height * 0.47);
                    using (var b = new LinearGradientBrush(new Rectangle(r.X, r.Y - 1, r.Width, hz - r.Y + 2), Color.FromArgb(127, 182, 230), Color.FromArgb(198, 220, 239), LinearGradientMode.Vertical)) g.FillRectangle(b, r.X, r.Y, r.Width, hz - r.Y);
                    using (var b = new LinearGradientBrush(new Rectangle(r.X, hz - 1, r.Width, r.Bottom - hz + 2), Color.FromArgb(140, 143, 134), Color.FromArgb(93, 96, 88), LinearGradientMode.Vertical)) g.FillRectangle(b, r.X, hz, r.Width, r.Bottom - hz);
                    break;
            }
            if (EditView)
            {
                using (var p = new Pen(Color.FromArgb(34, 76, 195, 255)))
                {
                    float step = 10 * zoom; if (step < 4) step *= 2;
                    for (float x = r.X; x < r.Right; x += step) g.DrawLine(p, x, r.Y, x, r.Bottom);
                    for (float y = r.Y; y < r.Bottom; y += step) g.DrawLine(p, r.X, y, r.Right, y);
                }
            }
        }

        // ---- dibujo de las teclas (guardado entre repintados) ----
        Bitmap keysBmp; Point keysAt; string keysSig; int version;
        string KeysSignature(int cw, int ch)
        {
            var sb = new System.Text.StringBuilder(64 + Cfg.keys.Count * 24);
            sb.Append(version).Append('|').Append(cw).Append('x').Append(ch).Append('|').Append(cx).Append(',').Append(cy)
              .Append('|').Append(Width).Append('x').Append(Height).Append('|').Append(ImgEdit).Append(ImgEditKey)
              .Append('|').Append(Cfg.position.x).Append(',').Append(Cfg.position.y).Append(',').Append(Cfg.position.scale);
            var gi = Cfg.image; sb.Append('|').Append(gi.cx).Append(',').Append(gi.cy).Append(',').Append(gi.w).Append(',').Append(gi.h).Append(',').Append(gi.rot);
            foreach (var k in Cfg.keys)
            {
                KeyAnim a; Anim.TryGetValue(k.id, out a);
                sb.Append('|').Append(k.x).Append(',').Append(k.y).Append(',').Append(k.w).Append(',').Append(k.h).Append(a != null && a.Active ? 'P' : 'u');
                if (k.img != null) sb.Append(',').Append(k.img.cx).Append(',').Append(k.img.cy).Append(',').Append(k.img.w).Append(',').Append(k.img.h).Append(',').Append(k.img.rot);
            }
            return sb.ToString();
        }
        void RenderKeys(int cw, int ch, long now, string sig)
        {
            if (keysBmp != null) { keysBmp.Dispose(); keysBmp = null; }
            keysSig = sig;
            var b = Cfg.KeysBounds();
            int m = 48, k = Math.Max(1, Math.Min(4, (int)Math.Ceiling(zoom * Cfg.position.scale)));
            int fw = Math.Max(1, (int)Math.Ceiling((b.Width + 2 * m) * k)), fh = Math.Max(1, (int)Math.Ceiling((b.Height + 2 * m) * k));
            using (var flat = new Bitmap(fw, fh, PixelFormat.Format32bppPArgb))
            {
                Action<Graphics> ghost = null;
                // en "Transformar" se ve la imagen entera en tenue para saber donde esta
                if (ImgEdit && EditImage() != null && (ImgEditKey != null || Cfg.image.mode != "each"))
                    ghost = fg =>
                    {
                        using (var gl = KeyPainter.ImageLayer(Cfg, EditImage(), EditFrame(), fw, fh, k, m, 0.3f))
                        { var gs = fg.Save(); fg.ResetTransform(); fg.DrawImage(gl, 0, 0, gl.Width, gl.Height); fg.Restore(gs); }
                    };
                KeyboardRender.DrawFlat(flat, Cfg, Anim, Cfg.keys, k, m, now, ghost);
                // se proyecta con la inclinacion (homografia); solo la parte visible (con zoom grande el lienzo es enorme)
                var vis = Rectangle.Intersect(new Rectangle(0, 0, cw, ch), new Rectangle(-cx, -cy, Width, Height));
                if (vis.Width <= 0 || vis.Height <= 0) return;
                var full = Mx.Mul(Mx.T(-vis.X, -vis.Y), M, new double[] { 1.0 / k, 0, -m, 0, 1.0 / k, -m, 0, 0, 1 });
                float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
                foreach (var pt in new[] { Mx.Ap(full, 0, 0), Mx.Ap(full, fw, 0), Mx.Ap(full, 0, fh), Mx.Ap(full, fw, fh) })
                { x0 = Math.Min(x0, pt.X); y0 = Math.Min(y0, pt.Y); x1 = Math.Max(x1, pt.X); y1 = Math.Max(y1, pt.Y); }
                var box = Rectangle.Intersect(new Rectangle(0, 0, vis.Width, vis.Height), Rectangle.FromLTRB((int)Math.Floor(x0) - 1, (int)Math.Floor(y0) - 1, (int)Math.Ceiling(x1) + 1, (int)Math.Ceiling(y1) + 1));
                if (box.Width <= 0 || box.Height <= 0) return;
                // solo se guarda el recuadro que ocupan las teclas, no todo el lienzo
                var dest = new Bitmap(box.Width, box.Height, PixelFormat.Format32bppPArgb);
                Warp(flat, dest, Mx.Inv(Mx.Mul(Mx.T(-box.X, -box.Y), full)), new Rectangle(0, 0, box.Width, box.Height));
                keysBmp = dest; keysAt = new Point(vis.X + box.X, vis.Y + box.Y);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var T = App.T;
            g.Clear(T.Bg);
            if (Cfg == null) return;
            int W = Cfg.canvas.width, H = Cfg.canvas.height;
            UpdateMatrix();
            int cw = Math.Max(1, (int)Math.Round(W * zoom)), ch = Math.Max(1, (int)Math.Round(H * zoom));
            var cr = new Rectangle(cx, cy, cw, ch);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            TextRenderer.DrawText(g, "Lienzo de OBS · " + W + " × " + H + " px" + " · así se verá en OBS" + (zoomMul > 1.01f ? " · zoom " + Math.Round(zoomMul * 100) + "% (doble clic para volver)" : " · rueda del ratón para acercar"),
                App.F(8.5f, true), new Rectangle(cx, cy - App.Dp(26), Math.Max(cw, App.Dp(420)), App.Dp(22)), T.Muted, G.Line | TextFormatFlags.Left);

            var st = g.Save();
            g.SetClip(cr);
            DrawBackground(g, cr);

            // las teclas solo se vuelven a dibujar si algo de ellas cambio (no al mover el raton, seleccionar, avisos...)
            long now = KeyPainter.Now;
            string sig = KeysSignature(cw, ch);
            if (keysBmp == null || sig != keysSig || KeysAnimating()) RenderKeys(cw, ch, now, sig);
            if (keysBmp != null) g.DrawImageUnscaled(keysBmp, cx + keysAt.X, cy + keysAt.Y);
            g.Restore(st);
            DrawOverlay(g);
            using (var p = new Pen(Color.FromArgb(120, T.Muted), 1.5f) { DashStyle = DashStyle.Dash }) g.DrawRectangle(p, cr);

            if (Cfg.keys.Count == 0)
                TextRenderer.DrawText(g, "No hay teclas. Añade una desde «Teclas».", App.F(11, true), cr, Color.White, G.Line | TextFormatFlags.HorizontalCenter);

            if (toast != null && now < toastUntil)
            {
                var tf = App.F(10, true);
                var sz = TextRenderer.MeasureText(toast, tf, new Size(Width - App.Dp(80), 1000), G.Wrap);
                var tr = new Rectangle((Width - sz.Width) / 2 - App.Dp(18), Height - sz.Height - App.Dp(46), sz.Width + App.Dp(36), sz.Height + App.Dp(24));
                G.Fill(g, tr, App.Dp(12), T.Card2);
                G.Stroke(g, tr, App.Dp(12), T.Line, 1);
                G.Fill(g, new RectangleF(tr.X, tr.Y, App.Dp(6), tr.Height), App.Dp(3), toastErr ? T.Danger : T.Ok);
                TextRenderer.DrawText(g, toast, tf, new Rectangle(tr.X + App.Dp(20), tr.Y + App.Dp(12), sz.Width + 6, sz.Height), T.Text, G.Wrap);
            }
            if (Focused && ShowFocusCues) G.Ring(g, new Rectangle(2, 2, Width - 4, Height - 4), App.Dp(8));
        }
    }

    // ================================================================
    // DIALOGOS
    // ================================================================
    public class DarkDialog : Form
    {
        public int Result = -1;
        public DarkDialog(string title, string text, string[] buttons, int primary)
        {
            Text = title; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent; BackColor = App.T.Panel; ForeColor = App.T.Text; KeyPreview = true;
            int w = App.Dp(520), pad = App.Dp(26), y = pad;
            var t = new Para(title, 14, true, false); t.BackColor = BackColor; int h = t.Measure(w - pad * 2); t.SetBounds(pad, y, w - pad * 2, h); Controls.Add(t); y += h + App.Dp(8);
            var p = new Para(text, 10.5f, false, true); p.BackColor = BackColor; h = p.Measure(w - pad * 2); p.SetBounds(pad, y, w - pad * 2, h); Controls.Add(p); y += h + App.Dp(22);
            int x = w - pad;
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                var b = new Btn(buttons[i], i == primary ? BtnKind.Primary : BtnKind.Normal, null) { BackColor = BackColor };
                int bw = b.PrefWidth(); x -= bw; b.SetBounds(x, y, bw, b.Measure(0)); x -= App.Dp(10);
                int idx = i; b.Click += (s, e) => { Result = idx; Close(); };
                Controls.Add(b);
                if (i == primary) b.TabIndex = 0;
            }
            y += App.Hit(40) + pad;
            ClientSize = new Size(w, y);
            Shown += (s, e) => { App.DarkTitle(this); App.Speak(title + ". " + text); };
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }
        public static int Ask(IWin32Window owner, string title, string text, string[] buttons, int primary)
        {
            using (var d = new DarkDialog(title, text, buttons, primary)) { d.ShowDialog(owner); return d.Result; }
        }
    }

    public class InputDialog : Form
    {
        TextField field; public string Value;
        InputDialog(string title, string text, string def)
        {
            Text = title; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent; BackColor = App.T.Panel; ForeColor = App.T.Text;
            int w = App.Dp(480), pad = App.Dp(24), y = pad;
            var t = new Para(title, 14, true, false) { BackColor = BackColor }; int h = t.Measure(w - 2 * pad); t.SetBounds(pad, y, w - 2 * pad, h); Controls.Add(t); y += h + App.Dp(6);
            field = new TextField(text, def, null, null) { BackColor = BackColor };
            h = field.Measure(w - 2 * pad); field.SetBounds(pad, y, w - 2 * pad, h); Controls.Add(field); y += h + App.Dp(18);
            var ok = new Btn("Guardar", BtnKind.Primary, null) { BackColor = BackColor };
            var cancel = new Btn("Cancelar", BtnKind.Normal, null) { BackColor = BackColor };
            int bw = App.Dp(120);
            ok.SetBounds(w - pad - bw, y, bw, App.Hit(40)); cancel.SetBounds(w - pad - 2 * bw - App.Dp(10), y, bw, App.Hit(40));
            ok.Click += (s, e) => { Value = field.Box.Text; DialogResult = DialogResult.OK; Close(); };
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            field.Box.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Value = field.Box.Text; DialogResult = DialogResult.OK; Close(); }
                else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; DialogResult = DialogResult.Cancel; Close(); }
            };
            Controls.Add(ok); Controls.Add(cancel);
            ClientSize = new Size(w, y + App.Hit(40) + pad);
            Shown += (s, e) => { App.DarkTitle(this); field.Box.Focus(); field.Box.SelectAll(); };
        }
        public static string Ask(IWin32Window owner, string title, string text, string def)
        {
            using (var d = new InputDialog(title, text, def)) return d.ShowDialog(owner) == DialogResult.OK ? d.Value : null;
        }
    }

    public class CaptureDialog : Form
    {
        public int Code = -1;
        public CaptureDialog(string text)
        {
            Text = "Pulsa una tecla"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent; BackColor = App.T.Panel;
            int w = App.Dp(540), pad = App.Dp(26), y = pad;
            var t = new Para("Pulsa la tecla", 15, true, false) { BackColor = BackColor }; int h = t.Measure(w - 2 * pad); t.SetBounds(pad, y, w - 2 * pad, h); Controls.Add(t); y += h + App.Dp(6);
            var p = new Para(text + " También puedes hacer clic con cualquier botón del ratón dentro del recuadro.", 10.5f, false, true) { BackColor = BackColor };
            h = p.Measure(w - 2 * pad); p.SetBounds(pad, y, w - 2 * pad, h); Controls.Add(p); y += h + App.Dp(14);
            var zone = new Zone { BackColor = BackColor }; zone.SetBounds(pad, y, w - 2 * pad, App.Dp(130)); Controls.Add(zone); y += App.Dp(146);
            zone.MouseDown += (s, e) => { Code = KeyCodes.FromMouse(e.Button); Close(); };
            var c = new Btn("Cancelar", BtnKind.Normal, null) { BackColor = BackColor, TabStop = false };
            int bw = c.PrefWidth(); c.SetBounds(w - pad - bw, y, bw, c.Measure(0)); c.Click += (s, e) => { Code = -1; Close(); }; Controls.Add(c);
            y += App.Hit(40) + pad;
            ClientSize = new Size(w, y);
            Shown += (s, e) => { App.DarkTitle(this); App.Speak("Pulsa la tecla. " + text); };
        }
        public void Got(int vc) { Code = vc; Close(); }
        class Zone : Ctl
        {
            public Zone() { TabStop = false; }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = Prep(e); var T = App.T;
                var r = new RectangleF(2, 2, Width - 4, Height - 4);
                G.Fill(g, r, App.Dp(14), T.Card);
                using (var pen = new Pen(T.Accent, App.Dp(3)) { DashStyle = DashStyle.Dash }) using (var p = G.RR(r, App.Dp(14))) g.DrawPath(pen, p);
                TextRenderer.DrawText(g, "Esperando una tecla…", App.F(14, true), new Rectangle(0, 0, Width, Height - App.Dp(26)), T.Text, G.Line | TextFormatFlags.HorizontalCenter);
                TextRenderer.DrawText(g, "Para cancelar usa el botón «Cancelar» con el ratón", App.F(9), new Rectangle(0, Height / 2 + App.Dp(6), Width, App.Dp(30)), T.Muted, G.Line | TextFormatFlags.HorizontalCenter);
            }
        }
    }
}
