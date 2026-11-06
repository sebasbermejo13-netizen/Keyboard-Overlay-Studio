// Keyboard Overlay Studio - ventana principal
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KOStudio
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Crash(e.ExceptionObject as Exception);
            try
            {
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => Crash(e.Exception);
                try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) App.Dpi = g.DpiX / 96f; } catch { App.Dpi = 1; }
                App.LoadPrefs();
                string bg = App.P.bg == "light" ? "light" : "dark";
                var recent = App.P.recentColors ?? new List<string>();
                var o = App.P;
                App.P = new Prefs { bg = bg, seenGuide = true, recentColors = recent,
                    overlayOn = o.overlayOn, overlayLocked = o.overlayLocked, overlayScale = o.overlayScale, overlayOpacity = o.overlayOpacity, overlayFps = o.overlayFps, overlayAnim = o.overlayAnim,
                    overlayX = o.overlayX, overlayY = o.overlayY, keepInTray = o.keepInTray };
                App.T = Theme.Get("dark");
                Application.Run(new MainForm(args));
            }
            catch (Exception ex) { Crash(ex); }
        }
        static bool crashShown;
        static void Crash(Exception ex)
        {
            string txt = DateTime.Now + "\r\n" + (ex == null ? "Error desconocido" : ex.ToString()) + "\r\n\r\n";
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(App.Dir, "error.txt"), txt); } catch { }
            if (crashShown) return; crashShown = true;
            try { MessageBox.Show("Keyboard Overlay Studio tuvo un error y lo guardó en error.txt:\n\n" + (ex == null ? "" : ex.Message), "Keyboard Overlay Studio", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
        }
    }

    public partial class MainForm : Form, IMessageFilter
    {
        // ---------- estado ----------
        Config cfg; string savedJson; List<string> hist = new List<string>(); int hIdx;
        string selId, section = "teclas"; bool editView; string zoomMode = "fit";
        HashSet<int> localDown = new HashSet<int>(), obsDown = new HashSet<int>();
        bool obsOn, closing, forceClose; CaptureDialog capture;
        string[] args;

        // ---------- interfaz ----------
        Panel side, navHost, insp, center, toolbar; Preview pv; Ctl statusBar, saveState; Btn saveBtn, undoBtn, redoBtn;
        Seg segBg;
        List<NavItem> navItems = new List<NavItem>();
        Chips keyChips; Slider sX, sY, sW, sH; CKey clipboardKey;
        int sx, sy, sw;
        System.Windows.Forms.Timer draftTimer;

        string ConfigPath { get { return Path.Combine(App.Dir, "config.js"); } }
        string DraftPath { get { return Path.Combine(App.Dir, "studio-borrador.json"); } }
        string OverlayPath { get { return Path.Combine(App.Dir, "overlay.html"); } }

        public MainForm(string[] args)
        {
            this.args = args ?? new string[0];
            Text = "Keyboard Overlay Studio";
            KeyPreview = true;
            StartPosition = FormStartPosition.CenterScreen;
            var wa = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(App.Dp(1500), wa.Width - 40), Math.Min(App.Dp(940), wa.Height - 40));
            MinimumSize = new Size(Math.Min(App.Dp(1080), wa.Width - 40), Math.Min(App.Dp(640), wa.Height - 40));
            try { Icon = MakeIcon(); } catch { }

            bool loadErr;
            cfg = LoadConfig(out loadErr);
            savedJson = Json.Write(cfg);
            hist.Add(savedJson); hIdx = 0;
            selId = cfg.keys.Count > 0 ? cfg.keys[0].id : null;
            section = "teclas";

            App.Tips = new ToolTip { InitialDelay = 450, AutoPopDelay = 15000, ReshowDelay = 100 };
            BuildUI();

            draftTimer = new System.Windows.Forms.Timer { Interval = 1200 };
            draftTimer.Tick += (s, e) => { draftTimer.Stop(); SaveDraft(); };

            Fonts.Loaded += () => { if (pv != null) pv.Touch(); if (insp != null) foreach (Control c in insp.Controls) c.Invalidate(); };
            Application.AddMessageFilter(this);

            Shown += (s, e) =>
            {
                App.DarkTitle(this);
                if (this.args.Length >= 3 && this.args[0] == "--snap") { Snapshot(); return; }
                if (this.args.Length >= 1 && this.args[0] == "--selftest") { cfg.tilt.mode = "left"; cfg.keys[0].label = "Ñ⇧"; cfg.colors.activeTop = "#11223344"; Save(); forceClose = true; closing = true; Close(); return; }
                if (loadErr) pv.Toast("No se pudo leer config.js: se abrió el diseño original.", true);
                CheckDraft();
                ObsLoop();
                InitOverlayHost();
            };
        }

        // ================================================================
        // CONSTRUIR LA VENTANA
        // ================================================================
        void BuildUI()
        {
            SuspendLayout();
            var old = Controls.Cast<Control>().ToList();
            Controls.Clear();
            foreach (var c in old) c.Dispose();
            navItems.Clear();
            App.T = Theme.Get(App.P.theme);
            if (App.Tips != null) App.Tips.RemoveAll();
            var T = App.T;
            BackColor = T.Bg; ForeColor = T.Text; Font = App.F(10);

            // ---- centro: barra + vista previa + estado ----
            center = new Panel { Dock = DockStyle.Fill, BackColor = T.Bg };
            pv = new Preview { Dock = DockStyle.Fill, Cfg = cfg, SelId = selId, EditView = editView, Bg = App.P.bg, ZoomMode = zoomMode };
            pv.KeySelected = id => SelectKey(id, true);
            pv.KeysMoved = commit => { Changed(commit); SyncXY(); };
            pv.DeleteRequested = DeleteSelected;
            pv.KeyCommand = KeyCommand;
            ColorRow.StyleColors = () => typeof(CColors).GetFields().Select(f => G.Col((string)f.GetValue(cfg.colors))).ToList();
            pv.MultiChanged = () => { selId = null; if (section != "teclas") ShowSection("teclas"); else BuildInspector(true); };
            pv.ImageChanged = commit => Changed(commit);
            pv.ImageEditDone = () => SetImgEdit(false, null);
            toolbar = new Panel { Dock = DockStyle.Top, Height = App.Hit(62), BackColor = T.Panel };
            toolbar.Paint += (s, e) => { using (var p = new Pen(T.Line)) e.Graphics.DrawLine(p, 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1); };
            segBg = new Seg("Fondo", new[] { "Oscuro", "Claro" }, new[] { "dark", "light" }, App.P.bg);
            segBg.Changed = v => { App.P.bg = v; pv.Bg = v; pv.Invalidate(); App.SavePrefs(); };
            undoBtn = new Btn("", BtnKind.Normal, "") { AccessibleName = "Deshacer" };
            redoBtn = new Btn("", BtnKind.Normal, "") { AccessibleName = "Rehacer" };
            if (!App.HasGlyphs) { undoBtn.Text = "↶"; redoBtn.Text = "↷"; }
            undoBtn.Click += (s, e) => Undo(); redoBtn.Click += (s, e) => Redo();
            App.Tips.SetToolTip(undoBtn, "Deshacer (Ctrl+Z)"); App.Tips.SetToolTip(redoBtn, "Rehacer (Ctrl+Y)");
            foreach (Control c in new Control[] { segBg }) { c.BackColor = T.Panel; toolbar.Controls.Add(c); }
            toolbar.Resize += (s, e) => LayoutToolbar();
            statusBar = new StatusBar(this) { Dock = DockStyle.Bottom, Height = App.Dp(38), BackColor = T.Panel };
            center.Controls.Add(pv); center.Controls.Add(toolbar); center.Controls.Add(statusBar);

            // ---- inspector (derecha) ----
            insp = new Panel { Dock = DockStyle.Right, Width = App.Dp(420), BackColor = T.Panel, AutoScroll = true };
            insp.HandleCreated += (s, e) => DarkScroll(insp);
            var sepR = new Panel { Dock = DockStyle.Right, Width = 1, BackColor = T.Line };

            // ---- menu (izquierda) ----
            side = new Panel { Dock = DockStyle.Left, Width = App.Dp(256), BackColor = T.Side };
            var sepL = new Panel { Dock = DockStyle.Left, Width = 1, BackColor = T.Line };
            var logo = new Logo { Dock = DockStyle.Top, Height = App.Dp(84), BackColor = T.Side };
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = App.Hit(40) + App.Dp(84), BackColor = T.Side };
            saveState = new SaveState(this) { BackColor = T.Side };
            saveBtn = new Btn("Guardar", BtnKind.Primary, "") { BackColor = T.Side, AccessibleDescription = "Guarda el estilo para OBS. Atajo Control más S." };
            saveBtn.Click += (s, e) => Save();
            App.Tips.SetToolTip(saveBtn, "Guarda el estilo en config.js de esta carpeta. OBS lo aplica solo (Ctrl+S)");
            undoBtn.BackColor = T.Side; redoBtn.BackColor = T.Side;
            bottom.Controls.Add(saveState); bottom.Controls.Add(undoBtn); bottom.Controls.Add(redoBtn); bottom.Controls.Add(saveBtn);
            bottom.Resize += (s, e) =>
            {
                int p = App.Dp(16), bh = App.Hit(44), ib = App.Hit(40), gap = App.Dp(6);
                saveState.SetBounds(p, App.Dp(12), bottom.Width - 2 * p, App.Dp(40));
                undoBtn.SetBounds(p, App.Dp(56), ib, bh);
                redoBtn.SetBounds(p + ib + gap, App.Dp(56), ib, bh);
                saveBtn.SetBounds(p + 2 * (ib + gap), App.Dp(56), bottom.Width - 2 * p - 2 * (ib + gap), bh);
            };
            bottom.Paint += (s, e) => { using (var p = new Pen(T.Line)) e.Graphics.DrawLine(p, 0, 0, bottom.Width, 0); };
            navHost = new Panel { Dock = DockStyle.Fill, BackColor = T.Side, AutoScroll = true };
            navHost.HandleCreated += (s, e) => DarkScroll(navHost);
            BuildNav();
            side.Controls.Add(navHost); side.Controls.Add(bottom); side.Controls.Add(logo);

            Controls.Add(center); Controls.Add(sepR); Controls.Add(insp); Controls.Add(sepL); Controls.Add(side);
            ResumeLayout(true);
            LayoutToolbar();
            UpdateDirty();
            ShowSection(section);
        }

        void BuildNav()
        {
            var groups = new[] {
                new { G = "Diseño", Items = new[] {
                    new[]{"teclas","","Teclas","Añadir, mover y asignar"},
                    new[]{"letra","","Letra","Tipografía y tamaño"},
                    new[]{"forma","","Forma","Bordes y volumen 3D"},
                    new[]{"colores","","Colores","Normal y al pulsar"},
                    new[]{"angulo","","Ángulo","Recto, izquierda, derecha, plano"},
                    new[]{"efectos","","Efectos","Destello, onda y brillo"},
                    new[]{"imagen","\uE91B","Imagen","Una imagen sobre todas las teclas"} } },
                new { G = "OBS", Items = new[] {
                    new[]{"obs","","OBS","Conectar, tamaño y posición"},
                    new[]{"estilos","","Estilos","Plantillas y copias"},
                    new[]{"plantillas","\uE8A9","Plantillas","Juegos y grupos de teclas"} } },
                new { G = "Extra", Items = new[] {
                    new[]{"visor","\uE7F4","Visor en pantalla","Sin OBS, para uso personal"} } }
            };
            int y = App.Dp(6);
            foreach (var g in groups)
            {
                var t = new Title(g.G) { BackColor = App.T.Side };
                t.SetBounds(App.Dp(22), y, App.Dp(256) - App.Dp(44), t.Measure(0)); navHost.Controls.Add(t); y += t.Height + App.Dp(2);
                foreach (var it in g.Items)
                {
                    var n = new NavItem(it[0], it[1], it[2], it[3]);
                    n.Picked = id => ShowSection(id);
                    n.SetBounds(0, y, App.Dp(256) - 2, n.Measure(0));
                    navHost.Controls.Add(n); navItems.Add(n);
                    y += n.Height + App.Dp(2);
                }
                y += App.Dp(8);
            }
        }

        void LayoutToolbar()
        {
            if (toolbar == null) return;
            int x = App.Dp(18), h = App.Hit(36), gap = App.Dp(14), y = App.Dp(12), rowH = h + App.Dp(10);
            foreach (var s in new[] { segBg })
            {
                int w = s.PrefWidth();
                if (x > App.Dp(18) && x + w > toolbar.Width - App.Dp(12)) { x = App.Dp(18); y += rowH; }
                s.SetBounds(x, y, w, h); x += w + gap;
            }
            int need = y + h + App.Dp(12);
            if (toolbar.Height != need) toolbar.Height = need;
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string app, string id);
        static void DarkScroll(Control c) { try { if (App.T.IsDark) SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { } }

        Icon MakeIcon()
        {
            var bmp = new Bitmap(64, 64);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                G.Fill(g, new RectangleF(6, 14, 52, 46), 12, Color.FromArgb(120, 45, 10));
                G.FillV(g, new RectangleF(6, 4, 52, 48), 12, ColorTranslator.FromHtml("#f0a24a"), ColorTranslator.FromHtml("#ce5b29"));
                using (var f = new Font("Arial", 26, FontStyle.Bold, GraphicsUnit.Pixel))
                    TextRenderer.DrawText(g, "W", f, new Rectangle(6, 4, 52, 48), ColorTranslator.FromHtml("#2b1400"), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        // ================================================================
        // SECCIONES
        // ================================================================
        void ShowSection(string id)
        {
            if (pv != null && pv.ImgEdit && ((editKey == null && id != "imagen") || (editKey != null && id != "teclas"))) { pv.ImgEdit = false; pv.ImgEditKey = null; editKey = null; pv.Invalidate(); }
            section = id;
            foreach (var n in navItems) { n.Selected = n.Id == id; n.Invalidate(); }
            BuildInspector(false);
            if (statusBar != null) statusBar.Invalidate();
        }

        T Add<T>(T c) where T : Ctl { return Add(c, App.Dp(12)); }
        T Add<T>(T c, int gap) where T : Ctl
        {
            c.BackColor = App.T.Panel;
            c.Width = sw;
            int h = c.Measure(sw);
            c.SetBounds(sx, sy, sw, h);
            insp.Controls.Add(c);
            sy += h + gap;
            return c;
        }
        void Row(params Btn[] bs)
        {
            int x = sx, rowH = 0, gap = App.Dp(8);
            foreach (var b in bs)
            {
                b.BackColor = App.T.Panel;
                int w = b.Wide ? sw : Math.Min(sw, b.PrefWidth()), h = b.Measure(0);
                if (x > sx && x + w > sx + sw) { x = sx; sy += rowH + gap; rowH = 0; }
                b.SetBounds(x, sy, w, h); insp.Controls.Add(b);
                x += w + gap; rowH = Math.Max(rowH, h);
            }
            sy += rowH + App.Dp(14);
        }
        void Head(string title, string desc)
        {
            Add(new Para(title, 16, true, false), App.Dp(4));
            Add(new Para(desc, 10, false, true), App.Dp(14));
        }
        void Sub(string t) { Add(new Title(t), App.Dp(8)); }
        Btn B(string text, BtnKind k, string glyph, Action click)
        {
            var b = new Btn(text, k, glyph); b.Click += (s, e) => click(); return b;
        }
        Slider S(string label, double min, double max, double step, string unit, string help, Func<double> get, Action<double> set)
        {
            var s = new Slider(label, min, max, step, unit, help);
            s.SetValue(get());
            s.Changed = (v, commit) => { set(v); Changed(commit); };
            return Add(s, App.Dp(10));
        }
        Toggle Tg(string title, string help, Func<bool> get, Action<bool> set)
        {
            var t = new Toggle(title, help, get());
            t.Changed = v => { set(v); Changed(true); };
            return Add(t, App.Dp(8));
        }
        ColorRow Cr(string field, string name, string help)
        {
            var fi = typeof(CColors).GetField(field);
            var c = new ColorRow(name, help, G.Col((string)fi.GetValue(cfg.colors)));
            c.Changed = (col, commit) => { fi.SetValue(cfg.colors, G.Hex(col)); Changed(commit); };
            return Add(c, App.Dp(8));
        }

        void BuildInspector(bool keepScroll)
        {
            if (insp == null) return;
            int scroll = keepScroll ? -insp.AutoScrollPosition.Y : 0;
            string focusName = null;
            var fc = ActiveControl;
            while (fc is ContainerControl && ((ContainerControl)fc).ActiveControl != null) fc = ((ContainerControl)fc).ActiveControl;
            if (fc != null && insp.Contains(fc)) focusName = fc.AccessibleName;

            insp.SuspendLayout();
            while (insp.Controls.Count > 0) { var c = insp.Controls[0]; insp.Controls.RemoveAt(0); c.Dispose(); }
            insp.AutoScrollPosition = new Point(0, 0);
            keyChips = null; sX = sY = sW = sH = null;
            sx = App.Dp(24); sw = insp.ClientSize.Width - sx * 2 - SystemInformation.VerticalScrollBarWidth + App.Dp(8); sy = App.Dp(24);

            switch (section)
            {
                case "teclas": SecKeys(); break;
                case "plantillas": SecLayouts(); break;
                case "letra": SecFont(); break;
                case "forma": SecShape(); break;
                case "colores": SecColors(); break;
                case "angulo": SecTilt(); break;
                case "efectos": SecEffects(); break;
                case "imagen": SecImage(); break;
                case "obs": SecObs(); break;
                case "estilos": SecPresets(); break;
                case "visor": SecOverlay(); break;
            }
            var pad = new Panel { BackColor = App.T.Panel }; pad.SetBounds(sx, sy, 10, App.Dp(40)); insp.Controls.Add(pad);
            insp.ResumeLayout(true);
            if (keepScroll) insp.AutoScrollPosition = new Point(0, scroll);
            if (focusName != null)
                foreach (Control c in insp.Controls)
                {
                    Control target = c.AccessibleName == focusName ? c : null;
                    if (target == null) foreach (Control cc in c.Controls) if (cc.AccessibleName == focusName) target = cc;
                    if (target != null && target.CanFocus) { target.Focus(); break; }
                }
        }

        // ---------------- TECLAS ----------------
        static readonly string[] SYMBOLS = { "@muro", "@suelo", "@escalera", "@techo", "@pico", "@editar", "⇧", "⌃", "⌥", "⊞", "↑", "↓", "←", "→", "⏎", "⌫", "⇥", "⎋", "␣", "★", "♥", "⚡", "●", "▲", "◆", "✦" };

        void SecKeys()
        {
            Head("Teclas", "Elige una tecla para editarla. En la vista previa puedes hacer clic sobre una tecla y arrastrarla para moverla.");
            Row(B("Añadir pulsando una tecla", BtnKind.Primary, "", AddByCapture), B("Tecla vacía", BtnKind.Normal, "", AddBlank));

            Sub("Tus teclas (" + cfg.keys.Count + ")");
            keyChips = new Chips("Tus teclas") { Empty = "Todavía no hay teclas. Añade una arriba." };
            foreach (var k in cfg.keys)
            {
                keyChips.Items.Add(KeyName(k));
                keyChips.Subs.Add(k.codes.Count == 0 ? "sin tecla" : KeyCodes.NameOf(k.codes[0]) + (k.codes.Count > 1 ? " +" + (k.codes.Count - 1) : ""));
            }
            keyChips.Selected = cfg.keys.FindIndex(k => k.id == selId);
            keyChips.Picked = i => { pv.Multi.Clear(); SelectKey(cfg.keys[i].id, false); };
            Add(keyChips, App.Dp(16));

            if (pv.Multi.Count > 1) { keyChips.Selected = -1; MultiPanel(); return; }
            var key = cfg.Find(selId);
            if (key == null) { if (cfg.keys.Count > 0) Add(new Para("Ninguna tecla elegida. Elige una de la lista o haz clic sobre ella en la vista previa.", 10, false, true)); return; }

            Sub("Texto de la tecla");
            var lf = Add(new TextField("Texto que se ve", key.label, "Una letra, una palabra (Saltar, Correr…) o nada.", null));
            lf.Live = t => { key.label = t; Changed(false); RefreshChip(); };
            lf.Commit = t => Changed(true);
            var icf = Add(new TextField("Símbolo (opcional)", key.icon, "Aparece a la izquierda del texto. Elige uno de abajo o escribe el tuyo.", "Quitar"));
            icf.Live = t => { key.icon = t; Changed(false); RefreshChip(); };
            icf.Commit = t => Changed(true);
            icf.Side.Click += (s, e) => { icf.Box.Text = ""; Changed(true); };
            var sym = new Chips("Símbolos rápidos") { Square = true };
            sym.Items.AddRange(SYMBOLS);
            sym.Picked = i => { icf.Box.Text = SYMBOLS[i]; Changed(true); };
            Add(sym, App.Dp(14));
            var cs = new Choices("Mayúsculas y minúsculas") { Cols = 3, CardH = 62 };
            cs.Items.Add(new Choice { Title = "Como lo escribo", Sub = "Sin cambios" });
            cs.Items.Add(new Choice { Title = "MAYÚSCULAS", Sub = "Ej. SHIFT" });
            cs.Items.Add(new Choice { Title = "minúsculas", Sub = "Ej. shift" });
            var cases = new[] { "asis", "upper", "lower" };
            cs.Selected = Math.Max(0, Array.IndexOf(cases, key.textCase ?? (key.small ? "upper" : "asis")));
            cs.Picked = i => { key.textCase = cases[i]; key.small = i == 1; Changed(true); RefreshChip(); };
            Add(cs, App.Dp(10));
            Tg("Barra de espacio en lugar del texto", "Una rayita, como en la barra espaciadora.", () => key.bar, v => key.bar = v);
            Tg("Ocultar texto y símbolo", "Útil si la imagen ya lo dice todo.", () => key.hideText, v => key.hideText = v);

            TextPlacement(new List<CKey> { key });

            Sub("Qué tecla la enciende");
            var tags = new Chips("Teclas asignadas") { Removable = true, Empty = "Sin tecla asignada: esta tecla no se encenderá." };
            foreach (var c in key.codes) tags.Items.Add(KeyCodes.NameOf(c));
            tags.Removed = i => { if (i >= 0 && i < key.codes.Count) { key.codes.RemoveAt(i); Changed(true); BuildInspector(true); } };
            Add(tags, App.Dp(10));
            Row(B("Pulsar la tecla para asignarla", BtnKind.Primary, "", () =>
            {
                int vc = CaptureKey("Pulsa la tecla que encenderá «" + KeyName(key) + "».");
                if (vc < 0) return;
                if (!key.codes.Contains(vc)) key.codes.Add(vc);
                Changed(true); BuildInspector(true); pv.Toast("Asignada: " + KeyCodes.NameOf(vc), false);
            }));
            var drop = Add(new Drop("O elígela de la lista", KeyCodes.List.Cast<object>(), "Puedes poner varias. Por ejemplo, Shift izquierdo y derecho en la misma tecla.", "Añadir"));
            drop.Side.Click += (s, e) =>
            {
                var it = drop.Box.SelectedItem as KeyCodes.Item; if (it == null) return;
                if (!key.codes.Contains(it.VC)) key.codes.Add(it.VC);
                Changed(true); BuildInspector(true);
            };


            Sub("Imagen en la tecla");
            ImageEditor(key);

            Sub("Acciones");
            Row(B("Duplicar", BtnKind.Normal, "", () =>
            {
                var n = Json.Read<CKey>(Json.Write(key)); n.id = Config.NewId(); n.x = key.x + key.w + 6; n.codes = new List<int>();
                cfg.keys.Add(n); selId = n.id; pv.SelId = n.id; Changed(true); BuildInspector(true);
                pv.Toast("Tecla duplicada: asígnale una tecla", false);
            }), B("Eliminar tecla", BtnKind.Danger, "", DeleteSelected));
        }

        static string KeyName(CKey k)
        {
            if (!string.IsNullOrEmpty(k.label)) return k.label;
            if (k.bar) return "Espacio";
            if (!string.IsNullOrEmpty(k.icon)) return VIcons.Is(k.icon) ? VIcons.Name(k.icon) : k.icon;
            return k.codes.Count > 0 ? KeyCodes.ShortLabel(k.codes[0]) : "(vacía)";
        }
        void RefreshChip()
        {
            if (keyChips == null) return;
            int i = cfg.keys.FindIndex(k => k.id == selId);
            if (i >= 0 && i < keyChips.Items.Count) { keyChips.Items[i] = KeyName(cfg.keys[i]); keyChips.Invalidate(); }
        }
        void SyncXY()
        {
            var k = cfg.Find(selId); if (k == null) return;
            if (sX != null && !sX.IsDisposed) sX.SetValue(k.x);
            if (sY != null && !sY.IsDisposed) sY.SetValue(k.y);
            if (sW != null && !sW.IsDisposed) sW.SetValue(k.w);
            if (sH != null && !sH.IsDisposed) sH.SetValue(k.h);
        }
        // varias teclas elegidas a la vez
        // posicion, tamano y desplazamiento del texto: para una tecla o para todas las elegidas a la vez
        void TextPlacement(List<CKey> ks)
        {
            var k0 = ks[0]; bool many = ks.Count > 1;
            Sub(many ? "Texto de las " + ks.Count + " teclas elegidas" : "Posición del texto en la tecla");
            var pg = new PosGrid(k0.textPos);
            Slider tsl = null;
            pg.Changed = v =>
            {
                foreach (var q in ks)
                {
                    q.textPos = v;
                    if (v != "center" && q.textScale == 100) q.textScale = 45;      // en una esquina: pequena
                    if (v == "center" && q.textScale == 45) q.textScale = 100;
                }
                if (tsl != null) tsl.SetValue(k0.textScale);
                Changed(true);
            };
            Add(pg);
            tsl = S(many ? "Tamaño del texto" : "Tamaño del texto en esta tecla", 10, 300, 5, "%", "Respecto al tamaño de la sección Letra.", () => k0.textScale, v => { foreach (var q in ks) q.textScale = (int)v; });
            S("Mover a los lados", -100, 100, 1, "px", null, () => k0.textX, v => { foreach (var q in ks) q.textX = (int)v; });
            S("Mover arriba / abajo", -100, 100, 1, "px", "Negativo = más arriba, positivo = más abajo.", () => k0.textY, v => { foreach (var q in ks) q.textY = (int)v; });
            Row(B("Centrar", BtnKind.Normal, null, () => { foreach (var q in ks) { q.textPos = "center"; q.textScale = 100; q.textX = 0; q.textY = 0; } Changed(true); BuildInspector(true); }),
                B("Copiar a todas las teclas", BtnKind.Normal, null, () =>
                {
                    foreach (var q in cfg.keys) { q.textPos = k0.textPos; q.textScale = k0.textScale; q.textX = k0.textX; q.textY = k0.textY; }
                    Changed(true); pv.Toast("Posición del texto copiada a todas las teclas", false);
                }));
        }

        void MultiPanel()
        {
            var ks = pv.SelKeys();
            Sub(ks.Count + " teclas elegidas");
            TextPlacement(ks);
            Sub("Igualar");
            Row(B("Mismo ancho", BtnKind.Normal, null, () => { var w = ks[0].w; foreach (var q in ks) q.w = w; Changed(true); }),
                B("Mismo alto", BtnKind.Normal, null, () => { var h = ks[0].h; foreach (var q in ks) q.h = h; Changed(true); }),
                B("Tamaño normal (80 × 94)", BtnKind.Normal, null, () => { foreach (var q in ks) { q.w = 80; q.h = 94; } Changed(true); }));
            Sub("Alinear");
            Row(B("Arriba", BtnKind.Normal, null, () => { var f0 = cfg.Frame(); int v = ks.Min(q => q.y); foreach (var q in ks) q.y = v; cfg.Reanchor(f0); Changed(true); }),
                B("Abajo", BtnKind.Normal, null, () => { var f0 = cfg.Frame(); int v = ks.Max(q => q.y + q.h); foreach (var q in ks) q.y = v - q.h; cfg.Reanchor(f0); Changed(true); }),
                B("Izquierda", BtnKind.Normal, null, () => { var f0 = cfg.Frame(); int v = ks.Min(q => q.x); foreach (var q in ks) q.x = v; cfg.Reanchor(f0); Changed(true); }),
                B("Derecha", BtnKind.Normal, null, () => { var f0 = cfg.Frame(); int v = ks.Max(q => q.x + q.w); foreach (var q in ks) q.x = v - q.w; cfg.Reanchor(f0); Changed(true); }));
            Row(B("Repartir en fila (6 px)", BtnKind.Normal, null, () =>
            {
                var f0 = cfg.Frame(); var o = ks.OrderBy(q => q.x).ToList(); int x = o[0].x;
                foreach (var q in o) { q.x = x; x += q.w + 6; }
                cfg.Reanchor(f0); Changed(true);
            }), B("Repartir en columna (6 px)", BtnKind.Normal, null, () =>
            {
                var f0 = cfg.Frame(); var o = ks.OrderBy(q => q.y).ToList(); int y = o[0].y;
                foreach (var q in o) { q.y = y; y += q.h + 6; }
                cfg.Reanchor(f0); Changed(true);
            }));
            Sub("Acciones");
            Row(B("Dejar de elegir", BtnKind.Normal, null, () => { pv.Multi.Clear(); selId = null; pv.SelId = null; pv.Invalidate(); BuildInspector(true); }),
                B("Eliminar " + ks.Count + " teclas", BtnKind.Danger, null, DeleteSelected));
        }
        void AddTips()
        {
            Sub("Atajos en la vista previa");
            Add(new Para("Arrastrar una tecla  →  moverla\nArrastrar sus bordes o esquinas  →  cambiar su tamaño\n  + Mayús  →  mantener la forma (proporción)\n  + Alt  →  desde el centro\n  + Ctrl  →  imán\nMayús al mover  →  imán: se pega a los bordes, centros y a 6 px de las teclas cercanas\nFlechas  →  mover 1 px (Mayús = 10 px)\nCtrl + flechas  →  cambiar tamaño\nClic en la nada o Esc  →  no elegir ninguna\nClic derecho en una tecla  →  menú (duplicar, tamaño, orden…)\nCtrl + D / Ctrl + C / Ctrl + V  →  duplicar / copiar / pegar\nArrastrar en la nada  →  recuadro para elegir varias\nMayús o Ctrl + clic  →  añadir o quitar de la selección\nCtrl + A  →  elegir todas\nSupr  →  borrar", 9.5f, false, true));
        }
        void KeyCommand(string cmd, string id)
        {
            var k = cfg.Find(id); if (k == null) return;
            int i = cfg.keys.IndexOf(k);
            switch (cmd)
            {
                case "dup": DuplicateKey(k, true); return;
                case "copy": clipboardKey = Json.Read<CKey>(Json.Write(k)); pv.Toast("Tecla copiada (Ctrl+V para pegar)", false); return;
                case "del": selId = id; DeleteSelected(); return;
                case "front": cfg.keys.RemoveAt(i); cfg.keys.Add(k); break;
                case "back": cfg.keys.RemoveAt(i); cfg.keys.Insert(0, k); break;
                case "normal": k.w = 80; k.h = 94; break;
                case "same":
                    CKey best = null; double bd = double.MaxValue;
                    foreach (var o in cfg.keys) { if (o == k) continue; double d = Math.Pow(o.x + o.w / 2.0 - k.x - k.w / 2.0, 2) + Math.Pow(o.y + o.h / 2.0 - k.y - k.h / 2.0, 2); if (d < bd) { bd = d; best = o; } }
                    if (best == null) return;
                    k.w = best.w; k.h = best.h; pv.Toast("Mismo tamaño que «" + KeyName(best) + "»: " + k.w + " × " + k.h, false); break;
            }
            Changed(true); if (section == "teclas") BuildInspector(true);
        }
        void DuplicateKey(CKey key, bool toast)
        {
            var n = Json.Read<CKey>(Json.Write(key)); n.id = Config.NewId() + cfg.keys.Count; n.x = key.x + key.w + 6; n.codes = new List<int>();
            cfg.keys.Add(n); selId = n.id; pv.SelId = n.id; Changed(true); BuildInspector(true);
            if (toast) pv.Toast("Tecla duplicada: asígnale una tecla", false);
        }
        void PasteKey()
        {
            if (clipboardKey == null) return;
            var n = Json.Read<CKey>(Json.Write(clipboardKey)); n.id = Config.NewId() + cfg.keys.Count; n.x += 20; n.y += 20;
            clipboardKey.x += 20; clipboardKey.y += 20;
            cfg.keys.Add(n); selId = n.id; pv.SelId = n.id; Changed(true); BuildInspector(true);
            pv.Toast("Tecla pegada", false);
        }

        void SelectKey(string id, bool fromPreview)
        {
            bool same = id == selId;
            if (!same && pv.ImgEdit && editKey != null) { pv.ImgEdit = false; pv.ImgEditKey = null; editKey = null; }
            selId = id; pv.SelId = id; pv.Invalidate();
            if (id == null) { if (!same && section == "teclas") BuildInspector(true); return; }
            if (section != "teclas") ShowSection("teclas");
            else if (!same) BuildInspector(true);
        }
        void DeleteSelected()
        {
            if (pv.Multi.Count > 1)
            {
                int n = cfg.keys.RemoveAll(q => pv.Multi.Contains(q.id));
                pv.Multi.Clear(); selId = null; pv.SelId = null;
                Changed(true); if (section == "teclas") BuildInspector(true);
                pv.Toast(n + " teclas eliminadas (Ctrl+Z para deshacer)", false);
                return;
            }
            var k = cfg.Find(selId); if (k == null) return;
            int i = cfg.keys.IndexOf(k);
            cfg.keys.RemoveAt(i);
            selId = cfg.keys.Count > 0 ? cfg.keys[Math.Max(0, i - 1)].id : null; pv.SelId = selId;
            Changed(true);
            if (section == "teclas") BuildInspector(true);
            pv.Toast("Tecla «" + KeyName(k) + "» eliminada (Ctrl+Z para deshacer)", false);
        }
        CKey NewKey(string label, int vc, int x, int y, int w)
        {
            var k = new CKey { id = Config.NewId() + cfg.keys.Count, label = label, x = x, y = y, w = w, h = 94, small = label.Length > 2 };
            if (vc >= 0) k.codes.Add(vc);
            return k;
        }
        void SecLayouts()
        {
            Head("Plantillas", "Distribuciones de teclas listas para usar. Se colocan en el centro del lienzo; luego puedes moverlas y cambiarlas a tu gusto.");
            GameTemplates();
            Sub("Añadir grupos sueltos");
            var packs = new[] { "Q E R F", "1 2 3 4", "Flechas", "Ratón", "Tab · Alt · Esc", "Z X C" };
            var pbs = new List<Btn>();
            foreach (var p in packs) { string name = p; var b = B(p, BtnKind.Normal, null, () => AddPack(name)); b.Small = true; b.AccessibleName = "Añadir grupo " + p; pbs.Add(b); }
            Row(pbs.ToArray());
        }

        // coloca teclas nuevas en el centro del lienzo de OBS (sin mover las que ya hay)
        void AddCentered(List<CKey> nk)
        {
            if (nk.Count == 0) return;
            var f0 = cfg.Frame();
            var c = Mx.Ap(Mx.Inv(f0), cfg.canvas.width / 2.0, cfg.canvas.height / 2.0);
            int x0 = nk.Min(k => k.x), y0 = nk.Min(k => k.y), x1 = nk.Max(k => k.x + k.w), y1 = nk.Max(k => k.y + k.h);
            int dx = (int)Math.Round(c.X - (x0 + x1) / 2.0), dy = (int)Math.Round(c.Y - (y0 + y1) / 2.0);
            foreach (var k in nk) { k.x += dx; k.y += dy; }
            cfg.keys.AddRange(nk);
            cfg.Reanchor(f0);
        }
        // deja todo el bloque de teclas en el centro del lienzo
        void CenterBlock()
        {
            var b = cfg.KeysBounds(); double sc = cfg.position.scale;
            int ox = (int)Math.Round((cfg.canvas.width - b.Width * sc) / 2), oy = (int)Math.Round((cfg.canvas.height - b.Height * sc) / 2);
            if (cfg.position.anchor != "center") { cfg.position.x = ox; cfg.position.y = oy; }
        }

        void AddByCapture()
        {
            int vc = CaptureKey("Pulsa la tecla que quieres añadir al overlay.");
            if (vc < 0) return;
            string label = KeyCodes.ShortLabel(vc);
            if (vc >= KeyCodes.MOUSE) label = new[] { "", "Clic I", "Clic D", "Rueda", "Lat 1", "Lat 2" }[Math.Min(5, vc - KeyCodes.MOUSE)];
            var k = NewKey(label, vc, 0, 0, label.Length > 2 ? 108 : 80);
            if (cfg.keys.Count == 0) { cfg.keys.Add(k); CenterBlock(); } else AddCentered(new List<CKey> { k });
            selId = k.id; pv.SelId = k.id; pv.Multi.Clear();
            Changed(true); BuildInspector(true);
            pv.Toast("Añadida: " + KeyCodes.NameOf(vc) + ". Arrástrala a su sitio en la vista previa.", false);
        }
        void AddBlank()
        {
            var k = NewKey("?", -1, 0, 0, 80);
            if (cfg.keys.Count == 0) { cfg.keys.Add(k); CenterBlock(); } else AddCentered(new List<CKey> { k });
            selId = k.id; pv.SelId = k.id; pv.Multi.Clear(); Changed(true); BuildInspector(true);
        }
        void AddPack(string name)
        {
            var b = cfg.KeysBounds();
            int y0 = cfg.keys.Count > 0 ? (int)b.Height + 6 : 0;
            var add = new List<CKey>();
            if (name == "Flechas")
            {
                add.Add(NewKey("↑", 0xE048, 86, y0, 80)); add.Add(NewKey("←", 0xE04B, 0, y0 + 100, 80));
                add.Add(NewKey("↓", 0xE050, 86, y0 + 100, 80)); add.Add(NewKey("→", 0xE04D, 172, y0 + 100, 80));
            }
            else
            {
                object[][] list;
                switch (name)
                {
                    case "Q E R F": list = new[] { new object[] { "Q", 0x10 }, new object[] { "E", 0x12 }, new object[] { "R", 0x13 }, new object[] { "F", 0x21 } }; break;
                    case "1 2 3 4": list = new[] { new object[] { "1", 0x02 }, new object[] { "2", 0x03 }, new object[] { "3", 0x04 }, new object[] { "4", 0x05 } }; break;
                    case "Z X C": list = new[] { new object[] { "Z", 0x2C }, new object[] { "X", 0x2D }, new object[] { "C", 0x2E } }; break;
                    case "Ratón": list = new[] { new object[] { "Clic I", KeyCodes.MOUSE + 1 }, new object[] { "Clic D", KeyCodes.MOUSE + 2 } }; break;
                    default: list = new[] { new object[] { "Tab", 0x0F }, new object[] { "Alt", 0x38 }, new object[] { "Esc", 0x01 } }; break;
                }
                int x = 0;
                foreach (var it in list)
                {
                    string l = (string)it[0]; int w = l.Length > 2 ? 108 : 80;
                    add.Add(NewKey(l, (int)it[1], x, y0, w)); x += w + 6;
                }
            }
            for (int i = 0; i < add.Count; i++) add[i].id = Config.NewId() + "p" + i;
            if (cfg.keys.Count == 0) { cfg.keys.AddRange(add); CenterBlock(); } else AddCentered(add);
            selId = add[0].id; pv.SelId = selId; pv.Multi.Clear();
            Changed(true); BuildInspector(true);
            var nb = cfg.KeysBounds();
            bool fits = nb.Width * cfg.position.scale + cfg.position.x <= cfg.canvas.width && nb.Height * cfg.position.scale + cfg.position.y <= cfg.canvas.height;
            pv.Toast(fits ? "Grupo «" + name + "» añadido." : "Grupo añadido, pero no cabe en el lienzo: usa «Ajustar el lienzo al teclado» en la sección OBS.", !fits);
        }
        int CaptureKey(string text)
        {
            using (var d = new CaptureDialog(text)) { capture = d; d.ShowDialog(this); capture = null; return d.Code; }
        }

        // ---------------- LETRA ----------------
        void SecFont()
        {
            Head("Letra", "La tipografía de las teclas. Arriba están las recomendadas; debajo, todas las que tienes instaladas en el ordenador.");
            Sub("Tipografía");
            var fp = new FontPicker(cfg.font.family, cfg.font.weight);
            fp.Picked = fam =>
            {
                cfg.font.family = fam;
                cfg.font.weight = Fonts.NearestWeight(fam, cfg.font.weight);
                Changed(true);
                pv.Toast("Fuente: " + fam + (App.Installed(fam) ? "" : " (se descarga de Google Fonts)"), false);
            };
            Add(fp, App.Dp(16));
            Sub("Tamaño y espaciado");
            S("Tamaño de letra", 6, 300, 1, "px", "Igual en todas las teclas. Puede ser tan grande como quieras: lo que sobresale se recorta con la forma de la tecla.", () => cfg.font.size, v => { cfg.font.size = (int)v; cfg.font.smallSize = (int)v; });
            S("Grosor", 100, 900, 100, "", "Más alto = letra más gruesa. Algunas fuentes solo tienen un grosor.", () => cfg.font.weight, v => cfg.font.weight = (int)v);
            S("Espacio entre letras", -10, 12, 0.1, "px", "Separa las letras entre sí. No mueve las teclas de una sola letra.", () => cfg.font.letterSpacing, v => cfg.font.letterSpacing = Math.Round(v, 1));
            Sub("Símbolo (⇧, flechas…)");
            S("Separación del símbolo", 0, 40, 1, "px", "Distancia entre el símbolo y el texto de la tecla.", () => cfg.font.iconGap, v => cfg.font.iconGap = (int)v);
            S("Tamaño del símbolo", 30, 250, 5, "%", "Respecto al tamaño de la letra.", () => cfg.font.iconScale, v => cfg.font.iconScale = (int)v);
            Add(new Para("La letra nunca se sale de la tecla: lo que sobresale se recorta con su forma, como una máscara.", 9, false, true));
        }

        // ---------------- FORMA ----------------
        void SecShape()
        {
            Head("Forma", "El redondeado y el volumen 3D de las teclas.");
            S("Redondeado del borde", 0, 48, 1, "px", "0 = esquinas rectas. Cuanto más alto, más redonda.", () => cfg.shape.radius, v => cfg.shape.radius = (int)v);
            S("Grosor del borde", 0, 8, 1, "px", null, () => cfg.shape.border, v => cfg.shape.border = (int)v);
            Sub("Volumen 3D");
            Tg("Cuerpo 3D de la tecla", "La parte de abajo que le da grosor. Apágalo para teclas planas.", () => cfg.effects.body, v => cfg.effects.body = v);
            S("Altura del cuerpo", 0, 24, 1, "px", "Cuánto se ve el costado de la tecla por debajo.", () => cfg.shape.depth, v => { cfg.shape.depth = (int)v; if (cfg.shape.press > v) cfg.shape.press = (int)v; });
            S("Cuánto se hunde al pulsar", 0, 24, 1, "px", "Recomendado: un poco menos que la altura del cuerpo.", () => cfg.shape.press, v => cfg.shape.press = (int)Math.Min(v, cfg.shape.depth));
        }

        // ---------------- COLORES ----------------
        public static readonly string[][] ACCENTS = {
            new[]{"Naranja SBB","#f0a24a","#ce5b29","#2b1400"}, new[]{"Azul hielo","#8fdcff","#2b8fd6","#00213d"},
            new[]{"Verde neón","#9dff8a","#22b455","#06240f"}, new[]{"Morado","#cfa6ff","#7c3aed","#1d0640"},
            new[]{"Rojo","#ff9a8a","#dc2626","#3a0505"}, new[]{"Rosa","#ffa6d8","#db2777","#3a0620"},
            new[]{"Amarillo","#fff07a","#e0b100","#2e2400"}, new[]{"Blanco","#ffffff","#c9ced6","#16181c"}
        };
        public static readonly string[][] BASES = {
            new[]{"Grafito (original)","#3a3f46ff","#1b1e22ff","#ffffff2e","#ffffffff","#1b1e22cc","#0d0f1299"},
            new[]{"Negro","#202226ff","#0b0c0eff","#ffffff1f","#ffffffff","#0b0c0ee6","#000000b3"},
            new[]{"Blanco","#f7f8faff","#d4d8deff","#00000026","#1b1e22ff","#a9afb8e6","#7d838cb3"},
            new[]{"Cristal","#ffffff33","#ffffff12","#ffffff59","#ffffffff","#ffffff1a","#ffffff0d"},
            new[]{"Azul noche","#2d3a55ff","#141b2bff","#9fb7ff33","#eaf0ffff","#141b2bcc","#080c16a6"},
            new[]{"Vino","#4a2630ff","#241015ff","#ffb3c233","#fff0f3ff","#241015cc","#12070aa6"}
        };
        public static void ApplyAccent(CColors c, string top, string bottom, string ink)
        {
            c.activeTop = top + "ff"; c.activeBottom = bottom + "ff"; c.activeText = ink + "ff";
            c.activeBorder = G.Shade(top, 0.45) + "cc"; c.activeBody = G.Shade(bottom, -0.3) + "bf"; c.activeBodyBottom = G.Shade(bottom, -0.5) + "8c";
            c.glow = bottom + "73"; c.ripple = top + "ff"; c.icon = top + "ff";
        }
        public static void ApplyBase(CColors c, string[] b) { c.capTop = b[1]; c.capBottom = b[2]; c.border = b[3]; c.text = b[4]; c.body = b[5]; c.bodyBottom = b[6]; }

        void SecColors()
        {
            Head("Colores", "Cada color tiene su opacidad: bájala para que esa parte sea más transparente. Haz clic en el cuadrado para elegir otro color.");
            Sub("Rápidos · tecla normal");
            var pb = new Palette("Colores rápidos de la tecla normal");
            foreach (var b in BASES) { pb.Colors.Add(new[] { G.Col(b[1]), G.Col(b[2]) }); pb.Names.Add(b[0]); }
            pb.Picked = i => { ApplyBase(cfg.colors, BASES[i]); Changed(true); BuildInspector(true); pv.Toast("Color base: " + BASES[i][0], false); };
            Add(pb, App.Dp(10));
            Sub("Rápidos · al pulsar");
            var pa = new Palette("Colores rápidos al pulsar");
            foreach (var a in ACCENTS) { pa.Colors.Add(new[] { G.Col(a[1]), G.Col(a[2]) }); pa.Names.Add(a[0]); }
            pa.Picked = i => { ApplyAccent(cfg.colors, ACCENTS[i][1], ACCENTS[i][2], ACCENTS[i][3]); Changed(true); BuildInspector(true); pv.Toast("Color al pulsar: " + ACCENTS[i][0], false); };
            Add(pa, App.Dp(10));
            Sub("Tecla normal");
            Cr("capTop", "Arriba de la tecla", "Inicio del degradado");
            Cr("capBottom", "Abajo de la tecla", "Final del degradado");
            Cr("border", "Borde", null);
            Cr("text", "Letra", null);
            Cr("icon", "Símbolo", "Ej. la flecha de Shift");
            Cr("body", "Cuerpo 3D · arriba", null);
            Cr("bodyBottom", "Cuerpo 3D · abajo", null);
            Cr("bar", "Barra del espacio", "La rayita de la tecla Espacio");
            Sub("Al pulsar");
            Cr("activeTop", "Arriba de la tecla", null);
            Cr("activeBottom", "Abajo de la tecla", null);
            Cr("activeBorder", "Borde", null);
            Cr("activeText", "Letra", null);
            Cr("activeBody", "Cuerpo 3D · arriba", null);
            Cr("activeBodyBottom", "Cuerpo 3D · abajo", null);
            Cr("glow", "Brillo alrededor", null);
            Cr("ripple", "Onda", null);
            Cr("activeBar", "Barra del espacio", null);
        }

        // ---------------- ANGULO ----------------
        static void PaintTilt(Graphics g, Rectangle art, string mode, bool sel)
        {
            // dibujo a tamaño de teclado real (x6) para que la perspectiva se note igual que en OBS
            const float K = 6;
            var H = Mx.Mul(Mx.T(66 * K, 86 * K), Mx.Tilt(new CTilt { mode = mode, amount = 100 }), Mx.T(-66 * K, -86 * K));
            var polys = new List<PointF[]>();
            for (int r = 0; r < 2; r++) for (int c = 0; c < 3; c++)
                {
                    float x = c * 46 * K, y = r * 46 * K, s = 40 * K;
                    polys.Add(new[] { Mx.Ap(H, x, y), Mx.Ap(H, x + s, y), Mx.Ap(H, x + s, y + s), Mx.Ap(H, x, y + s) });
                }
            float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
            foreach (var p in polys) foreach (var q in p) { x0 = Math.Min(x0, q.X); y0 = Math.Min(y0, q.Y); x1 = Math.Max(x1, q.X); y1 = Math.Max(y1, q.Y + 6 * K); }
            float sc = Math.Min(art.Width / (x1 - x0), art.Height / (y1 - y0)) * 0.9f;
            float ox = art.X + (art.Width - (x1 - x0) * sc) / 2, oy = art.Y + (art.Height - (y1 - y0) * sc) / 2;
            for (int i = 0; i < polys.Count; i++)
            {
                var pts = polys[i].Select(q => new PointF(ox + (q.X - x0) * sc, oy + (q.Y - y0) * sc)).ToArray();
                var body = pts.Select(q => new PointF(q.X, q.Y + 5 * K * sc)).ToArray();
                using (var b = new SolidBrush(Color.FromArgb(18, 20, 23))) g.FillPolygon(b, body);
                Color top = i == 1 ? ColorTranslator.FromHtml("#f0a24a") : Color.FromArgb(78, 84, 94);
                Color bot = i == 1 ? ColorTranslator.FromHtml("#ce5b29") : Color.FromArgb(40, 44, 50);
                var bb = RectangleF.FromLTRB(pts.Min(q => q.X), pts.Min(q => q.Y) - 1, pts.Max(q => q.X), pts.Max(q => q.Y) + 1);
                using (var b = new LinearGradientBrush(bb, top, bot, LinearGradientMode.Vertical)) g.FillPolygon(b, pts);
            }
        }
        void SecTilt()
        {
            Head("Ángulo", "Cómo se inclina el bloque de teclas en el stream.");
            var ch = new Choices("Inclinación") { Cols = 2, CardH = 150 };
            var modes = new[] { "center", "left", "right", "side-left", "side-right", "flat" };
            var names = new[] { "Recto inclinado", "Izquierda inclinado", "Derecha inclinado", "Lateral izquierdo", "Lateral derecho", "Plano" };
            var subs = new[] { "De frente, inclinado hacia atrás", "Girado hacia la izquierda", "Girado hacia la derecha", "El lado izquierdo se aleja", "El lado derecho se aleja", "Totalmente plano" };
            for (int i = 0; i < modes.Length; i++) { string m = modes[i]; ch.Items.Add(new Choice { Title = names[i], Sub = subs[i], Paint = (g, r, s) => PaintTilt(g, r, m, s) }); }
            ch.Selected = Math.Max(0, Array.IndexOf(modes, cfg.tilt.mode));
            ch.Picked = i => { cfg.tilt.mode = modes[i]; Changed(true); pv.Toast("Ángulo: " + names[i], false); };
            Add(ch, App.Dp(16));
            Sub("Intensidad");
            S("Cuánta inclinación", 0, 150, 5, "%", "100% = la del diseño original. No afecta a «Plano».", () => cfg.tilt.amount, v => cfg.tilt.amount = (int)v);
            Add(new Para("Consejo: si quieres dos fuentes en OBS con ángulos distintos y el mismo estilo, añade ?tilt=left, ?tilt=right, ?tilt=center, ?tilt=side-left, ?tilt=side-right o ?tilt=flat al final de la ruta de overlay.html.", 9, false, true));
        }

        // ---------------- EFECTOS ----------------
        // ---------------- IMAGEN COMUN ----------------
        void SecImage()
        {
            Head("Imagen", "Una imagen que se pone encima de TODAS las teclas a la vez. La letra siempre queda por encima.");
            ImageEditor(null);
        }

        CKey editKey;   // tecla cuya imagen se transforma (null = imagen comun)
        CImage ImgOf(CKey key) { return key == null ? cfg.image : key.img; }
        void SetImageData(CKey key, string data)
        {
            var img = Images.Get(data);
            CImage im;
            if (key == null) im = cfg.image; else { if (key.img == null) key.img = new CImage { mode = "each" }; im = key.img; }
            im.src = data; im.iw = img.Width; im.ih = img.Height; im.custom = false; im.rot = 0; im.fx = "";
            im.hue = im.sat = im.light = 0; im.colorize = false;
            if (pv.ImgEdit) { pv.ImgEdit = false; pv.ImgEditKey = null; editKey = null; }
            Changed(true); BuildInspector(true);
            pv.Toast(key == null ? "Imagen puesta sobre todas las teclas" : "Imagen puesta en «" + KeyName(key) + "»", false);
        }
        void PickImageFor(CKey key)
        {
            using (var d = new OpenFileDialog { Title = key == null ? "Elige una imagen para todas las teclas" : "Elige una imagen para la tecla", Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp|Todos los archivos|*.*" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string data = Images.FromFile(d.FileName, key == null ? 1400 : 700);
                    if (Images.Get(data) == null) throw new Exception("formato no soportado");
                    SetImageData(key, data);
                }
                catch (Exception ex) { pv.Toast("No se pudo abrir esa imagen (" + ex.Message + "). Prueba con PNG o JPG.", true); }
            }
        }

        // MISMO editor para la imagen comun (key == null) y para la imagen de una tecla
        void ImageEditor(CKey key)
        {
            bool global = key == null;
            var im = ImgOf(key);
            bool has = im != null && !string.IsNullOrEmpty(im.src);
            Add(new ImageBox(has ? im.src : ""));
            if (!has) Row(B("Elegir imagen", BtnKind.Primary, "", () => PickImageFor(key)));
            else Row(B("Cambiar imagen", BtnKind.Primary, "", () => PickImageFor(key)), B("Quitar", BtnKind.Danger, "", () =>
            {
                if (pv.ImgEdit && editKey == key) { pv.ImgEdit = false; pv.ImgEditKey = null; editKey = null; }
                if (global) { im.src = ""; im.fx = ""; im.custom = false; } else key.img = null;
                Changed(true); BuildInspector(true);
            }));
            UrlField("O pégala desde internet (URL)", url => LoadUrl(url, global ? 1400 : 700, data => SetImageData(key, data)));
            if (!has)
            {
                Add(new Para(global ? "Consejo: si quieres una imagen distinta en una sola tecla, eso se hace en Teclas → Imagen en la tecla."
                                    : "Tiene las mismas opciones que la imagen de todas las teclas (sección Imagen): transformar, girar, modo de fusión y tono/saturación.", 9, false, true));
                return;
            }

            if (global)
            {
                Sub("Cómo se reparte");
                var mode = new Choices("Cómo se reparte la imagen") { Cols = 2, CardH = 118 };
                mode.Items.Add(new Choice { Title = "Repartida", Sub = "Una sola imagen sobre todo el teclado", Paint = (g, r, s) => PaintModeDemo(g, r, true) });
                mode.Items.Add(new Choice { Title = "En cada tecla", Sub = "La misma imagen en todas", Paint = (g, r, s) => PaintModeDemo(g, r, false) });
                mode.Selected = im.mode == "each" ? 1 : 0;
                mode.Picked = i => { im.mode = i == 1 ? "each" : "span"; if (pv.ImgEdit && editKey == null) SetImgEdit(false, null); Changed(true); BuildInspector(true); };
                Add(mode, App.Dp(14));
            }

            Sub("Ajuste");
            var fit = new Choices("Ajuste de la imagen") { Cols = 3, CardH = 62 };
            fit.Items.Add(new Choice { Title = "Rellenar", Sub = "Recorta lo que sobra" });
            fit.Items.Add(new Choice { Title = "Encajar", Sub = "Se ve entera" });
            fit.Items.Add(new Choice { Title = "Estirar", Sub = "Ocupa todo" });
            var fits = new[] { "cover", "contain", "stretch" };
            fit.Selected = Math.Max(0, Array.IndexOf(fits, im.fit));
            fit.Picked = i => { im.fit = fits[i]; im.custom = false; Changed(true); BuildInspector(true); };
            Add(fit, App.Dp(14));

            if (!global || im.mode != "each")
            {
                bool editing = pv.ImgEdit && editKey == key;
                Sub("Posición, tamaño y giro");
                if (!editing)
                    Row(B("Transformar", BtnKind.Primary, "", () => SetImgEdit(true, key)),
                        B("Restablecer", BtnKind.Normal, null, () => { im.custom = false; im.rot = 0; Changed(true); BuildInspector(true); }));
                else
                    Row(B("Listo", BtnKind.Primary, "", () => SetImgEdit(false, null)),
                        B("Restablecer", BtnKind.Normal, null, () => { im.custom = false; im.rot = 0; Changed(true); }));
                Add(new Para(editing
                    ? "Arrastra para mover (Mayús = en línea recta). Tira de las asas para cambiar el tamaño (Mayús = mantener la forma, Alt = desde el centro). El círculo de arriba la gira (Mayús = de 15° en 15°). Enter, Esc o clic fuera para terminar."
                    : "Pulsa Transformar y edítala directamente sobre el teclado, como en un programa de diseño." + (im.custom ? "  Giro actual: " + Math.Round(im.rot) + "°." : ""), 9, false, true));
            }

            Sub("Modo de fusión");
            var modes = KeyPainter.BlendModes;
            var bd = Add(new Drop("Cómo se mezcla con el color de la tecla", modes.Select(m => (object)m[1]), "Igual que en Photoshop o Photopea. Multiplicar oscurece, Trama aclara, Superponer da contraste…", null));
            bd.Box.SelectedIndex = Math.Max(0, Array.FindIndex(modes, m => m[0] == im.blend));
            bd.Box.SelectedIndexChanged += (s2, e2) => { im.blend = modes[Math.Max(0, bd.Box.SelectedIndex)][0]; Changed(true); };

            Sub("Tono / Saturación");
            Tg("Colorear", "Tiñe toda la imagen de un solo color (el tono de abajo).", () => im.colorize, v => { im.colorize = v; ApplyFx(im, true, global); });
            S(im.colorize ? "Tono (color)" : "Tono", -180, 180, 1, "°", null, () => im.hue, v => { }).Changed = (v, c2) => { im.hue = (int)v; ApplyFx(im, c2, global); };
            S("Saturación", -100, 100, 1, "", null, () => im.sat, v => { }).Changed = (v, c2) => { im.sat = (int)v; ApplyFx(im, c2, global); };
            S("Luminosidad", -100, 100, 1, "", null, () => im.light, v => { }).Changed = (v, c2) => { im.light = (int)v; ApplyFx(im, c2, global); };
            Row(B("Restablecer color", BtnKind.Normal, null, () => { im.hue = im.sat = im.light = 0; im.colorize = false; ApplyFx(im, true, global); BuildInspector(true); }));

            Sub("Transparencia");
            S("Opacidad normal", 0, 100, 1, "%", "La letra siempre queda por encima de la imagen.", () => im.opacity, v => im.opacity = (int)v);
            S("Opacidad al pulsar", 0, 100, 1, "%", "Bájala para que se vea el color de pulsado a través de la imagen.", () => im.pressedOpacity, v => im.pressedOpacity = (int)v);
        }
        void SetImgEdit(bool on, CKey key)
        {
            if (pv == null) return;
            var im = ImgOf(key);
            if (on && (im == null || Images.Get(im.Shown()) == null)) return;
            if (on && !im.custom)
            {
                var f = key == null ? cfg.ImageFrame() : cfg.KeyImageFrame(key);
                double ox = key == null ? 0 : key.x, oy = key == null ? 0 : key.y;
                im.custom = true; im.cx = f.cx - ox; im.cy = f.cy - oy; im.w = f.w; im.h = f.h; im.rot = f.rot;
            }
            editKey = on ? key : null;
            pv.ImgEdit = on; pv.ImgEditKey = on && key != null ? key.id : null; pv.Invalidate();
            if (on) { pv.Focus(); pv.Toast("Transformar: arrastra, estira por las asas o gira con el círculo de arriba. Enter para terminar.", false); }
            else Changed(true);
            if (section == "imagen" || section == "teclas") BuildInspector(true);
        }
        // aplica Tono/Saturacion: mientras arrastras usa una version pequena (rapida), al soltar la buena
        void ApplyFx(CImage im, bool full, bool global)
        {
            var src = Images.Get(im.src);
            if (src == null) return;
            if (im.hue == 0 && im.sat == 0 && im.light == 0 && !im.colorize) im.fx = "";
            else using (var b = Images.Adjust(src, im.hue, im.sat, im.light, im.colorize, full ? (global ? 1400 : 700) : 360)) im.fx = Images.ToPngUrl(b);
            Changed(full);
        }

        void UrlField(string label, Action<string> go)
        {
            var f = Add(new TextField(label, "", "Enlace directo a la imagen (termina en .png, .jpg, .gif…). Se descarga y se guarda dentro del estilo.", "Usar"));
            Action run = () => { var u = f.Box.Text.Trim(); if (u != "") go(u); };
            f.Side.Click += (s, e) => run();
            f.Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; run(); } };
        }
        async void LoadUrl(string url, int max, Action<string> done)
        {
            pv.Toast("Descargando imagen…", false);
            try
            {
                string data = await Images.FromUrl(url, max);
                if (Images.Get(data) == null) throw new Exception("no es una imagen válida");
                done(data);
            }
            catch (Exception ex)
            {
                string msg = ex is AggregateException && ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                pv.Toast("No se pudo usar esa URL: " + msg + ". Usa un enlace directo a la imagen (clic derecho en la imagen → Copiar dirección de imagen).", true);
            }
        }

        static void PaintModeDemo(Graphics g, Rectangle r, bool span)
        {
            int cols = 3, rows = 2, gap = Math.Max(2, r.Width / 40);
            float kw = (r.Width - gap * (cols - 1)) / (float)cols, kh = (r.Height - gap * (rows - 1)) / (float)rows;
            for (int y = 0; y < rows; y++) for (int x = 0; x < cols; x++)
                {
                    var kr = new RectangleF(r.X + x * (kw + gap), r.Y + y * (kh + gap), kw, kh);
                    using (var p = G.RR(kr, kh * 0.2f))
                    {
                        var st = g.Save(); g.SetClip(p, CombineMode.Intersect);
                        var src = span ? new RectangleF(r.X, r.Y, r.Width, r.Height) : kr;
                        using (var b = new LinearGradientBrush(src, ColorTranslator.FromHtml("#f0a24a"), ColorTranslator.FromHtml("#7c3aed"), 35f))
                            g.FillRectangle(b, kr);
                        using (var b = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                            g.FillEllipse(b, span ? r.X + r.Width * 0.35f : kr.X + kw * 0.3f, span ? r.Y + r.Height * 0.15f : kr.Y + kh * 0.2f, span ? r.Width * 0.3f : kw * 0.4f, span ? r.Height * 0.7f : kh * 0.6f);
                        g.Restore(st);
                    }
                }
        }

        // ---------------- OBS ----------------
        void SecObs()
        {
            Head("OBS", "Cómo se conecta con OBS y dónde aparece el teclado.");
            Sub("Ponerlo en OBS (solo una vez)");
            Add(new Para("1.  En OBS: Fuentes → + → Navegador.\n2.  Marca «Archivo local» y elige overlay.html de esta carpeta (usa el botón de abajo para copiar la ruta).\n3.  Ancho " + cfg.canvas.width + " y alto " + cfg.canvas.height + ".\n\nCada vez que pulses Guardar aquí, OBS se actualiza solo en unos 2 segundos.", 10, false, false), App.Dp(12));
            Row(B("Copiar ruta de overlay.html", BtnKind.Primary, "", () =>
            {
                try { Clipboard.SetText(OverlayPath); pv.Toast("Ruta copiada. Pégala en «Archivo local» de OBS.", false); } catch { pv.Toast(OverlayPath, false); }
            }), B("Abrir la carpeta", BtnKind.Normal, "", () => { try { Process.Start("explorer.exe", "/select,\"" + OverlayPath + "\""); } catch { } }));
            ObsPluginPanel();
            Sub("Tamaño del lienzo (fuente de navegador)");
            S("Ancho", 100, 3840, 10, "px", null, () => cfg.canvas.width, v => cfg.canvas.width = (int)v);
            S("Alto", 100, 2160, 10, "px", "Pon el mismo ancho y alto en la fuente de navegador de OBS.", () => cfg.canvas.height, v => cfg.canvas.height = (int)v);
            Row(B("Ajustar el lienzo al teclado", BtnKind.Normal, "", () =>
            {
                var b = cfg.KeysBounds(); double s = cfg.position.scale; int m = Math.Max(Math.Max(cfg.position.x, cfg.position.y), 20);
                cfg.canvas.width = (int)Math.Ceiling((b.Width * s + m * 2) / 10) * 10;
                cfg.canvas.height = (int)Math.Ceiling((b.Height * s + m * 2 + 30) / 10) * 10;
                Changed(true); BuildInspector(true);
                pv.Toast("Lienzo: " + cfg.canvas.width + " × " + cfg.canvas.height + ". Pon ese tamaño en OBS.", false);
            }));
        }

        void ExportJson()
        {
            using (var d = new SaveFileDialog { Title = "Exportar estilo", Filter = "Estilo de teclado (*.json)|*.json", FileName = "estilo-teclado.json" })
                if (d.ShowDialog(this) == DialogResult.OK)
                    try { File.WriteAllText(d.FileName, Json.Pretty(Json.Write(cfg)), new UTF8Encoding(false)); pv.Toast("Estilo exportado", false); }
                    catch (Exception ex) { pv.Toast("No se pudo exportar: " + ex.Message, true); }
        }
        void ImportJson()
        {
            using (var d = new OpenFileDialog { Title = "Importar estilo", Filter = "Estilo de teclado (*.json;*.js)|*.json;*.js" })
                if (d.ShowDialog(this) == DialogResult.OK)
                    try
                    {
                        string t = File.ReadAllText(d.FileName);
                        var m = Regex.Match(t, @"\{[\s\S]*\}");
                        cfg = Json.Parse(m.Value); pv.Cfg = cfg;
                        selId = cfg.keys.Count > 0 ? cfg.keys[0].id : null; pv.SelId = selId;
                        Changed(true); BuildInspector(false); pv.Toast("Estilo importado", false);
                    }
                    catch { pv.Toast("Ese archivo no es un estilo válido", true); }
        }

        // ---------------- GUIA ----------------
        void SecGuide()
        {
            Head("Guía rápida", "Todo lo que cambies se ve al momento en el centro. Cuando te guste, pulsa Guardar (abajo a la izquierda) y OBS se actualiza solo.");
            var steps = new[] {
                new[]{"Elige tus teclas","Añade, quita o mueve teclas y decide qué tecla real las enciende.","teclas","Ir a Teclas"},
                new[]{"Dale tu estilo","Tipografía, bordes redondeados y colores normales y al pulsar, cada uno con su transparencia.","colores","Ir a Colores"},
                new[]{"Pon una imagen en una tecla","En Teclas, elige una tecla y sube una imagen. La letra queda encima y puedes bajar su opacidad.","teclas","Subir una imagen"},
                new[]{"Elige el ángulo","Recto inclinado, izquierda, derecha o plano, con la intensidad que quieras.","angulo","Ir a Ángulo"},
                new[]{"Guarda","Botón Guardar abajo a la izquierda, o Ctrl+S. Se guarda en config.js de esta misma carpeta.",null,null},
                new[]{"Ponlo en OBS (una vez)","Fuente de navegador con overlay.html de esta carpeta. Desde ese momento, cada Guardar se aplica solo.","obs","Ir a OBS"}
            };
            for (int i = 0; i < steps.Length; i++)
            {
                Add(new Step(i + 1, steps[i][0], steps[i][1]), App.Dp(6));
                if (steps[i][2] != null) { string go = steps[i][2]; var b = B(steps[i][3], BtnKind.Normal, null, () => ShowSection(go)); b.Small = true; sx += App.Dp(48); sw -= App.Dp(48); Row(b); sx -= App.Dp(48); sw += App.Dp(48); }
                else sy += App.Dp(10);
            }
            Sub("Atajos de teclado");
            Add(new Para("Ctrl + S  →  Guardar\nCtrl + Z  →  Deshacer\nCtrl + Y  →  Rehacer\nF1  →  Abrir esta guía\nTab  →  Pasar de un control a otro\nFlechas  →  Cambiar valores, o mover la tecla elegida en la vista previa (Mayús = 10 px)\nSupr  →  Borrar la tecla elegida en la vista previa\nDoble clic en un número  →  Escribir el valor exacto\nClic derecho y arrastrar  →  Mover la vista previa", 10, false, false));
            Sub("¿Te cuesta leer o usar algo?");
            Add(new Para("En Accesibilidad puedes agrandar todo el programa, usar alto contraste o tema claro, una letra más fácil de leer, botones más grandes o que te lea los controles en voz alta.", 10, false, true));
            Row(B("Ir a Accesibilidad", BtnKind.Normal, "", () => ShowSection("acc")));
        }

        // ---------------- ACCESIBILIDAD ----------------
        void SecAccess()
        {
            Head("Accesibilidad", "Ajusta el programa para que te resulte cómodo. Esto no cambia lo que se ve en OBS.");
            var sc = new Slider("Tamaño de todo el programa", 80, 200, 5, "%", "Se aplica al soltar el control.");
            sc.SetValue(App.P.uiScale);
            sc.Changed = (v, commit) => { if (commit && (int)v != App.P.uiScale) { App.P.uiScale = (int)v; PrefsChanged(); } };
            Add(sc, App.Dp(10));
            Sub("Tema");
            var th = new Choices("Tema de colores") { Cols = 3, CardH = 100 };
            var ids = new[] { "dark", "light", "contrast" };
            var nm = new[] { "Oscuro", "Claro", "Alto contraste" };
            for (int i = 0; i < 3; i++)
            {
                var t = Theme.Get(ids[i]);
                th.Items.Add(new Choice
                {
                    Title = nm[i],
                    Paint = (g, r, s) =>
                    {
                        G.Fill(g, r, App.Dp(8), t.Bg); G.Stroke(g, r, App.Dp(8), t.Line, 1);
                        G.Fill(g, new RectangleF(r.X + r.Width * .1f, r.Y + r.Height * .25f, r.Width * .5f, r.Height * .18f), 3, t.Text);
                        G.Fill(g, new RectangleF(r.X + r.Width * .1f, r.Y + r.Height * .55f, r.Width * .3f, r.Height * .22f), 3, t.Accent);
                    }
                });
            }
            th.Selected = Math.Max(0, Array.IndexOf(ids, App.P.theme));
            th.Picked = i => { App.P.theme = ids[i]; PrefsChanged(); };
            Add(th, App.Dp(16));
            Sub("Opciones");
            PT("Letra fácil de leer", "Usa Verdana, más ancha y clara.", () => App.P.readable, v => App.P.readable = v);
            PT("Botones más grandes", "Más fácil de pulsar con ratón o pantalla táctil.", () => App.P.bigTargets, v => App.P.bigTargets = v);
            PT("Ocultar textos de ayuda", "Para cuando ya conoces el programa.", () => App.P.hideHelp, v => App.P.hideHelp = v);
            PT("Consejos al pasar el ratón", "Muestra una explicación al dejar el ratón encima de un control.", () => App.P.tips, v => App.P.tips = v);
            PT("Leer en voz alta", "Lee cada control al llegar a él con Tab (usa las voces de Windows).", () => App.P.speak, v => App.P.speak = v);
        }
        void PT(string title, string help, Func<bool> get, Action<bool> set)
        {
            var t = new Toggle(title, help, get());
            t.Changed = v => { set(v); PrefsChanged(); };
            Add(t, App.Dp(8));
        }
        void PrefsChanged()
        {
            App.SavePrefs();
            BeginInvoke((Action)(() => { BuildUI(); App.DarkTitle(this); }));
        }

        // ================================================================
        // CAMBIOS, DESHACER, GUARDAR
        // ================================================================
        void Changed(bool commit)
        {
            if (pv != null) { pv.Cfg = cfg; pv.Invalidate(); }
            if (overlay != null) overlay.SetConfig(cfg);
            InputHub.SetMouse(UsesMouse());
            Lights();
            if (commit) Commit(); else dirty = true;
            UpdateDirty();
            draftTimer.Stop(); draftTimer.Start();
        }
        void Commit()
        {
            string j = Json.Write(cfg);
            dirty = j != savedJson;
            if (j == hist[hIdx]) return;
            if (hIdx < hist.Count - 1) hist.RemoveRange(hIdx + 1, hist.Count - hIdx - 1);
            hist.Add(j);
            // historial limitado por pasos y por memoria (con imagenes cada paso puede ocupar varios MB)
            long total = 0; foreach (var h in hist) total += h.Length;
            while (hist.Count > 1 && (hist.Count > 120 || total > 40000000)) { total -= hist[0].Length; hist.RemoveAt(0); }
            hIdx = hist.Count - 1;
        }
        void Undo() { if (hIdx <= 0) return; hIdx--; Restore(); pv.Toast("Deshecho", false); }
        void Redo() { if (hIdx >= hist.Count - 1) return; hIdx++; Restore(); pv.Toast("Rehecho", false); }
        void Restore()
        {
            cfg = Json.Parse(hist[hIdx]);
            if (cfg.Find(selId) == null) selId = cfg.keys.Count > 0 ? cfg.keys[0].id : null;
            pv.Cfg = cfg; pv.SelId = selId;
            Changed(false); dirty = hist[hIdx] != savedJson; UpdateDirty(); BuildInspector(true);
        }
        // "hay cambios sin guardar": se calcula al confirmar un cambio, no en cada repintado (el JSON puede pesar MB con imagenes)
        bool dirty;
        public bool Dirty { get { return dirty; } }
        public bool ObsOn { get { return obsOn || InputHub.Clients > 0; } }
        public string Section { get { return section; } }
        public int KeyCount { get { return cfg.keys.Count; } }
        void UpdateDirty()
        {
            bool d = dirty;
            Text = "Keyboard Overlay Studio" + (d ? "  •  cambios sin guardar" : "");
            if (saveState != null) saveState.Invalidate();
            if (undoBtn != null) { undoBtn.Enabled = hIdx > 0; redoBtn.Enabled = hIdx < hist.Count - 1; }
        }

        Config LoadConfig(out bool error)
        {
            error = false;
            try
            {
                if (!File.Exists(ConfigPath)) return new Config();
                string t = File.ReadAllText(ConfigPath);
                int i = t.IndexOf("KB_CONFIG", StringComparison.Ordinal);
                if (i < 0) return new Config();
                int a = t.IndexOf('{', i), b = t.LastIndexOf('}');
                int semi = t.IndexOf(';', i);
                if (a < 0 || b < a || (semi >= 0 && semi < a)) return new Config(); // KB_CONFIG = null;
                return Json.Parse(t.Substring(a, b - a + 1));
            }
            catch { error = true; return new Config(); }
        }
        void Save()
        {
            try
            {
                string text = "/* Estilo guardado por Keyboard Overlay Studio - " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + "\n" +
                    "   No hace falta tocarlo a mano: abre «Keyboard Overlay Studio.exe», cambia lo que quieras y pulsa GUARDAR. */\n" +
                    "window.KB_CONFIG = " + Json.Pretty(Json.Write(cfg)) + ";\n";
                if (File.Exists(ConfigPath)) File.Copy(ConfigPath, Path.Combine(App.Dir, "config-anterior.js"), true);
                File.WriteAllText(ConfigPath, text, new UTF8Encoding(false));
                savedJson = Json.Write(cfg); dirty = false;
                try { if (File.Exists(DraftPath)) File.Delete(DraftPath); } catch { }
                UpdateDirty();
                pv.Toast("✔ Guardado. OBS lo aplica en unos 2 segundos.", false);
                App.Speak("Guardado");
            }
            catch (Exception ex) { pv.Toast("No se pudo guardar: " + ex.Message, true); }
        }
        void SaveDraft()
        {
            try { if (Dirty) File.WriteAllText(DraftPath, Json.Write(cfg)); else if (File.Exists(DraftPath)) File.Delete(DraftPath); } catch { }
        }
        void CheckDraft()
        {
            try
            {
                if (!File.Exists(DraftPath)) return;
                var d = Json.Parse(File.ReadAllText(DraftPath));
                if (Json.Write(d) == savedJson) { File.Delete(DraftPath); return; }
                int r = DarkDialog.Ask(this, "Hay cambios sin guardar", "La última vez cerraste el programa con cambios sin guardar. ¿Quieres recuperarlos?", new[] { "Descartar", "Recuperar" }, 1);
                if (r == 1) { cfg = d; pv.Cfg = cfg; selId = cfg.keys.Count > 0 ? cfg.keys[0].id : null; pv.SelId = selId; Changed(true); BuildInspector(false); pv.Toast("Cambios recuperados. Pulsa Guardar para aplicarlos en OBS.", false); }
                else File.Delete(DraftPath);
            }
            catch { }
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // con el visor abierto, cerrar el editor lo deja en la bandeja (como Crosshair X)
            // y si OBS esta usando el programa para recibir las teclas, tambien (si no, OBS dejaria de iluminarlas)
            bool obsUsing = InputHub.Serving && InputHub.Clients > 0;
            if (!forceClose && !exitRequested && e.CloseReason == CloseReason.UserClosing && (OverlayVisible || obsUsing) && App.P.keepInTray)
            {
                e.Cancel = true; Hide();
                if (tray != null) tray.ShowBalloonTip(2500, "Keyboard Overlay Studio", (OverlayVisible ? "El visor sigue en pantalla" : "Sigue mandando tus teclas a OBS") + ". Doble clic en este icono para abrir el editor; clic derecho para salir.", ToolTipIcon.Info);
                return;
            }
            if (!forceClose && Dirty && e.CloseReason == CloseReason.UserClosing)
            {
                int r = DarkDialog.Ask(this, "¿Guardar los cambios?", "Si no guardas, OBS seguirá mostrando el estilo anterior.", new[] { "Cancelar", "No guardar", "Guardar" }, 2);
                if (r == 2) Save();
                else if (r == 1) { try { if (File.Exists(DraftPath)) File.Delete(DraftPath); } catch { } }
                else { e.Cancel = true; return; }
            }
            closing = true;
            ShutdownOverlay();
            base.OnFormClosing(e);
        }

        void SetView(bool edit)
        {
            editView = edit; pv.EditView = edit; pv.Invalidate();
        }

        // ================================================================
        // TECLAS DE PRUEBA, ATAJOS Y OBS
        // ================================================================
        public bool PreFilterMessage(ref Message m)
        {
            const int KD = 0x100, KU = 0x101, SKD = 0x104, SKU = 0x105, WHEEL = 0x20A;
            if (m.Msg == WHEEL && pv != null && !pv.IsDisposed && capture == null)
            {
                var sp = Cursor.Position;
                if (pv.RectangleToScreen(pv.ClientRectangle).Contains(sp) && pv.FindForm() == Form.ActiveForm)
                {
                    int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                    pv.Wheel(delta, pv.PointToClient(sp));
                    return true;
                }
                return false;
            }
            if (m.Msg != KD && m.Msg != KU && m.Msg != SKD && m.Msg != SKU) return false;
            bool down = m.Msg == KD || m.Msg == SKD;
            int vc = KeyCodes.FromLParam(m.LParam);
            if (capture != null) { if (down) capture.Got(vc); return true; }
            if (!ContainsFocus) return false;
            if (down) { if (((m.LParam.ToInt64() >> 30) & 1) == 0 && localDown.Add(vc)) Lights(); }
            else if (localDown.Remove(vc)) Lights();
            return false;
        }
        protected override void OnDeactivate(EventArgs e) { localDown.Clear(); Lights(); base.OnDeactivate(e); }
        void Lights()
        {
            if (pv == null) return;
            bool busy = pv.MouseBusy, ch = false;   // mientras editas con el raton, Mayus/Ctrl/Alt no "pulsan" teclas
            foreach (var k in cfg.keys)
            {
                bool on = false;
                if (!busy) foreach (var c in k.codes) if (localDown.Contains(c) || obsDown.Contains(c) || hookDown.Contains(c)) { on = true; break; }
                ch |= pv.SetPressed(k.id, on);
            }
            if (ch) pv.Invalidate();
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            bool inText = ActiveControlDeep() is TextBoxBase;
            if (keyData == (Keys.Control | Keys.S)) { Save(); return true; }
            if (!inText && keyData == (Keys.Control | Keys.D)) { var k = cfg.Find(selId); if (k != null) DuplicateKey(k, true); return true; }
            if (!inText && keyData == (Keys.Control | Keys.C)) { if (selId != null) KeyCommand("copy", selId); return true; }
            if (!inText && keyData == (Keys.Control | Keys.V)) { PasteKey(); return true; }
            if (!inText && keyData == (Keys.Control | Keys.A)) { pv.SelectAll(); return true; }
            if (keyData == (Keys.Control | Keys.Z) && !inText) { Undo(); return true; }
            if ((keyData == (Keys.Control | Keys.Y) || keyData == (Keys.Control | Keys.Shift | Keys.Z)) && !inText) { Redo(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        Control ActiveControlDeep()
        {
            Control c = ActiveControl;
            while (c is ContainerControl && ((ContainerControl)c).ActiveControl != null) c = ((ContainerControl)c).ActiveControl;
            return c;
        }

        async void ObsLoop()
        {
            while (!closing)
            {
                // el programa ya hace de servidor para OBS: las teclas le llegan directas, no hace falta conectarse
                if (InputHub.Serving) { await Task.Delay(2000); if (statusBar != null && Visible) statusBar.Invalidate(); continue; }
                if (!Visible) { await Task.Delay(3000); continue; }   // editor en la bandeja: las luces de OBS no se ven, no hace falta conectar
                try
                {
                    using (var ws = new ClientWebSocket())
                    {
                        var cts = new CancellationTokenSource(2500);
                        await ws.ConnectAsync(new Uri("ws://127.0.0.1:16899/"), cts.Token);
                        obsOn = true; statusBar.Invalidate();
                        var buf = new byte[16384]; var sb = new StringBuilder();
                        while (ws.State == WebSocketState.Open && !closing)
                        {
                            var r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                            if (r.MessageType == WebSocketMessageType.Close) break;
                            sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count));
                            if (r.EndOfMessage) { HandleObs(sb.ToString()); sb.Clear(); }
                        }
                    }
                }
                catch { }
                if (obsOn) { obsOn = false; obsDown.Clear(); Lights(); if (statusBar != null) statusBar.Invalidate(); }
                if (!closing) await Task.Delay(3000);
            }
        }
        void HandleObs(string json)
        {
            try
            {
                var o = Json.Obj(json) as Dictionary<string, object>;
                if (o == null || !o.ContainsKey("event_type")) return;
                string t = Convert.ToString(o["event_type"]);
                int code;
                if (t == "key_pressed" || t == "key_released") code = Convert.ToInt32(o["keycode"]);
                else if (t == "mouse_pressed" || t == "mouse_released") code = KeyCodes.MOUSE + Convert.ToInt32(o["button"]);
                else return;
                if (t.EndsWith("pressed")) obsDown.Add(code); else obsDown.Remove(code);
                Lights();
            }
            catch { }
        }

        // ================================================================
        // CAPTURAS (solo para pruebas: --snap seccion archivo.png)
        // ================================================================
        void Snapshot()
        {
            if (args.Length >= 4) { var p = args[3].Split('x'); Size = new Size(int.Parse(p[0]), int.Parse(p[1])); }
            if (args.Length >= 5 && args[4] == "press") { localDown.Add(0x1E); localDown.Add(0x2A); Lights(); }
            if (args.Length >= 5 && args[4] == "edit") SetView(true);
            ShowSection(args[1]);
            if (args.Length >= 5 && args[4] == "imgedit") SetImgEdit(true, null);
            if (args.Length >= 5 && args[4] == "multi") pv.SelectAll();
            if (args.Length >= 5 && args[4] == "keyedit") { SelectKey(cfg.keys[1].id, false); SetImgEdit(true, cfg.keys[1]); }
            var t = new System.Windows.Forms.Timer { Interval = 900 };
            t.Tick += (s, e) =>
            {
                t.Stop();
                using (var bmp = new Bitmap(ClientSize.Width, ClientSize.Height))
                {
                    using (var g = Graphics.FromImage(bmp)) { g.Clear(BackColor); SnapDraw(g, this); }
                    bmp.Save(args[2], System.Drawing.Imaging.ImageFormat.Png);
                }
                forceClose = true; closing = true; Close();
            };
            t.Start();
        }

        void SnapDraw(Graphics g, Control parent)
        {
            for (int i = parent.Controls.Count - 1; i >= 0; i--)
            {
                var c = parent.Controls[i];
                if (!c.Visible || c.Width <= 0 || c.Height <= 0) continue;
                var p = PointToClient(c.PointToScreen(Point.Empty));
                var clip = RectangleToClient(parent.RectangleToScreen(parent.ClientRectangle));
                using (var b = new Bitmap(c.Width, c.Height))
                {
                    try { c.DrawToBitmap(b, new Rectangle(0, 0, c.Width, c.Height)); } catch { }
                    var st = g.Save(); g.SetClip(clip, CombineMode.Intersect);
                    g.DrawImage(b, p.X, p.Y);
                    g.Restore(st);
                }
                SnapDraw(g, c);
            }
        }

        // ================================================================
        // PIEZAS PINTADAS
        // ================================================================
        class Logo : Ctl
        {
            public Logo() { TabStop = false; SetStyle(ControlStyles.Selectable, false); }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = Prep(e); var T = App.T;
                int s = App.Dp(42), x = App.Dp(20), y = (Height - s) / 2;
                G.Fill(g, new RectangleF(x, y + App.Dp(5), s, s), App.Dp(11), G.Mix(T.Accent, Color.Black, .45f));
                G.FillV(g, new RectangleF(x, y, s, s - App.Dp(3)), App.Dp(11), T.Accent2, T.Accent);
                TextRenderer.DrawText(g, "W", App.F(14, true), new Rectangle(x, y, s, s - App.Dp(3)), T.AccentInk, G.Line | TextFormatFlags.HorizontalCenter);
                int tx = x + s + App.Dp(12);
                TextRenderer.DrawText(g, "Keyboard Overlay", App.F(11.5f, true), new Rectangle(tx, y - App.Dp(2), Width - tx, App.Dp(24)), T.Text, G.Line | TextFormatFlags.Left);
                TextRenderer.DrawText(g, "STUDIO · para OBS", App.F(8, true), new Rectangle(tx, y + App.Dp(22), Width - tx, App.Dp(20)), T.Accent2, G.Line | TextFormatFlags.Left);
                using (var p = new Pen(T.Line)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
            }
        }
        class SaveState : Ctl
        {
            MainForm f;
            public SaveState(MainForm f) { this.f = f; TabStop = false; SetStyle(ControlStyles.Selectable, false); }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = Prep(e); var T = App.T;
                bool d = f.Dirty;
                int r = App.Dp(10);
                using (var b = new SolidBrush(d ? T.Warn : T.Ok)) g.FillEllipse(b, 0, (Height - r) / 2 - App.Dp(8), r, r);
                TextRenderer.DrawText(g, d ? "Cambios sin guardar" : "Todo guardado", App.F(9.5f, true), new Rectangle(r + App.Dp(10), 0, Width, Height / 2 + App.Dp(4)), T.Text, G.Line | TextFormatFlags.Left);
                TextRenderer.DrawText(g, d ? "OBS aún muestra lo anterior" : "OBS muestra este estilo", App.F(8.25f), new Rectangle(r + App.Dp(10), Height / 2, Width, Height / 2), T.Muted, G.Line | TextFormatFlags.Left);
            }
        }
        class StatusBar : Ctl
        {
            MainForm f;
            public StatusBar(MainForm f) { this.f = f; TabStop = false; SetStyle(ControlStyles.Selectable, false); }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = Prep(e); var T = App.T;
                using (var p = new Pen(T.Line)) g.DrawLine(p, 0, 0, Width, 0);
                int x = App.Dp(18), r = App.Dp(9);
                using (var b = new SolidBrush(f.ObsOn ? T.Ok : T.Muted)) g.FillEllipse(b, x, (Height - r) / 2, r, r);
                string o = InputHub.Serving
                    ? (InputHub.Clients > 0 ? "OBS conectado (sin plugin)" : "Listo para OBS: añade overlay.html como fuente de navegador")
                    : f.ObsOn ? "OBS conectado: la vista previa usa tus teclas reales" : "OBS no conectado (pulsa tus teclas aquí para probar)";
                int ow = TextRenderer.MeasureText(o, App.F(8.75f, true)).Width;
                TextRenderer.DrawText(g, o, App.F(8.75f, true), new Rectangle(x + r + App.Dp(8), 0, ow + 4, Height), f.ObsOn ? T.Text : T.Muted, G.Line | TextFormatFlags.Left);
                string hint = "Arrastra: mover  ·  Bordes: estirar  ·  Ctrl: imán  ·  Mayús: recto / proporción  ·  Doble clic: la de debajo  ·  Clic en la nada: soltar  ·  Clic der.: menú  ·  Rueda: zoom";
                int hx = x + r + ow + App.Dp(40);
                TextRenderer.DrawText(g, hint, App.F(8.75f), new Rectangle(hx, 0, Width - hx - App.Dp(110), Height), T.Muted, G.Line | TextFormatFlags.Left);
                TextRenderer.DrawText(g, f.KeyCount + " teclas", App.F(8.75f, true), new Rectangle(Width - App.Dp(110), 0, App.Dp(92), Height), T.Muted, G.Line | TextFormatFlags.Right);
            }
        }
        class Step : Ctl
        {
            int n; string title, text;
            public Step(int n, string title, string text)
            {
                this.n = n; this.title = title; this.text = text; TabStop = false; SetStyle(ControlStyles.Selectable, false);
                AccessibleRole = AccessibleRole.StaticText; AccessibleName = "Paso " + n + ": " + title + ". " + text;
            }
            public override int Measure(int w)
            {
                int tw = w - App.Dp(48);
                return TextRenderer.MeasureText(title, App.F(10.5f, true), new Size(tw, 1000), G.Wrap).Height + TextRenderer.MeasureText(text, App.F(9.5f), new Size(tw, 1000), G.Wrap).Height + App.Dp(6);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = Prep(e); var T = App.T;
                int d = App.Dp(34);
                G.FillV(g, new RectangleF(0, 0, d, d), App.Dp(10), T.Accent2, T.Accent);
                TextRenderer.DrawText(g, n.ToString(), App.F(11, true), new Rectangle(0, 0, d, d), T.AccentInk, G.Line | TextFormatFlags.HorizontalCenter);
                int tx = App.Dp(48), tw = Width - tx;
                int th = TextRenderer.MeasureText(title, App.F(10.5f, true), new Size(tw, 1000), G.Wrap).Height;
                TextRenderer.DrawText(g, title, App.F(10.5f, true), new Rectangle(tx, 0, tw, th), T.Text, G.Wrap);
                TextRenderer.DrawText(g, text, App.F(9.5f), new Rectangle(tx, th + App.Dp(2), tw, Height - th), T.Muted, G.Wrap);
            }
        }
    }
}
