// Keyboard Overlay Studio - entrada global + servidor para OBS (sustituye al plugin input-overlay)
//  · Un unico hilo lee teclado y raton con Raw Input (sin ganchos: no retrasa nada al juego).
//  · Reparte cada pulsacion al visor en pantalla, al editor y a OBS.
//  · OBS: servidor WebSocket en 127.0.0.1:16899 con los mismos mensajes que input-overlay,
//    asi overlay.html funciona igual con o sin el plugin. Si el plugin ya ocupa el puerto, se le deja a el.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace KOStudio
{
    public static class InputHub
    {
        public const int Port = 16899;
        // se llama en el hilo de entrada: quien lo use debe ser rapido y pasarlo a su propio hilo
        public static event Action<int, bool> Raw;
        // estado del servidor para OBS
        public static bool Serving { get { return listener != null; } }
        public static int Clients { get { lock (clients) return clients.Count; } }

        [StructLayout(LayoutKind.Sequential)] struct RID { public ushort page, usage; public uint flags; public IntPtr target; }
        [DllImport("user32.dll")] static extern bool RegisterRawInputDevices(RID[] d, uint n, uint size);
        [DllImport("user32.dll")] static extern uint GetRawInputData(IntPtr h, uint cmd, IntPtr data, ref uint size, uint hdr);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        const int WM_INPUT = 0xFF, WM_STOP = 0x8000 + 90, WM_MOUSE = 0x8000 + 91;

        static Thread th; static Sink win; static volatile bool wantMouse;
        static readonly HashSet<int> held = new HashSet<int>();

        public static void Start()
        {
            if (th != null) return;
            StartServer();
            var ready = new ManualResetEvent(false);
            th = new Thread(() =>
            {
                try { win = new Sink(); win.CreateHandle(new CreateParams { Caption = "kos-input" }); win.Register(true, wantMouse); } catch { }
                ready.Set();
                Application.Run();
                try { if (win != null) { win.Register(false, false); win.DestroyHandle(); } } catch { }
                win = null;
            }) { IsBackground = true, Name = "kos-input" };
            th.SetApartmentState(ApartmentState.STA);
            th.Start();
            ready.WaitOne(2000);
        }
        public static void Stop()
        {
            StopServer();
            var t = th; th = null;
            try { if (win != null) PostMessage(win.Handle, WM_STOP, IntPtr.Zero, IntPtr.Zero); } catch { }
            if (t != null) t.Join(1000);
        }
        // el raton solo se escucha si el diseño tiene botones del raton (los ratones de 1000-8000 Hz mandan miles de avisos)
        public static void SetMouse(bool on)
        {
            if (wantMouse == on) return;
            wantMouse = on;
            try { if (win != null) PostMessage(win.Handle, WM_MOUSE, IntPtr.Zero, IntPtr.Zero); } catch { }
        }

        static void Fire(int vc, bool down)
        {
            var h = Raw; if (h != null) { try { h(vc, down); } catch { } }
            Broadcast(vc, down);
        }

        sealed class Sink : NativeWindow
        {
            readonly IntPtr buf = Marshal.AllocHGlobal(256);
            bool mouseOn;
            public void Register(bool on, bool mouse)
            {
                var list = new List<RID>();
                if (on) list.Add(new RID { page = 1, usage = 6, flags = 0x100, target = Handle });   // teclado (INPUTSINK: tambien sin foco)
                else list.Add(new RID { page = 1, usage = 6, flags = 0x1 });
                if (on && mouse) list.Add(new RID { page = 1, usage = 2, flags = 0x100, target = Handle });
                else if (mouseOn) list.Add(new RID { page = 1, usage = 2, flags = 0x1 });
                RegisterRawInputDevices(list.ToArray(), (uint)list.Count, (uint)Marshal.SizeOf(typeof(RID)));
                mouseOn = on && mouse;
            }
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_INPUT) { try { Read(m.LParam); } catch { } }
                else if (m.Msg == WM_MOUSE) { Register(true, wantMouse); return; }
                else if (m.Msg == WM_STOP) { Application.ExitThread(); return; }
                base.WndProc(ref m);
            }
            void Read(IntPtr h)
            {
                int hs = 8 + 2 * IntPtr.Size; uint size = 256;
                if (GetRawInputData(h, 0x10000003, buf, ref size, (uint)hs) == unchecked((uint)-1)) return;
                int type = Marshal.ReadInt32(buf, 0);
                if (type == 0)   // raton: solo botones; el movimiento se descarta al instante
                {
                    int f = (ushort)Marshal.ReadInt16(buf, hs + 4);
                    if (f == 0) return;
                    for (int i = 0; i < 5; i++)
                    {
                        if ((f & (1 << (i * 2))) != 0) Key(KeyCodes.MOUSE + i + 1, true);
                        if ((f & (2 << (i * 2))) != 0) Key(KeyCodes.MOUSE + i + 1, false);
                    }
                }
                else if (type == 1)
                {
                    int make = (ushort)Marshal.ReadInt16(buf, hs), fl = (ushort)Marshal.ReadInt16(buf, hs + 2);
                    if (make == 0 || make >= 0xFF || (fl & 4) != 0) return;
                    long lp = ((long)(make & 0xFF) << 16) | ((long)((fl & 2) != 0 ? 1 : 0) << 24);
                    Key(KeyCodes.FromLParam(new IntPtr(lp)), (fl & 1) == 0);
                }
            }
            static void Key(int vc, bool down) { if (down ? held.Add(vc) : held.Remove(vc)) Fire(vc, down); }   // sin auto-repeticion
        }

        // =====================================================================
        // SERVIDOR WEBSOCKET (minimo: solo envia; mismo formato que input-overlay)
        // =====================================================================
        static TcpListener listener;
        static readonly List<Client> clients = new List<Client>();

        static void StartServer()
        {
            try
            {
                var l = new TcpListener(IPAddress.Loopback, Port);
                l.ExclusiveAddressUse = true;
                l.Start();
                listener = l;
                l.BeginAcceptTcpClient(OnAccept, l);
            }
            catch { listener = null; }   // puerto ocupado (p.ej. por el plugin input-overlay): OBS usara el suyo
        }
        static void StopServer()
        {
            var l = listener; listener = null;
            try { if (l != null) l.Stop(); } catch { }
            Client[] all; lock (clients) { all = clients.ToArray(); clients.Clear(); }
            foreach (var c in all) c.Close();
        }
        static void OnAccept(IAsyncResult ar)
        {
            var l = (TcpListener)ar.AsyncState;
            TcpClient tc = null;
            try { tc = l.EndAcceptTcpClient(ar); } catch { }
            try { if (listener == l) l.BeginAcceptTcpClient(OnAccept, l); } catch { }
            if (tc != null) new Thread(() => Serve(tc)) { IsBackground = true, Name = "kos-ws" }.Start();
        }
        static void Serve(TcpClient tc)
        {
            Client c = null;
            try
            {
                tc.NoDelay = true;
                var s = tc.GetStream();
                s.ReadTimeout = 5000;
                // 1) saludo HTTP -> WebSocket
                var req = new StringBuilder(); var b = new byte[1024];
                while (req.Length < 8192 && req.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal) < 0)
                {
                    int n = s.Read(b, 0, b.Length); if (n <= 0) { tc.Close(); return; }
                    req.Append(Encoding.ASCII.GetString(b, 0, n));
                }
                string key = null;
                foreach (var line in req.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None))
                    if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase)) key = line.Substring(18).Trim();
                if (key == null) { tc.Close(); return; }
                string accept;
                using (var sha = SHA1.Create()) accept = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                var resp = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n");
                s.Write(resp, 0, resp.Length);
                s.ReadTimeout = Timeout.Infinite;
                c = new Client(tc);
                lock (clients) clients.Add(c);
                // 2) lo que mande el navegador solo se lee para saber si cierra (y contestar a los "ping")
                while (true)
                {
                    int h0 = s.ReadByte(), h1 = s.ReadByte(); if (h0 < 0 || h1 < 0) break;
                    int op = h0 & 0x0F; long len = h1 & 0x7F; bool masked = (h1 & 0x80) != 0;
                    if (len == 126) len = (s.ReadByte() << 8) | s.ReadByte();
                    else if (len == 127) { len = 0; for (int i = 0; i < 8; i++) len = (len << 8) | (uint)s.ReadByte(); }
                    var mask = new byte[4]; if (masked) ReadAll(s, mask, 4);
                    if (len > 1 << 20) break;
                    var data = new byte[len]; ReadAll(s, data, (int)len);
                    if (masked) for (int i = 0; i < data.Length; i++) data[i] ^= mask[i & 3];
                    if (op == 0x8) break;                       // cerrar
                    if (op == 0x9) c.Send(0xA, data);           // ping -> pong
                }
            }
            catch { }
            if (c != null) { lock (clients) clients.Remove(c); c.Close(); }
            else try { tc.Close(); } catch { }
        }
        static void ReadAll(System.IO.Stream s, byte[] b, int n) { int o = 0; while (o < n) { int r = s.Read(b, o, n - o); if (r <= 0) throw new System.IO.IOException(); o += r; } }

        static void Broadcast(int vc, bool down)
        {
            if (listener == null) return;
            Client[] all; lock (clients) { if (clients.Count == 0) return; all = clients.ToArray(); }
            string json = vc >= KeyCodes.MOUSE
                ? "{\"event_type\":\"" + (down ? "mouse_pressed" : "mouse_released") + "\",\"button\":" + (vc - KeyCodes.MOUSE) + "}"
                : "{\"event_type\":\"" + (down ? "key_pressed" : "key_released") + "\",\"keycode\":" + vc + "}";
            var payload = Encoding.UTF8.GetBytes(json);
            foreach (var c in all) c.Send(0x1, payload);
        }

        // cada cliente tiene su cola: un navegador lento nunca frena la lectura de teclas
        sealed class Client
        {
            readonly TcpClient tc; readonly Queue<byte[]> q = new Queue<byte[]>(); bool closed;
            public Client(TcpClient tc)
            {
                this.tc = tc;
                new Thread(Writer) { IsBackground = true, Name = "kos-ws-out" }.Start();
            }
            public void Send(int op, byte[] payload)
            {
                int n = payload.Length;
                byte[] f;
                if (n < 126) { f = new byte[2 + n]; f[1] = (byte)n; Buffer.BlockCopy(payload, 0, f, 2, n); }
                else { f = new byte[4 + n]; f[1] = 126; f[2] = (byte)(n >> 8); f[3] = (byte)n; Buffer.BlockCopy(payload, 0, f, 4, n); }
                f[0] = (byte)(0x80 | op);
                lock (q) { if (closed) return; if (q.Count < 512) q.Enqueue(f); Monitor.Pulse(q); }
            }
            void Writer()
            {
                try
                {
                    var s = tc.GetStream();
                    while (true)
                    {
                        byte[] f;
                        lock (q) { while (q.Count == 0 && !closed) Monitor.Wait(q); if (closed) return; f = q.Dequeue(); }
                        s.Write(f, 0, f.Length);
                    }
                }
                catch { Close(); }
            }
            public void Close()
            {
                lock (q) { closed = true; Monitor.PulseAll(q); }
                try { tc.Close(); } catch { }
            }
        }
    }
}
