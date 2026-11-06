// Keyboard Overlay Studio - plantillas de juegos, estilos y seccion de efectos
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace KOStudio
{
    // posicion del texto dentro de la tecla (9 posiciones)
    public class PosGrid : Ctl
    {
        static readonly string[] ids = { "top-left", "top", "top-right", "left", "center", "right", "bottom-left", "bottom", "bottom-right" };
        static readonly string[] names = { "Arriba izquierda", "Arriba", "Arriba derecha", "Izquierda", "Centro", "Derecha", "Abajo izquierda", "Abajo", "Abajo derecha" };
        public string Value; public Action<string> Changed;
        public PosGrid(string v) { Value = string.IsNullOrEmpty(v) ? "center" : v; TabStop = true; AccessibleRole = AccessibleRole.List; AccessibleName = "Posición del texto"; }
        int C { get { return App.Hit(40); } }
        public override int Measure(int w) { return C * 3 + App.Dp(12); }
        Rectangle R(int i) { int g = App.Dp(5); return new Rectangle((i % 3) * (C + g), (i / 3) * (C + g), C, C); }
        protected override string SpeakText() { int i = Array.IndexOf(ids, Value); return "Posición del texto: " + (i >= 0 ? names[i] : "") + ". Flechas para cambiar."; }
        protected override bool IsInputKey(Keys k) { var kc = k & Keys.KeyCode; return kc == Keys.Left || kc == Keys.Right || kc == Keys.Up || kc == Keys.Down || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            int i = Math.Max(0, Array.IndexOf(ids, Value)), r = i / 3, c = i % 3;
            if (e.KeyCode == Keys.Left) c = Math.Max(0, c - 1); else if (e.KeyCode == Keys.Right) c = Math.Min(2, c + 1);
            else if (e.KeyCode == Keys.Up) r = Math.Max(0, r - 1); else if (e.KeyCode == Keys.Down) r = Math.Min(2, r + 1);
            else { base.OnKeyDown(e); return; }
            e.Handled = true; Set(ids[r * 3 + c]);
        }
        void Set(string v) { Value = v; Invalidate(); if (Changed != null) Changed(v); App.Speak(names[Array.IndexOf(ids, v)]); }
        protected override void OnMouseClick(MouseEventArgs e) { Focus(); for (int i = 0; i < 9; i++) if (R(i).Contains(e.Location)) Set(ids[i]); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = Prep(e); var T = App.T;
            for (int i = 0; i < 9; i++)
            {
                var r = R(i); var rf = new RectangleF(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
                bool sel = ids[i] == Value;
                G.Fill(g, rf, App.Dp(8), sel ? T.Card2 : T.Card);
                G.Stroke(g, rf, App.Dp(8), sel ? T.Accent : T.Line, sel ? 2.5f : 1);
                // mini "Aa" en la posicion
                float cx = rf.X + rf.Width * (0.22f + 0.28f * (i % 3)), cy = rf.Y + rf.Height * (0.25f + 0.25f * (i / 3));
                using (var b = new SolidBrush(sel ? T.Accent2 : T.Muted)) g.FillEllipse(b, cx - App.Dp(3), cy - App.Dp(3), App.Dp(6), App.Dp(6));
                if (Focused && sel) G.Ring(g, r, App.Dp(8));
            }
            TextRenderer.DrawText(g, names[Math.Max(0, Array.IndexOf(ids, Value))], App.F(9.5f, true), new Rectangle(C * 3 + App.Dp(24), 0, Width - C * 3 - App.Dp(24), C * 3), T.Muted, G.Wrap | TextFormatFlags.VerticalCenter);
        }
    }

    public partial class MainForm
    {
        // ================================================================
        // PLANTILLAS DE JUEGOS
        // ================================================================
        class GK
        {
            public string L, K, Icon; public double X, Y, W = 1, H = 1; public bool Small, Bar, Abs;
            public GK(string label, string key, double x, double y) { L = label; K = key; X = x; Y = y; }
        }
        static GK K(string l, double x, double y) { return new GK(l, l, x, y); }
        static GK K(string l, string key, double x, double y, double w) { return new GK(l, key, x, y) { W = w, Small = l.Length > 2 }; }
        static GK Bld(string l, string icon, double x, double y) { return new GK(l, l, x, y) { Icon = icon }; }   // tecla con icono de construir
        // tecla colocada en pixeles exactos del lienzo (x, y, ancho, alto)
        static GK Px(string l, string key, string icon, double x, double y, double w, double h)
        { return new GK(l, key, x, y) { Icon = icon, W = w, H = h, Abs = true, Small = l.Length > 2, Bar = key == "Espacio" }; }
        static GK Sp(double x, double y, double w) { return new GK("", "Espacio", x, y) { W = w, H = 0.75, Bar = true }; }

        class Game { public string Name, Sub; public GK[] Keys; public string Json; }
        // plantilla guardada tal cual (teclas exactas en JSON); Keys solo sirve para el dibujo de la tarjeta
        static Game Raw(string name, string sub, string json)
        {
            var ks = KOStudio.Json.Read<List<CKey>>(json);
            return new Game { Name = name, Sub = sub, Json = json,
                Keys = ks.Select(k => new GK(string.IsNullOrEmpty(k.label) && !string.IsNullOrEmpty(k.icon) ? VIcons.Name(k.icon) : k.label ?? "", "", k.x, k.y) { W = k.w, H = k.h, Abs = true, Bar = k.bar }).ToArray() };
        }
        const string FortniteJson = @"[{""label"":""1"",""icon"":"""",""codes"":[2],""x"":274,""y"":36,""w"":86,""h"":94,""textPos"":""center"",""textScale"":100,""textX"":0,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":false,""hideText"":false},{""label"":""2"",""icon"":"""",""codes"":[3],""x"":366,""y"":36,""w"":86,""h"":94,""textPos"":""center"",""textScale"":100,""textX"":0,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":false,""hideText"":false},{""label"":""3"",""icon"":"""",""codes"":[4],""x"":459,""y"":36,""w"":86,""h"":94,""textPos"":""center"",""textScale"":100,""textX"":0,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":false,""hideText"":false},{""label"":""4"",""icon"":"""",""codes"":[5],""x"":551,""y"":36,""w"":86,""h"":94,""textPos"":""center"",""textScale"":100,""textX"":0,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":false,""hideText"":false},{""label"":""5"",""icon"":"""",""codes"":[6],""x"":644,""y"":37,""w"":86,""h"":94,""textPos"":""center"",""textScale"":100,""textX"":0,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":false,""hideText"":false},{""label"":""W"",""icon"":"""",""codes"":[17],""x"":390,""y"":137,""w"":108,""h"":127,""textPos"":""center"",""textScale"":100,""textX"":0,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":false,""hideText"":false},{""label"":""R"",""icon"":"""",""codes"":[19],""x"":622,""y"":137,""w"":108,""h"":127,""textPos"":""center"",""textScale"":100,""textX"":0,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":false,""hideText"":false},{""label"":"""",""icon"":""@editar"",""codes"":[34],""x"":124,""y"":136,""w"":134,""h"":127,""textPos"":""center"",""textScale"":60,""textX"":0,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":false,""hideText"":false},{""label"":""A"",""icon"":"""",""codes"":[30],""x"":274,""y"":272,""w"":108,""h"":127,""textPos"":""center"",""textScale"":100,""textX"":0,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":false,""hideText"":false},{""label"":""S"",""icon"":"""",""codes"":[31],""x"":390,""y"":272,""w"":108,""h"":127,""textPos"":""center"",""textScale"":100,""textX"":0,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":false,""hideText"":false},{""label"":""D"",""icon"":"""",""codes"":[32],""x"":506,""y"":272,""w"":108,""h"":127,""textPos"":""center"",""textScale"":100,""textX"":0,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":false,""hideText"":false},{""label"":""F"",""icon"":"""",""codes"":[33],""x"":622,""y"":272,""w"":108,""h"":127,""textPos"":""center"",""textScale"":100,""textX"":0,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":false,""hideText"":false},{""label"":"""",""icon"":""@muro"",""codes"":[44],""x"":0,""y"":0,""w"":108,""h"":127,""textPos"":""center"",""textScale"":60,""textX"":0,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":false,""hideText"":false},{""label"":"""",""icon"":""@suelo"",""codes"":[45],""x"":0,""y"":136,""w"":108,""h"":127,""textPos"":""center"",""textScale"":60,""textX"":0,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":false,""hideText"":false},{""label"":"""",""icon"":""@escalera"",""codes"":[46],""x"":0,""y"":271,""w"":108,""h"":127,""textPos"":""center"",""textScale"":60,""textX"":0,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":false,""hideText"":false},{""label"":"""",""icon"":""@techo"",""codes"":[47],""x"":0,""y"":406,""w"":108,""h"":127,""textPos"":""center"",""textScale"":60,""textX"":0,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":false,""hideText"":false},{""label"":""Shift"",""icon"":"""",""codes"":[42,54],""x"":124,""y"":306,""w"":134,""h"":94,""textPos"":""center"",""textScale"":90,""textX"":0,""textY"":0,""textCase"":""upper"",""small"":true,""bar"":false,""hideText"":false},{""label"":""Ctrl"",""icon"":"""",""codes"":[29,3613],""x"":124,""y"":406,""w"":134,""h"":94,""textPos"":""center"",""textScale"":90,""textX"":0,""textY"":0,""textCase"":""upper"",""small"":true,""bar"":false,""hideText"":false},{""label"":"""",""icon"":"""",""codes"":[57],""x"":274,""y"":407,""w"":456,""h"":94,""textPos"":""center"",""textScale"":100,""textX"":0,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":true,""hideText"":false},{""label"":"""",""icon"":""@pico"",""codes"":[58],""x"":124,""y"":36,""w"":134,""h"":94,""textPos"":""center"",""textScale"":60,""textX"":-3,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":false,""hideText"":false},{""label"":""E"",""icon"":"""",""codes"":[18],""x"":506,""y"":137,""w"":108,""h"":127,""textPos"":""center"",""textScale"":100,""textX"":0,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":false,""hideText"":false},{""label"":""Q"",""icon"":"""",""codes"":[16],""x"":274,""y"":136,""w"":108,""h"":127,""textPos"":""center"",""textScale"":100,""textX"":0,""textY"":0,""textCase"":""asis"",""small"":false,""bar"":false,""hideText"":false}]";
        static readonly Game[] Games = {
            new Game { Name = "League of Legends", Sub = "Q W E R · D F · B", Keys = new[] {
                K("Q",0,0), K("W",1,0), K("E",2,0), K("R",3,0), K("D",0.5,1), K("F",1.5,1), K("B",2.5,1),
                K("Clic I","Clic I",0,2,1.5), K("Clic D","Clic D",1.6,2,1.5) } },
            new Game { Name = "Valorant", Sub = "Movimiento + habilidades C Q E X", Keys = new[] {
                K("W",1,0), K("C",3.2,0), K("Q",4.2,0), K("E",5.2,0), K("X",6.2,0),
                K("A",0,1), K("S",1,1), K("D",2,1), K("R",3.2,1), K("F",4.2,1),
                K("Shift","Shift",0,2,1.4), K("Ctrl","Ctrl",1.5,2,1.4), Sp(3.2,2,3) } },
            new Game { Name = "Counter-Strike 2", Sub = "Movimiento, comprar, soltar", Keys = new[] {
                K("Q",0,0), K("W",1,0), K("E",2,0), K("R",3,0), K("B",4,0),
                K("A",0,1), K("S",1,1), K("D",2,1), K("G",3,1),
                K("Shift","Shift",0,2,1.4), K("Ctrl","Ctrl",1.5,2,1.4), Sp(3,2,2) } },
            // plantilla exacta hecha por Sebb (exportada desde el programa): construir con iconos, pico, editar, armas y movimiento
            Raw("Fortnite", "Construir con iconos, pico, editar, armas y movimiento", FortniteJson),
            new Game { Name = "Overwatch 2", Sub = "Habilidades Shift E Q", Keys = new[] {
                K("Q",0,0), K("W",1,0), K("E",2,0), K("R",3,0),
                K("A",0,1), K("S",1,1), K("D",2,1), K("Shift","Shift",3,1,1.3),
                K("Ctrl","Ctrl",0,2,1.4), Sp(1.5,2,2), K("Clic D","Clic D",3.6,2,1.3) } },
            new Game { Name = "Dota 2 / MOBA", Sub = "Q W E R · D F", Keys = new[] {
                K("Q",0,0), K("W",1,0), K("E",2,0), K("R",3,0), K("D",1,1), K("F",2,1) } },
            new Game { Name = "osu!", Sub = "Z X + clics", Keys = new[] {
                K("Z",0,0), K("X",1,0), K("Clic I","Clic I",0,1,1), K("Clic D","Clic D",1,1,1) } },
            new Game { Name = "Ritmo 4K", Sub = "D F · J K", Keys = new[] { K("D",0,0), K("F",1,0), K("J",2.3,0), K("K",3.3,0) } },
            new Game { Name = "Plataformas", Sub = "Flechas + Z X C", Keys = new[] {
                K("↑","Flecha arriba",1,0,1), K("←","Flecha izquierda",0,1,1), K("↓","Flecha abajo",1,1,1), K("→","Flecha derecha",2,1,1),
                K("Z",3.4,1), K("X",4.4,1), K("C",5.4,1) } },
            new Game { Name = "Básica", Sub = "WASD · Shift · Ctrl · Espacio", Keys = new[] {
                K("W",1,0), K("A",0,1), K("S",1,1), K("D",2,1), K("Shift","Shift",3,1,1.4), Sp(0,2,2.1), K("Ctrl","Ctrl",2.2,2,2.1) } },
        };

        static List<int> CodesFor(string key)
        {
            switch (key)
            {
                case "Shift": return new List<int> { 0x2A, 0x36 };
                case "Ctrl": return new List<int> { 0x1D, 0x0E1D };
                case "Espacio": return new List<int> { 0x39 };
                case "Clic I": return new List<int> { KeyCodes.MOUSE + 1 };
                case "Clic D": return new List<int> { KeyCodes.MOUSE + 2 };
            }
            var it = KeyCodes.List.FirstOrDefault(x => x.Name == key);
            return it == null ? new List<int>() : new List<int> { it.VC };
        }
        List<CKey> BuildGame(Game gm, int y0)
        {
            if (gm.Json != null)
            {
                var c = new Config { keys = KOStudio.Json.Read<List<CKey>>(gm.Json) };
                int i = 0; foreach (var k in c.keys) { k.id = Config.NewId() + "g" + (i++); k.y += y0; }
                c.Normalize();
                return c.keys;
            }
            var list = new List<CKey>();
            int n = 0;
            foreach (var g in gm.Keys)
            {
                int w = g.Abs ? (int)g.W : (int)Math.Round(g.W * 80 + (g.W - 1) * 6), h = g.Abs ? (int)g.H : (int)Math.Round(g.H * 94);
                var k = new CKey { id = Config.NewId() + "g" + (n++), label = g.L, icon = g.Icon ?? "", bar = g.Bar, codes = CodesFor(g.K),
                    x = g.Abs ? (int)g.X : (int)Math.Round(g.X * 86), y = y0 + (g.Abs ? (int)g.Y : (int)Math.Round(g.Y * 100)), w = w, h = h, textCase = g.Small ? "upper" : "asis", small = g.Small };
                // tecla solo con icono: que el icono ocupe ~45% de la tecla, sea cual sea el tamaño de letra del estilo
                if (string.IsNullOrEmpty(g.L) && !string.IsNullOrEmpty(g.Icon))
                {
                    double isize = Math.Max(4, (cfg.font.size - 4) * Math.Max(10, cfg.font.iconScale) / 100.0);
                    k.textScale = (int)Math.Max(10, Math.Min(400, Math.Round(0.45 * Math.Min(w, h) / isize * 100)));
                }
                list.Add(k);
            }
            return list;
        }
        void ApplyGame(Game gm) { ApplyLayout(gm.Name, BuildGame(gm, 0)); }
        void ApplyLayout(string name, List<CKey> ks)
        {
            int r = cfg.keys.Count == 0 ? 2 : DarkDialog.Ask(this, name, "¿Quieres cambiar tus teclas por las de «" + name + "» o añadirlas en el centro del lienzo junto a las que ya tienes? (Ctrl+Z para deshacer)", new[] { "Cancelar", "Añadir en el centro", "Reemplazar" }, 2);
            if (r <= 0) return;
            if (r == 2) { cfg.keys = ks; CenterBlock(); }
            else AddCentered(ks);
            selId = ks.Count > 0 ? ks[0].id : null; pv.SelId = selId; pv.Multi.Clear();
            Changed(true); BuildInspector(false);
            var nb = cfg.KeysBounds();
            bool fits = nb.Width * cfg.position.scale <= cfg.canvas.width && nb.Height * cfg.position.scale <= cfg.canvas.height;
            pv.Toast("Plantilla «" + name + "» " + (r == 2 ? "puesta" : "añadida") + (fits ? "." : ". No cabe entera: usa «Ajustar el lienzo al teclado» en la sección OBS."), !fits);
        }
        void GameTemplates()
        {
            MyLayouts();
            Sub("Plantillas de juegos");
            var ch = new Choices("Plantillas de juegos") { Cols = 2, CardH = 104, Radio = false };
            foreach (var gm in Games)
            {
                var game = gm;
                ch.Items.Add(new Choice { Title = gm.Name, Sub = gm.Sub, Paint = (g, r, s) => PaintLayout(g, r, game) });
            }
            ch.Picked = i => ApplyGame(Games[i]);
            Add(ch, App.Dp(14));
        }
        static void PaintLayout(Graphics g, Rectangle r, Game gm)
        {
            double mx = 0, my = 0;
            foreach (var k in gm.Keys) { mx = Math.Max(mx, k.X + k.W); my = Math.Max(my, k.Y + k.H); }
            float u = (float)Math.Min(r.Width / mx, r.Height / my);
            float ox = r.X + (r.Width - (float)mx * u) / 2, oy = r.Y + (r.Height - (float)my * u) / 2;
            foreach (var k in gm.Keys)
            {
                var kr = new RectangleF(ox + (float)k.X * u, oy + (float)k.Y * u, (float)k.W * u - 2, (float)k.H * u - 2);
                G.Fill(g, kr, Math.Min(4, u * 0.15f), App.T.Card2);
                G.Stroke(g, kr, Math.Min(4, u * 0.15f), App.T.Line, 1);
                float ku = k.Abs ? 94 * u : u;   // tamaño de una tecla normal en la tarjeta (las plantillas en pixeles usan 94 px)
                if (!k.Bar && ku > 12)
                    TextRenderer.DrawText(g, k.L.Length > 3 ? k.L.Substring(0, 3) : k.L, App.F(Math.Max(5.5f, Math.Min(8, ku * 0.28f)), true), Rectangle.Round(kr), App.T.Text, G.Line | TextFormatFlags.HorizontalCenter);
            }
        }

        // ================================================================
        // ESTILOS (por categorias)
        // ================================================================
        class PresetDef { public string Name, Sub, Cat; public Action<Config> Apply; }
        static void Pal(Config c, string top, string bottom, string ink) { ApplyAccent(c.colors, top, bottom, ink); }
        static void Bas(Config c, string capTop, string capBottom, string border, string text, string body, string bodyBottom)
        { var x = c.colors; x.capTop = capTop; x.capBottom = capBottom; x.border = border; x.text = text; x.body = body; x.bodyBottom = bodyBottom; }
        static void Fnt(Config c, string fam, int weight) { c.font.family = fam; c.font.weight = weight; }

        static readonly PresetDef[] Presets = {
            // ---- clasicos ----
            new PresetDef { Cat = "Clásicos", Name = "Original SBB", Sub = "Grafito y naranja", Apply = c => { } },
            new PresetDef { Cat = "Clásicos", Name = "Hielo", Sub = "Azul noche y celeste", Apply = c => { ApplyBase(c.colors, BASES[4]); Pal(c, "#8fdcff", "#2b8fd6", "#00213d"); } },
            new PresetDef { Cat = "Clásicos", Name = "Minimal", Sub = "Teclas blancas", Apply = c => { ApplyBase(c.colors, BASES[2]); c.effects.textShadow = false; } },
            new PresetDef { Cat = "Clásicos", Name = "Carbono", Sub = "Negro mate y blanco", Apply = c => { Bas(c, "#2a2a2aff", "#111111ff", "#ffffff1a", "#e6e6e6ff", "#0a0a0ae6", "#000000b3"); Pal(c, "#ffffff", "#c9ced6", "#16181c"); c.shape.radius = 8; } },
            new PresetDef { Cat = "Clásicos", Name = "Oro", Sub = "Negro y dorado", Apply = c => { Bas(c, "#1e1b14ff", "#0b0a07ff", "#e7c66b55", "#f5e6b8ff", "#0b0a07e6", "#000000b3"); Pal(c, "#ffe08a", "#c9a227", "#2b2000"); Fnt(c, "Montserrat", 800); c.effects.ripple2 = true; } },
            new PresetDef { Cat = "Clásicos", Name = "Papel", Sub = "Plano y limpio", Apply = c => { Bas(c, "#fbfaf7ff", "#ebe7dfff", "#00000022", "#2b2b2bff", "#cfc8bbe6", "#a69f92b3"); Pal(c, "#3a3a3a", "#111111", "#ffffff"); c.effects.textShadow = false; c.effects.body = false; c.effects.flash = false; c.effects.pressMode = "shrink"; c.shape.radius = 10; } },
            // ---- neon ----
            new PresetDef { Cat = "Neón y brillo", Name = "Neón", Sub = "Negro y verde neón", Apply = c => { ApplyBase(c.colors, BASES[1]); Pal(c, "#9dff8a", "#22b455", "#06240f"); c.shape.radius = 12; c.effects.neon = true; c.effects.textGlow = true; } },
            new PresetDef { Cat = "Neón y brillo", Name = "Cyberpunk", Sub = "Amarillo eléctrico", Apply = c => { Bas(c, "#14141aff", "#07070bff", "#f3e60066", "#f3e600ff", "#07070be6", "#000000b3"); Pal(c, "#fff36b", "#f3e600", "#1a1a00"); c.colors.icon = "#00f0ffff"; Fnt(c, "Orbitron", 800); c.shape.radius = 2; c.effects.neon = true; c.effects.sparks = true; c.effects.shake = true; } },
            new PresetDef { Cat = "Neón y brillo", Name = "Synthwave", Sub = "Rosa y violeta 80s", Apply = c => { Bas(c, "#2a1446ff", "#12082aff", "#ff4fd866", "#ffd6f6ff", "#12082ae6", "#05001ab3"); Pal(c, "#ff8de8", "#b026ff", "#2a0033"); Fnt(c, "Exo 2", 800); c.effects.neon = true; c.effects.textGlow = true; c.effects.ripple2 = true; } },
            new PresetDef { Cat = "Neón y brillo", Name = "Matrix", Sub = "Verde digital", Apply = c => { Bas(c, "#0c140cff", "#030803ff", "#39ff1444", "#39ff14ff", "#030803e6", "#000000b3"); Pal(c, "#7dff6b", "#0f9d22", "#002200"); Fnt(c, "Chakra Petch", 700); c.shape.radius = 4; c.effects.textGlow = true; c.effects.sparks = true; } },
            new PresetDef { Cat = "Neón y brillo", Name = "Plasma", Sub = "Cian y magenta", Apply = c => { Bas(c, "#101a33ff", "#070d1cff", "#4cc3ff55", "#e6f7ffff", "#070d1ce6", "#000000b3"); Pal(c, "#7af1ff", "#d946ef", "#1b0026"); c.effects.ripple2 = true; c.effects.pressMode = "pop"; c.effects.neon = true; } },
            new PresetDef { Cat = "Neón y brillo", Name = "Lava", Sub = "Rojo fuego con chispas", Apply = c => { Bas(c, "#2a1210ff", "#120605ff", "#ff6a3d55", "#ffe2d6ff", "#120605e6", "#000000b3"); Pal(c, "#ffb347", "#ff3d00", "#2a0800"); c.effects.sparks = true; c.effects.shake = true; c.effects.glow = true; } },
            // ---- juegos ----
            new PresetDef { Cat = "Inspirados en juegos", Name = "Valorant", Sub = "Rojo táctico", Apply = c => { Bas(c, "#1f2731ff", "#0f1923ff", "#ff465533", "#ece8e1ff", "#0f1923e6", "#05080cb3"); Pal(c, "#ff6b76", "#ff4655", "#ffffff"); Fnt(c, "Bebas Neue", 400); c.font.size = 34; c.shape.radius = 2; c.effects.sparks = true; } },
            new PresetDef { Cat = "Inspirados en juegos", Name = "League", Sub = "Azul y oro hextech", Apply = c => { Bas(c, "#0a1428ff", "#010a13ff", "#c8aa6e66", "#f0e6d2ff", "#010a13e6", "#000000b3"); Pal(c, "#f0d58a", "#c8aa6e", "#1e1205"); Fnt(c, "Cinzel", 700); c.effects.ripple2 = true; c.effects.glow = true; } },
            new PresetDef { Cat = "Inspirados en juegos", Name = "Bloques", Sub = "Pixelado y verde", Apply = c => { Bas(c, "#7a7a7aff", "#4d4d4dff", "#00000088", "#ffffffff", "#3a3a3ae6", "#1f1f1fb3"); Pal(c, "#7ed957", "#3f8f2b", "#0b2205"); Fnt(c, "Press Start 2P", 400); c.font.size = 16; c.shape.radius = 0; c.shape.border = 3; } },
            new PresetDef { Cat = "Inspirados en juegos", Name = "Battle royale", Sub = "Azul y amarillo fiesta", Apply = c => { Bas(c, "#2b2f86ff", "#1a1c55ff", "#ffffff33", "#ffffffff", "#1a1c55e6", "#0b0c2bb3"); Pal(c, "#ffe14d", "#ff9f1c", "#2b1400"); Fnt(c, "Luckiest Guy", 400); c.effects.pressMode = "pop"; c.effects.confetti = true; c.shape.radius = 14; } },
            new PresetDef { Cat = "Inspirados en juegos", Name = "Rocket", Sub = "Naranja turbo", Apply = c => { Bas(c, "#0e2a47ff", "#071a2eff", "#2fa8ff55", "#e8f4ffff", "#071a2ee6", "#000000b3"); Pal(c, "#ff9a2e", "#ff5e00", "#2b1000"); Fnt(c, "Russo One", 400); c.effects.sparks = true; c.effects.ripple2 = true; } },
            new PresetDef { Cat = "Inspirados en juegos", Name = "Calle", Sub = "Verde de ciudad", Apply = c => { Bas(c, "#1d1d1dff", "#0b0b0bff", "#ffffff22", "#f2f2f2ff", "#0b0b0be6", "#000000b3"); Pal(c, "#9be36b", "#4caf50", "#0b2a0b"); Fnt(c, "Bangers", 400); c.font.size = 32; c.effects.shake = true; } },
            new PresetDef { Cat = "Inspirados en juegos", Name = "Héroes", Sub = "Blanco y naranja", Apply = c => { Bas(c, "#f4f6f8ff", "#d5dbe2ff", "#00000022", "#1f2933ff", "#9aa5b1e6", "#52606db3"); Pal(c, "#ffb14d", "#f99e1a", "#2b1400"); Fnt(c, "Teko", 600); c.font.size = 34; c.shape.radius = 6; c.effects.textShadow = false; } },
            new PresetDef { Cat = "Inspirados en juegos", Name = "Leyendas", Sub = "Gris y rojo", Apply = c => { Bas(c, "#2b2b2bff", "#141414ff", "#da292a55", "#ffffffff", "#141414e6", "#000000b3"); Pal(c, "#ff5a5a", "#da292a", "#ffffff"); Fnt(c, "Rajdhani", 700); c.shape.radius = 3; } },
            new PresetDef { Cat = "Inspirados en juegos", Name = "Arcade", Sub = "Cuadradas y rojas", Apply = c => { ApplyBase(c.colors, BASES[5]); Pal(c, "#ff9a8a", "#dc2626", "#3a0505"); c.shape.radius = 4; Fnt(c, "Press Start 2P", 400); c.font.size = 16; c.effects.confetti = true; } },
            // ---- suaves ----
            new PresetDef { Cat = "Suaves y pastel", Name = "Chicle", Sub = "Redondas y rosas", Apply = c => { ApplyBase(c.colors, BASES[0]); Pal(c, "#ffa6d8", "#db2777", "#3a0620"); c.shape.radius = 40; Fnt(c, "Fredoka", 700); } },
            new PresetDef { Cat = "Suaves y pastel", Name = "Menta", Sub = "Verde agua suave", Apply = c => { Bas(c, "#e9fbf5ff", "#c4eee0ff", "#00000018", "#1d4d43ff", "#8cc9b7e6", "#5e9c8ab3"); Pal(c, "#9ef0d0", "#3cc79a", "#0b3328"); Fnt(c, "Nunito", 800); c.shape.radius = 22; c.effects.textShadow = false; c.effects.pressMode = "pop"; } },
            new PresetDef { Cat = "Suaves y pastel", Name = "Lavanda", Sub = "Lila tranquilo", Apply = c => { Bas(c, "#f3eeffff", "#ddd2fbff", "#00000018", "#3d2a6bff", "#a99ad6e6", "#7a6aa8b3"); Pal(c, "#cdb8ff", "#8b5cf6", "#ffffff"); Fnt(c, "Quicksand", 700); c.shape.radius = 22; c.effects.textShadow = false; } },
            new PresetDef { Cat = "Suaves y pastel", Name = "Melocotón", Sub = "Cálido y redondo", Apply = c => { Bas(c, "#fff1e8ff", "#ffd9c2ff", "#00000018", "#6b3420ff", "#e0a78ae6", "#b77a5eb3"); Pal(c, "#ffc29e", "#ff7b54", "#3a1206"); Fnt(c, "Fredoka", 600); c.shape.radius = 30; c.effects.textShadow = false; c.effects.confetti = true; } },
            new PresetDef { Cat = "Suaves y pastel", Name = "Nube", Sub = "Blanco esponjoso", Apply = c => { Bas(c, "#ffffffff", "#eef2f7ff", "#00000014", "#5b6b80ff", "#cfd8e3e6", "#a9b5c4b3"); Pal(c, "#bfe3ff", "#7cc2ff", "#0b2a44"); Fnt(c, "Nunito", 900); c.shape.radius = 34; c.effects.textShadow = false; c.effects.pressMode = "shrink"; } },
            // ---- especiales ----
            new PresetDef { Cat = "Especiales", Name = "Cristal", Sub = "Transparentes", Apply = c => { ApplyBase(c.colors, BASES[3]); Pal(c, "#8fdcff", "#2b8fd6", "#00213d"); c.effects.body = false; } },
            new PresetDef { Cat = "Especiales", Name = "Fantasma", Sub = "Solo el contorno", Apply = c => { Bas(c, "#ffffff10", "#ffffff05", "#ffffffaa", "#ffffffff", "#ffffff08", "#ffffff04"); Pal(c, "#ffffff", "#c9ced6", "#16181c"); c.shape.border = 2; c.effects.body = false; c.effects.neon = true; } },
            new PresetDef { Cat = "Especiales", Name = "Violeta", Sub = "Morado con brillo", Apply = c => { ApplyBase(c.colors, BASES[0]); Pal(c, "#cfa6ff", "#7c3aed", "#1d0640"); c.effects.textGlow = true; } },
            new PresetDef { Cat = "Especiales", Name = "Fiesta", Sub = "Confeti y saltos", Apply = c => { Bas(c, "#1b1035ff", "#0d0820ff", "#ffffff22", "#ffffffff", "#0d0820e6", "#000000b3"); Pal(c, "#ffe14d", "#ff4fa3", "#2a0016"); c.effects.pressMode = "jump"; c.effects.confetti = true; c.effects.ripple2 = true; c.shape.radius = 20; } },
        };
        static Config PresetConfig(PresetDef p) { var c = new Config(); p.Apply(c); return c; }

        void SecPresets()
        {
            Head("Estilos", "Empieza desde un estilo hecho. Cambia colores, forma, letra y efectos: tus teclas e imágenes se quedan.");
            MyStyles();
            foreach (var cat in Presets.Select(p => p.Cat).Distinct().ToList())
            {
                Sub(cat);
                var defs = Presets.Where(p => p.Cat == cat).ToList();
                var ch = new Choices("Estilos " + cat) { Cols = 2, CardH = 128, Radio = false };
                foreach (var d in defs)
                {
                    var pc = PresetConfig(d);
                    ch.Items.Add(new Choice
                    {
                        Title = d.Name, Sub = d.Sub,
                        Paint = (g, r, s) =>
                        {
                            var k1 = new CKey { id = "a", label = "W", x = 0, y = 0, w = 80, h = 94, textCase = "asis" };
                            var k2 = new CKey { id = "b", label = "A", x = 86, y = 0, w = 80, h = 94, textCase = "asis" };
                            float sc = Math.Min(r.Width / 176f, r.Height / 100f);
                            var st = g.Save();
                            g.TranslateTransform(r.X + (r.Width - 166 * sc) / 2, r.Y + (r.Height - 94 * sc) / 2);
                            g.ScaleTransform(sc, sc);
                            KeyPainter.Draw(g, pc, k1, null, 100000, false);
                            KeyPainter.Draw(g, pc, k2, new KeyAnim { Active = true, PressAt = 0 }, 100000, false);
                            g.Restore(st);
                        }
                    });
                }
                ch.Picked = i =>
                {
                    var p = PresetConfig(defs[i]);
                    cfg.colors = p.colors; cfg.shape = p.shape; cfg.effects = p.effects; cfg.font = p.font;
                    Changed(true); pv.Toast("Estilo «" + defs[i].Name + "» aplicado (Ctrl+Z para deshacer)", false);
                };
                Add(ch, App.Dp(14));
            }
            Sub("Copias de seguridad");
            Row(B("Exportar estilo…", BtnKind.Normal, null, ExportJson), B("Importar estilo…", BtnKind.Normal, null, ImportJson));
            Add(new Para("Al guardar, la versión anterior queda en config-anterior.js por si quieres volver atrás.", 9, false, true));
            Sub("Volver al principio");
            Row(B("Restablecer el diseño original", BtnKind.Danger, null, () =>
            {
                if (DarkDialog.Ask(this, "¿Restablecer el diseño original?", "Vuelve al teclado W A S D · SHIFT · ESPACIO · CTRL naranja. Puedes deshacerlo con Ctrl+Z.", new[] { "Cancelar", "Restablecer" }, 1) != 1) return;
                cfg = new Config(); cfg.Normalize(); pv.Cfg = cfg; selId = cfg.keys[0].id; pv.SelId = selId;
                Changed(true); pv.Toast("Diseño original restaurado", false);
            }));
        }

        // ================================================================
        // EFECTOS (por categorias)
        // ================================================================
        void SecEffects()
        {
            var ef = cfg.effects;
            Head("Efectos", "Las animaciones al pulsar una tecla. Pulsa tus teclas (o «Probar») para verlas en la vista previa.");
            Row(B("Probar efectos", BtnKind.Primary, null, TestEffects));

            Sub("Al pulsar · movimiento");
            var modes = new[] { "sink", "shrink", "pop", "tilt", "jump", "none" };
            var mn = new[] { "Hundir", "Encoger", "Crecer", "Inclinar", "Saltar", "Quieta" };
            var ms = new[] { "Baja como una tecla real", "Se hace pequeña", "Se agranda un poco", "Gira un poco", "Sube en vez de bajar", "No se mueve" };
            var ch = new Choices("Movimiento al pulsar") { Cols = 3, CardH = 66 };
            for (int i = 0; i < modes.Length; i++) ch.Items.Add(new Choice { Title = mn[i], Sub = ms[i] });
            ch.Selected = Math.Max(0, Array.IndexOf(modes, ef.pressMode ?? "sink"));
            ch.Picked = i => { ef.pressMode = modes[i]; Changed(true); TestEffects(); };
            Add(ch, App.Dp(12));
            Tg("Rebote al soltar", "La tecla vuelve con un pequeño rebote.", () => ef.bounce, v => ef.bounce = v);
            Tg("Temblor", "Toda la tecla tiembla un instante al pulsarla.", () => ef.shake, v => ef.shake = v);

            Sub("Al pulsar · luz");
            Tg("Destello", "Un golpe de luz blanca al pulsar.", () => ef.flash, v => ef.flash = v);
            Tg("Brillo alrededor", "Un halo de color mientras está pulsada (color «Brillo alrededor»).", () => ef.glow, v => ef.glow = v);
            Tg("Borde de neón", "El borde se ilumina como un tubo de neón (color «Onda»).", () => ef.neon, v => ef.neon = v);

            Sub("Partículas y ondas");
            Tg("Onda", "Un aro que sale de la tecla y se desvanece.", () => ef.ripple, v => ef.ripple = v);
            Tg("Doble onda", "Un segundo aro un instante después.", () => ef.ripple2, v => ef.ripple2 = v);
            Tg("Chispas", "Puntos de luz que salen disparados desde el centro.", () => ef.sparks, v => ef.sparks = v);
            Tg("Confeti", "Papelitos de colores que saltan y caen.", () => ef.confetti, v => ef.confetti = v);

            Sub("Letra");
            Tg("Sombra de la letra", "Ayuda a leer la letra sobre fondos claros.", () => ef.textShadow, v => ef.textShadow = v);
            Tg("Letra que brilla al pulsar", "La letra se ilumina con el color «Brillo alrededor».", () => ef.textGlow, v => ef.textGlow = v);
            Tg("Letra que crece al pulsar", "La letra se agranda un poco mientras está pulsada.", () => ef.textPop, v => ef.textPop = v);

            Sub("Velocidad");
            S("Velocidad de las animaciones", 25, 300, 5, "%", "100% = normal. Más alto = más rápido.", () => ef.speed, v => ef.speed = (int)v);
        }
        void TestEffects()
        {
            foreach (var k in cfg.keys) pv.SetPressed(k.id, true);
            pv.Invalidate();
            var t = new Timer { Interval = 450 };
            t.Tick += (s, e) => { t.Stop(); t.Dispose(); foreach (var k in cfg.keys) pv.SetPressed(k.id, false); pv.Invalidate(); Lights(); };
            t.Start();
        }

        // ================================================================
        // MIS ESTILOS / MIS PLANTILLAS (guardados por el usuario, en esta carpeta)
        // ================================================================
        public class SavedStyle { public string name; public CColors colors; public CShape shape; public CEffects effects; public CFont font; public CImage image; }
        public class SavedLayout { public string name; public List<CKey> keys; }
        string StylesPath { get { return System.IO.Path.Combine(App.Dir, "mis-estilos.json"); } }
        string LayoutsPath { get { return System.IO.Path.Combine(App.Dir, "mis-plantillas.json"); } }
        List<T> LoadList<T>(string path)
        {
            try { if (System.IO.File.Exists(path)) return Json.Read<List<T>>(System.IO.File.ReadAllText(path)) ?? new List<T>(); } catch { }
            return new List<T>();
        }
        void SaveList<T>(string path, List<T> list)
        {
            try { System.IO.File.WriteAllText(path, Json.Write(list), new System.Text.UTF8Encoding(false)); }
            catch (Exception ex) { pv.Toast("No se pudo guardar: " + ex.Message, true); }
        }

        void MyStyles()
        {
            var list = LoadList<SavedStyle>(StylesPath);
            Sub("Mis estilos");
            Row(B("Añadir mi estilo actual", BtnKind.Primary, "", () =>
            {
                string n = InputDialog.Ask(this, "Guardar estilo", "Ponle un nombre a tu estilo (colores, forma, letra, efectos e imagen):", "Mi estilo " + (list.Count + 1));
                if (string.IsNullOrWhiteSpace(n)) return;
                var c = cfg.Clone();
                list.RemoveAll(x => x.name == n.Trim());
                list.Add(new SavedStyle { name = n.Trim(), colors = c.colors, shape = c.shape, effects = c.effects, font = c.font, image = c.image });
                SaveList(StylesPath, list); BuildInspector(true);
                pv.Toast("Estilo «" + n.Trim() + "» guardado", false);
            }));
            if (list.Count == 0) return;
            var ch = new Choices("Mis estilos") { Cols = 2, CardH = 128, Radio = false };
            foreach (var st in list)
            {
                var pc = new Config(); pc.colors = st.colors; pc.shape = st.shape; pc.effects = st.effects; pc.font = st.font; pc.Normalize();
                ch.Items.Add(new Choice
                {
                    Title = st.name, Sub = "Guardado por ti",
                    Paint = (g, r, s) =>
                    {
                        var k1 = new CKey { id = "a", label = "W", x = 0, y = 0, w = 80, h = 94, textCase = "asis" };
                        var k2 = new CKey { id = "b", label = "A", x = 86, y = 0, w = 80, h = 94, textCase = "asis" };
                        float sc = Math.Min(r.Width / 176f, r.Height / 100f);
                        var gs = g.Save();
                        g.TranslateTransform(r.X + (r.Width - 166 * sc) / 2, r.Y + (r.Height - 94 * sc) / 2);
                        g.ScaleTransform(sc, sc);
                        KeyPainter.Draw(g, pc, k1, null, 100000, false);
                        KeyPainter.Draw(g, pc, k2, new KeyAnim { Active = true, PressAt = 0 }, 100000, false);
                        g.Restore(gs);
                    }
                });
            }
            ch.Picked = i =>
            {
                var st = Json.Read<SavedStyle>(Json.Write(list[i]));
                if (st.colors != null) cfg.colors = st.colors;
                if (st.shape != null) cfg.shape = st.shape;
                if (st.effects != null) cfg.effects = st.effects;
                if (st.font != null) cfg.font = st.font;
                if (st.image != null) cfg.image = st.image;
                cfg.Normalize();
                Changed(true); pv.Toast("Estilo «" + list[i].name + "» aplicado (Ctrl+Z para deshacer)", false);
            };
            ch.Deleted = i => ConfirmDelete(list[i].name, () => { list.RemoveAt(i); SaveList(StylesPath, list); BuildInspector(true); });
            Add(ch, App.Dp(14));
        }

        void MyLayouts()
        {
            var list = LoadList<SavedLayout>(LayoutsPath);
            Sub("Mis plantillas");
            Row(B("Añadir mis teclas como plantilla", BtnKind.Primary, "", () =>
            {
                if (cfg.keys.Count == 0) { pv.Toast("No hay teclas que guardar", true); return; }
                string n = InputDialog.Ask(this, "Guardar plantilla", "Ponle un nombre a esta distribución de teclas:", "Mi plantilla " + (list.Count + 1));
                if (string.IsNullOrWhiteSpace(n)) return;
                list.RemoveAll(x => x.name == n.Trim());
                list.Add(new SavedLayout { name = n.Trim(), keys = cfg.Clone().keys });
                SaveList(LayoutsPath, list); BuildInspector(true);
                pv.Toast("Plantilla «" + n.Trim() + "» guardada", false);
            }));
            Row(B("Exportar plantilla…", BtnKind.Normal, null, ExportLayout), B("Importar plantilla…", BtnKind.Normal, null, () => ImportLayout(list)));
            if (list.Count == 0) return;
            var ch = new Choices("Mis plantillas") { Cols = 2, CardH = 104, Radio = false };
            foreach (var lay in list)
            {
                var ks = lay.keys ?? new List<CKey>();
                ch.Items.Add(new Choice { Title = lay.name, Sub = ks.Count + " teclas", Paint = (g, r, s) => PaintKeys(g, r, ks) });
            }
            ch.Picked = i =>
            {
                var c = new Config { keys = Json.Read<List<CKey>>(Json.Write(list[i].keys ?? new List<CKey>())) };
                int n = 0; foreach (var k in c.keys) k.id = Config.NewId() + "u" + (n++);
                c.Normalize();
                ApplyLayout(list[i].name, c.keys);
            };
            ch.Deleted = i => ConfirmDelete(list[i].name, () => { list.RemoveAt(i); SaveList(LayoutsPath, list); BuildInspector(true); });
            Add(ch, App.Dp(14));
        }
        // ---- compartir plantillas: un archivo .json con la distribucion exacta (posiciones, tamaños, textos, iconos y teclas) ----
        public class LayoutFile { public string tipo = "plantilla-teclado"; public int version = 1; public string name; public List<CKey> keys; }
        void ExportLayout()
        {
            if (cfg.keys.Count == 0) { pv.Toast("No hay teclas que exportar", true); return; }
            string n = InputDialog.Ask(this, "Exportar plantilla", "Nombre de la plantilla (lo verá quien la importe):", "Mi plantilla");
            if (string.IsNullOrWhiteSpace(n)) return;
            string file = System.Text.RegularExpressions.Regex.Replace(n.Trim(), @"[\\/:*?""<>|]", "");
            using (var d = new SaveFileDialog { Title = "Exportar plantilla", Filter = "Plantilla de teclas (*.json)|*.json", FileName = "plantilla-" + (file.Length > 0 ? file : "teclas") + ".json" })
                if (d.ShowDialog(this) == DialogResult.OK)
                    try
                    {
                        var lf = new LayoutFile { name = n.Trim(), keys = cfg.Clone().keys };
                        System.IO.File.WriteAllText(d.FileName, Json.Pretty(Json.Write(lf)), new System.Text.UTF8Encoding(false));
                        pv.Toast("Plantilla exportada. Pásale ese archivo a quien quieras.", false);
                    }
                    catch (Exception ex) { pv.Toast("No se pudo exportar: " + ex.Message, true); }
        }
        void ImportLayout(List<SavedLayout> list)
        {
            using (var d = new OpenFileDialog { Title = "Importar plantilla", Filter = "Plantilla o estilo de teclas (*.json;*.js)|*.json;*.js" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                List<CKey> keys = null; string name = System.IO.Path.GetFileNameWithoutExtension(d.FileName);
                try
                {
                    string t = System.IO.File.ReadAllText(d.FileName);
                    var m = System.Text.RegularExpressions.Regex.Match(t, @"\{[\s\S]*\}");
                    var o = Json.Obj(m.Value) as Dictionary<string, object>;
                    if (o != null && o.ContainsKey("keys"))
                    {
                        // vale una plantilla exportada o un estilo completo (config.js / estilo exportado): se toman solo sus teclas
                        var lf = Json.Read<LayoutFile>(m.Value);
                        keys = lf.keys;
                        if (!string.IsNullOrWhiteSpace(lf.name)) name = lf.name.Trim();
                    }
                }
                catch { keys = null; }
                if (keys == null || keys.Count == 0) { pv.Toast("Ese archivo no tiene una plantilla de teclas válida", true); return; }
                var c = new Config { keys = keys };
                int n = 0; foreach (var k in c.keys) k.id = Config.NewId() + "i" + (n++);
                c.Normalize();
                // queda guardada en "Mis plantillas" y ademas se aplica
                list.RemoveAll(x => x.name == name);
                list.Add(new SavedLayout { name = name, keys = Json.Read<List<CKey>>(Json.Write(c.keys)) });
                SaveList(LayoutsPath, list);
                ApplyLayout(name, c.keys);
                BuildInspector(true);
            }
        }
        void ConfirmDelete(string name, Action del)
        {
            if (DarkDialog.Ask(this, "¿Borrar «" + name + "»?", "Se borra de tus guardados. No afecta a lo que tienes ahora en el teclado.", new[] { "Cancelar", "Borrar" }, 1) == 1)
            {
                del(); pv.Toast("«" + name + "» borrado", false);
            }
        }
        static void PaintKeys(Graphics g, Rectangle r, List<CKey> ks)
        {
            if (ks.Count == 0) return;
            float x0 = ks.Min(k => k.x), y0 = ks.Min(k => k.y), x1 = ks.Max(k => k.x + k.w), y1 = ks.Max(k => k.y + k.h);
            float u = Math.Min(r.Width / Math.Max(1, x1 - x0), r.Height / Math.Max(1, y1 - y0));
            float ox = r.X + (r.Width - (x1 - x0) * u) / 2, oy = r.Y + (r.Height - (y1 - y0) * u) / 2;
            foreach (var k in ks)
            {
                var kr = new RectangleF(ox + (k.x - x0) * u, oy + (k.y - y0) * u, k.w * u - 1, k.h * u - 1);
                G.Fill(g, kr, Math.Min(4, k.h * u * 0.15f), App.T.Card2);
                G.Stroke(g, kr, Math.Min(4, k.h * u * 0.15f), App.T.Line, 1);
                string l = k.bar ? "" : (k.label ?? "");
                if (l.Length > 0 && kr.Height > 10)
                    TextRenderer.DrawText(g, l.Length > 3 ? l.Substring(0, 3) : l, App.F(Math.Max(5.5f, Math.Min(8, kr.Height * 0.3f)), true), Rectangle.Round(kr), App.T.Text, G.Line | TextFormatFlags.HorizontalCenter);
            }
        }
    }
}
