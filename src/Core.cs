// KEMAS - Core: settings, paths, logging.  (no WPF dependencies)
// Designed & developed by Amzar Syafiq. Engineered with Claude (Anthropic).
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Kemas
{
    public static class AppInfo
    {
        public const string Name = "KEMAS";
        public const string Version = "3.4.5";
        public const string Tagline = "Your PC, Kemas.";
        // GitHub repository used for updates: "owner/repo". Empty = updates disabled.
        public const string UpdateRepo = "AmzSyafxProject/KEMAS";
        public const string FeedbackUrl = "https://tally.so/r/4aXGXo";
        public const string Channel = "Beta";

        public static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KEMAS");
        public static string SettingsFile { get { return Path.Combine(DataDir, "settings.json"); } }
        public static string ErrorLog { get { return Path.Combine(DataDir, "error.log"); } }
        public static string UndoDir { get { return Path.Combine(DataDir, "undo"); } }

        public static Version ParsedVersion { get { return new Version(Version); } }

        // Running as the Microsoft Store (MSIX) package? Then the Store handles install, updates and signing.
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int GetCurrentPackageFullName(ref int length, StringBuilder name);
        public static readonly bool IsStore = DetectPackage();
        static bool DetectPackage()
        {
            try { int len = 0; return GetCurrentPackageFullName(ref len, null) == 122; }   // 122 = buffer too small -> has identity; 15700 = no package
            catch { return false; }                                                         // Windows 7: API missing
        }
    }

    public static class Log
    {
        static readonly object Gate = new object();
        public static void Error(string where, Exception ex)
        {
            Write(where + ": " + (ex == null ? "" : ex.ToString()));
        }
        public static void Write(string text)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(AppInfo.DataDir);
                    var fi = new FileInfo(AppInfo.ErrorLog);
                    if (fi.Exists && fi.Length > 512 * 1024) fi.Delete();
                    File.AppendAllText(AppInfo.ErrorLog, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + text + Environment.NewLine);
                }
            }
            catch { }
        }
    }

    [DataContract]
    public class OrgRule
    {
        [DataMember] public string Name;
        [DataMember] public string Extensions;   // "jpg, png, gif"
        public OrgRule() { }
        public OrgRule(string name, string ext) { Name = name; Extensions = ext; }
    }

    [DataContract]
    public class OrgFolder
    {
        [DataMember] public string Name;
        [DataMember] public string Path;
        [DataMember] public bool Enabled;
        [DataMember] public bool ByDate;
        public OrgFolder() { }
        public OrgFolder(string name, string path, bool enabled, bool byDate) { Name = name; Path = path; Enabled = enabled; ByDate = byDate; }
    }

    [DataContract]
    public class OrgProfile   // what users export / import to share their setup
    {
        [DataMember] public string App = "KEMAS";
        [DataMember] public int Format = 1;
        [DataMember] public List<OrgRule> Rules;
        [DataMember] public string OthersName;
        [DataMember] public int DateFormat;
    }

    [DataContract]
    public class Settings
    {
        [DataMember] public string Theme = "dark";          // dark | light | system
        [DataMember] public string Language = "en";         // en | ms
        [DataMember] public bool ReducedMotion = false;
        [DataMember] public string DisplayName = "";        // empty = detect from Windows
        [DataMember] public bool StartWithWindows = false;
        [DataMember] public bool CloseToTray = true;
        [DataMember] public bool ScanOnStart = true;
        [DataMember] public bool ConfirmClean = true;
        [DataMember] public bool SkipRecentTemp = true;     // keep temp files newer than 24 h
        [DataMember] public bool RemoveLockedAtRestart = true;  // old temp files in use are removed at the next restart
        [DataMember] public bool IntroSeen;                 // launch film shown once
        [DataMember] public bool WelcomeDone;               // first-run welcome finished
        [DataMember] public bool TrimRamOnSmart = false;     // memory trim is manual only (Windows manages RAM itself)
        [DataMember] public int SettingsRev;
        [DataMember] public string AutoClean = "off";       // off | daily | weekly
        [DataMember] public bool AutoUpdate = true;
        [DataMember] public string SkippedVersion = "";
        [DataMember] public List<string> CleanSelection;    // keys ticked in System Cleaner
        [DataMember] public List<OrgFolder> OrgFolders;
        [DataMember] public List<OrgRule> OrgRules;
        [DataMember] public string OrgOthersName = "Others";
        [DataMember] public int OrgDateFormat = 0;          // 0 = 2026\10 - October, 1 = 2026-10, 2 = 2026\October
        [DataMember] public string LastClean = "";
        [DataMember] public double LastFreed;
        [DataMember] public double TotalFreed;
        [DataMember] public int CleanCount;
        [DataMember] public bool InstallOffered;
        [DataMember] public string LastSeenVersion = "";    // drives the "What's new" window after an update

        public static Settings Current;

        // DataContract skips constructors/initializers, so set defaults before reading a (possibly older) file
        [OnDeserializing]
        void Defaults(StreamingContext c)
        {
            Theme = "dark"; Language = "en"; AutoClean = "off"; OrgOthersName = "Others"; DisplayName = "";
            CloseToTray = true; ScanOnStart = true; ConfirmClean = true; SkipRecentTemp = true; TrimRamOnSmart = false; AutoUpdate = true; RemoveLockedAtRestart = true;
        }

        public void FillDefaults()
        {
            if (string.IsNullOrEmpty(Theme)) Theme = "dark";
            if (string.IsNullOrEmpty(Language)) Language = "en";
            if (string.IsNullOrEmpty(AutoClean)) AutoClean = "off";
            if (string.IsNullOrEmpty(OrgOthersName)) OrgOthersName = "Others";
            if (OrgRules == null || OrgRules.Count == 0) OrgRules = DefaultRules();
            if (OrgFolders == null || OrgFolders.Count == 0) OrgFolders = DefaultFolders();
            if (SettingsRev < 1)    // 3.4.0: safer defaults for existing users too
            {
                TrimRamOnSmart = false;
                if (CleanSelection != null) CleanSelection.RemoveAll(k => k == "prefetch" || k == "recycle");
                SettingsRev = 1;
            }
        }

        public static List<OrgRule> DefaultRules()
        {
            return new List<OrgRule>
            {
                new OrgRule("Images", "jpg, jpeg, png, gif, bmp, webp, heic, heif, tif, tiff, svg, ico, raw, cr2, cr3, nef, arw, dng"),
                new OrgRule("Videos", "mp4, mov, mkv, avi, wmv, flv, webm, m4v, mts, m2ts, 3gp, mpg, mpeg"),
                new OrgRule("Music", "mp3, wav, flac, aac, m4a, ogg, wma, aiff, opus, mid, midi"),
                new OrgRule("Documents", "pdf, doc, docx, xls, xlsx, ppt, pptx, txt, rtf, odt, ods, odp, csv, md, epub"),
                new OrgRule("Design Files", "psd, psb, ai, eps, indd, xd, fig, sketch, aep, prproj, drp, blend, cdr, xmp, cube, lut"),
                new OrgRule("Archives", "zip, rar, 7z, tar, gz, bz2, xz, iso"),
                new OrgRule("Installers", "exe, msi, msix, appx, msixbundle"),
                new OrgRule("Code", "py, js, ts, html, css, json, xml, yml, yaml, java, c, cpp, cs, php, sql, ps1, bat, sh"),
                new OrgRule("Fonts", "ttf, otf, woff, woff2")
            };
        }

        public static List<OrgFolder> DefaultFolders()
        {
            return new List<OrgFolder>
            {
                new OrgFolder("Downloads", KnownFolders.Downloads, true, false),
                new OrgFolder("Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), true, true),
                new OrgFolder("Videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), true, true),
                new OrgFolder("Music", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), true, true),
                new OrgFolder("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), false, false)
            };
        }

        public static Settings Load()
        {
            Settings s = null;
            try
            {
                if (File.Exists(AppInfo.SettingsFile))
                    s = Json.Read<Settings>(File.ReadAllText(AppInfo.SettingsFile, Encoding.UTF8));
            }
            catch (Exception ex) { Log.Error("Settings.Load", ex); }
            if (s == null) s = new Settings();
            s.FillDefaults();
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppInfo.DataDir);
                string tmp = AppInfo.SettingsFile + ".tmp";
                File.WriteAllText(tmp, Json.Write(this), Encoding.UTF8);
                if (File.Exists(AppInfo.SettingsFile)) File.Delete(AppInfo.SettingsFile);
                File.Move(tmp, AppInfo.SettingsFile);
            }
            catch (Exception ex) { Log.Error("Settings.Save", ex); }
        }
    }

    public static class Json
    {
        public static T Read<T>(string text) where T : class
        {
            var ser = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(text))) return ser.ReadObject(ms) as T;
        }
        public static string Write<T>(T obj)
        {
            var ser = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream())
            {
                ser.WriteObject(ms, obj);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
    }

    public static class KnownFolders
    {
        public static string Downloads
        {
            get
            {
                try
                {
                    using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders"))
                    {
                        if (k != null)
                        {
                            var v = k.GetValue("{374DE290-123F-4565-9164-39C4925E467B}") as string;
                            if (!string.IsNullOrEmpty(v)) return Environment.ExpandEnvironmentVariables(v);
                        }
                    }
                }
                catch { }
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            }
        }
    }

    public static class Fmt
    {
        public static string Size(double bytes)
        {
            if (bytes < 0) bytes = 0;
            if (bytes >= 1099511627776.0) return (bytes / 1099511627776.0).ToString("0.00") + " TB";
            if (bytes >= 1073741824.0) return (bytes / 1073741824.0).ToString("0.00") + " GB";
            if (bytes >= 1048576.0) return (bytes / 1048576.0).ToString("0.0") + " MB";
            if (bytes >= 1024.0) return (bytes / 1024.0).ToString("0") + " KB";
            return bytes.ToString("0") + " B";
        }
    }
}
