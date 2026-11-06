// Keyboard Overlay Studio - control del visor en pantalla desde el editor (bandeja, atajos, arranque con Windows)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KOStudio
{
    public partial class MainForm
    {
        ScreenOverlay overlay;
        NotifyIcon tray;
        readonly HashSet<int> hookDown = new HashSet<int>();
        bool exitRequested, overlayHostReady;
        ToolStripMenuItem miShow, miLock;

        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
        const int HK_SHOW = 0x4B01, HK_LOCK = 0x4B02;   // Ctrl+Alt+K / Ctrl+Alt+L

        bool OverlayVisible { get { return overlay != null && overlay.Visible; } }

        // "--overlay": arranca solo con el visor (sin abrir el editor)
        protected override void SetVisibleCore(bool value)
        {
            if (!IsHandleCreated && args != null && (args.Contains("--overlay") || args.Contains("--tray"))) { CreateHandle(); value = false; InitOverlayHost(); }
            base.SetVisibleCore(value);
        }

        void InitOverlayHost()
        {
            if (overlayHostReady) return;
            overlayHostReady = true;
            tray = new NotifyIcon { Icon = Icon ?? SystemIcons.Application, Text = "Keyboard Overlay Studio", Visible = true };
            var menu = new ContextMenuStrip();
            miShow = new ToolStripMenuItem("Mostrar visor en pantalla   Ctrl+Alt+K", null, (s, e) => SetOverlay(!OverlayVisible));
            miLock = new ToolStripMenuItem("Bloquear (los clics lo atraviesan)   Ctrl+Alt+L", null, (s, e) => SetOverlayLocked(!App.P.overlayLocked));
            menu.Items.Add(miShow); menu.Items.Add(miLock);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Abrir el editor", null, (s, e) => OpenEditor()));
            menu.Items.Add(new ToolStripMenuItem("Salir", null, (s, e) => ExitApp()));
            menu.Opening += (s, e) => { miShow.Checked = OverlayVisible; miLock.Checked = App.P.overlayLocked; };
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (s, e) => OpenEditor();
            try { RegisterHotKey(Handle, HK_SHOW, 0x2 | 0x1 | 0x4000, (uint)Keys.K); RegisterHotKey(Handle, HK_LOCK, 0x2 | 0x1 | 0x4000, (uint)Keys.L); } catch { }
            // lector de teclas comun: visor, vista previa y servidor para OBS (sustituye al plugin input-overlay)
            InputHub.SetMouse(UsesMouse());
            InputHub.Raw += (vc, d) => { try { BeginInvoke((Action)(() => OnGlobalKey(vc, d))); } catch { } };
            InputHub.Start();
            if (args.Contains("--overlay") || App.P.overlayOn) SetOverlay(true);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x312)   // WM_HOTKEY
            {
                int id = m.WParam.ToInt32();
                if (id == HK_SHOW) SetOverlay(!OverlayVisible);
                else if (id == HK_LOCK && OverlayVisible) SetOverlayLocked(!App.P.overlayLocked);
            }
            base.WndProc(ref m);
        }
        // el visor detecta las teclas en su propio hilo; aqui solo se usan para iluminar la vista previa
        void OnGlobalKey(int vc, bool down)
        {
            bool ch = down ? hookDown.Add(vc) : hookDown.Remove(vc);
            if (ch && Visible) Lights();
        }

        void SetOverlay(bool on)
        {
            if (on)
            {
                if (overlay == null)
                {
                    overlay = new ScreenOverlay(cfg) { Locked = App.P.overlayLocked, ScalePct = App.P.overlayScale, OpacityPct = App.P.overlayOpacity, Fps = App.P.overlayFps, Animate = App.P.overlayAnim };
                    overlay.Moved = pt => { App.P.overlayX = pt.X; App.P.overlayY = pt.Y; App.SavePrefs(); };
                    overlay.Resized = (sc, pt) => { App.P.overlayScale = sc; App.P.overlayX = pt.X; App.P.overlayY = pt.Y; App.SavePrefs(); };
                    var wa = Screen.PrimaryScreen.WorkingArea;
                    bool known = App.P.overlayX != int.MinValue && Screen.AllScreens.Any(sc => sc.WorkingArea.Contains(App.P.overlayX + 20, App.P.overlayY + 20));
                    overlay.Location = known ? new Point(App.P.overlayX, App.P.overlayY) : new Point(wa.Left + 40, wa.Bottom - overlay.Height - 40);
                    overlay.Show();
                }
            }
            else
            {
                // al ocultarlo se libera del todo (memoria de los fotogramas guardados); volver a abrirlo es instantaneo
                if (overlay != null) overlay.Close();
                overlay = null;
            }
            App.P.overlayOn = on; App.SavePrefs();
            if (section == "visor" && Visible) BuildInspector(true);
            if (!on && !Visible) OpenEditor();   // sin visor ni editor no quedaria nada a la vista
        }
        void SetOverlayLocked(bool l)
        {
            App.P.overlayLocked = l; App.SavePrefs();
            if (overlay != null) overlay.SetLocked(l);
            if (tray != null && OverlayVisible) tray.ShowBalloonTip(1200, "Visor", l ? "Bloqueado: los clics lo atraviesan" : "Desbloqueado: arrástralo para moverlo", ToolTipIcon.None);
            if (section == "visor" && Visible) BuildInspector(true);
        }
        void OpenEditor()
        {
            Show(); if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate(); BringToFront();
        }
        void ExitApp()
        {
            exitRequested = true;
            if (!Visible) Show();
            Close();
            if (!IsDisposed && Visible) exitRequested = false;   // cancelado en el "¿Guardar cambios?"
        }
        void ShutdownOverlay()
        {
            try { UnregisterHotKey(Handle, HK_SHOW); UnregisterHotKey(Handle, HK_LOCK); } catch { }
            InputHub.Stop();
            if (overlay != null) overlay.Close();
            overlay = null;
            if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
        }

        bool UsesMouse() { foreach (var k in cfg.keys) foreach (var c in k.codes) if (c >= KeyCodes.MOUSE) return true; return false; }

        // ---------- arranque con Windows ----------
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", RunName = "KeyboardOverlayStudio";
        static bool Autostart
        {
            get { try { using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(RunName) != null; } catch { return false; } }
            set
            {
                try
                {
                    using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true))
                    {
                        if (k == null) return;
                        if (value) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\" --tray");
                        else k.DeleteValue(RunName, false);
                    }
                }
                catch { }
            }
        }

        // ---------- seccion del editor ----------
        void SecOverlay()
        {
            Head("Visor en pantalla", "Tu teclado flotando encima de todo, para uso personal. No necesita OBS: detecta tus teclas directamente y usa el estilo que tienes en el editor.");
            Add(new Toggle("Mostrar el teclado en mi pantalla", "Atajo en cualquier momento: Ctrl + Alt + K.", OverlayVisible) { Changed = v => SetOverlay(v) }, App.Dp(8));
            Add(new Toggle("Bloqueado", "Bloqueado: los clics lo atraviesan y no molesta. Desbloqueado: arrástralo para moverlo y tira de las esquinas o los lados para cambiar el tamaño. Atajo: Ctrl + Alt + L.", App.P.overlayLocked) { Changed = v => SetOverlayLocked(v) }, App.Dp(8));
            Sub("Aspecto");
            var op = new Slider("Opacidad", 10, 100, 5, "%", "Para que se vea más transparente encima del juego."); op.SetValue(App.P.overlayOpacity);
            op.Changed = (v, c) => { App.P.overlayOpacity = (int)v; if (overlay != null) { overlay.OpacityPct = (int)v; overlay.Repaint(); } if (c) App.SavePrefs(); };
            Add(op, App.Dp(10));
            Add(new Toggle("Animaciones", "Si notas que el juego baja de FPS, desactívalas: las teclas cambian al instante entre suelta y pulsada y el visor casi no hace nada.", App.P.overlayAnim)
            { Changed = v => { App.P.overlayAnim = v; App.SavePrefs(); if (overlay != null) { overlay.Animate = v; overlay.Redraw(); } BuildInspector(true); } }, App.Dp(10));
            var fp = new Slider("Fluidez de las animaciones", 30, 144, 1, "fps", "Más alto = más suave. 60 va bien en casi todos los monitores; súbelo si tu pantalla es de 120/144 Hz."); fp.SetValue(App.P.overlayFps);
            fp.Changed = (v, c) => { App.P.overlayFps = (int)v; if (overlay != null) { overlay.Fps = (int)v; if (c) overlay.Redraw(); } if (c) App.SavePrefs(); };
            if (App.P.overlayAnim) Add(fp, App.Dp(10));
            Sub("Al cerrar y al encender");
            Add(new Toggle("Seguir en la bandeja al cerrar el editor", "Si el visor está abierto, cerrar el editor lo deja junto al reloj de Windows (como Crosshair X).", App.P.keepInTray) { Changed = v => { App.P.keepInTray = v; App.SavePrefs(); } }, App.Dp(8));
            Add(new Toggle("Abrir con Windows", "Al encender el PC se queda en la bandeja, listo para OBS y para el visor, sin abrir el editor.", Autostart) { Changed = v => Autostart = v }, App.Dp(12));
            Add(new Para("Funciona encima de juegos en ventana o «pantalla completa sin bordes». En pantalla completa exclusiva Windows no deja dibujar nada encima: cambia el juego a sin bordes.", 9, false, true));
        }
    }
}
