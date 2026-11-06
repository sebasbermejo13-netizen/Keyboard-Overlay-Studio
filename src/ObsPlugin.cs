// Keyboard Overlay Studio - instalar / quitar el mini plugin de OBS (kos-teclas.dll)
// Con el plugin, OBS recibe las teclas por si mismo: no hace falta tener este programa abierto.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace KOStudio
{
    public partial class MainForm
    {
        const string PluginName = "kos-teclas";
        static string PluginSource { get { return Path.Combine(App.Dir, "obs-plugin", PluginName + ".dll"); } }
        static string PluginRoot { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "obs-studio", "plugins", PluginName); } }
        // OBS 33+: plugins\kos-teclas\kos-teclas.dll   ·   OBS anteriores: plugins\kos-teclas\bin\64bit\kos-teclas.dll
        static string PluginNew { get { return Path.Combine(PluginRoot, PluginName + ".dll"); } }
        static string PluginLegacy { get { return Path.Combine(PluginRoot, "bin", "64bit", PluginName + ".dll"); } }
        static bool PluginInstalled { get { return File.Exists(PluginNew) || File.Exists(PluginLegacy); } }
        static bool ObsRunning { get { try { return Process.GetProcessesByName("obs64").Length > 0 || Process.GetProcessesByName("obs32").Length > 0; } catch { return false; } } }

        void ObsPluginPanel()
        {
            Sub("Teclas en OBS");
            bool inst = PluginInstalled;
            Add(new Para(inst
                ? "✔ El plugin de teclas está instalado en OBS. OBS ilumina tus teclas él solo: no hace falta tener este programa abierto ni ningún otro plugin."
                : "Instala nuestro mini plugin en OBS y las teclas se iluminarán aunque este programa esté cerrado. No necesitas input-overlay.\nSin el plugin también funciona, pero dejando este programa abierto (en la bandeja).", 10, false, !inst), App.Dp(10));
            if (inst)
                Row(B("Actualizar el plugin", BtnKind.Normal, "", () => InstallPlugin(true)), B("Quitar de OBS", BtnKind.Danger, "", RemovePlugin));
            else
                Row(B("Instalar plugin en OBS", BtnKind.Primary, "", () => InstallPlugin(false)));
        }

        void InstallPlugin(bool update)
        {
            if (!File.Exists(PluginSource)) { pv.Toast("Falta obs-plugin\\" + PluginName + ".dll en la carpeta del programa", true); return; }
            if (ObsRunning && DarkDialog.Ask(this, "Cierra OBS", "OBS está abierto. Ciérralo para instalar el plugin; al volver a abrirlo ya funcionará.", new[] { "Cancelar", "Ya lo cerré" }, 1) != 1) return;
            if (ObsRunning) { pv.Toast("OBS sigue abierto: ciérralo y vuelve a intentarlo", true); return; }
            bool ok = false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PluginLegacy));
                Directory.CreateDirectory(Path.Combine(PluginRoot, "data"));
                File.Copy(PluginSource, PluginNew, true);
                File.Copy(PluginSource, PluginLegacy, true);
                ok = true;
            }
            catch (UnauthorizedAccessException) { ok = Elevated("mkdir \"" + Path.GetDirectoryName(PluginLegacy) + "\" & mkdir \"" + Path.Combine(PluginRoot, "data") + "\" & copy /y \"" + PluginSource + "\" \"" + PluginNew + "\" & copy /y \"" + PluginSource + "\" \"" + PluginLegacy + "\""); }
            catch (Exception ex) { pv.Toast("No se pudo instalar: " + ex.Message, true); return; }
            ok = ok && PluginInstalled;
            pv.Toast(ok ? (update ? "Plugin actualizado." : "Plugin instalado. Abre OBS: las teclas ya se iluminan sin este programa.") : "No se pudo instalar el plugin", !ok);
            BuildInspector(true);
        }
        void RemovePlugin()
        {
            if (DarkDialog.Ask(this, "Quitar el plugin", "Se borra el plugin de teclas de OBS. Para que OBS siga iluminando las teclas tendrás que dejar este programa abierto.", new[] { "Cancelar", "Quitar" }, 1) != 1) return;
            if (ObsRunning) { pv.Toast("Cierra OBS antes de quitar el plugin", true); return; }
            try { Directory.Delete(PluginRoot, true); }
            catch (UnauthorizedAccessException) { Elevated("rmdir /s /q \"" + PluginRoot + "\""); }
            catch (Exception ex) { pv.Toast("No se pudo quitar: " + ex.Message, true); return; }
            pv.Toast(PluginInstalled ? "No se pudo quitar el plugin" : "Plugin quitado de OBS", PluginInstalled);
            BuildInspector(true);
        }
        // si Windows no deja escribir en la carpeta de OBS, se pide permiso de administrador solo para copiar
        static bool Elevated(string cmd)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo("cmd.exe", "/c " + cmd) { Verb = "runas", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
                if (p != null) p.WaitForExit(15000);
                return true;
            }
            catch { return false; }   // el usuario dijo que no
        }
    }
}
