// KEMAS - Platform services: updates (GitHub), installer, scheduled tasks, display info.  (no WPF dependencies)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace Kemas
{
    // ===================================================================== DISPLAY
    public static class Display
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplaySettings(string device, int mode, ref DEVMODE dm);

        /// Refresh rate of the main screen (60, 120, 144, 165, 240 ...). Falls back to 60.
        public static int RefreshRate()
        {
            try
            {
                var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
                if (EnumDisplaySettings(null, -1, ref dm) && dm.dmDisplayFrequency >= 30 && dm.dmDisplayFrequency <= 500) return dm.dmDisplayFrequency;
            }
            catch { }
            return 60;
        }
    }

    // ===================================================================== SCHEDULED TASKS (start with Windows / auto clean)
    public static class Tasks
    {
        public const string StartupTask = "KEMAS Startup";
        public const string CleanTask = "KEMAS Auto Clean";

        static int Schtasks(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", args) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var p = Process.Start(psi)) { p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit(15000); return p.ExitCode; }
            }
            catch (Exception ex) { Log.Error("schtasks " + args, ex); return -1; }
        }

        // Runs elevated at logon without a UAC prompt each time (that is why a task is used, not the Run key).
        public static bool SetStartup(bool on)
        {
            if (!on) { Schtasks("/Delete /F /TN \"" + StartupTask + "\""); return true; }
            return Schtasks("/Create /F /TN \"" + StartupTask + "\" /TR \"\\\"" + Installer.LaunchPath + "\\\" --tray\" /SC ONLOGON /RL HIGHEST /DELAY 0000:20") == 0;
        }

        public static bool SetAutoClean(string mode)
        {
            if (mode == "off") { Schtasks("/Delete /F /TN \"" + CleanTask + "\""); return true; }
            string sched = mode == "weekly" ? "/SC WEEKLY /D SUN /ST 12:00" : "/SC DAILY /ST 12:00";
            return Schtasks("/Create /F /TN \"" + CleanTask + "\" /TR \"\\\"" + Installer.LaunchPath + "\\\" --autoclean\" " + sched + " /RL HIGHEST") == 0;
        }

        public static void RemoveAll() { SetStartup(false); SetAutoClean("off"); }
    }

    // ===================================================================== INSTALLER
    public static class Installer
    {
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\KEMAS";

        public static string ExePath { get { return Assembly.GetExecutingAssembly().Location; } }
        // What scheduled tasks launch. Store build: the app execution alias, so the task starts KEMAS with its package identity.
        public static string LaunchPath
        {
            get { return AppInfo.IsStore ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\kemas.exe") : ExePath; }
        }
        public static string InstallDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "KEMAS"); } }
        public static string InstalledExe { get { return Path.Combine(InstallDir, "KEMAS.exe"); } }

        public static bool IsInstalledCopy
        {
            get { return string.Equals(Path.GetFullPath(ExePath), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase); }
        }

        public static bool IsInstalled { get { return File.Exists(InstalledExe); } }

        public static void Install()
        {
            Directory.CreateDirectory(InstallDir);
            if (!IsInstalledCopy) File.Copy(ExePath, InstalledExe, true);
            var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "KEMAS.lnk");
            var desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "KEMAS.lnk");
            Shortcut(startMenu, InstalledExe);
            Shortcut(desktop, InstalledExe);
            using (var k = Registry.LocalMachine.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", "KEMAS");
                k.SetValue("DisplayVersion", AppInfo.Version);
                k.SetValue("Publisher", "Amzar Syafiq");
                k.SetValue("DisplayIcon", InstalledExe + ",0");
                k.SetValue("InstallLocation", InstallDir);
                k.SetValue("UninstallString", "\"" + InstalledExe + "\" --uninstall");
                k.SetValue("QuietUninstallString", "\"" + InstalledExe + "\" --uninstall --quiet");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExe).Length / 1024) + 1, RegistryValueKind.DWord);
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                if (!string.IsNullOrEmpty(AppInfo.UpdateRepo)) k.SetValue("URLInfoAbout", "https://github.com/" + AppInfo.UpdateRepo);
            }
        }

        public static void UpdateRegistryVersion()
        {
            try { using (var k = Registry.LocalMachine.OpenSubKey(UninstallKey, true)) if (k != null) k.SetValue("DisplayVersion", AppInfo.Version); } catch { }
        }

        static void Shortcut(string lnk, string target)
        {
            try
            {
                var t = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                var st = sc.GetType();
                st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { target });
                st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(target) });
                st.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { "KEMAS - " + AppInfo.Tagline });
                st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { target + ",0" });
                st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
                Marshal.FinalReleaseComObject(sc); Marshal.FinalReleaseComObject(shell);
            }
            catch (Exception ex) { Log.Error("Shortcut", ex); }
        }

        public static void Uninstall(bool removeData)
        {
            Tasks.RemoveAll();
            foreach (var lnk in new[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "KEMAS.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "KEMAS.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "KEMAS.lnk") })
                try { if (File.Exists(lnk)) File.Delete(lnk); } catch { }
            try { Registry.LocalMachine.DeleteSubKeyTree(UninstallKey, false); } catch { }
            string cmd = "/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"" + InstallDir + "\"";
            if (removeData) cmd += " & rmdir /s /q \"" + AppInfo.DataDir + "\"";
            try { Process.Start(new ProcessStartInfo("cmd.exe", cmd) { CreateNoWindow = true, UseShellExecute = false, WorkingDirectory = Path.GetTempPath() }); } catch { }
        }

        public static void CleanupOldFiles()
        {
            if (AppInfo.IsStore) return;
            try { var old = ExePath + ".old"; if (File.Exists(old)) File.Delete(old); } catch { }
        }
    }

    // ===================================================================== UPDATER (GitHub Releases)
    [DataContract] public class GhAsset { [DataMember(Name = "name")] public string Name; [DataMember(Name = "browser_download_url")] public string Url; [DataMember(Name = "size")] public long Size; }
    [DataContract]
    public class GhRelease
    {
        [DataMember(Name = "tag_name")] public string Tag;
        [DataMember(Name = "name")] public string Title;
        [DataMember(Name = "body")] public string Body;
        [DataMember(Name = "html_url")] public string Page;
        [DataMember(Name = "draft")] public bool Draft;
        [DataMember(Name = "prerelease")] public bool Prerelease;
        [DataMember(Name = "assets")] public List<GhAsset> Assets;
    }

    public class UpdateInfo
    {
        public Version Version; public string Tag, Notes, Page, ExeUrl, HashUrl; public long Size;
    }

    public static class Updater
    {
        public static bool Configured { get { return !AppInfo.IsStore && !string.IsNullOrEmpty(AppInfo.UpdateRepo); } }

        static string Get(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "KEMAS-Updater/" + AppInfo.Version;
            req.Accept = "application/vnd.github+json";
            req.Timeout = 15000;
            using (var resp = req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd();
        }

        public static Version ParseTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            var t = new string(tag.TrimStart('v', 'V').TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
            Version v;
            if (t.Count(c => c == '.') == 0) t += ".0";
            return Version.TryParse(t, out v) ? v : null;
        }

        /// Returns update info when a newer release exists, otherwise null. Throws on network errors.
        public static UpdateInfo Check()
        {
            if (!Configured) return null;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var rel = Json.Read<GhRelease>(Get("https://api.github.com/repos/" + AppInfo.UpdateRepo + "/releases/latest"));
            if (rel == null || rel.Draft || rel.Prerelease) return null;
            var v = ParseTag(rel.Tag);
            if (v == null || v <= AppInfo.ParsedVersion) return null;
            var info = new UpdateInfo { Version = v, Tag = rel.Tag, Notes = rel.Body ?? "", Page = rel.Page };
            if (rel.Assets != null)
                foreach (var a in rel.Assets)
                {
                    if (a.Name.Equals("KEMAS.exe", StringComparison.OrdinalIgnoreCase)) { info.ExeUrl = a.Url; info.Size = a.Size; }
                    else if (a.Name.Equals("KEMAS.exe.sha256", StringComparison.OrdinalIgnoreCase)) info.HashUrl = a.Url;
                }
            return info.ExeUrl == null ? null : info;
        }

        /// Downloads the new exe, verifies it, swaps it in place and returns the path to restart.
        public static string DownloadAndApply(UpdateInfo info, Action<double> progress)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            string dir = Path.Combine(AppInfo.DataDir, "update");
            Directory.CreateDirectory(dir);
            string tmp = Path.Combine(dir, "KEMAS.new.exe");
            var req = (HttpWebRequest)WebRequest.Create(info.ExeUrl);
            req.UserAgent = "KEMAS-Updater/" + AppInfo.Version;
            req.Timeout = 30000;
            using (var resp = req.GetResponse())
            using (var src = resp.GetResponseStream())
            using (var dst = File.Create(tmp))
            {
                long total = resp.ContentLength > 0 ? resp.ContentLength : info.Size, done = 0;
                var buf = new byte[81920]; int n;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    dst.Write(buf, 0, n); done += n;
                    if (progress != null && total > 0) progress((double)done / total);
                }
            }
            // integrity: must be a Windows program, and match the published SHA-256 when one is provided
            var head = new byte[2];
            using (var f = File.OpenRead(tmp)) f.Read(head, 0, 2);
            if (head[0] != 'M' || head[1] != 'Z') throw new InvalidDataException("Downloaded file is not a valid program.");
            if (info.HashUrl != null)
            {
                string expected = Get(info.HashUrl).Trim().Split(' ', '\t', '\n', '\r')[0].ToLowerInvariant();
                string actual;
                using (var sha = SHA256.Create()) using (var f = File.OpenRead(tmp))
                    actual = BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
                if (expected != actual) { File.Delete(tmp); throw new InvalidDataException("Update file failed the security check (SHA-256 mismatch)."); }
            }
            // a running exe can be renamed (not overwritten) on Windows
            string exe = Installer.ExePath, old = exe + ".old";
            if (File.Exists(old)) File.Delete(old);
            File.Move(exe, old);
            try { File.Move(tmp, exe); }
            catch { File.Move(old, exe); throw; }
            return exe;
        }
    }
}
