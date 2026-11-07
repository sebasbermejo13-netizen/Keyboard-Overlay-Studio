// Keyboard Overlay Studio - modelo, tema, utilidades, fuentes y dibujo de teclas
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace KOStudio
{
    // ================================================================
    // MODELO (mismos nombres que lee overlay.html / kb-render.js)
    // ================================================================
    public class CCanvas { public int width = 1000; public int height = 1000; }
    public class CPosition { public string anchor = "bottom-left"; public int x = 40; public int y = 40; public double scale = 1; }
    public class CTilt { public string mode = "center"; public int amount = 100; }
    public class CFont { public string family = "Quicksand"; public int weight = 700; public int size = 28; public int smallSize = 19; public double letterSpacing = 1.6; public int iconGap = 8; public int iconScale = 100; public int offsetX, offsetY; }
    public class CShape { public int radius = 18; public int border = 2; public int depth = 8; public int press = 6; }
    public class CColors
    {
        public string capTop = "#3a3f46ff", capBottom = "#1b1e22ff", border = "#ffffff2e", text = "#ffffffff",
            icon = "#d99a3aff", body = "#1b1e22cc", bodyBottom = "#0d0f1299",
            activeTop = "#f0a24aff", activeBottom = "#ce5b29ff", activeBorder = "#ffd2a0cc",
            activeText = "#2b1400ff", activeBody = "#963e12bf", activeBodyBottom = "#6426088c",
            glow = "#ce5b2973", ripple = "#f0a24aff",
            bar = "#ffffff73", activeBar = "#00000059";
    }
    public class CEffects
    {
        public bool body = true, flash = true, ripple = true, glow = true, bounce = true, textShadow = true;
        public string pressMode = "sink";          // sink hundir | shrink encoger | pop crecer | tilt inclinar | jump saltar | none
        public bool ripple2, sparks, confetti, neon, textGlow, textPop, shake;
        public int speed = 100;                    // % velocidad de las animaciones
    }
    // imagen comun a TODO el teclado (encima de la cara de las teclas, debajo de la letra)
    public class CImage
    {
        public string src = "";
        public int iw, ih;                 // tamano original (para calcular el ajuste igual en OBS)
        public string mode = "span";       // span = una sola imagen repartida por todas las teclas | each = la misma en cada tecla
        public string fit = "cover";       // cover | contain | stretch
        public int opacity = 100, pressedOpacity = 35;
        public int zoom = 100, offsetX = 0, offsetY = 0;   // (versiones anteriores)
        // transformacion libre (boton Transformar): centro, tamano y giro en px de las teclas
        public bool custom;
        public double cx, cy, w, h, rot;
        // modo de fusion (como Photoshop/Photopea) y Tono/Saturacion
        public string blend = "normal";
        public int hue, sat, light;
        public bool colorize;
        public string fx = "";             // imagen ya con Tono/Saturacion aplicados (la usa OBS)
        public string Shown() { return string.IsNullOrEmpty(fx) ? src : fx; }
    }
    public class ImgFrame { public double cx, cy, w, h, rot; }
    public class CKey
    {
        public string id;
        public string label = "";
        public string icon = "";
        public bool small, bar, hideText;
        public string textCase;                    // asis | upper | lower
        public string textPos = "center";          // center, top, bottom, left, right, top-left...
        public int textScale = 100;
        public int textX, textY;                   // desplazamiento propio del texto (px)
        public List<int> codes = new List<int>();
        public int x, y, w = 80, h = 94;
        public string image = "";
        public int imageOpacity = 100, imagePressedOpacity = 35;
        public string imageFit = "cover";
        public CImage img;   // imagen propia de la tecla (mismas opciones que la imagen comun)
    }
    public class Config
    {
        public int version = 1;
        public CCanvas canvas = new CCanvas();
        public CPosition position = new CPosition();
        public CTilt tilt = new CTilt();
        public CFont font = new CFont();
        public CShape shape = new CShape();
        public CColors colors = new CColors();
        public CEffects effects = new CEffects();
        public CImage image = new CImage();
        public List<CKey> keys = DefaultKeys();

        public static List<CKey> DefaultKeys()
        {
            return new List<CKey> {
                K("w", "W", "", false, false, new[] { 0x11 }, 86, 0, 80, 94),
                K("a", "A", "", false, false, new[] { 0x1E }, 0, 100, 80, 94),
                K("s", "S", "", false, false, new[] { 0x1F }, 86, 100, 80, 94),
                K("d", "D", "", false, false, new[] { 0x20 }, 172, 100, 80, 94),
                K("shift", "Shift", "⇧", true, false, new[] { 0x2A, 0x36 }, 258, 100, 108, 94),
                K("space", "", "", false, true, new[] { 0x39 }, 0, 200, 180, 70),
                K("ctrl", "Ctrl", "", true, false, new[] { 0x1D, 0x0E1D }, 186, 200, 180, 70)
            };
        }
        static CKey K(string id, string label, string icon, bool small, bool bar, int[] codes, int x, int y, int w, int h)
        {
            return new CKey { id = id, label = label, icon = icon, small = small, bar = bar, codes = new List<int>(codes), x = x, y = y, w = w, h = h };
        }

        public static string NewId() { return "k" + DateTime.Now.Ticks.ToString("x").Substring(6) + new Random().Next(1000, 9999); }

        public void Normalize()
        {
            if (canvas == null) canvas = new CCanvas();
            if (position == null) position = new CPosition();
            if (tilt == null) tilt = new CTilt();
            if (font == null) font = new CFont();
            if (shape == null) shape = new CShape();
            if (colors == null) colors = new CColors();
            if (effects == null) effects = new CEffects();
            if (string.IsNullOrEmpty(effects.pressMode)) effects.pressMode = "sink";
            if (font != null && (font.offsetX != 0 || font.offsetY != 0) && keys != null)
            {   // versiones anteriores: el desplazamiento era global, ahora va en cada tecla
                foreach (var k in keys) { k.textX += font.offsetX; k.textY += font.offsetY; }
                font.offsetX = 0; font.offsetY = 0;
            }
            if (effects.speed <= 0) effects.speed = 100;
            if (image == null) image = new CImage();
            if (image.src == null) image.src = "";
            if (image.fx == null) image.fx = "";
            if (string.IsNullOrEmpty(image.blend)) image.blend = "normal";
            if (keys == null) keys = new List<CKey>();
            var ids = new HashSet<string>();
            foreach (var k in keys)
            {
                if (string.IsNullOrEmpty(k.id) || ids.Contains(k.id)) k.id = NewId() + ids.Count;
                ids.Add(k.id);
                if (k.codes == null) k.codes = new List<int>();
                if (k.label == null) k.label = "";
                if (string.IsNullOrEmpty(k.textCase)) k.textCase = k.small ? "upper" : "asis";
                if (string.IsNullOrEmpty(k.textPos)) k.textPos = "center";
                if (k.textScale <= 0) k.textScale = 100;
                if (k.icon == null) k.icon = "";
                if (k.image == null) k.image = "";
                if (string.IsNullOrEmpty(k.imageFit)) k.imageFit = "cover";
                // versiones anteriores guardaban la imagen de la tecla en "image": se pasa a "img"
                if (!string.IsNullOrEmpty(k.image) && (k.img == null || string.IsNullOrEmpty(k.img.src)))
                    k.img = new CImage { src = k.image, opacity = k.imageOpacity, pressedOpacity = k.imagePressedOpacity, fit = k.imageFit, mode = "each" };
                k.image = "";
                if (k.img != null)
                {
                    if (string.IsNullOrEmpty(k.img.src)) k.img = null;
                    else { if (k.img.fx == null) k.img.fx = ""; if (string.IsNullOrEmpty(k.img.blend)) k.img.blend = "normal"; if (string.IsNullOrEmpty(k.img.fit)) k.img.fit = "cover"; }
                }
                k.w = Math.Max(10, k.w); k.h = Math.Max(10, k.h);
            }
            if (position.scale <= 0) position.scale = 1;
            canvas.width = Math.Max(50, canvas.width); canvas.height = Math.Max(50, canvas.height);
        }

        // deja la tecla de mas arriba/izquierda en 0,0 sin que nada se mueva en pantalla
        // (se compensa con la posicion del bloque). Devuelve cuanto se desplazaron las teclas.
        // transformacion teclas -> lienzo (sin inclinacion), para comparar antes/despues de un cambio
        public double[] Frame() { return Mx.LocalToCanvas(this, true); }
        // Despues de mover/redimensionar teclas: deja la de mas arriba/izquierda en 0,0 y corrige la
        // posicion del bloque para que todo quede EXACTAMENTE donde se puso respecto a "before"
        // (el marco de antes del cambio). Asi se puede mover cualquier tecla, o todas, en cualquier direccion.
        public Point Reanchor(double[] before)
        {
            if (keys.Count == 0 || before == null) return Point.Empty;
            var k0 = keys[0];
            var P = Mx.Ap(before, k0.x, k0.y);
            int mx = int.MaxValue, my = int.MaxValue;
            foreach (var k in keys) { mx = Math.Min(mx, k.x); my = Math.Min(my, k.y); }
            foreach (var k in keys) { k.x -= mx; k.y -= my; }
            if (image != null && image.custom) { image.cx -= mx; image.cy -= my; }
            var Q = Mx.Ap(Frame(), k0.x, k0.y);
            double dx = P.X - Q.X, dy = P.Y - Q.Y;
            string an = position.anchor ?? "bottom-left";
            if (an.EndsWith("left")) position.x += (int)Math.Round(dx);
            else if (an.EndsWith("right")) position.x -= (int)Math.Round(dx);
            if (an.StartsWith("top")) position.y += (int)Math.Round(dy);
            else if (an.StartsWith("bottom")) position.y -= (int)Math.Round(dy);
            return new Point(-mx, -my);
        }

        // rectangulo de la imagen comun en coordenadas de las teclas (modo "span")
        public RectangleF SpanRect()
        {
            var b = KeysBounds(); var im = image;
            float iw = Math.Max(1, im.iw), ih = Math.Max(1, im.ih), z = im.zoom / 100f;
            float w, h;
            if (im.fit == "stretch") { w = b.Width * z; h = b.Height * z; }
            else
            {
                float s = im.fit == "contain" ? Math.Min(b.Width / iw, b.Height / ih) : Math.Max(b.Width / iw, b.Height / ih);
                w = iw * s * z; h = ih * s * z;
            }
            return new RectangleF((b.Width - w) / 2 + b.Width * im.offsetX / 100f, (b.Height - h) / 2 + b.Height * im.offsetY / 100f, w, h);
        }

        public ImgFrame ImageFrame()
        {
            if (image.custom && image.w > 0 && image.h > 0) return new ImgFrame { cx = image.cx, cy = image.cy, w = image.w, h = image.h, rot = image.rot };
            var r = SpanRect();
            return new ImgFrame { cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, w = r.Width, h = r.Height, rot = 0 };
        }

        // marco de la imagen propia de una tecla (en coordenadas de las teclas)
        public ImgFrame KeyImageFrame(CKey k)
        {
            var im = k.img;
            float B = shape.border, depth = effects.body ? shape.depth : 0;
            var inner = new RectangleF(k.x + B, k.y + B, k.w - 2 * B, k.h - depth - 2 * B);
            if (im != null && im.custom && im.w > 0 && im.h > 0) return new ImgFrame { cx = k.x + im.cx, cy = k.y + im.cy, w = im.w, h = im.h, rot = im.rot };
            var img = im == null ? null : Images.Get(im.Shown());
            var r = KeyPainter.FitRect(inner, img == null ? inner.Width : img.Width, img == null ? inner.Height : img.Height, im == null ? "cover" : im.fit);
            return new ImgFrame { cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, w = r.Width, h = r.Height, rot = 0 };
        }

        public Config Clone() { return Json.Parse(Json.Write(this)); }

        public SizeF KeysBounds()
        {
            float w = 0, h = 0;
            foreach (var k in keys) { w = Math.Max(w, k.x + k.w); h = Math.Max(h, k.y + k.h); }
            return new SizeF(w, h);
        }
        public CKey Find(string id)
        {
            if (id == null) return null;
            foreach (var k in keys) if (k.id == id) return k;
            return null;
        }
    }

    public static class Json
    {
        static JavaScriptSerializer js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 200 };
        public static string Write(object o) { return js.Serialize(o); }
        public static Config Parse(string s) { var c = js.Deserialize<Config>(s); c.Normalize(); return c; }
        public static T Read<T>(string s) { return js.Deserialize<T>(s); }
        public static object Obj(string s) { return js.DeserializeObject(s); }

        public static string Pretty(string json)
        {
            var sb = new StringBuilder(json.Length + json.Length / 4);
            int ind = 0; bool str = false, escp = false;
            for (int i = 0; i < json.Length; i++)
            {
                char ch = json[i];
                if (str)
                {
                    sb.Append(ch);
                    if (escp) escp = false; else if (ch == '\\') escp = true; else if (ch == '"') str = false;
                    continue;
                }
                switch (ch)
                {
                    case '"': str = true; sb.Append(ch); break;
                    case '{':
                    case '[':
                        sb.Append(ch);
                        if (i + 1 < json.Length && (json[i + 1] == '}' || json[i + 1] == ']')) { sb.Append(json[i + 1]); i++; break; }
                        ind++; sb.Append('\n').Append(' ', ind * 2); break;
                    case '}':
                    case ']': ind--; sb.Append('\n').Append(' ', ind * 2).Append(ch); break;
                    case ',': sb.Append(ch).Append('\n').Append(' ', ind * 2); break;
                    case ':': sb.Append(": "); break;
                    default: sb.Append(ch); break;
                }
            }
            return sb.ToString();
        }
    }

    // ================================================================
    // TECLAS (codigos VC_* de libuiohook = los que manda input-overlay)
    // ================================================================
    public static class KeyCodes
    {
        public const int MOUSE = 0x10000;
        public class Item { public int VC; public string Name; public override string ToString() { return Name; } }
        public static readonly List<Item> List = new List<Item>();
        static readonly Dictionary<int, Item> byVC = new Dictionary<int, Item>();

        static void A(int vc, string n) { var it = new Item { VC = vc, Name = n }; List.Add(it); byVC[vc] = it; }
        static KeyCodes()
        {
            A(0x01, "Esc");
            for (int i = 1; i <= 10; i++) A(0x3A + i, "F" + i);
            A(0x57, "F11"); A(0x58, "F12");
            A(0x29, "º");
            A(0x02, "1"); A(0x03, "2"); A(0x04, "3"); A(0x05, "4"); A(0x06, "5"); A(0x07, "6"); A(0x08, "7"); A(0x09, "8"); A(0x0A, "9"); A(0x0B, "0");
            A(0x0C, "'"); A(0x0D, "¡"); A(0x0E, "Borrar"); A(0x0F, "Tab");
            string row1 = "QWERTYUIOP"; for (int i = 0; i < row1.Length; i++) A(0x10 + i, row1[i].ToString());
            A(0x1A, "`"); A(0x1B, "+"); A(0x1C, "Enter"); A(0x3A, "Bloq Mayús");
            string row2 = "ASDFGHJKL"; for (int i = 0; i < row2.Length; i++) A(0x1E + i, row2[i].ToString());
            A(0x27, "Ñ"); A(0x28, "´"); A(0x2B, "Ç"); A(0x2A, "Shift izq."); A(0x56, "<");
            string row3 = "ZXCVBNM"; for (int i = 0; i < row3.Length; i++) A(0x2C + i, row3[i].ToString());
            A(0x33, ","); A(0x34, "."); A(0x35, "-"); A(0x36, "Shift der.");
            A(0x1D, "Ctrl izq."); A(0x0E5B, "Windows"); A(0x38, "Alt"); A(0x39, "Espacio"); A(0x0E38, "Alt Gr");
            A(0x0E5C, "Windows der."); A(0x0E5D, "Menú"); A(0x0E1D, "Ctrl der.");
            A(0x0E37, "Impr Pant"); A(0x46, "Bloq Despl"); A(0x0E45, "Pausa");
            A(0x0E52, "Insert"); A(0x0E47, "Inicio"); A(0x0E49, "Re Pág"); A(0x0E53, "Supr"); A(0x0E4F, "Fin"); A(0x0E51, "Av Pág");
            A(0xE048, "Flecha arriba"); A(0xE04B, "Flecha izquierda"); A(0xE050, "Flecha abajo"); A(0xE04D, "Flecha derecha");
            A(0x45, "Bloq Num"); A(0x0E35, "Num /"); A(0x37, "Num *"); A(0x4A, "Num -"); A(0x4E, "Num +"); A(0x0E1C, "Num Enter"); A(0x53, "Num .");
            A(0x52, "Num 0"); A(0x4F, "Num 1"); A(0x50, "Num 2"); A(0x51, "Num 3"); A(0x4B, "Num 4"); A(0x4C, "Num 5"); A(0x4D, "Num 6");
            A(0x47, "Num 7"); A(0x48, "Num 8"); A(0x49, "Num 9");
            A(MOUSE + 1, "Clic izquierdo"); A(MOUSE + 2, "Clic derecho"); A(MOUSE + 3, "Clic rueda");
            A(MOUSE + 4, "Botón lateral 1"); A(MOUSE + 5, "Botón lateral 2");
        }

        public static string NameOf(int vc)
        {
            Item it;
            if (byVC.TryGetValue(vc, out it)) return it.Name;
            return "Código 0x" + vc.ToString("X");
        }

        // WM_KEYDOWN lParam -> codigo VC de libuiohook
        public static int FromLParam(IntPtr lp)
        {
            long l = lp.ToInt64();
            int scan = (int)((l >> 16) & 0xFF);
            bool ext = ((l >> 24) & 1) == 1;
            if (scan == 0x45) return ext ? 0x45 : 0x0E45; // Bloq Num / Pausa
            if (!ext) return scan;
            if (scan == 0x48 || scan == 0x4B || scan == 0x4D || scan == 0x50) return 0xE000 | scan;
            return 0x0E00 | scan;
        }
        public static int FromMouse(MouseButtons b)
        {
            switch (b)
            {
                case MouseButtons.Right: return MOUSE + 2;
                case MouseButtons.Middle: return MOUSE + 3;
                case MouseButtons.XButton1: return MOUSE + 4;
                case MouseButtons.XButton2: return MOUSE + 5;
                default: return MOUSE + 1;
            }
        }
        // texto corto para crear una tecla nueva
        public static string ShortLabel(int vc)
        {
            string n = NameOf(vc);
            n = n.Replace(" izq.", "").Replace(" der.", "");
            if (n.StartsWith("Flecha ")) { switch (vc) { case 0xE048: return "↑"; case 0xE04B: return "←"; case 0xE050: return "↓"; default: return "→"; } }
            if (n.StartsWith("Código")) return "?";
            return n;
        }
    }

    // ================================================================
    // TEMA Y PREFERENCIAS
    // ================================================================
    public class Theme
    {
        public Color Bg, Side, Panel, Card, Card2, Input, Line, Text, Muted, Accent, Accent2, AccentInk, Focus, Ok, Danger, Warn;
        public bool IsDark = true;
        static Color H(string s) { return ColorTranslator.FromHtml(s); }
        public static Theme Get(string name)
        {
            if (name == "light") return new Theme
            {
                IsDark = false, Bg = H("#e9e6df"), Side = H("#ffffff"), Panel = H("#fbfaf7"), Card = H("#f1eee8"), Card2 = H("#e4dfd5"),
                Input = H("#ffffff"), Line = H("#d3cdc1"), Text = H("#1d1f23"), Muted = H("#596069"), Accent = H("#c4511f"),
                Accent2 = H("#e48a32"), AccentInk = H("#ffffff"), Focus = H("#0b6bcb"), Ok = H("#1f9d63"), Danger = H("#c93434"), Warn = H("#b7791f")
            };
            if (name == "contrast") return new Theme
            {
                Bg = H("#000000"), Side = H("#000000"), Panel = H("#000000"), Card = H("#0d0d0d"), Card2 = H("#262626"),
                Input = H("#000000"), Line = H("#ffffff"), Text = H("#ffffff"), Muted = H("#ffffff"), Accent = H("#ff8a3d"),
                Accent2 = H("#ffb36b"), AccentInk = H("#000000"), Focus = H("#ffff00"), Ok = H("#00ff88"), Danger = H("#ff5555"), Warn = H("#ffff00")
            };
            return new Theme
            {
                Bg = H("#101216"), Side = H("#15181d"), Panel = H("#191c22"), Card = H("#21252c"), Card2 = H("#2c313a"),
                Input = H("#111317"), Line = H("#323843"), Text = H("#eef0f3"), Muted = H("#9aa3ae"), Accent = H("#ce5b29"),
                Accent2 = H("#f0a24a"), AccentInk = H("#2b1400"), Focus = H("#4cc3ff"), Ok = H("#3ecf8e"), Danger = H("#ef5b5b"), Warn = H("#f5b942")
            };
        }
    }

    public class Prefs
    {
        public string theme = "dark";
        public int uiScale = 100;
        public bool readable, bigTargets, hideHelp, speak, seenGuide;
        public bool tips = true;
        public string bg = "game";
        public bool autoEdit = true;
        // actualizaciones
        public bool autoUpdate = true;
        public long lastUpdateCheck;
        public string notifiedVersion = "";
        public List<string> recentColors = new List<string>();
        // visor en pantalla
        public bool overlayOn, overlayLocked = true, keepInTray = true, overlayAnim = true;
        public int overlayScale = 100, overlayOpacity = 100, overlayFps = 60, overlayX = int.MinValue, overlayY = int.MinValue;
    }

    public static class App
    {
        public static Theme T = Theme.Get("dark");
        public static Prefs P = new Prefs();
        public static float Dpi = 1f;
        public static string Dir = AppDomain.CurrentDomain.BaseDirectory;
        public static ToolTip Tips;
        static Dictionary<string, Font> fontCache = new Dictionary<string, Font>();
        static HashSet<string> installed;

        public static float Scale { get { return Dpi * P.uiScale / 100f; } }
        public static int Dp(float n) { return (int)Math.Round(n * Scale); }
        public static int Hit(float n) { return Dp(n * (P.bigTargets ? 1.22f : 1f)); }

        public static bool Installed(string fam)
        {
            if (installed == null)
            {
                installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try { using (var c = new InstalledFontCollection()) foreach (var f in c.Families) installed.Add(f.Name); } catch { }
            }
            return installed.Contains(fam);
        }
        public static string UiFamily
        {
            get
            {
                if (P.readable && Installed("Verdana")) return "Verdana";
                if (Installed("Segoe UI")) return "Segoe UI";
                return FontFamily.GenericSansSerif.Name;
            }
        }
        public static Font F(float pt) { return F(pt, false); }
        public static Font F(float pt, bool bold)
        {
            float size = pt * P.uiScale / 100f;
            string key = UiFamily + "|" + size + "|" + bold;
            Font f;
            if (!fontCache.TryGetValue(key, out f)) { f = new Font(UiFamily, size, bold ? FontStyle.Bold : FontStyle.Regular); fontCache[key] = f; }
            return f;
        }
        public static bool HasGlyphs { get { return Installed("Segoe MDL2 Assets") || Installed("Segoe Fluent Icons"); } }
        public static Font Glyph(float pt)
        {
            string fam = Installed("Segoe Fluent Icons") ? "Segoe Fluent Icons" : Installed("Segoe MDL2 Assets") ? "Segoe MDL2 Assets" : UiFamily;
            float size = pt * P.uiScale / 100f;
            string key = "G|" + fam + size;
            Font f;
            if (!fontCache.TryGetValue(key, out f)) { f = new Font(fam, size); fontCache[key] = f; }
            return f;
        }

        // ---------- voz (System.Speech, cargado en tiempo de ejecucion) ----------
        static dynamic synth; static bool synthFailed;
        public static void Speak(string text)
        {
            if (!P.speak || string.IsNullOrEmpty(text) || synthFailed) return;
            try
            {
                if (synth == null)
                {
                    var t = Type.GetType("System.Speech.Synthesis.SpeechSynthesizer, System.Speech, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                    if (t == null) { synthFailed = true; return; }
                    synth = Activator.CreateInstance(t);
                    try
                    {
                        foreach (dynamic v in synth.GetInstalledVoices())
                            if (v.VoiceInfo.Culture.TwoLetterISOLanguageName == "es") { synth.SelectVoice(v.VoiceInfo.Name); break; }
                    }
                    catch { }
                }
                synth.SpeakAsyncCancelAll();
                synth.SpeakAsync(text);
            }
            catch { synthFailed = true; }
        }

        public static void LoadPrefs()
        {
            try { var p = Path.Combine(Dir, "studio-prefs.json"); if (File.Exists(p)) P = Json.Read<Prefs>(File.ReadAllText(p)) ?? new Prefs(); } catch { P = new Prefs(); }
            P.uiScale = Math.Max(80, Math.Min(200, P.uiScale));
            T = Theme.Get(P.theme);
        }
        public static void SavePrefs()
        {
            try { File.WriteAllText(Path.Combine(Dir, "studio-prefs.json"), Json.Write(P)); } catch { }
        }

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
        public static void DarkTitle(Form f)
        {
            try { int v = T.IsDark ? 1 : 0; DwmSetWindowAttribute(f.Handle, 20, ref v, 4); } catch { }
        }
    }

    // ================================================================
    // DIBUJO
    // ================================================================
    public static class G
    {
        public static GraphicsPath RR(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            rad = Math.Max(0, Math.Min(rad, Math.Min(r.Width, r.Height) / 2f));
            if (rad < 0.5f || r.Width <= 0 || r.Height <= 0) { p.AddRectangle(r); return p; }
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
        public static void Fill(Graphics g, RectangleF r, float rad, Color c)
        {
            using (var p = RR(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
        }
        public static void FillV(Graphics g, RectangleF r, float rad, Color top, Color bottom)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (var p = RR(r, rad))
            using (var b = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2), top, bottom, LinearGradientMode.Vertical))
            {
                b.WrapMode = WrapMode.TileFlipXY; // sin esto GDI+ repite el color de abajo en el borde de arriba
                g.FillPath(b, p);
            }
        }
        public static void Stroke(Graphics g, RectangleF r, float rad, Color c, float w)
        {
            using (var p = RR(r, rad)) using (var pen = new Pen(c, w)) g.DrawPath(pen, p);
        }
        public static void Ring(Graphics g, Rectangle r, float rad)
        {
            var old = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            Stroke(g, new RectangleF(r.X + 1.5f, r.Y + 1.5f, r.Width - 3, r.Height - 3), rad, App.T.Focus, App.Dp(2.5f));
            g.SmoothingMode = old;
        }
        public static void Checker(Graphics g, RectangleF r, float sz, Color a, Color b)
        {
            using (var ba = new SolidBrush(a)) g.FillRectangle(ba, r);
            using (var bb = new SolidBrush(b))
                for (float y = r.Y; y < r.Bottom; y += sz)
                    for (float x = r.X + ((int)((y - r.Y) / sz) % 2) * sz; x < r.Right; x += sz * 2)
                        g.FillRectangle(bb, x, y, Math.Min(sz, r.Right - x), Math.Min(sz, r.Bottom - y));
        }
        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
        public static Color Alpha(Color c, double mul) { return Color.FromArgb(Math.Max(0, Math.Min(255, (int)(c.A * mul))), c.R, c.G, c.B); }

        public static Color Col(string hex)
        {
            try
            {
                string h = (hex ?? "#000000ff").Trim().TrimStart('#');
                if (h.Length == 3) h = "" + h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
                if (h.Length == 6) h += "ff";
                return Color.FromArgb(Convert.ToInt32(h.Substring(6, 2), 16), Convert.ToInt32(h.Substring(0, 2), 16),
                    Convert.ToInt32(h.Substring(2, 2), 16), Convert.ToInt32(h.Substring(4, 2), 16));
            }
            catch { return Color.Magenta; }
        }
        public static string Hex(Color c) { return string.Format("#{0:x2}{1:x2}{2:x2}{3:x2}", c.R, c.G, c.B, c.A); }
        public static string Shade(string hex6, double f)
        {
            var c = Col(hex6);
            Func<int, int> m = v => (int)Math.Round(f < 0 ? v * (1 + f) : v + (255 - v) * f);
            return string.Format("#{0:x2}{1:x2}{2:x2}", m(c.R), m(c.G), m(c.B));
        }

        public const TextFormatFlags Wrap = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl;
        public const TextFormatFlags Line = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
    }

    // ---------- matrices 3x3 (homografias) ----------
    public static class Mx
    {
        public static double[] I() { return new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 }; }
        public static double[] T(double x, double y) { return new double[] { 1, 0, x, 0, 1, y, 0, 0, 1 }; }
        public static double[] S(double s) { return new double[] { s, 0, 0, 0, s, 0, 0, 0, 1 }; }
        public static double[] Mul(params double[][] ms)
        {
            var r = ms[0];
            for (int n = 1; n < ms.Length; n++)
            {
                var a = r; var b = ms[n]; var o = new double[9];
                for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++)
                        o[i * 3 + j] = a[i * 3] * b[j] + a[i * 3 + 1] * b[3 + j] + a[i * 3 + 2] * b[6 + j];
                r = o;
            }
            return r;
        }
        public static double[] Inv(double[] m)
        {
            double a = m[0], b = m[1], c = m[2], d = m[3], e = m[4], f = m[5], g = m[6], h = m[7], i = m[8];
            double A = e * i - f * h, B = -(d * i - f * g), C = d * h - e * g;
            double det = a * A + b * B + c * C;
            if (Math.Abs(det) < 1e-12) return I();
            return new double[] {
                A / det, -(b * i - c * h) / det, (b * f - c * e) / det,
                B / det, (a * i - c * g) / det, -(a * f - c * d) / det,
                C / det, -(a * h - b * g) / det, (a * e - b * d) / det };
        }
        public static PointF Ap(double[] m, double x, double y)
        {
            double w = m[6] * x + m[7] * y + m[8];
            if (Math.Abs(w) < 1e-9) w = 1e-9;
            return new PointF((float)((m[0] * x + m[1] * y + m[2]) / w), (float)((m[3] * x + m[4] * y + m[5]) / w));
        }
        // perspective(p) rotateX(a) rotateZ(b), igual que el CSS del overlay
        public static double[] Tilt(CTilt t)
        {
            double amt = t.amount / 100.0, p, ax, bz;
            switch (t.mode)
            {
                case "left": p = 1300; ax = 22 * amt; bz = -8 * amt; break;
                case "right": p = 1300; ax = 22 * amt; bz = 8 * amt; break;
                case "flat": return I();
                // de lado: gira sobre el eje vertical (como rotateY): el lado que se aleja se encoge en perspectiva
                case "side-left": case "side-right":
                    {
                        double by = (t.mode == "side-right" ? 30 : -30) * amt * Math.PI / 180;
                        return new double[] { Math.Cos(by), 0, 0, 0, 1, 0, Math.Sin(by) / 1000.0, 0, 1 };
                    }
                default: p = 1000; ax = 30 * amt; bz = 0; break;
            }
            double a = ax * Math.PI / 180, b = bz * Math.PI / 180;
            double ca = Math.Cos(a), sa = Math.Sin(a), cb = Math.Cos(b), sb = Math.Sin(b);
            return new double[] { cb, -sb, 0, ca * sb, ca * cb, 0, -(sa / p) * sb, -(sa / p) * cb, 1 };
        }
        // coordenadas de las teclas -> coordenadas del lienzo de OBS
        public static double[] LocalToCanvas(Config c, bool flat)
        {
            var b = c.KeysBounds();
            double w = b.Width, h = b.Height, s = c.position.scale;
            double W = c.canvas.width, H = c.canvas.height, X = c.position.x, Y = c.position.y;
            double L, Tp, ox, oy;
            switch (c.position.anchor)
            {
                case "bottom-right": L = W - X - w; Tp = H - Y - h; ox = w; oy = h; break;
                case "top-left": L = X; Tp = Y; ox = 0; oy = 0; break;
                case "top-right": L = W - X - w; Tp = Y; ox = w; oy = 0; break;
                case "center": L = W / 2 - w / 2; Tp = H / 2 - h / 2; ox = w / 2; oy = h / 2; break;
                default: L = X; Tp = H - Y - h; ox = 0; oy = h; break;
            }
            // eje del giro: abajo en el centro; en los laterales, el borde que queda cerca (asi nada crece, solo se aleja el otro lado)
            double tx = w / 2, ty = h;
            if (c.tilt.mode == "side-right") { tx = 0; ty = h / 2; } else if (c.tilt.mode == "side-left") { tx = w; ty = h / 2; }
            var tilt = flat ? I() : Mul(T(tx, ty), Tilt(c.tilt), T(-tx, -ty));
            return Mul(T(L + ox, Tp + oy), S(s), T(-ox, -oy), tilt);
        }
    }

    // ================================================================
    // FUENTES (Google Fonts se descargan una vez a la carpeta "fonts")
    // ================================================================
    public static class Fonts
    {
        public static readonly string[][] List = {
            new[]{"Quicksand","400;500;600;700"}, new[]{"Poppins","400;500;600;700;800;900"}, new[]{"Montserrat","400;500;600;700;800;900"},
            new[]{"Rajdhani","400;500;600;700"}, new[]{"Orbitron","400;500;600;700;800;900"}, new[]{"Russo One","400"},
            new[]{"Bebas Neue","400"}, new[]{"Oswald","400;500;600;700"}, new[]{"Roboto","400;500;700;900"},
            new[]{"Inter","400;500;600;700;800;900"}, new[]{"Nunito","400;600;700;800;900"}, new[]{"Fredoka","400;500;600;700"},
            new[]{"Exo 2","400;500;600;700;800;900"}, new[]{"Teko","400;500;600;700"}, new[]{"Chakra Petch","400;500;600;700"},
            new[]{"Press Start 2P","400"}, new[]{"Bangers","400"}, new[]{"Permanent Marker","400"}, new[]{"Lexend","400;500;600;700;800;900"},
            new[]{"Cinzel","400;500;600;700;800;900"}, new[]{"Luckiest Guy","400"}, new[]{"Anton","400"},
            new[]{"Barlow","400;500;600;700;800;900"}, new[]{"Kanit","400;500;600;700;800;900"}, new[]{"Outfit","400;500;600;700;800;900"},
            new[]{"Space Grotesk","400;500;600;700"}, new[]{"Righteous","400"}, new[]{"Audiowide","400"},
            new[]{"Black Ops One","400"}, new[]{"Bungee","400"}, new[]{"Titillium Web","400;600;700;900"},
            new[]{"Rubik","400;500;600;700;800;900"}, new[]{"VT323","400"}, new[]{"Silkscreen","400;700"},
            new[]{"Lilita One","400"},
            new[]{"Arial",""}, new[]{"Segoe UI",""}, new[]{"Verdana",""}, new[]{"Impact",""}, new[]{"Consolas",""}
        };
        public static event Action Loaded;
        static readonly Dictionary<string, FontFamily> loaded = new Dictionary<string, FontFamily>();
        static readonly HashSet<string> pending = new HashSet<string>(), failed = new HashSet<string>();
        static readonly List<PrivateFontCollection> keep = new List<PrivateFontCollection>();
        static SynchronizationContext ui;
        static string Folder { get { return Path.Combine(App.Dir, "fonts"); } }

        public static string Weights(string fam)
        {
            foreach (var f in List) if (string.Equals(f[0], fam, StringComparison.OrdinalIgnoreCase)) return f[1];
            return null;
        }
        public static int NearestWeight(string fam, int w)
        {
            string ws = Weights(fam);
            if (string.IsNullOrEmpty(ws)) return w >= 600 ? 700 : 400;
            int best = 400, bd = 9999;
            foreach (var s in ws.Split(';')) { int v = int.Parse(s); if (Math.Abs(v - w) < bd) { bd = Math.Abs(v - w); best = v; } }
            return best;
        }
        static FontStyle Avail(FontFamily ff, FontStyle want)
        {
            if (ff.IsStyleAvailable(want)) return want;
            foreach (FontStyle s in new[] { FontStyle.Regular, FontStyle.Bold, FontStyle.Italic, FontStyle.Bold | FontStyle.Italic })
                if (ff.IsStyleAvailable(s)) return s;
            return FontStyle.Regular;
        }
        static string FileFor(string fam, int w) { return Path.Combine(Folder, Regex.Replace(fam, "[^A-Za-z0-9]", "") + "-" + w + ".ttf"); }

        // las familias del sistema se crean una sola vez (antes se creaba una nueva por tecla y por fotograma)
        static readonly Dictionary<string, FontFamily> sysFam = new Dictionary<string, FontFamily>(StringComparer.OrdinalIgnoreCase);
        static FontFamily Sys(string name)
        {
            FontFamily f;
            if (!sysFam.TryGetValue(name, out f)) { f = new FontFamily(name); sysFam[name] = f; }
            return f;
        }
        public static void Get(string fam, int weight, out FontFamily ff, out FontStyle st)
        {
            if (ui == null) ui = SynchronizationContext.Current;
            if (string.IsNullOrWhiteSpace(fam)) fam = "Segoe UI";
            string known = Weights(fam);
            // fuente del sistema (o una Google instalada en el PC)
            if (string.IsNullOrEmpty(known) && App.Installed(fam))
            {
                ff = Sys(fam);
                st = Avail(ff, weight >= 600 ? FontStyle.Bold : FontStyle.Regular);
                return;
            }
            int w = NearestWeight(fam, weight);
            string key = fam.ToLowerInvariant() + "-" + w;
            FontFamily f;
            if (loaded.TryGetValue(key, out f)) { ff = f; st = Avail(f, FontStyle.Regular); return; }
            string file = FileFor(fam, w);
            if (File.Exists(file) && LoadFile(key, file)) { ff = loaded[key]; st = Avail(ff, FontStyle.Regular); return; }
            Request(fam, w, key, file);
            if (App.Installed(fam)) { ff = Sys(fam); st = Avail(ff, weight >= 600 ? FontStyle.Bold : FontStyle.Regular); return; }
            ff = Sys(App.Installed("Segoe UI") ? "Segoe UI" : FontFamily.GenericSansSerif.Name);
            st = Avail(ff, weight >= 600 ? FontStyle.Bold : FontStyle.Regular);
        }
        public static bool IsReady(string fam, int weight)
        {
            if (App.Installed(fam)) return true;
            return loaded.ContainsKey(fam.ToLowerInvariant() + "-" + NearestWeight(fam, weight));
        }

        static bool LoadFile(string key, string file)
        {
            try
            {
                var pfc = new PrivateFontCollection();
                pfc.AddFontFile(file);
                if (pfc.Families.Length == 0) return false;
                keep.Add(pfc);
                loaded[key] = pfc.Families[0];
                return true;
            }
            catch { return false; }
        }

        static void Request(string fam, int w, string key, string file)
        {
            if (pending.Contains(key) || failed.Contains(key)) return;
            pending.Add(key);
            Task.Run(() =>
            {
                bool ok = false;
                try
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    Directory.CreateDirectory(Folder);
                    using (var wc = new WebClient())
                    {
                        wc.Encoding = Encoding.UTF8;
                        string fq = Uri.EscapeDataString(fam).Replace("%20", "+");
                        string css;
                        try { css = wc.DownloadString("https://fonts.googleapis.com/css2?family=" + fq + ":wght@" + w); }
                        catch { css = wc.DownloadString("https://fonts.googleapis.com/css2?family=" + fq); }
                        var m = Regex.Match(css, @"url\((https://[^)]+?\.ttf)\)");
                        if (m.Success) { wc.DownloadFile(m.Groups[1].Value, file); ok = true; }
                    }
                }
                catch { }
                Action done = () =>
                {
                    pending.Remove(key);
                    if (ok && LoadFile(key, file)) { if (Loaded != null) Loaded(); }
                    else failed.Add(key);
                };
                if (ui != null) ui.Post(_ => done(), null); else done();
            });
        }
    }

    // ================================================================
    // IMAGENES (data URL <-> Image)
    // ================================================================
    public static class Images
    {
        static readonly Dictionary<int, KeyValuePair<string, Image>> cache = new Dictionary<int, KeyValuePair<string, Image>>();
        public static Image Get(string dataUrl)
        {
            if (string.IsNullOrEmpty(dataUrl)) return null;
            int h = dataUrl.Length ^ dataUrl.GetHashCode();
            KeyValuePair<string, Image> kv;
            if (cache.TryGetValue(h, out kv) && (ReferenceEquals(kv.Key, dataUrl) || kv.Key == dataUrl)) return kv.Value;
            Image img = null;
            try
            {
                int i = dataUrl.IndexOf("base64,", StringComparison.Ordinal);
                if (i >= 0)
                {
                    var bytes = Convert.FromBase64String(dataUrl.Substring(i + 7));
                    using (var ms = new MemoryStream(bytes)) using (var tmp = Image.FromStream(ms)) img = new Bitmap(tmp);
                }
            }
            catch { img = null; }
            if (cache.Count > 64) cache.Clear();
            cache[h] = new KeyValuePair<string, Image>(dataUrl, img);
            return img;
        }
        static readonly Dictionary<Image, KeyValuePair<int, Bitmap>> fadedCache = new Dictionary<Image, KeyValuePair<int, Bitmap>>();
        public static Image Faded(Image img, float op)
        {
            int key = (int)Math.Round(op * 100);
            if (key >= 100) return img;
            KeyValuePair<int, Bitmap> kv;
            if (fadedCache.TryGetValue(img, out kv) && kv.Key == key) return kv.Value;
            if (fadedCache.ContainsKey(img)) { fadedCache[img].Value.Dispose(); fadedCache.Remove(img); }
            if (fadedCache.Count > 16) { foreach (var v in fadedCache.Values) v.Value.Dispose(); fadedCache.Clear(); }
            var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp)) { g.Clear(Color.Transparent); g.DrawImage(img, 0, 0, img.Width, img.Height); }
            var d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                var buf = new byte[d.Stride * d.Height];
                Marshal.Copy(d.Scan0, buf, 0, buf.Length);
                for (int i = 3; i < buf.Length; i += 4) buf[i] = (byte)(buf[i] * key / 100);
                Marshal.Copy(buf, 0, d.Scan0, buf.Length);
            }
            finally { bmp.UnlockBits(d); }
            fadedCache[img] = new KeyValuePair<int, Bitmap>(key, bmp);
            return bmp;
        }
        // Tono/Saturacion/Luminosidad (+ Colorear), parecido al de Photoshop
        public static Bitmap Adjust(Image src, int hue, int sat, int light, bool colorize, int max)
        {
            double sc = Math.Min(1.0, (double)max / Math.Max(src.Width, src.Height));
            int w = Math.Max(1, (int)(src.Width * sc)), h = Math.Max(1, (int)(src.Height * sc));
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(src, 0, 0, w, h); }
            var d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                var buf = new byte[d.Stride * h];
                Marshal.Copy(d.Scan0, buf, 0, buf.Length);
                double hs = hue / 360.0, sf = sat / 100.0, lf = light / 100.0;
                for (int i = 0; i < buf.Length; i += 4)
                {
                    double b = buf[i] / 255.0, gr = buf[i + 1] / 255.0, r = buf[i + 2] / 255.0;
                    double H, S, L; ToHsl(r, gr, b, out H, out S, out L);
                    if (colorize) { H = ((hue % 360) + 360) % 360 / 360.0; S = Math.Max(0, Math.Min(1, 0.25 + sat / 100.0 * 0.75)); }
                    else
                    {
                        H = (H + hs) % 1; if (H < 0) H += 1;
                        S = sf >= 0 ? S + (1 - S) * sf * S : S * (1 + sf);
                    }
                    L = lf >= 0 ? L + (1 - L) * lf : L * (1 + lf);
                    FromHsl(H, Math.Max(0, Math.Min(1, S)), Math.Max(0, Math.Min(1, L)), out r, out gr, out b);
                    buf[i] = (byte)Math.Round(b * 255); buf[i + 1] = (byte)Math.Round(gr * 255); buf[i + 2] = (byte)Math.Round(r * 255);
                }
                Marshal.Copy(buf, 0, d.Scan0, buf.Length);
            }
            finally { bmp.UnlockBits(d); }
            return bmp;
        }
        static void ToHsl(double r, double g, double b, out double h, out double s, out double l)
        {
            double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            l = (mx + mn) / 2; h = 0; s = 0;
            if (mx == mn) return;
            double d = mx - mn;
            s = l > 0.5 ? d / (2 - mx - mn) : d / (mx + mn);
            if (mx == r) h = (g - b) / d + (g < b ? 6 : 0); else if (mx == g) h = (b - r) / d + 2; else h = (r - g) / d + 4;
            h /= 6;
        }
        static double Hue2(double p, double q, double t) { if (t < 0) t += 1; if (t > 1) t -= 1; if (t < 1 / 6.0) return p + (q - p) * 6 * t; if (t < 0.5) return q; if (t < 2 / 3.0) return p + (q - p) * (2 / 3.0 - t) * 6; return p; }
        static void FromHsl(double h, double s, double l, out double r, out double g, out double b)
        {
            if (s == 0) { r = g = b = l; return; }
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
            r = Hue2(p, q, h + 1 / 3.0); g = Hue2(p, q, h); b = Hue2(p, q, h - 1 / 3.0);
        }
        public static string ToPngUrl(Bitmap bmp)
        {
            using (var o = new MemoryStream()) { bmp.Save(o, ImageFormat.Png); return "data:image/png;base64," + Convert.ToBase64String(o.ToArray()); }
        }

        public static string FromFile(string path) { return FromFile(path, 320); }
        public static string FromFile(string path, int max) { return FromBytes(File.ReadAllBytes(path), Path.GetExtension(path).ToLowerInvariant(), max); }

        // descarga una imagen de internet (se guarda dentro del estilo, asi funciona aunque luego se borre de la web)
        public static Task<string> FromUrl(string url, int max)
        {
            return Task.Run(() =>
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                url = url.Trim();
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
                using (var wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) KeyboardOverlayStudio";
                    var bytes = wc.DownloadData(url);
                    if (bytes.Length > 25000000) throw new Exception("la imagen pesa demasiado (máx. 25 MB)");
                    string ct = (wc.ResponseHeaders[HttpResponseHeader.ContentType] ?? "").ToLowerInvariant();
                    bool gif = ct.Contains("gif") || (bytes.Length > 3 && bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F');
                    bool jpg = ct.Contains("jpeg") || ct.Contains("jpg") || (bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8);
                    return FromBytes(bytes, gif ? ".gif" : jpg ? ".jpg" : ".png", max);
                }
            });
        }

        public static string FromBytes(byte[] bytes, string ext, int max)
        {
            if (ext == ".gif" && bytes.Length < 1500000) return "data:image/gif;base64," + Convert.ToBase64String(bytes);
            using (var ms = new MemoryStream(bytes))
            using (var src = Image.FromStream(ms))
            {
                double s = Math.Min(1.0, (double)max / Math.Max(src.Width, src.Height));
                int w = Math.Max(1, (int)Math.Round(src.Width * s)), h = Math.Max(1, (int)Math.Round(src.Height * s));
                using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(src, 0, 0, w, h); }
                    using (var o = new MemoryStream())
                    {
                        bool jpg = ext == ".jpg" || ext == ".jpeg";
                        bmp.Save(o, jpg ? ImageFormat.Jpeg : ImageFormat.Png);
                        return "data:image/" + (jpg ? "jpeg" : "png") + ";base64," + Convert.ToBase64String(o.ToArray());
                    }
                }
            }
        }
    }

    // ================================================================
    // DIBUJO DE UNA TECLA (replica el CSS del overlay)
    // ================================================================
    // ================================================================
    // ICONOS VECTORIALES PROPIOS ("@muro", "@suelo"...): se pintan con el color del simbolo,
    // asi cada uno los personaliza desde Colores. Cuadricula de 24x24; cada capa = opacidad + poligonos.
    // (kb-render.js tiene los mismos datos para OBS)
    // ================================================================
    public static class VIcons
    {
        // capa = opacidad + poligonos rellenos (W = 0) o lineas de ese grosor (aristas del efecto "cristal")
        class Layer { public float A, W; public float[][] P; public Layer(float a, float w, params float[][] p) { A = a; W = w; P = p; } }
        static float[] Rc(float x0, float y0, float x1, float y1) { return new[] { x0, y0, x1, y0, x1, y1, x0, y1 }; }
        static readonly Dictionary<string, Layer[]> defs = new Dictionary<string, Layer[]>
        {
            { "@muro", new[] {
                new Layer(0.42f, 0f, new[] { 6.16f, 15.88f, 15.89f, 21.5f, 15.89f, 9.24f, 6.16f, 3.62f }),
                new Layer(0.28f, 0f, new[] { 17.84f, 20.38f, 15.89f, 21.5f, 15.89f, 9.24f, 17.84f, 8.12f }),
                new Layer(0.6f, 0f, new[] { 8.11f, 2.5f, 17.84f, 8.12f, 15.89f, 9.24f, 6.16f, 3.62f }),
                new Layer(0.35f, 0.7f, new[] { 8.11f, 14.76f, 17.84f, 20.38f }, new[] { 8.11f, 14.76f, 6.16f, 15.88f }, new[] { 8.11f, 14.76f, 8.11f, 2.5f }),
                new Layer(1f, 1.1f, new[] { 8.11f, 2.5f, 17.84f, 8.12f }, new[] { 17.84f, 8.12f, 15.89f, 9.24f }, new[] { 15.89f, 9.24f, 6.16f, 3.62f }, new[] { 6.16f, 3.62f, 8.11f, 2.5f }, new[] { 6.16f, 15.88f, 15.89f, 21.5f }, new[] { 6.16f, 15.88f, 6.16f, 3.62f }, new[] { 15.89f, 21.5f, 15.89f, 9.24f }, new[] { 17.84f, 20.38f, 15.89f, 21.5f }, new[] { 17.84f, 20.38f, 17.84f, 8.12f }) } },
            { "@suelo", new[] {
                new Layer(0.42f, 0f, new[] { 2.5f, 13.1f, 12f, 18.58f, 12f, 16.39f, 2.5f, 10.9f }),
                new Layer(0.28f, 0f, new[] { 21.5f, 13.1f, 12f, 18.58f, 12f, 16.39f, 21.5f, 10.9f }),
                new Layer(0.6f, 0f, new[] { 12f, 5.42f, 21.5f, 10.9f, 12f, 16.39f, 2.5f, 10.9f }),
                new Layer(0.35f, 0.7f, new[] { 12f, 7.61f, 21.5f, 13.1f }, new[] { 12f, 7.61f, 2.5f, 13.1f }, new[] { 12f, 7.61f, 12f, 5.42f }),
                new Layer(1f, 1.1f, new[] { 12f, 5.42f, 21.5f, 10.9f }, new[] { 21.5f, 10.9f, 12f, 16.39f }, new[] { 12f, 16.39f, 2.5f, 10.9f }, new[] { 2.5f, 10.9f, 12f, 5.42f }, new[] { 2.5f, 13.1f, 12f, 18.58f }, new[] { 2.5f, 13.1f, 2.5f, 10.9f }, new[] { 12f, 18.58f, 12f, 16.39f }, new[] { 21.5f, 13.1f, 12f, 18.58f }, new[] { 21.5f, 13.1f, 21.5f, 10.9f }) } },
            { "@escalera", new[] {
                new Layer(0.42f, 0f, new[] { 3.4f, 17.25f, 10.77f, 21.5f, 10.77f, 18.48f, 3.4f, 14.22f }),
                new Layer(0.6f, 0f, new[] { 6.68f, 12.33f, 14.05f, 16.58f, 10.77f, 18.48f, 3.4f, 14.22f }),
                new Layer(0.42f, 0f, new[] { 6.68f, 12.33f, 14.05f, 16.58f, 14.05f, 13.56f, 6.68f, 9.31f }),
                new Layer(0.6f, 0f, new[] { 9.95f, 7.42f, 17.32f, 11.67f, 14.05f, 13.56f, 6.68f, 9.31f }),
                new Layer(0.42f, 0f, new[] { 9.95f, 7.42f, 17.32f, 11.67f, 17.32f, 8.64f, 9.95f, 4.39f }),
                new Layer(0.6f, 0f, new[] { 13.23f, 2.5f, 20.6f, 6.75f, 17.32f, 8.64f, 9.95f, 4.39f }),
                new Layer(0.28f, 0f, new[] { 10.77f, 21.5f, 10.77f, 18.48f, 14.05f, 16.58f, 14.05f, 13.56f, 17.32f, 11.67f, 17.32f, 8.64f, 20.6f, 6.75f, 20.6f, 15.83f }),
                new Layer(0.35f, 0.7f, new[] { 13.23f, 11.57f, 20.6f, 15.83f }, new[] { 13.23f, 11.57f, 3.4f, 17.25f }, new[] { 13.23f, 11.57f, 13.23f, 2.5f }),
                new Layer(1f, 1.1f, new[] { 10.77f, 21.5f, 10.77f, 18.48f }, new[] { 10.77f, 18.48f, 14.05f, 16.58f }, new[] { 14.05f, 16.58f, 14.05f, 13.56f }, new[] { 14.05f, 13.56f, 17.32f, 11.67f }, new[] { 17.32f, 11.67f, 17.32f, 8.64f }, new[] { 17.32f, 8.64f, 20.6f, 6.75f }, new[] { 20.6f, 6.75f, 20.6f, 15.83f }, new[] { 20.6f, 15.83f, 10.77f, 21.5f }, new[] { 3.4f, 17.25f, 10.77f, 21.5f }, new[] { 3.4f, 14.22f, 10.77f, 18.48f }, new[] { 6.68f, 12.33f, 14.05f, 16.58f }, new[] { 6.68f, 9.31f, 14.05f, 13.56f }, new[] { 9.95f, 7.42f, 17.32f, 11.67f }, new[] { 9.95f, 4.39f, 17.32f, 8.64f }, new[] { 13.23f, 2.5f, 20.6f, 6.75f }, new[] { 3.4f, 17.25f, 3.4f, 14.22f }, new[] { 3.4f, 14.22f, 6.68f, 12.33f }, new[] { 6.68f, 12.33f, 6.68f, 9.31f }, new[] { 6.68f, 9.31f, 9.95f, 7.42f }, new[] { 9.95f, 7.42f, 9.95f, 4.39f }, new[] { 9.95f, 4.39f, 13.23f, 2.5f }) } },
            { "@techo", new[] {
                new Layer(0.42f, 0f, new[] { 2.5f, 13.83f, 12f, 19.31f, 12f, 4.69f }),
                new Layer(0.28f, 0f, new[] { 21.5f, 13.83f, 12f, 19.31f, 12f, 4.69f }),
                new Layer(0.35f, 0.7f, new[] { 12f, 8.34f, 21.5f, 13.83f }, new[] { 12f, 8.34f, 2.5f, 13.83f }, new[] { 12f, 8.34f, 12f, 4.69f }),
                new Layer(1f, 1.1f, new[] { 21.5f, 13.83f, 12f, 19.31f }, new[] { 12f, 19.31f, 2.5f, 13.83f }, new[] { 21.5f, 13.83f, 12f, 4.69f }, new[] { 12f, 19.31f, 12f, 4.69f }, new[] { 2.5f, 13.83f, 12f, 4.69f }) } },
            { "@pico", new[] { new Layer(1, 0, new[] { 3.8f, 16.2f, 3.81f, 14.39f, 4.02f, 12.73f, 4.42f, 11.21f, 5.02f, 9.85f, 5.82f, 8.63f, 6.81f, 7.57f, 8f, 6.65f, 9.38f, 5.88f, 10.96f, 5.26f, 12.74f, 4.79f, 14.71f, 4.47f, 16.88f, 4.3f, 19.24f, 4.27f, 21.8f, 4.4f, 19.98f, 4.7f, 18.25f, 5.08f, 16.6f, 5.55f, 15.02f, 6.1f, 13.53f, 6.73f, 12.13f, 7.45f, 10.8f, 8.25f, 9.56f, 9.13f, 8.39f, 10.1f, 7.31f, 11.16f, 6.31f, 12.29f, 5.39f, 13.51f, 4.56f, 14.81f }, new[] { 6.75f, 8.48f, 10.27f, 5.52f, 12.45f, 8.12f, 8.93f, 11.08f }, new[] { 8.8f, 8.98f, 19.8f, 21.88f, 21.4f, 20.52f, 10.4f, 7.62f }) } },
            { "@editar", new[] {
                new Layer(1, 0, Rc(3, 3, 8.4f, 8.4f), Rc(9.3f, 3, 14.7f, 8.4f),
                    Rc(3, 9.3f, 8.4f, 14.7f), Rc(9.3f, 9.3f, 14.7f, 14.7f), Rc(15.6f, 9.3f, 21, 14.7f),
                    Rc(3, 15.6f, 8.4f, 21), Rc(9.3f, 15.6f, 14.7f, 21), Rc(15.6f, 15.6f, 21, 21)),
                new Layer(0.35f, 0, Rc(15.6f, 3, 21, 8.4f)) } },
        };
        public static readonly string[][] List = {
            new[] { "@muro", "Muro" }, new[] { "@suelo", "Suelo" }, new[] { "@escalera", "Escalera" }, new[] { "@techo", "Techo" }, new[] { "@pico", "Pico" }, new[] { "@editar", "Editar" } };
        public static bool Is(string id) { return !string.IsNullOrEmpty(id) && id[0] == '@' && defs.ContainsKey(id); }
        public static string Name(string id) { foreach (var it in List) if (it[0] == id) return it[1]; return id; }
        // capas ya colocadas en el rectangulo (x, y, lado); quien llama las libera
        public static List<KeyValuePair<float, GraphicsPath>> Paths(string id, float x, float y, float size)
        {
            var res = new List<KeyValuePair<float, GraphicsPath>>();
            Layer[] ls; if (!defs.TryGetValue(id, out ls)) return res;
            float s = size / 24f;
            foreach (var l in ls)
            {
                var p = new GraphicsPath(FillMode.Winding);
                foreach (var poly in l.P)
                {
                    var pts = new PointF[poly.Length / 2];
                    for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(x + poly[i * 2] * s, y + poly[i * 2 + 1] * s);
                    if (l.W > 0) Line(p, pts, l.W * s); else p.AddPolygon(pts);
                }
                res.Add(new KeyValuePair<float, GraphicsPath>(l.A, p));
            }
            return res;
        }
        // linea gruesa como relleno (tramos + puntas redondas): asi brilla, se escala y se sombrea igual que el resto
        static void Line(GraphicsPath p, PointF[] pts, float w)
        {
            float h = w / 2;
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                PointF a = pts[i], b = pts[i + 1];
                float dx = b.X - a.X, dy = b.Y - a.Y, len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len < 0.001f) continue;
                float nx = -dy / len * h, ny = dx / len * h;
                p.AddPolygon(new[] { new PointF(a.X + nx, a.Y + ny), new PointF(b.X + nx, b.Y + ny), new PointF(b.X - nx, b.Y - ny), new PointF(a.X - nx, a.Y - ny) });
            }
            foreach (var q in pts) p.AddEllipse(q.X - h, q.Y - h, w, w);
        }
        // para botones y listas del editor
        public static void Draw(Graphics g, string id, RectangleF r, Color c)
        {
            float sz = Math.Min(r.Width, r.Height);
            var st = g.Save(); g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var kv in Paths(id, r.X + (r.Width - sz) / 2, r.Y + (r.Height - sz) / 2, sz))
                using (kv.Value) using (var b = new SolidBrush(Color.FromArgb((int)(c.A * kv.Key), c))) g.FillPath(b, kv.Value);
            g.Restore(st);
        }
    }

    public class KeyAnim { public bool Active; public long PressAt = -100000, ReleaseAt = -100000; }

    public static class KeyPainter
    {
        public static readonly Stopwatch Clock = Stopwatch.StartNew();
        public static long Now { get { return Clock.ElapsedMilliseconds; } }
        static double Cl(double v) { return v < 0 ? 0 : v > 1 ? 1 : v; }
        static double EaseOut(double t) { return 1 - (1 - t) * (1 - t); }
        static double Back(double t) { double c1 = 1.70158, c3 = c1 + 1; return 1 + c3 * Math.Pow(t - 1, 3) + c1 * Math.Pow(t - 1, 2); }

        static Bitmap mb = new Bitmap(1, 1);
        static Graphics mg = Graphics.FromImage(mb);
        // medir texto es caro (crea una fuente cada vez) y se repite en cada fotograma: se guarda el resultado
        static readonly Dictionary<string, float> measured = new Dictionary<string, float>();
        static StringFormat measureFmt;
        static float Measure(string s, FontFamily ff, FontStyle st, float size)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            string key = ff.Name + "\u0001" + (int)st + "\u0001" + size.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\u0001" + s;
            float w;
            if (measured.TryGetValue(key, out w)) return w;
            if (measureFmt == null) { measureFmt = (StringFormat)StringFormat.GenericTypographic.Clone(); measureFmt.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces; }
            using (var f = new Font(ff, size, st, GraphicsUnit.Pixel)) w = mg.MeasureString(s, f, PointF.Empty, measureFmt).Width;
            if (measured.Count > 4096) measured.Clear();
            measured[key] = w;
            return w;
        }
        static FontFamily symbolFamily;
        static FontFamily SymbolFamily { get { if (symbolFamily == null && App.Installed("Segoe UI Symbol")) symbolFamily = new FontFamily("Segoe UI Symbol"); return symbolFamily; } }
        static float LineH(FontFamily ff, FontStyle st, float size)
        {
            return size * (ff.GetCellAscent(st) + ff.GetCellDescent(st)) / (float)ff.GetEmHeight(st);
        }

        public class St { public bool act; public float off, flashA, bright, rip = -1, rip2 = -1, spark = -1, conf = -1, scale = 1, rot, shake, pop = 1; }
        public static St State(Config c, KeyAnim a, long now)
        {
            var ef = c.effects; var st = new St();
            st.act = a != null && a.Active; st.flashA = st.act ? 1 : 0;
            float press = ef.body ? c.shape.press : 0;
            double sp = Math.Max(10, ef.speed) / 100.0;
            double p = st.act ? 1 : 0;
            if (a != null)
            {
                if (st.act)
                {
                    double dt = (now - a.PressAt) * sp;
                    p = EaseOut(Cl(dt / 80.0));
                    st.flashA = (float)Cl(dt / 50.0);
                    if (ef.flash && dt < 320) st.bright = (float)(1 - dt / 320.0);
                    if (ef.ripple && dt < 450) st.rip = (float)(dt / 450.0);
                    if (ef.ripple2 && dt >= 120 && dt < 570) st.rip2 = (float)((dt - 120) / 450.0);
                    if (ef.sparks && dt < 600) st.spark = (float)(dt / 600.0);
                    if (ef.confetti && dt < 800) st.conf = (float)(dt / 800.0);
                    if (ef.shake && dt < 260) st.shake = (float)(3 * Math.Sin(dt / 65.0 * 2 * Math.PI) * (1 - dt / 260.0));
                }
                else
                {
                    double dt = (now - a.ReleaseAt) * sp;
                    double t = Cl(dt / 220.0);
                    p = 1 - (ef.bounce ? Back(t) : EaseOut(t));
                    st.flashA = (float)(1 - Cl(dt / 180.0));
                }
            }
            switch (ef.pressMode ?? "sink")
            {
                case "shrink": st.off = (float)(press * 0.3 * p); st.scale = (float)(1 - 0.08 * p); break;
                case "pop": st.scale = (float)(1 + 0.06 * p); break;
                case "tilt": st.off = (float)(press * 0.5 * p); st.rot = (float)(-4 * p); break;
                case "jump": st.off = (float)(-Math.Max(press, 6) * p); break;
                case "none": break;
                default: st.off = (float)(press * p); break;
            }
            if (ef.textPop) st.pop = (float)(1 + 0.15 * p);
            return st;
        }
        // transformacion de la cara (encoger / crecer / inclinar) y temblor de toda la tecla
        static GraphicsState CapXf(Graphics g, St s, RectangleF cap, bool capToo)
        {
            var gs = g.Save();
            if (s.shake != 0) g.TranslateTransform(s.shake, 0);
            if (capToo && (s.scale != 1 || s.rot != 0))
            {
                float cx = cap.X + cap.Width / 2, cy = cap.Y + cap.Height / 2;
                g.TranslateTransform(cx, cy); g.RotateTransform(s.rot); g.ScaleTransform(s.scale, s.scale); g.TranslateTransform(-cx, -cy);
            }
            return gs;
        }
        static RectangleF CapRect(Config c, CKey k, St s) { float depth = c.effects.body ? c.shape.depth : 0; return new RectangleF(k.x, k.y + s.off, k.w, k.h - depth); }
        static RectangleF InnerRect(Config c, RectangleF cap) { float B = c.shape.border; return new RectangleF(cap.X + B, cap.Y + B, cap.Width - 2 * B, cap.Height - 2 * B); }

        // tecla completa (estilos, miniaturas): base + imagen comun normal + parte de arriba
        public static void Draw(Graphics g, Config c, CKey k, KeyAnim a, long now, bool selected)
        {
            DrawBase(g, c, k, a, now);
            DrawGlobalImage(g, c, k, a, now);
            DrawKeyImage(g, c, k, a, now, 2);
            DrawTop(g, c, k, a, now);
        }

        // 1) cuerpo, brillo, cara, destello y biseles
        public static void DrawBase(Graphics g, Config c, CKey k, KeyAnim a, long now)
        {
            var col = c.colors; var sh = c.shape; var ef = c.effects;
            var s = State(c, a, now); bool act = s.act;
            float R = sh.radius, B = sh.border, press = ef.body ? sh.press : 0;
            var cap = CapRect(c, k, s); var inner = InnerRect(c, cap);
            float iR = Math.Max(0, R - B);
            var gs0 = CapXf(g, s, cap, false);
            if (ef.body)
            {
                float bodyTop = Math.Max(press + 4, (float)Math.Round(k.h * 0.32));
                var br = new RectangleF(k.x, k.y + bodyTop, k.w, k.h - bodyTop);
                if (act) G.FillV(g, br, R, G.Col(col.activeBody), G.Col(col.activeBodyBottom));
                else G.FillV(g, br, R, G.Col(col.body), G.Col(col.bodyBottom));
            }
            g.Restore(gs0);
            var gs1 = CapXf(g, s, cap, true);
            if (act && ef.neon)
            {
                var nc = G.Col(col.ripple);
                for (int i = 1; i <= 7; i++)
                {
                    float sp = i * 1.8f;
                    G.Stroke(g, RectangleF.Inflate(cap, sp, sp), R + sp, G.Alpha(nc, Math.Pow(1 - i / 8.0, 2) * 0.45), 2f);
                }
            }
            if (act && ef.glow)
            {
                var gc = G.Col(col.glow);
                for (int i = 1; i <= 10; i++)
                {
                    float sp = i * 2.2f;
                    var rr = RectangleF.Inflate(cap, sp, sp);
                    double fall = Math.Pow(1 - i / 11.0, 2) * 0.16;
                    G.Stroke(g, rr, R + sp, G.Alpha(gc, fall), 2.4f);
                }
            }
            G.FillV(g, cap, R, G.Col(col.capTop), G.Col(col.capBottom));
            if (s.flashA > 0.001f)
                G.FillV(g, inner, iR, G.Alpha(G.Col(col.activeTop), s.flashA), G.Alpha(G.Col(col.activeBottom), s.flashA));
            if (s.bright > 0) G.Fill(g, inner, iR, Color.FromArgb((int)(120 * s.bright * s.flashA), 255, 255, 255));
            using (var ip = G.RR(inner, iR))
            {
                var st = g.Save();
                g.SetClip(ip, CombineMode.Intersect);
                // los biseles siguen la forma de la tecla
                if (act)
                {
                    Crescent(g, ip, 2, Color.FromArgb(55, 0, 0, 0));
                    Crescent(g, ip, 5, Color.FromArgb(35, 0, 0, 0));
                    Crescent(g, ip, -2, Color.FromArgb(64, 255, 255, 255));
                }
                else
                {
                    Crescent(g, ip, 1, Color.FromArgb(22, 255, 255, 255));
                    Crescent(g, ip, -4, Color.FromArgb(77, 0, 0, 0));
                }
                g.Restore(st);
            }
            g.Restore(gs1);
        }

        // 2) imagen comun (recortada a la cara de esta tecla). Se dibuja en una capa aparte
        //    cuando hay modo de fusion, para mezclarla pixel a pixel como Photoshop / el navegador.
        // capa con una imagen colocada/girada dentro de "region" (coordenadas de teclas), a "ss" pixeles por unidad
        public static Bitmap Layer(Image img, ImgFrame f, RectangleF region, int ss, float op)
        {
            var bmp = new Bitmap(Math.Max(1, (int)Math.Ceiling(region.Width * ss)), Math.Max(1, (int)Math.Ceiling(region.Height * ss)), PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                // sin transformaciones del Graphics (algunas versiones de GDI+ las ignoran al girar imagenes)
                DrawRotated(g, Images.Faded(img, op), (f.cx - region.X) * ss, (f.cy - region.Y) * ss, f.w * ss, f.h * ss, f.rot);
            }
            return bmp;
        }
        // rellena la forma "p" con la capa (se desplaza "off" cuando la tecla se hunde)
        public static void FillLayer(Graphics g, GraphicsPath p, Bitmap layer, RectangleF region, int ss, float off)
        {
            using (var br = new TextureBrush(layer, WrapMode.Clamp))
            {
                var mt = new Matrix();
                mt.Translate(region.X, region.Y + off);
                mt.Scale(1f / ss, 1f / ss);
                br.Transform = mt;
                g.FillPath(br, p);
            }
        }
        public static Bitmap ImageLayer(Config c, int w, int h, int ss, int mm, float op) { return ImageLayer(c, c.image, c.ImageFrame(), w, h, ss, mm, op); }
        public static Bitmap ImageLayer(Config c, CImage im, ImgFrame f, int w, int h, int ss, int mm, float op)
        {
            var img = im == null ? null : Images.Get(im.Shown()); if (img == null) return null;
            return Layer(img, f, new RectangleF(-mm, -mm, w / (float)ss, h / (float)ss), ss, op);
        }
        // imagen propia de la tecla
        public static void DrawKeyImage(Graphics g, Config c, CKey k, KeyAnim a, long now, int ss)
        {
            var im = k.img; if (im == null) return;
            var img = Images.Get(im.Shown()); if (img == null) return;
            var s = State(c, a, now);
            float op = (s.act ? im.pressedOpacity : im.opacity) / 100f;
            if (op <= 0.001f) return;
            ss = Math.Max(1, ss);
            var region = new RectangleF(k.x, k.y, k.w, k.h);
            var inner = InnerRect(c, CapRect(c, k, s));
            float iR = Math.Max(0, c.shape.radius - c.shape.border);
            var gs = CapXf(g, s, CapRect(c, k, s), true);
            using (var layer = Layer(img, c.KeyImageFrame(k), region, ss, op))
            using (var ip = G.RR(inner, iR))
                FillLayer(g, ip, layer, region, ss, s.off);
            g.Restore(gs);
        }
        public static void DrawGlobalImage(Graphics g, Config c, CKey k, KeyAnim a, long now) { DrawGlobalImage(g, c, k, a, now, null, null, 1, 0); }
        // layerN / layerP: capas de ImageLayer con la opacidad normal / pulsada (modo "span")
        public static void DrawGlobalImage(Graphics g, Config c, CKey k, KeyAnim a, long now, Bitmap layerN, Bitmap layerP, int ss, int mm)
        {
            var gim = c.image; if (gim == null) return;
            var img = Images.Get(gim.Shown()); if (img == null) return;
            var s = State(c, a, now);
            float op = (s.act ? gim.pressedOpacity : gim.opacity) / 100f;
            if (op <= 0.001f) return;
            var inner = InnerRect(c, CapRect(c, k, s));
            float iR = Math.Max(0, c.shape.radius - c.shape.border);
            var gsx = CapXf(g, s, CapRect(c, k, s), true);
            try
            {
            using (var ip = G.RR(inner, iR))
            {
                if (gim.mode == "each")
                {
                    var st = g.Save(); g.SetClip(ip, CombineMode.Intersect);
                    DrawImg(g, img, FitRect(inner, img.Width, img.Height, gim.fit), inner, op);
                    g.Restore(st);
                    return;
                }
                var layer = s.act ? layerP : layerN;
                bool own = false;
                if (layer == null) { var bb = c.KeysBounds(); ss = 2; mm = 100; layer = ImageLayer(c, (int)((bb.Width + 200) * ss), (int)((bb.Height + 200) * ss), ss, mm, op); own = true; }
                if (layer == null) return;
                FillLayer(g, ip, layer, new RectangleF(-mm, -mm, layer.Width / (float)ss, layer.Height / (float)ss), ss, s.off);
                if (own) layer.Dispose();
            }
            }
            finally { g.Restore(gsx); }
        }
        public static void DrawRotated(Graphics g, Image img, double cx, double cy, double w, double h, double rot)
        {
            double a = rot * Math.PI / 180, ca = Math.Cos(a), sa = Math.Sin(a);
            Func<double, double, PointF> P = (x, y) => new PointF((float)(cx + x * ca - y * sa), (float)(cy + x * sa + y * ca));
            var pts = new[] { P(-w / 2, -h / 2), P(w / 2, -h / 2), P(-w / 2, h / 2) };
            g.DrawImage(img, pts, new RectangleF(0, 0, img.Width, img.Height), GraphicsUnit.Pixel);
        }

        // 3) letra, borde, ondas y particulas
        public static void DrawTop(Graphics g, Config c, CKey k, KeyAnim a, long now)
        {
            var col = c.colors; var sh = c.shape; var ef = c.effects;
            var s = State(c, a, now); bool act = s.act;
            float R = sh.radius, B = sh.border;
            var cap = CapRect(c, k, s); var inner = InnerRect(c, cap);
            var gs = CapXf(g, s, cap, true);
            if (!k.hideText) { try { DrawContent(g, c, k, cap, inner, act, s.pop); } catch { } }
            if (B > 0) G.Stroke(g, new RectangleF(cap.X + B / 2, cap.Y + B / 2, cap.Width - B, cap.Height - B), R - B / 2, G.Col(act ? (ef.neon ? col.ripple : col.activeBorder) : col.border), B);
            foreach (var rp in new[] { s.rip, s.rip2 })
            {
                if (rp < 0) continue;
                double e = EaseOut(rp);
                float sc = (float)(0.96 + 0.36 * e);
                var rc = RectangleF.Inflate(cap, B, B);
                var rr = new RectangleF(rc.X + rc.Width / 2 - rc.Width * sc / 2, rc.Y + rc.Height / 2 - rc.Height * sc / 2, rc.Width * sc, rc.Height * sc);
                G.Stroke(g, RectangleF.Inflate(rr, -1.5f * sc, -1.5f * sc), R * sc, G.Alpha(G.Col(col.ripple), 0.9 * (1 - e)), 3 * sc);
            }
            g.Restore(gs);
            // particulas (salen del centro de la cara)
            float pcx = cap.X + cap.Width / 2 + s.shake, pcy = cap.Y + cap.Height / 2, dist = Math.Max(k.w, k.h) * 0.75f;
            if (s.spark >= 0)
            {
                double e = EaseOut(s.spark);
                using (var b = new SolidBrush(G.Alpha(G.Col(col.activeTop), 1 - s.spark)))
                    for (int i = 0; i < 10; i++)
                    {
                        double ang = i * 36 * Math.PI / 180;
                        float x = pcx + (float)(Math.Cos(ang) * dist * e), y = pcy + (float)(Math.Sin(ang) * dist * e);
                        float rad = (float)(3 * (1 - 0.8 * e));
                        g.FillEllipse(b, x - rad, y - rad, rad * 2, rad * 2);
                    }
            }
            if (s.conf >= 0)
            {
                double e = EaseOut(s.conf);
                var pal = new[] { col.activeTop, col.activeBottom, col.ripple, col.icon, col.glow };
                for (int i = 0; i < 14; i++)
                {
                    double ang = i * 137.5 * Math.PI / 180, d = dist * (0.5 + 0.4 * ((i * 7) % 5) / 4.0);
                    float x = pcx + (float)(Math.Cos(ang) * d * e), y = pcy + (float)((Math.Sin(ang) * d + 24) * e);
                    float rot = (float)((i % 2 == 0 ? 1 : -1) * (180 + i * 20) * e);
                    var st2 = g.Save();
                    g.TranslateTransform(x, y); g.RotateTransform(rot);
                    using (var b = new SolidBrush(G.Alpha(Color.FromArgb(255, G.Col(pal[i % pal.Length])), 1 - s.conf))) g.FillRectangle(b, -3, -2, 6, 4);
                    g.Restore(st2);
                }
            }
        }

        // ---------- modos de fusion (formulas del estandar W3C, las mismas que usa OBS/el navegador) ----------
        public static readonly string[][] BlendModes = {
            new[]{"normal","Normal"},
            new[]{"darken","Oscurecer"}, new[]{"multiply","Multiplicar"}, new[]{"color-burn","Subexponer color"},
            new[]{"lighten","Aclarar"}, new[]{"screen","Trama"}, new[]{"color-dodge","Sobreexponer color"},
            new[]{"overlay","Superponer"}, new[]{"soft-light","Luz suave"}, new[]{"hard-light","Luz fuerte"},
            new[]{"difference","Diferencia"}, new[]{"exclusion","Exclusión"},
            new[]{"hue","Tono"}, new[]{"saturation","Saturación"}, new[]{"color","Color"}, new[]{"luminosity","Luminosidad"}
        };
        static double Bsep(string m, double b, double s)
        {
            switch (m)
            {
                case "multiply": return b * s;
                case "screen": return b + s - b * s;
                case "overlay": return Bsep("hard-light", s, b);
                case "darken": return Math.Min(b, s);
                case "lighten": return Math.Max(b, s);
                case "color-dodge": return b == 0 ? 0 : s >= 1 ? 1 : Math.Min(1, b / (1 - s));
                case "color-burn": return b >= 1 ? 1 : s <= 0 ? 0 : 1 - Math.Min(1, (1 - b) / s);
                case "hard-light": return s <= 0.5 ? b * 2 * s : Bsep("screen", b, 2 * s - 1);
                case "soft-light":
                    if (s <= 0.5) return b - (1 - 2 * s) * b * (1 - b);
                    double d = b <= 0.25 ? ((16 * b - 12) * b + 4) * b : Math.Sqrt(b);
                    return b + (2 * s - 1) * (d - b);
                case "difference": return Math.Abs(b - s);
                case "exclusion": return b + s - 2 * b * s;
                default: return s;
            }
        }
        static double Lum(double r, double g, double b) { return 0.3 * r + 0.59 * g + 0.11 * b; }
        static void ClipColor(ref double r, ref double g, ref double b)
        {
            double l = Lum(r, g, b), n = Math.Min(r, Math.Min(g, b)), x = Math.Max(r, Math.Max(g, b));
            if (n < 0) { r = l + (r - l) * l / (l - n); g = l + (g - l) * l / (l - n); b = l + (b - l) * l / (l - n); }
            if (x > 1) { r = l + (r - l) * (1 - l) / (x - l); g = l + (g - l) * (1 - l) / (x - l); b = l + (b - l) * (1 - l) / (x - l); }
        }
        static void SetLum(ref double r, ref double g, ref double b, double l) { double d = l - Lum(r, g, b); r += d; g += d; b += d; ClipColor(ref r, ref g, ref b); }
        static double Sat(double r, double g, double b) { return Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)); }
        static void SetSat(ref double r, ref double g, ref double b, double s)
        {
            double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)), md = r + g + b - mx - mn;
            double nmx = 0, nmd = 0;
            if (mx > mn) { nmd = (md - mn) * s / (mx - mn); nmx = s; }
            // reasigna respetando que canal era maximo/medio/minimo
            double[] v = { r, g, b }; double[] o = new double[3];
            int imx = v[0] >= v[1] && v[0] >= v[2] ? 0 : v[1] >= v[2] ? 1 : 2;
            int imn = v[0] < v[1] && v[0] <= v[2] ? 0 : v[1] <= v[2] && v[1] < v[0] ? 1 : 2;
            if (imn == imx) imn = (imx + 1) % 3;
            int imd = 3 - imx - imn;
            o[imx] = nmx; o[imd] = nmd; o[imn] = 0;
            r = o[0]; g = o[1]; b = o[2];
        }
        static void Bnon(string m, double br, double bg, double bb, double sr, double sg, double sb, out double rr, out double rg, out double rb)
        {
            double r, g, b;
            switch (m)
            {
                case "hue": r = sr; g = sg; b = sb; SetSat(ref r, ref g, ref b, Sat(br, bg, bb)); SetLum(ref r, ref g, ref b, Lum(br, bg, bb)); break;
                case "saturation": r = br; g = bg; b = bb; SetSat(ref r, ref g, ref b, Sat(sr, sg, sb)); SetLum(ref r, ref g, ref b, Lum(br, bg, bb)); break;
                case "color": r = sr; g = sg; b = sb; SetLum(ref r, ref g, ref b, Lum(br, bg, bb)); break;
                default: r = br; g = bg; b = bb; SetLum(ref r, ref g, ref b, Lum(sr, sg, sb)); break; // luminosity
            }
            rr = r; rg = g; rb = b;
        }
        // mezcla "layer" (ya recortada a las teclas) sobre "dst" con el modo pedido. Ambas 32bppPArgb y del mismo tamano.
        public static unsafe void Blend(Bitmap dst, Bitmap layer, string mode)
        {
            var rect = new Rectangle(0, 0, dst.Width, dst.Height);
            var dd = dst.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
            var ld = layer.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            bool nonSep = mode == "hue" || mode == "saturation" || mode == "color" || mode == "luminosity";
            try
            {
                for (int y = 0; y < rect.Height; y++)
                {
                    uint* dp = (uint*)((byte*)dd.Scan0 + y * dd.Stride);
                    uint* lp = (uint*)((byte*)ld.Scan0 + y * ld.Stride);
                    for (int x = 0; x < rect.Width; x++)
                    {
                        uint L = lp[x]; uint la = L >> 24; if (la == 0) continue;
                        uint D = dp[x]; uint da = D >> 24;
                        double as_ = la / 255.0, ab = da / 255.0;
                        // des-premultiplicar
                        double sr = ((L >> 16) & 255) / 255.0 / as_, sg = ((L >> 8) & 255) / 255.0 / as_, sb = (L & 255) / 255.0 / as_;
                        double br = 0, bg = 0, bb = 0;
                        if (da > 0) { br = ((D >> 16) & 255) / 255.0 / ab; bg = ((D >> 8) & 255) / 255.0 / ab; bb = (D & 255) / 255.0 / ab; }
                        double mr, mg, mb;
                        if (nonSep) Bnon(mode, br, bg, bb, sr, sg, sb, out mr, out mg, out mb);
                        else { mr = Bsep(mode, br, sr); mg = Bsep(mode, bg, sg); mb = Bsep(mode, bb, sb); }
                        // Cs' = (1 - ab) * Cs + ab * B(Cb, Cs); luego "source-over"
                        double cr = (1 - ab) * sr + ab * mr, cg = (1 - ab) * sg + ab * mg, cb = (1 - ab) * sb + ab * mb;
                        double oa = as_ + ab * (1 - as_);
                        double pr = cr * as_ + br * ab * (1 - as_), pg = cg * as_ + bg * ab * (1 - as_), pb = cb * as_ + bb * ab * (1 - as_);
                        uint A = (uint)Math.Round(Math.Min(1, oa) * 255), R = (uint)Math.Round(Math.Min(1, Math.Max(0, pr)) * 255),
                             Gc = (uint)Math.Round(Math.Min(1, Math.Max(0, pg)) * 255), Bc = (uint)Math.Round(Math.Min(1, Math.Max(0, pb)) * 255);
                        dp[x] = (A << 24) | (R << 16) | (Gc << 8) | Bc;
                    }
                }
            }
            finally { dst.UnlockBits(dd); layer.UnlockBits(ld); }
        }

        public static RectangleF FitRect(RectangleF box, float iw, float ih, string fit)
        {
            if (fit == "stretch") return box;
            float sc = fit == "contain" ? Math.Min(box.Width / iw, box.Height / ih) : Math.Max(box.Width / iw, box.Height / ih);
            float dw = iw * sc, dh = ih * sc;
            return new RectangleF(box.X + (box.Width - dw) / 2, box.Y + (box.Height - dh) / 2, dw, dh);
        }
        // dibuja solo el trozo de la imagen que cae dentro de "box" (la cara de la tecla)
        static void DrawImg(Graphics g, Image img, RectangleF dr, RectangleF box, float op)
        {
            var vis = RectangleF.Intersect(dr, box);
            if (vis.Width <= 0.01f || vis.Height <= 0.01f || dr.Width <= 0 || dr.Height <= 0) return;
            float sx = img.Width / dr.Width, sy = img.Height / dr.Height;
            var src = new RectangleF((vis.X - dr.X) * sx, (vis.Y - dr.Y) * sy, vis.Width * sx, vis.Height * sy);
            // la opacidad se aplica a una copia de la imagen (mas fiable que ImageAttributes)
            var faded = Images.Faded(img, op);
            g.DrawImage(faded, vis, src, GraphicsUnit.Pixel);
        }
        static void Crescent(Graphics g, GraphicsPath shape, float dy, Color c)
        {
            using (var moved = (GraphicsPath)shape.Clone())
            using (var m = new Matrix())
            {
                m.Translate(0, dy); moved.Transform(m);
                using (var rg = new Region(shape))
                using (var b = new SolidBrush(c))
                {
                    rg.Exclude(moved);
                    g.FillRegion(b, rg);
                }
            }
        }
        static void DrawContent(Graphics g, Config c, CKey k, RectangleF cap, RectangleF inner, bool act, float pop)
        {
            var col = c.colors; var ef = c.effects;
            FontFamily ff; FontStyle st;
            Fonts.Get(c.font.family, c.font.weight, out ff, out st);
            float size = c.font.size;
            float spacing = (float)c.font.letterSpacing;            // puede ser negativo
            string tcase = string.IsNullOrEmpty(k.textCase) ? (k.small ? "upper" : "asis") : k.textCase;
            string raw = k.label ?? "";
            string label = k.bar ? "" : tcase == "upper" ? raw.ToUpper() : tcase == "lower" ? raw.ToLower() : raw;

            FontFamily iff = ff; FontStyle ist = st;
            if (!string.IsNullOrEmpty(k.icon) && SymbolFamily != null) { iff = SymbolFamily; ist = FontStyle.Regular; }
            float isize = Math.Max(4, (c.font.size - 4) * Math.Max(10, c.font.iconScale) / 100f);

            bool vIcon = VIcons.Is(k.icon);
            float iconW = string.IsNullOrEmpty(k.icon) ? 0 : vIcon ? isize : Measure(k.icon, iff, ist, isize);
            float labW = 0;
            var advances = new List<float>();
            for (int i = 0; i < label.Length; i++)
            {
                float w = Measure(label[i].ToString(), ff, st, size) + (i < label.Length - 1 ? spacing : 0);
                advances.Add(w); labW += w;
            }
            labW = Math.Max(0, labW);
            float barW = k.bar ? inner.Width * 0.44f : 0;
            float partW = k.bar ? barW : labW;
            float gap = (iconW > 0 && partW > 0) ? c.font.iconGap : 0;
            float total = iconW + gap + partW;
            if (total <= 0) return;
            float lh = label.Length > 0 ? LineH(ff, st, size) : 0, ilh = iconW > 0 ? (vIcon ? isize : LineH(iff, ist, isize)) : 0;
            float lineH = Math.Max(Math.Max(lh, ilh), k.bar ? 4 : 0);

            // posicion del texto en la tecla (centro, bordes o esquinas) + tamano propio + desplazamiento global
            string pos = string.IsNullOrEmpty(k.textPos) ? "center" : k.textPos;
            float ts = Math.Max(0.05f, k.textScale / 100f) * pop;
            float pad = pos == "center" ? 0 : 8;
            float bw = total * ts, bh = lineH * ts;
            float tx = pos.Contains("left") ? inner.X + pad : pos.Contains("right") ? inner.Right - pad - bw : inner.X + inner.Width / 2 - bw / 2;
            float ty = pos.StartsWith("top") ? inner.Y + pad : pos.StartsWith("bottom") ? inner.Bottom - pad - bh : inner.Y + inner.Height / 2 - bh / 2;
            tx += c.font.offsetX + k.textX; ty += c.font.offsetY + k.textY;
            var fm = new Matrix();
            fm.Translate(tx, ty); fm.Scale(ts, ts);

            Color tc = G.Col(act ? col.activeText : col.text);
            Color ic = G.Col(act ? col.activeText : col.icon);
            bool shadow = ef.textShadow && !act;
            Color? glow = act && ef.textGlow ? (Color?)G.Col(col.glow) : null;

            // la cara de la tecla hace de mascara: la letra nunca se sale
            var clipState = g.Save();
            using (var mask = G.RR(inner, Math.Max(0, c.shape.radius - c.shape.border))) g.SetClip(mask, CombineMode.Intersect);
            float x = 0;
            if (iconW > 0)
            {
                if (vIcon)
                {
                    var layers = VIcons.Paths(k.icon, 0, (lineH - ilh) / 2, isize);
                    using (var all = new GraphicsPath(FillMode.Winding))
                    {
                        foreach (var kv in layers) { kv.Value.Transform(fm); all.AddPath(kv.Value, false); }
                        if (shadow || glow.HasValue) FillText(g, all, Color.Transparent, shadow, glow);
                    }
                    foreach (var kv in layers) using (kv.Value) using (var b = new SolidBrush(Color.FromArgb((int)(ic.A * kv.Key), ic))) g.FillPath(b, kv.Value);
                }
                else
                using (var p = new GraphicsPath(FillMode.Winding))
                {
                    p.AddString(k.icon, iff, (int)ist, isize, new PointF(0, (lineH - ilh) / 2), StringFormat.GenericTypographic);
                    p.Transform(fm);
                    FillText(g, p, ic, shadow, glow);
                }
                x += iconW + gap;
            }
            if (k.bar)
            {
                using (var bp = G.RR(new RectangleF(x, lineH / 2 - 2, barW, 4), 2))
                using (var bb = new SolidBrush(G.Col(act ? (col.activeBar ?? "#00000059") : (col.bar ?? "#ffffff73"))))
                { bp.Transform(fm); g.FillPath(bb, bp); }
            }
            else if (label.Length > 0)
            {
                using (var p = new GraphicsPath(FillMode.Winding))
                {
                    for (int i = 0; i < label.Length; i++)
                    {
                        if (label[i] != ' ') p.AddString(label[i].ToString(), ff, (int)st, size, new PointF(x, (lineH - lh) / 2), StringFormat.GenericTypographic);
                        x += advances[i];
                    }
                    p.Transform(fm);
                    FillText(g, p, tc, shadow, glow);
                }
            }
            g.Restore(clipState);
        }
        static void FillText(Graphics g, GraphicsPath p, Color c, bool shadow, Color? glow)
        {
            if (glow.HasValue)
                foreach (var gw in new[] { new[] { 12f, 35f }, new[] { 7f, 60f }, new[] { 3f, 100f } })
                    using (var pen = new Pen(G.Alpha(Color.FromArgb(255, glow.Value), gw[1] / 255.0), gw[0]) { LineJoin = LineJoin.Round })
                        g.DrawPath(pen, p);
            if (shadow)
            {
                using (var sp = (GraphicsPath)p.Clone())
                {
                    var m = new Matrix(); m.Translate(0, 2); sp.Transform(m);
                    using (var b = new SolidBrush(Color.FromArgb(150, 0, 0, 0))) g.FillPath(b, sp);
                    m.Reset(); m.Translate(0, 1); sp.Transform(m);
                    using (var pen = new Pen(Color.FromArgb(60, 0, 0, 0), 2f)) g.DrawPath(pen, sp);
                }
            }
            using (var b = new SolidBrush(c)) g.FillPath(b, p);
        }
    }
}
