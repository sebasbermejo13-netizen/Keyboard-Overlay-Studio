// Keyboard Overlay Studio - actualizaciones automaticas desde GitHub Releases
//  1) Mira la ultima Release del repositorio (solo lee, no envia nada tuyo).
//  2) Si hay version nueva, descarga el .zip, comprueba su SHA256 (si la Release lo publica) y lo aplica al cerrar el programa.
//  3) NUNCA toca tus archivos: config.js, mis-estilos.json, mis-plantillas.json, studio-prefs.json...
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace KOStudio
{
    public static class Upd
    {
        public const string Version = "1.1.0";
        public const string Repo = "sebasbermejo13-netizen/Keyboard-Overlay-Studio";

        public class Info
        {
            public string Tag = "", Notes = "", ZipUrl = "", Sha = "";
            public long Size;
            public bool IsNewer;
        }
        public static Info Latest;          // la ultima Release encontrada (null si aun no se ha mirado o fallo)
        public static string LastError = "";

        static bool Tls() { try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { } return true; }

        static HttpWebRequest Req(string url, int timeout)
        {
            Tls();
            var r = (HttpWebRequest)WebRequest.Create(url);
            r.UserAgent = "KeyboardOverlayStudio/" + Version;
            r.Accept = "application/vnd.github+json, application/octet-stream, */*";
            r.Timeout = timeout; r.ReadWriteTimeout = timeout;
            return r;
        }

        static Version ParseVer(string tag)
        {
            var m = Regex.Match(tag ?? "", @"(\d+)\.(\d+)(?:\.(\d+))?");
            if (!m.Success) return null;
            try { return new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0); }
            catch { return null; }
        }

        public static Info ParseRelease(string json)
        {
                var root = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.DeserializeObject(json) as System.Collections.Generic.Dictionary<string, object>;
                if (root == null) return null;
                var inf = new Info();
                object o;
                if (root.TryGetValue("tag_name", out o) && o != null) inf.Tag = o.ToString();
                if (root.TryGetValue("body", out o) && o != null) inf.Notes = o.ToString();
                var assets = root.TryGetValue("assets", out o) ? o as System.Collections.IEnumerable : null;
                if (assets != null)
                    foreach (var a in assets)
                    {
                        var d = a as System.Collections.Generic.Dictionary<string, object>; if (d == null) continue;
                        string name = d.ContainsKey("name") && d["name"] != null ? d["name"].ToString() : "";
                        if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                        inf.ZipUrl = d.ContainsKey("browser_download_url") && d["browser_download_url"] != null ? d["browser_download_url"].ToString() : "";
                        if (d.ContainsKey("size") && d["size"] != null) long.TryParse(d["size"].ToString(), out inf.Size);
                        break;
                    }
                // el hash del .zip va en las notas de la Release:  SHA256: <64 caracteres>
                var m = Regex.Match(inf.Notes, @"(?i)sha-?256[^\r\n]{0,60}?\b([0-9a-f]{64})\b");
                if (m.Success) inf.Sha = m.Groups[1].Value.ToLowerInvariant();
                var nv = ParseVer(inf.Tag); var cv = ParseVer(Version);
                inf.IsNewer = nv != null && cv != null && nv > cv && inf.ZipUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            return inf;
        }

        // consulta GitHub (bloquea: llamar desde un hilo)
        public static Info Check()
        {
            LastError = "";
            try
            {
                string json;
                using (var resp = Req("https://api.github.com/repos/" + Repo + "/releases/latest", 10000).GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) json = sr.ReadToEnd();
                var inf = ParseRelease(json);
                if (inf == null) { LastError = "Respuesta no válida de GitHub."; return null; }
                Latest = inf;
                return inf;
            }
            catch (WebException ex)
            {
                var hr = ex.Response as HttpWebResponse;
                LastError = hr != null && hr.StatusCode == HttpStatusCode.NotFound ? "Aún no hay ninguna versión publicada." : "No se pudo conectar con GitHub. Comprueba tu conexión.";
                return null;
            }
            catch (Exception ex) { LastError = "No se pudo comprobar: " + ex.Message; return null; }
        }

        static string WorkDir { get { return Path.Combine(Path.GetTempPath(), "kos-update"); } }

        // descarga el .zip y comprueba su integridad (bloquea: llamar desde un hilo)
        public static bool Download(Info inf, out string zipPath, out string err, Action<int> progress)
        {
            zipPath = Path.Combine(WorkDir, "update.zip"); err = "";
            try
            {
                Directory.CreateDirectory(WorkDir);
                if (File.Exists(zipPath)) File.Delete(zipPath);
                using (var resp = Req(inf.ZipUrl, 30000).GetResponse())
                using (var src = resp.GetResponseStream())
                using (var dst = File.Create(zipPath))
                {
                    long total = resp.ContentLength > 0 ? resp.ContentLength : inf.Size, done = 0; int last = -1;
                    var buf = new byte[64 * 1024]; int n;
                    while ((n = src.Read(buf, 0, buf.Length)) > 0)
                    {
                        dst.Write(buf, 0, n); done += n;
                        if (total > 0 && progress != null) { int p = (int)(done * 100 / total); if (p / 10 != last / 10) { last = p; progress(p); } }
                    }
                }
                var fi = new FileInfo(zipPath);
                if (fi.Length < 1024) { err = "La descarga está incompleta."; return false; }
                using (var fs = File.OpenRead(zipPath)) { if (fs.ReadByte() != 'P' || fs.ReadByte() != 'K') { err = "El archivo descargado no es un .zip."; return false; } }
                if (inf.Sha.Length == 64)
                {
                    string h;
                    using (var fs = File.OpenRead(zipPath)) h = BitConverter.ToString(SHA256.Create().ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
                    if (h != inf.Sha) { try { File.Delete(zipPath); } catch { } err = "La descarga no coincide con el SHA256 publicado: se cancela por seguridad."; return false; }
                }
                return true;
            }
            catch (Exception ex) { err = "No se pudo descargar: " + ex.Message; return false; }
        }

        // prepara el script que, cuando este programa se cierre, copia los archivos nuevos encima y lo vuelve a abrir
        public static bool Apply(string zipPath, out string err)
        {
            err = "";
            try
            {
                string app = App.Dir.TrimEnd('\\', '/');
                string exe = Path.GetFileName(Application.ExecutablePath);
                string[] ca = Environment.GetCommandLineArgs(); string args = "";
                for (int i = 1; i < ca.Length; i++) args += (args.Length > 0 ? " " : "") + (ca[i].IndexOf(' ') >= 0 ? "\"" + ca[i] + "\"" : ca[i]);
                string pid = Process.GetCurrentProcess().Id.ToString();
                var s = new StringBuilder();
                s.AppendLine("@echo off");
                s.AppendLine("set n=0");
                s.AppendLine(":wait");
                s.AppendLine("tasklist /fi \"PID eq " + pid + "\" 2>nul | find \"" + pid + "\" >nul");
                s.AppendLine("if errorlevel 1 goto go");
                s.AppendLine("set /a n+=1");
                s.AppendLine("if %n% gtr 180 exit /b 1");
                s.AppendLine("ping -n 2 127.0.0.1 >nul");
                s.AppendLine("goto wait");
                s.AppendLine(":go");
                s.AppendLine("rd /s /q \"%KOS_WORK%\\x\" 2>nul");
                s.AppendLine("powershell -NoProfile -ExecutionPolicy Bypass -Command \"Expand-Archive -LiteralPath $env:KOS_ZIP -DestinationPath ($env:KOS_WORK + '\\x') -Force\"");
                s.AppendLine("if errorlevel 1 goto fail");
                s.AppendLine("set \"SRC=%KOS_WORK%\\x\"");
                s.AppendLine("for /d %%D in (\"%KOS_WORK%\\x\\*\") do if exist \"%%D\\%KOS_EXE%\" set \"SRC=%%D\"");
                s.AppendLine("if not exist \"%SRC%\\%KOS_EXE%\" goto fail");
                s.AppendLine("robocopy \"%SRC%\" \"%KOS_APP%\" /E /R:5 /W:1 /NFL /NDL /NJH /NJS /XF config.js config-anterior.js mis-estilos.json mis-plantillas.json studio-prefs.json studio-borrador.json error.txt >nul");
                s.AppendLine(":fail");
                s.AppendLine("start \"\" \"%KOS_APP%\\%KOS_EXE%\" %KOS_ARGS%");
                s.AppendLine("ping -n 3 127.0.0.1 >nul");
                s.AppendLine("rd /s /q \"%KOS_WORK%\" 2>nul");
                string cmd = Path.Combine(WorkDir, "apply.cmd");
                Directory.CreateDirectory(WorkDir);
                File.WriteAllText(cmd, s.ToString(), new UTF8Encoding(false));
                var psi = new ProcessStartInfo("cmd.exe", "/c \"" + cmd + "\"") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
                psi.EnvironmentVariables["KOS_APP"] = app; psi.EnvironmentVariables["KOS_EXE"] = exe;
                psi.EnvironmentVariables["KOS_ZIP"] = zipPath; psi.EnvironmentVariables["KOS_WORK"] = WorkDir;
                psi.EnvironmentVariables["KOS_ARGS"] = args;
                Process.Start(psi);
                return true;
            }
            catch (Exception ex) { err = "No se pudo preparar la actualización: " + ex.Message; return false; }
        }
    }

    public partial class MainForm
    {
        bool updBusy, updChecked;
        readonly System.Windows.Forms.Timer updTimer = new System.Windows.Forms.Timer { Interval = 2500 };

        // al abrir el editor: mira GitHub siempre y, si hay version nueva, avisa
        void AutoUpdateCheck()
        {
            if (!App.P.autoUpdate) return;
            updTimer.Tick += (s, e) => { updTimer.Stop(); CheckUpdateAsync(false); };
            updTimer.Start();
        }

        void UpdUi(Action a) { try { if (!IsDisposed && IsHandleCreated) BeginInvoke(a); } catch { } }
        void UpdRefresh() { if (section == "actualizaciones" && Visible) BuildInspector(true); }

        void CheckUpdateAsync(bool manual)
        {
            if (updBusy) return;
            updBusy = true;
            if (manual) pv.Toast("Buscando actualizaciones…", false);
            UpdRefresh();
            new Thread(() =>
            {
                var r = Upd.Check();
                UpdUi(() =>
                {
                    updBusy = false; updChecked = r != null;
                    if (r != null) { App.P.lastUpdateCheck = DateTime.UtcNow.Ticks; App.SavePrefs(); }
                    if (r == null) { if (manual) pv.Toast(Upd.LastError, true); }
                    else if (r.IsNewer)
                    {
                        if (manual) pv.Toast("Hay una versión nueva: " + r.Tag, false);
                        else if (Visible)
                        {
                            if (DarkDialog.Ask(this, "Hay una versión nueva", "Está disponible " + r.Tag + " (tienes " + Upd.Version + ").\nSe actualiza sola y no pierdes tus estilos ni tus plantillas.", new[] { "Más tarde", "Actualizar ahora" }, 1) == 1) StartUpdate();
                        }
                    }
                    else if (manual) pv.Toast("Ya tienes la última versión.", false);
                    UpdRefresh();
                });
            }) { IsBackground = true, Name = "kos-upd" }.Start();
        }

        void StartUpdate()
        {
            var lat = Upd.Latest;
            if (lat == null || !lat.IsNewer || updBusy) return;
            updBusy = true; pv.Toast("Descargando " + lat.Tag + "…", false); UpdRefresh();
            new Thread(() =>
            {
                string zip, err;
                bool ok = Upd.Download(lat, out zip, out err, p => UpdUi(() => pv.Toast("Descargando " + lat.Tag + "… " + p + "%", false)));
                UpdUi(() =>
                {
                    updBusy = false;
                    if (ok) ok = Upd.Apply(zip, out err);
                    if (!ok) { pv.Toast(err, true); UpdRefresh(); return; }
                    App.SavePrefs();
                    pv.Toast("Instalando… el programa se reiniciará solo.", false);
                    ExitApp();
                });
            }) { IsBackground = true, Name = "kos-upd" }.Start();
        }

        void SecUpdate()
        {
            Head("Actualizaciones", "El programa mira en GitHub si hay una versión nueva y se actualiza solo. No pierdes tus estilos, plantillas ni tu diseño: esos archivos no se tocan.");
            Add(new Para("Versión instalada: " + Upd.Version, 10, true, false), App.Dp(6));
            var lat = Upd.Latest;
            if (updBusy) Add(new Para("Trabajando…", 10, false, true), App.Dp(8));
            else if (lat != null && lat.IsNewer)
            {
                string notes = (lat.Notes ?? "").Trim(); if (notes.Length > 420) notes = notes.Substring(0, 420) + "…";
                Add(new Para("Hay una versión nueva: " + lat.Tag + (notes.Length > 0 ? "\n\n" + notes : ""), 10, false, false), App.Dp(8));
                Row(B("Actualizar ahora", BtnKind.Primary, "", StartUpdate));
            }
            else if (updChecked) Add(new Para("✔ Tienes la última versión.", 10, false, true), App.Dp(8));
            else if (Upd.LastError.Length > 0) Add(new Para(Upd.LastError, 10, false, true), App.Dp(8));
            Row(B("Buscar actualizaciones", BtnKind.Normal, "", () => CheckUpdateAsync(true)));
            Sub("Opciones");
            Add(new Toggle("Buscar al abrir el programa", "Cada vez que abres el programa mira si hay versión nueva y te lo avisa. Solo lee la página de versiones de GitHub; no envía nada tuyo.", App.P.autoUpdate) { Changed = v => { App.P.autoUpdate = v; App.SavePrefs(); } }, App.Dp(10));
            Add(new Para("Cada descarga se comprueba con su SHA256 antes de instalarse. Si algo falla, tu programa actual sigue intacto.", 9, false, true));
        }
    }
}
