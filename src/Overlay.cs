// Keyboard Overlay Studio - visor en pantalla (sin OBS): ventana transparente siempre encima,
// los clics la atraviesan y detecta las teclas con Raw Input (sin ganchos).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KOStudio
{
    // ---------------- dibujo del teclado a un bitmap (mismo resultado que la vista previa) ----------------
    public static class KeyboardRender
    {
        // devuelve un bitmap con fondo transparente y "origin" = esquina del bitmap en coordenadas del lienzo
        public static Bitmap Render(Config c, Dictionary<string, KeyAnim> anim, float zoom, out PointF origin)
        { Rectangle at; return Core(c, anim, zoom, null, 1, out origin, out at); }
        // solo una tecla (con la geometria de todo el teclado), recortada a su zona: "at" = posicion dentro del bitmap completo
        public static Bitmap RenderKey(Config c, Dictionary<string, KeyAnim> anim, float zoom, CKey only, out Rectangle at)
        { PointF o; return Core(c, anim, zoom, only, 2, out o, out at); }
        public static Size FullSize(Config c, float zoom, out PointF origin)
        {
            var b = c.KeysBounds(); int m = 60;
            var L = Mx.LocalToCanvas(c, false);
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var p in new[] { Mx.Ap(L, -m, -m), Mx.Ap(L, b.Width + m, -m), Mx.Ap(L, -m, b.Height + m), Mx.Ap(L, b.Width + m, b.Height + m) })
            { x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); }
            origin = new PointF(x0, y0);
            return new Size(Math.Max(1, (int)Math.Ceiling((x1 - x0) * zoom)), Math.Max(1, (int)Math.Ceiling((y1 - y0) * zoom)));
        }
        static Bitmap Core(Config c, Dictionary<string, KeyAnim> anim, float zoom, CKey only, int maxSS, out PointF origin, out Rectangle at)
        {
            var b = c.KeysBounds();
            int m = 60;
            var L = Mx.LocalToCanvas(c, false);
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var p in new[] { Mx.Ap(L, -m, -m), Mx.Ap(L, b.Width + m, -m), Mx.Ap(L, -m, b.Height + m), Mx.Ap(L, b.Width + m, b.Height + m) })
            { x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); }
            origin = new PointF(x0, y0);
            int W = Math.Max(1, (int)Math.Ceiling((x1 - x0) * zoom)), H = Math.Max(1, (int)Math.Ceiling((y1 - y0) * zoom));
            int k = Math.Max(1, Math.Min(maxSS, (int)Math.Ceiling(zoom * c.position.scale)));
            int fw = (int)Math.Ceiling((b.Width + 2 * m) * k), fh = (int)Math.Ceiling((b.Height + 2 * m) * k);
            long now = KeyPainter.Now;
            at = new Rectangle(0, 0, W, H);
            if (only != null)
            {
                float e = 60, dep = c.shape.depth + 10;
                float qx0 = float.MaxValue, qy0 = float.MaxValue, qx1 = float.MinValue, qy1 = float.MinValue;
                foreach (var p in new[] { Mx.Ap(L, only.x - e, only.y - e), Mx.Ap(L, only.x + only.w + e, only.y - e), Mx.Ap(L, only.x - e, only.y + only.h + dep + e), Mx.Ap(L, only.x + only.w + e, only.y + only.h + dep + e) })
                { qx0 = Math.Min(qx0, p.X); qy0 = Math.Min(qy0, p.Y); qx1 = Math.Max(qx1, p.X); qy1 = Math.Max(qy1, p.Y); }
                at = Rectangle.Intersect(at, Rectangle.FromLTRB((int)Math.Floor((qx0 - x0) * zoom), (int)Math.Floor((qy0 - y0) * zoom), (int)Math.Ceiling((qx1 - x0) * zoom), (int)Math.Ceiling((qy1 - y0) * zoom)));
                if (at.Width < 1 || at.Height < 1) at = new Rectangle(0, 0, 1, 1);
            }
            var dest = new Bitmap(at.Width, at.Height, PixelFormat.Format32bppPArgb);
            var keys = only == null ? c.keys : new List<CKey> { only };
            using (var flat = new Bitmap(Math.Max(1, fw), Math.Max(1, fh), PixelFormat.Format32bppPArgb))
            {
                DrawFlat(flat, c, anim, keys, k, m, now, null);
                // proyeccion con la inclinacion
                var M = Mx.Mul(Mx.T(-at.X, -at.Y), Mx.S(zoom), Mx.T(-x0, -y0), L);
                var full = Mx.Mul(M, new double[] { 1.0 / k, 0, -m, 0, 1.0 / k, -m, 0, 0, 1 });
                Preview.Warp(flat, dest, Mx.Inv(full), new Rectangle(0, 0, at.Width, at.Height));
            }
            return dest;
        }
        // dibuja las teclas "en plano" (sin inclinacion) en un bitmap con supermuestreo k y margen m.
        // Lo usan la vista previa del editor y el visor en pantalla: un solo sitio para el orden de capas.
        public static void DrawFlat(Bitmap flat, Config c, Dictionary<string, KeyAnim> anim, IList<CKey> keys, int k, int m, long now, Action<Graphics> before)
        {
            using (var fg = Graphics.FromImage(flat))
            {
                fg.Clear(Color.Transparent);
                fg.SmoothingMode = SmoothingMode.AntiAlias; fg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                fg.CompositingQuality = CompositingQuality.HighQuality; fg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                fg.ScaleTransform(k, k); fg.TranslateTransform(m, m);
                if (before != null) before(fg);
                Func<string, KeyAnim> A = id => { KeyAnim a; anim.TryGetValue(id, out a); return a; };
                foreach (var key in keys) KeyPainter.DrawBase(fg, c, key, A(key.id), now);
                // imagen comun
                if (Images.Get(c.image.Shown()) != null)
                {
                    Bitmap layN = null, layP = null;
                    if (c.image.mode != "each")
                    {
                        bool anyAct = false; foreach (var key in keys) { var a = A(key.id); if (a != null && a.Active) { anyAct = true; break; } }
                        layN = KeyPainter.ImageLayer(c, flat.Width, flat.Height, k, m, c.image.opacity / 100f);
                        layP = anyAct && c.image.pressedOpacity != c.image.opacity ? KeyPainter.ImageLayer(c, flat.Width, flat.Height, k, m, c.image.pressedOpacity / 100f) : layN;
                    }
                    string bm = c.image.blend ?? "normal";
                    if (bm == "normal") foreach (var key in keys) KeyPainter.DrawGlobalImage(fg, c, key, A(key.id), now, layN, layP, k, m);
                    else BlendPass(flat, fg, k, m, bm, lg => { foreach (var key in keys) KeyPainter.DrawGlobalImage(lg, c, key, A(key.id), now, layN, layP, k, m); });
                    if (layP != null && layP != layN) layP.Dispose();
                    if (layN != null) layN.Dispose();
                }
                // imagenes de cada tecla (cada modo de fusion en su propia capa)
                List<string> modes = null;
                foreach (var key in keys)
                {
                    if (key.img == null || Images.Get(key.img.Shown()) == null) continue;
                    string km = key.img.blend ?? "normal";
                    if (km == "normal") KeyPainter.DrawKeyImage(fg, c, key, A(key.id), now, k);
                    else { if (modes == null) modes = new List<string>(); if (!modes.Contains(km)) modes.Add(km); }
                }
                if (modes != null)
                    foreach (var km in modes)
                    {
                        string mode = km;
                        BlendPass(flat, fg, k, m, mode, lg => { foreach (var key in keys) if (key.img != null && (key.img.blend ?? "normal") == mode) KeyPainter.DrawKeyImage(lg, c, key, A(key.id), now, k); });
                    }
                foreach (var key in keys) KeyPainter.DrawTop(fg, c, key, A(key.id), now);
            }
        }
        static void BlendPass(Bitmap flat, Graphics fg, int k, int m, string mode, Action<Graphics> draw)
        {
            using (var layer = new Bitmap(flat.Width, flat.Height, PixelFormat.Format32bppPArgb))
            {
                using (var lg = Graphics.FromImage(layer))
                {
                    lg.Clear(Color.Transparent);
                    lg.SmoothingMode = SmoothingMode.AntiAlias; lg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    lg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    lg.ScaleTransform(k, k); lg.TranslateTransform(m, m);
                    draw(lg);
                }
                fg.Flush();
                KeyPainter.Blend(flat, layer, mode);
            }
        }
    }

    // =====================================================================================
    // VISOR EN PANTALLA
    //  · El editor pre-dibuja cada fotograma de la animacion de cada tecla (una sola vez por diseño).
    //  · Un hilo propio, con prioridad alta, recibe las teclas (Raw Input: no se mete en medio del
    //    juego) y solo pega esos fotogramas ya hechos en una ventana transparente. Responde en el
    //    mismo instante de la pulsacion y, sin teclas animandose, duerme sin gastar nada.
    // =====================================================================================
    public sealed class Spr { public Bitmap B; public Rectangle At; }
    public sealed class SpriteBank
    {
        public Size Full; public int Q, PressN, RelN, Scale;
        public string[] Ids; public int[][] Codes;
        public Spr[] Up, Down; public Spr[][] Press, Rel;
        public Bitmap Hint;   // marco y texto del modo "desbloqueado"
        public void Dispose()
        {
            Action<Spr> d = s => { if (s != null && s.B != null) s.B.Dispose(); };
            if (Up != null) foreach (var s in Up) d(s);
            if (Down != null) foreach (var s in Down) d(s);
            if (Press != null) foreach (var a in Press) if (a != null) foreach (var s in a) d(s);
            if (Rel != null) foreach (var a in Rel) if (a != null) foreach (var s in a) d(s);
            if (Hint != null) Hint.Dispose();
        }
    }

    // ---------------- lado del editor: prepara los fotogramas y controla la ventana ----------------
    public class ScreenOverlay
    {
        Config src;
        public bool Locked = true;
        public int ScalePct = 100, OpacityPct = 100, Fps = 60;
        public bool Animate = true;   // false = modo rendimiento: la tecla cambia de golpe, sin fotogramas intermedios
        public Action<Point> Moved;
        public Action<int, Point> Resized;   // tamaño cambiado con los tiradores (porcentaje, posicion)
        OverlayThread th;
        readonly Timer work;
        readonly Queue<Action> jobs = new Queue<Action>();
        SpriteBank building; bool published, rebuild; long cfgAt;
        Point loc; bool closed;
        long budget;

        public ScreenOverlay(Config c)
        {
            src = c;
            work = new Timer { Interval = 10 };
            work.Tick += (s, e) => Work();
        }
        public bool Visible { get { return th != null && !closed; } }
        public bool IsDisposed { get { return closed; } }
        public Point Location { get { return loc; } set { loc = value; if (th != null) th.MoveTo(value); } }
        public int Left { get { return loc.X; } }
        public int Top { get { return loc.Y; } }
        Size CurSize { get { PointF o; return KeyboardRender.FullSize(src, ScalePct / 100f * App.Dpi, out o); } }
        public int Width { get { return CurSize.Width; } }
        public int Height { get { return CurSize.Height; } }

        public void Show()
        {
            if (th != null || closed) return;
            th = new OverlayThread(loc, Locked, OpacityPct, System.Threading.SynchronizationContext.Current);
            th.Moved = p => { loc = p; if (Moved != null) Moved(p); };
            th.Resized = (sc, p) => { loc = p; ScalePct = sc; if (Resized != null) Resized(sc, p); Redraw(); };
            th.Start();
            Build();
        }
        public void Close()
        {
            if (closed) return;
            closed = true;
            work.Stop(); work.Dispose(); jobs.Clear();
            if (building != null && !published) building.Dispose();
            building = null;
            if (th != null) th.Stop();
            th = null;
        }
        // cambios del diseño: se espera a que pare de cambiar (p.ej. al arrastrar un deslizador)
        public void SetConfig(Config c) { src = c; RedrawLater(); }
        public void RedrawLater() { if (closed) return; rebuild = true; cfgAt = KeyPainter.Now; work.Start(); }
        public void Redraw() { if (closed) return; rebuild = false; Build(); }
        public void Repaint() { if (th != null) th.SetAlpha(OpacityPct); }
        public void SetLocked(bool l) { Locked = l; if (th != null) th.SetLocked(l); }

        void Work()
        {
            if (rebuild && KeyPainter.Now - cfgAt >= 150) { rebuild = false; Build(); }
            var sw = Stopwatch.StartNew();
            while (jobs.Count > 0 && sw.ElapsedMilliseconds < 6) { try { jobs.Dequeue()(); } catch { } }
            if (jobs.Count == 0 && !rebuild) work.Stop();
        }

        void Build()
        {
            if (th == null || closed) return;
            jobs.Clear();
            if (building != null && !published) building.Dispose();   // uno a medias que nunca llego a verse
            Config c;
            try { c = Json.Parse(Json.Write(src)); } catch { return; }   // copia: el editor puede seguir cambiando el suyo
            float zoom = ScalePct / 100f * App.Dpi;
            var b = new SpriteBank();
            PointF o; b.Full = KeyboardRender.FullSize(c, zoom, out o);
            b.Q = Math.Max(4, 1000 / Math.Max(15, Fps));
            // cuanto duran las animaciones con los efectos que estan activos (mismos tiempos que KeyPainter.State)
            var ef = c.effects; double sp = Math.Max(10, ef.speed) / 100.0, dt = 80;
            if (ef.flash) dt = Math.Max(dt, 320); if (ef.ripple) dt = Math.Max(dt, 450); if (ef.ripple2) dt = Math.Max(dt, 570);
            if (ef.sparks) dt = Math.Max(dt, 600); if (ef.confetti) dt = Math.Max(dt, 800); if (ef.shake) dt = Math.Max(dt, 260);
            b.PressN = Animate ? (int)Math.Ceiling(dt / sp / b.Q) : 0;
            b.RelN = Animate ? (int)Math.Ceiling(220 / sp / b.Q) : 0;
            int n = c.keys.Count;
            b.Ids = new string[n]; b.Codes = new int[n][]; b.Up = new Spr[n]; b.Down = new Spr[n]; b.Press = new Spr[n][]; b.Rel = new Spr[n][];
            for (int i = 0; i < n; i++) { b.Ids[i] = c.keys[i].id; b.Codes[i] = c.keys[i].codes.ToArray(); b.Press[i] = new Spr[b.PressN]; b.Rel[i] = new Spr[b.RelN]; }
            b.Hint = MakeHint(b.Full); b.Scale = ScalePct;
            building = b; published = false; budget = 220L * 1024 * 1024;
            // 1) estados quietos de todas las teclas -> ya se puede mostrar
            for (int i = 0; i < n; i++)
            {
                int k = i;
                jobs.Enqueue(() => { b.Up[k] = Render(c, zoom, c.keys[k], false, 100000); });
                jobs.Enqueue(() => { b.Down[k] = Render(c, zoom, c.keys[k], true, 100000); });
            }
            jobs.Enqueue(() => { if (th != null && building == b) { th.SetBank(b); published = true; } });
            // 2) fotogramas de pulsar y de soltar (primero los del principio, que son los que mas se ven)
            int max = Math.Max(b.PressN, b.RelN);
            for (int f = 0; f < max; f++)
                for (int i = 0; i < n; i++)
                {
                    int k = i, fr = f;
                    if (fr < b.PressN) jobs.Enqueue(() => { if (budget > 0) b.Press[k][fr] = Render(c, zoom, c.keys[k], true, (fr + 1) * b.Q); });
                    if (fr < b.RelN) jobs.Enqueue(() => { if (budget > 0) b.Rel[k][fr] = Render(c, zoom, c.keys[k], false, (fr + 1) * b.Q); });
                }
            work.Start();
        }
        Spr Render(Config c, float zoom, CKey k, bool act, long ms)
        {
            long now = KeyPainter.Now;
            var d = new Dictionary<string, KeyAnim>();
            d[k.id] = act ? new KeyAnim { Active = true, PressAt = now - ms, ReleaseAt = now - 200000 }
                          : new KeyAnim { Active = false, PressAt = now - 200000, ReleaseAt = now - ms };
            Rectangle at;
            var bmp = KeyboardRender.RenderKey(c, d, zoom, k, out at);
            budget -= (long)bmp.Width * bmp.Height * 4;
            return new Spr { B = bmp, At = at };
        }
        static Bitmap MakeHint(Size s)
        {
            var bmp = new Bitmap(Math.Max(1, s.Width), Math.Max(1, s.Height), PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp)) { g.Clear(Color.Transparent); DrawFrame(g, s, true); }
            return bmp;
        }
        // modo desbloqueado: marco con tiradores como en el editor (esquinas y lados = tamaño, el resto = mover)
        public const int Grip = 9;
        public static Point[] Grips(Size s)
        {
            int w = s.Width - 1, h = s.Height - 1, g = Grip / 2 + 1;
            return new[] { new Point(g, g), new Point(w / 2, g), new Point(w - g, g), new Point(w - g, h / 2),
                           new Point(w - g, h - g), new Point(w / 2, h - g), new Point(g, h - g), new Point(g, h / 2) };
        }
        public static void DrawFrame(Graphics g, Size s, bool text)
        {
            // un velo casi invisible: sin el, los clics en las zonas vacias atravesarian la ventana
            using (var b = new SolidBrush(Color.FromArgb(28, 20, 120, 200))) g.FillRectangle(b, 0, 0, s.Width, s.Height);
            int gp = Grip / 2 + 1;
            using (var p = new Pen(Color.FromArgb(230, 76, 195, 255), 1.2f) { DashStyle = DashStyle.Dash })
                g.DrawRectangle(p, gp, gp, s.Width - 1 - 2 * gp, s.Height - 1 - 2 * gp);
            using (var fill = new SolidBrush(Color.White))
            using (var pen = new Pen(Color.FromArgb(255, 40, 150, 230), 1.5f))
                foreach (var q in Grips(s))
                {
                    var r = new Rectangle(q.X - Grip / 2, q.Y - Grip / 2, Grip, Grip);
                    g.FillRectangle(fill, r); g.DrawRectangle(pen, r);
                }
            if (!text) return;
            using (var f = new Font(App.UiFamily, 9f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var tb = new SolidBrush(Color.White))
                g.DrawString("Arrastra para mover · esquinas y lados: tamaño · Ctrl+Alt+L para bloquear", f, tb, new RectangleF(Grip + 6, Grip + 4, s.Width - 2 * Grip - 12, 18));
        }
    }

    // ---------------- hilo del visor: entrada + ventana transparente ----------------
    sealed class OverlayThread
    {
        [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr hwnd; public int message; public IntPtr wParam, lParam; public int time, x, y; }
        [StructLayout(LayoutKind.Sequential)] struct PT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] struct SZ { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int l, t, r, b; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLEND { public byte op, flags, alpha, format; }
        [StructLayout(LayoutKind.Sequential)] struct BIH { public int size, w, h; public short planes, bits; public int comp, sizeImg, xppm, yppm, used, important; }
        [DllImport("user32.dll")] static extern bool PeekMessage(out MSG m, IntPtr h, uint a, uint b, uint remove);
        [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG m);
        [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG m);
        [DllImport("user32.dll")] static extern uint MsgWaitForMultipleObjectsEx(uint n, IntPtr handles, uint ms, uint mask, uint flags);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, IntPtr pos, ref SZ size, IntPtr src, ref PT srcPos, int key, ref BLEND b, int flags);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
        [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr dc, ref BIH bmi, uint usage, out IntPtr bits, IntPtr sec, uint off);
        [DllImport("winmm.dll")] static extern int timeBeginPeriod(int ms);
        [DllImport("winmm.dll")] static extern int timeEndPeriod(int ms);
        const int WM_WAKE = 0x8000 + 78, WS_EX_TRANSPARENT = 0x20;

        public Action<Point> Moved; public Action<int, Point> Resized;
        readonly System.Threading.SynchronizationContext ui;
        readonly object gate = new object();
        readonly Queue<Action> cmds = new Queue<Action>();
        System.Threading.Thread t; volatile bool run; Win win;
        SpriteBank bank; Point pos; bool locked, force = true, hiRes, dirty = true; byte alpha;
        delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int obj, int child, uint thread, uint time);
        [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc proc, uint pid, uint tid, uint flags);
        [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr h);
        WinEventProc fgProc; IntPtr fgHook;
        readonly HashSet<int> down = new HashSet<int>();
        readonly Dictionary<string, KeyAnim> anim = new Dictionary<string, KeyAnim>();
        readonly Stopwatch clock = Stopwatch.StartNew();
        Spr[] shown = new Spr[0], pick = new Spr[0];
        IntPtr memDC, dib, bits; Bitmap surf; Graphics gs; Size dibSize;
        Action<int, bool> onRaw;

        public OverlayThread(Point p, bool l, int opacity, System.Threading.SynchronizationContext ctx)
        { pos = p; locked = l; alpha = Alpha(opacity); ui = ctx; }
        static byte Alpha(int pct) { return (byte)Math.Max(10, Math.Min(255, pct * 255 / 100)); }

        public void Start()
        {
            run = true;
            var ready = new System.Threading.ManualResetEvent(false);
            t = new System.Threading.Thread(() => { try { Init(); } catch { } ready.Set(); try { Loop(); } catch { } Cleanup(); })
            { IsBackground = true, Name = "kos-visor" };
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.Start();
            ready.WaitOne(2000);
        }
        public void Stop() { run = false; Wake(); if (t != null) t.Join(1500); t = null; }
        void Post(Action a) { lock (gate) cmds.Enqueue(a); Wake(); }
        void Wake() { var w = win; if (w != null && w.Handle != IntPtr.Zero) PostMessage(w.Handle, WM_WAKE, IntPtr.Zero, IntPtr.Zero); }
        public void SetBank(SpriteBank b)
        {
            Post(() =>
            {
                var old = bank; bank = b; hold = false;
                shown = new Spr[b.Ids.Length]; pick = new Spr[b.Ids.Length];
                Refresh(); force = true;
                if (old != null) old.Dispose();
            });
        }
        public void SetAlpha(int pct) { Post(() => { alpha = Alpha(pct); force = true; }); }
        public void MoveTo(Point p) { Post(() => { pos = p; if (win != null) SetWindowPos(win.Handle, new IntPtr(-1), p.X, p.Y, 0, 0, 0x1 | 0x10); }); }
        public void SetLocked(bool l)
        {
            Post(() =>
            {
                locked = l; force = true;
                int ex = GetWindowLong(win.Handle, -20);
                SetWindowLong(win.Handle, -20, l ? ex | WS_EX_TRANSPARENT : ex & ~WS_EX_TRANSPARENT);
            });
        }

        void Init()
        {
            win = new Win(this);
            win.CreateHandle(new CreateParams
            {
                Caption = "Keyboard Overlay Studio - visor", X = pos.X, Y = pos.Y, Width = 1, Height = 1,
                Style = unchecked((int)0x80000000),                                            // WS_POPUP
                ExStyle = 0x80000 | 0x80 | 0x8000000 | 0x8 | (locked ? WS_EX_TRANSPARENT : 0)  // capas, sin barra de tareas, sin foco, encima
            });
            ShowWindow(win.Handle, 4);   // SW_SHOWNOACTIVATE
            // las teclas llegan del lector comun (InputHub); aqui solo se encolan y se despierta el bucle
            onRaw = (vc, d) => Post(() => Key(vc, d));
            InputHub.Raw += onRaw;
            // volver a ponerse encima solo cuando cambia la ventana activa (no con un temporizador)
            fgProc = (h, ev, hw, o, c, tid, tm) => { if (win != null) SetWindowPos(win.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x1 | 0x2 | 0x10 | 0x200); };
            fgHook = SetWinEventHook(3, 3, IntPtr.Zero, fgProc, 0, 0, 0x2);   // EVENT_SYSTEM_FOREGROUND, fuera de proceso, sin el nuestro
        }
        void Cleanup()
        {
            if (onRaw != null) { InputHub.Raw -= onRaw; onRaw = null; }
            if (fgHook != IntPtr.Zero) { UnhookWinEvent(fgHook); fgHook = IntPtr.Zero; }
            HiRes(false);
            FreeDib();
            if (bank != null) { bank.Dispose(); bank = null; }
            try { if (win != null) win.DestroyHandle(); } catch { }
            win = null;
        }

        void Loop()
        {
            long nextFrame = 0; bool animating = false;
            while (run)
            {
                MSG m;
                while (PeekMessage(out m, IntPtr.Zero, 0, 0, 1)) { TranslateMessage(ref m); DispatchMessage(ref m); }
                for (;;) { Action a; lock (gate) { if (cmds.Count == 0) break; a = cmds.Dequeue(); } dirty = true; try { a(); } catch { } }
                if (!run) break;
                long now = clock.ElapsedMilliseconds;
                // solo se dibuja si cambio una tecla o toca el siguiente fotograma de una animacion
                if (dirty || (animating && now >= nextFrame))
                {
                    dirty = false;
                    animating = Frame(now);
                    nextFrame = now + (bank != null ? bank.Q : 16);
                }
                HiRes(animating);
                uint wait = animating ? (uint)Math.Max(1, nextFrame - clock.ElapsedMilliseconds) : 0xFFFFFFFF;
                MsgWaitForMultipleObjectsEx(0, IntPtr.Zero, wait, 0x4FF, 0x4);   // duerme; despierta al instante con una tecla
            }
        }
        void HiRes(bool on)
        {
            if (on == hiRes) return;
            try { if (on) timeBeginPeriod(1); else timeEndPeriod(1); hiRes = on; } catch { }
        }

        // ---- entrada ----
        void Key(int vc, bool d)
        {
            if (d ? !down.Add(vc) : !down.Remove(vc)) return;   // sin auto-repeticion
            Refresh();
        }
        // recalcula que teclas del diseño estan pulsadas
        void Refresh()
        {
            if (bank == null) return;
            long now = clock.ElapsedMilliseconds;
            for (int i = 0; i < bank.Ids.Length; i++)
            {
                bool on = false;
                foreach (var c in bank.Codes[i]) if (down.Contains(c)) { on = true; break; }
                KeyAnim a; if (!anim.TryGetValue(bank.Ids[i], out a)) { a = new KeyAnim { PressAt = -1000000, ReleaseAt = -1000000 }; anim[bank.Ids[i]] = a; }
                if (on && !a.Active) { a.Active = true; a.PressAt = now; dirty = true; }
                else if (!on && a.Active) { a.Active = false; a.ReleaseAt = now; dirty = true; }
            }
        }

        // ---- dibujo: elegir el fotograma de cada tecla y pegarlo ----
        static Spr Pick(Spr[] frames, long idx, Spr settled)
        {
            if (idx >= frames.Length) return settled;
            for (long i = idx; i >= 0; i--) if (frames[i] != null) return frames[i];   // aun no dibujado: el anterior que haya
            return settled;
        }
        bool Frame(long now)
        {
            if (hold && now - holdAt > 4000) { hold = false; force = true; }   // por si los fotogramas nuevos no llegan
            var b = bank; if (b == null || win == null || hold || drag >= 0) return false;
            bool anim = false, changed = force;
            int n = b.Ids.Length;
            for (int i = 0; i < n; i++)
            {
                KeyAnim a; this.anim.TryGetValue(b.Ids[i], out a);
                Spr s;
                if (a != null && a.Active)
                {
                    long idx = (now - a.PressAt) / b.Q;
                    s = Pick(b.Press[i], idx, b.Down[i]); if (idx < b.PressN) anim = true;
                }
                else if (a != null && now - a.ReleaseAt < (long)b.RelN * b.Q)
                {
                    s = Pick(b.Rel[i], (now - a.ReleaseAt) / b.Q, b.Up[i]); anim = true;
                }
                else s = b.Up[i];
                pick[i] = s;
                if (i >= shown.Length || !ReferenceEquals(shown[i], s))
                {
                    // zona que cambia: solo esa se redibuja y se manda a Windows
                    if (s != null) dirtyR = dirtyR.IsEmpty ? s.At : Rectangle.Union(dirtyR, s.At);
                    var o = i < shown.Length ? shown[i] : null;
                    if (o != null) dirtyR = dirtyR.IsEmpty ? o.At : Rectangle.Union(dirtyR, o.At);
                    changed = true;
                }
            }
            if (!changed) return anim;
            bool all = force || surf == null || dibSize != b.Full;
            force = false; var tmp = shown; shown = pick; pick = tmp;
            if (!EnsureDib(b.Full)) return anim;
            var r = all ? new Rectangle(Point.Empty, dibSize) : Rectangle.Intersect(dirtyR, new Rectangle(Point.Empty, dibSize));
            dirtyR = Rectangle.Empty;
            if (r.Width <= 0 || r.Height <= 0) return anim;
            gs.SetClip(r);
            gs.CompositingMode = CompositingMode.SourceCopy;   // borrar solo la zona que cambia
            using (var clr = new SolidBrush(Color.Transparent)) gs.FillRectangle(clr, r);
            gs.CompositingMode = CompositingMode.SourceOver;
            if (!locked) gs.DrawImageUnscaled(b.Hint, 0, 0);
            foreach (var s in shown) if (s != null && s.B != null && s.At.IntersectsWith(r)) gs.DrawImageUnscaled(s.B, s.At.X, s.At.Y);
            gs.ResetClip();
            gs.Flush();
            Push(r, all);
            return anim;
        }
        Rectangle dirtyR = Rectangle.Empty;

        // ---- cambiar el tamaño con los tiradores (modo desbloqueado) ----
        int drag = -1; bool hold; long holdAt; Rectangle dragR0; Point dragM0; Bitmap dragKeys;
        [DllImport("user32.dll")] static extern bool GetCursorPos(out PT p);
        [DllImport("user32.dll")] static extern IntPtr SetCapture(IntPtr h);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr LoadCursor(IntPtr inst, int id);
        [DllImport("user32.dll")] static extern IntPtr SetCursor(IntPtr c);
        static readonly int[] gsx = { -1, 0, 1, 1, 1, 0, -1, -1 }, gsy = { -1, -1, -1, 0, 1, 1, 1, 0 };
        static readonly int[] gcur = { 32642, 32645, 32643, 32644, 32642, 32645, 32643, 32644 };   // flechas de redimensionar
        int GripAt(Point screen)
        {
            RECT r; if (bank == null || !GetWindowRect(win.Handle, out r)) return -1;
            var g = ScreenOverlay.Grips(new Size(r.r - r.l, r.b - r.t));
            for (int i = 0; i < g.Length; i++)
                if (Math.Abs(screen.X - r.l - g[i].X) <= 11 && Math.Abs(screen.Y - r.t - g[i].Y) <= 11) return i;
            return -1;
        }
        void DragStart(int grip)
        {
            RECT r; if (!GetWindowRect(win.Handle, out r) || surf == null) return;
            drag = grip; dragR0 = new Rectangle(r.l, r.t, r.r - r.l, r.b - r.t);
            PT m; GetCursorPos(out m); dragM0 = new Point(m.x, m.y);
            // foto solo de las teclas (sin el marco) para estirarla mientras se arrastra
            dragKeys = new Bitmap(dibSize.Width, dibSize.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(dragKeys)) foreach (var s in shown) if (s != null && s.B != null) g.DrawImageUnscaled(s.B, s.At.X, s.At.Y);
            SetCapture(win.Handle);
        }
        double DragFactor()
        {
            PT m; GetCursorPos(out m);
            int dx = m.x - dragM0.X, dy = m.y - dragM0.Y, sx = gsx[drag], sy = gsy[drag];
            double f;
            if (sx != 0 && sy != 0) f = Math.Max((dragR0.Width + sx * dx) / (double)dragR0.Width, (dragR0.Height + sy * dy) / (double)dragR0.Height);
            else if (sx != 0) f = (dragR0.Width + sx * dx) / (double)dragR0.Width;
            else f = (dragR0.Height + sy * dy) / (double)dragR0.Height;
            int sc0 = bank != null ? bank.Scale : 100;
            return Math.Max(25.0 / sc0, Math.Min(300.0 / sc0, f));
        }
        Rectangle DragRect(double f)
        {
            int w = Math.Max(20, (int)Math.Round(dragR0.Width * f)), h = Math.Max(20, (int)Math.Round(dragR0.Height * f));
            int sx = gsx[drag], sy = gsy[drag];
            int x = sx < 0 ? dragR0.Right - w : sx > 0 ? dragR0.X : dragR0.X + (dragR0.Width - w) / 2;
            int y = sy < 0 ? dragR0.Bottom - h : sy > 0 ? dragR0.Y : dragR0.Y + (dragR0.Height - h) / 2;
            return new Rectangle(x, y, w, h);
        }
        unsafe void DragMove()
        {
            var nr = DragRect(DragFactor());
            if (!EnsureDib(nr.Size)) return;
            gs.CompositingMode = CompositingMode.SourceCopy;
            using (var clr = new SolidBrush(Color.Transparent)) gs.FillRectangle(clr, 0, 0, nr.Width, nr.Height);
            gs.CompositingMode = CompositingMode.SourceOver;
            ScreenOverlay.DrawFrame(gs, nr.Size, false);
            gs.InterpolationMode = InterpolationMode.Bilinear;
            gs.DrawImage(dragKeys, 0, 0, nr.Width, nr.Height);
            gs.InterpolationMode = InterpolationMode.NearestNeighbor;
            gs.Flush();
            var size = new SZ { cx = nr.Width, cy = nr.Height }; var src = new PT(); var dst = new PT { x = nr.X, y = nr.Y };
            var bl = new BLEND { op = 0, flags = 0, alpha = alpha, format = 1 };
            UpdateLayeredWindow(win.Handle, IntPtr.Zero, (IntPtr)(&dst), ref size, memDC, ref src, 0, ref bl, 2);
            pos = nr.Location;
        }
        void DragEnd()
        {
            int sc0 = bank != null ? bank.Scale : 100;
            double f = DragFactor();
            var nr = DragRect(f);
            int sc = (int)Math.Round(sc0 * f);
            drag = -1;          // antes de soltar la captura (eso vuelve a avisar a esta ventana)
            ReleaseCapture();
            if (dragKeys != null) { dragKeys.Dispose(); dragKeys = null; }
            pos = nr.Location;
            if (sc == sc0) { force = true; dirty = true; SetWindowPos(win.Handle, new IntPtr(-1), pos.X, pos.Y, 0, 0, 0x1 | 0x10); return; }
            hold = true; holdAt = clock.ElapsedMilliseconds;   // se queda la vista estirada hasta que llegan los fotogramas al tamaño nuevo
            var cb = Resized; var p = pos;
            if (cb != null && ui != null) ui.Post(_ => cb(sc, p), null);
        }
        [StructLayout(LayoutKind.Sequential)] struct ULWINFO { public int cbSize; public IntPtr hdcDst, pptDst, psize, hdcSrc, pptSrc; public int crKey; public IntPtr pblend; public int dwFlags; public IntPtr prcDirty; }
        [DllImport("user32.dll")] static extern bool UpdateLayeredWindowIndirect(IntPtr h, ref ULWINFO info);
        unsafe void Push(Rectangle r, bool all)
        {
            var size = new SZ { cx = dibSize.Width, cy = dibSize.Height }; var src = new PT();
            var bl = new BLEND { op = 0, flags = 0, alpha = alpha, format = 1 };
            var rc = new RECT { l = r.Left, t = r.Top, r = r.Right, b = r.Bottom };
            var info = new ULWINFO
            {
                cbSize = Marshal.SizeOf(typeof(ULWINFO)), psize = (IntPtr)(&size), hdcSrc = memDC, pptSrc = (IntPtr)(&src),
                pblend = (IntPtr)(&bl), dwFlags = 2, prcDirty = all ? IntPtr.Zero : (IntPtr)(&rc)
            };
            if (!UpdateLayeredWindowIndirect(win.Handle, ref info))
                UpdateLayeredWindow(win.Handle, IntPtr.Zero, IntPtr.Zero, ref size, memDC, ref src, 0, ref bl, 2);
        }
        // superficie de dibujo compartida con Windows (sin copias por fotograma)
        bool EnsureDib(Size s)
        {
            if (surf != null && dibSize == s) return true;
            FreeDib();
            var bih = new BIH { size = 40, w = s.Width, h = -s.Height, planes = 1, bits = 32 };
            dib = CreateDIBSection(IntPtr.Zero, ref bih, 0, out bits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero) return false;
            memDC = CreateCompatibleDC(IntPtr.Zero); SelectObject(memDC, dib);
            surf = new Bitmap(s.Width, s.Height, s.Width * 4, PixelFormat.Format32bppPArgb, bits);
            gs = Graphics.FromImage(surf);
            gs.CompositingMode = CompositingMode.SourceOver; gs.CompositingQuality = CompositingQuality.HighSpeed;
            gs.InterpolationMode = InterpolationMode.NearestNeighbor; gs.PixelOffsetMode = PixelOffsetMode.None;
            dibSize = s;
            return true;
        }
        void FreeDib()
        {
            if (gs != null) { gs.Dispose(); gs = null; }
            if (surf != null) { surf.Dispose(); surf = null; }
            if (memDC != IntPtr.Zero) { DeleteDC(memDC); memDC = IntPtr.Zero; }
            if (dib != IntPtr.Zero) { DeleteObject(dib); dib = IntPtr.Zero; }
        }

        sealed class Win : NativeWindow
        {
            readonly OverlayThread o;
            public Win(OverlayThread o) { this.o = o; }
            protected override void WndProc(ref Message m)
            {
                switch (m.Msg)
                {
                    case WM_WAKE: return;
                    case 0x84:   // WM_NCHITTEST: tiradores = nuestros (tamaño), el resto = arrastrar la ventana
                        if (!o.locked)
                        {
                            long lp = m.LParam.ToInt64();
                            var pt = new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
                            m.Result = new IntPtr(o.drag >= 0 || o.GripAt(pt) >= 0 ? 1 : 2);
                            return;
                        }
                        break;
                    case 0x20:   // WM_SETCURSOR: flechas de tamaño sobre los tiradores, cruz de mover en el resto
                        if (!o.locked)
                        {
                            PT cp; GetCursorPos(out cp);
                            int gi = o.drag >= 0 ? o.drag : o.GripAt(new Point(cp.x, cp.y));
                            SetCursor(LoadCursor(IntPtr.Zero, gi >= 0 ? gcur[gi] : 32646));
                            m.Result = new IntPtr(1); return;
                        }
                        break;
                    case 0x201:   // WM_LBUTTONDOWN sobre un tirador
                        { PT cp; GetCursorPos(out cp); int gi = o.GripAt(new Point(cp.x, cp.y)); if (gi >= 0 && !o.locked) o.DragStart(gi); }
                        return;
                    case 0x200: if (o.drag >= 0) o.DragMove(); return;   // WM_MOUSEMOVE
                    case 0x202: if (o.drag >= 0) o.DragEnd(); return;    // WM_LBUTTONUP
                    case 0x215: if (o.drag >= 0) o.DragEnd(); break;     // WM_CAPTURECHANGED
                    case 0x21: m.Result = new IntPtr(3); return;                               // WM_MOUSEACTIVATE -> no robar el foco
                    case 0x232:                                                                 // WM_EXITSIZEMOVE
                        RECT r;
                        if (GetWindowRect(Handle, out r)) { o.pos = new Point(r.l, r.t); var cb = o.Moved; var p = o.pos; if (cb != null && o.ui != null) o.ui.Post(_ => cb(p), null); }
                        break;
                }
                base.WndProc(ref m);
            }
        }
    }
}
