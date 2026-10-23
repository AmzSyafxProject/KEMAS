// KEMAS - Engine: cleaner, organizer, memory, programs, startup apps.  (no WPF dependencies)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Kemas
{
    public static class Native
    {
        [DllImport("psapi.dll")] public static extern bool EmptyWorkingSet(IntPtr hProcess);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern int SHEmptyRecycleBin(IntPtr hwnd, string root, uint flags);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern int SHQueryRecycleBin(string root, ref SHQUERYRBINFO info);
        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")] public static extern uint DnsFlushResolverCache();
        [DllImport("secur32.dll", CharSet = CharSet.Unicode)] public static extern bool GetUserNameEx(int format, StringBuilder name, ref uint size);
        [DllImport("kernel32.dll")] public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);
        [DllImport("kernel32.dll")] public static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool MoveFileEx(string existing, string newName, int flags);

        // ---- fast directory walk (FindFirstFileEx + large fetch: several times faster than DirectoryInfo on big trees)
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime, ftLastAccessTime, ftLastWriteTime;
            public uint nFileSizeHigh, nFileSizeLow, dwReserved0, dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)] public string cAlternateFileName;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr FindFirstFileExW(string name, int infoLevel, out WIN32_FIND_DATA data, int searchOp, IntPtr filter, int flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool FindNextFileW(IntPtr h, out WIN32_FIND_DATA data);
        [DllImport("kernel32.dll")] static extern bool FindClose(IntPtr h);

        public static readonly bool IsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

        /// Total size of a folder tree without following links. Returns -1 when the native walk is unavailable.
        public static long FastTreeSize(string dir, Func<bool> cancel, DateTime olderThan = default(DateTime))
        {
            long cutoff = olderThan == default(DateTime) ? long.MaxValue : olderThan.ToFileTimeUtc();
            if (!IsWindows) return -1;
            long total = 0;
            var stack = new Stack<string>();
            stack.Push(dir);
            while (stack.Count > 0)
            {
                if (cancel != null && cancel()) break;
                string cur = stack.Pop();
                string q = cur.StartsWith(@"\\", StringComparison.Ordinal) ? cur : @"\\?\" + cur;
                WIN32_FIND_DATA d;
                IntPtr h = FindFirstFileExW(q.TrimEnd('\\') + @"\*", 1 /* FindExInfoBasic */, out d, 0, IntPtr.Zero, 2 /* FIND_FIRST_EX_LARGE_FETCH */);
                if (h == new IntPtr(-1)) continue;
                try
                {
                    do
                    {
                        if (d.cFileName == "." || d.cFileName == "..") continue;
                        if ((d.dwFileAttributes & 0x10) != 0)
                        {
                            if ((d.dwFileAttributes & 0x400) == 0) stack.Push(Path.Combine(cur, d.cFileName));   // skip junctions / symlinks
                        }
                        else
                        {
                            long written = ((long)(uint)d.ftLastWriteTime.dwHighDateTime << 32) | (uint)d.ftLastWriteTime.dwLowDateTime;
                            if (written < cutoff) total += ((long)d.nFileSizeHigh << 32) | d.nFileSizeLow;
                        }
                    } while (FindNextFileW(h, out d));
                }
                finally { FindClose(h); }
            }
            return total;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength; public uint dwMemoryLoad; public ulong ullTotalPhys; public ulong ullAvailPhys;
            public ulong ullTotalPageFile; public ulong ullAvailPageFile; public ulong ullTotalVirtual; public ulong ullAvailVirtual; public ulong ullAvailExtendedVirtual;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SystemParametersInfo(int action, int param, string value, int flags);
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wp, string lp, uint flags, uint timeout, out IntPtr result);
        // asks Windows to reload the current wallpaper from its own settings (nothing is changed); a no-op when the wallpaper is already fine
        public static void RefreshDesktop()
        {
            try { SystemParametersInfo(0x0014, 0, (string)null, 0x02); } catch { }
            try { IntPtr r; SendMessageTimeout((IntPtr)0xFFFF, 0x001A, IntPtr.Zero, "ImmersiveColorSet", 0x2, 1500, out r); } catch { }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SystemParametersInfo(int action, int param, StringBuilder value, int flags);
        /// full path of the wallpaper Windows is showing right now ("" if unknown)
        public static string WallpaperPath()
        {
            try { var sb = new StringBuilder(520); if (SystemParametersInfo(0x0073, sb.Capacity, sb, 0)) return sb.ToString(); } catch { }
            return "";
        }

        public static void TrimSelf()
        {
            try { GC.Collect(); GC.WaitForPendingFinalizers(); EmptyWorkingSet(GetCurrentProcess()); } catch { }
        }
    }

    public static class UserName
    {
        // "Hi, Amzar" - friendly first name from the Windows account (display name first, then account name)
        public static string Detect()
        {
            string n = null;
            try
            {
                uint size = 256; var sb = new StringBuilder(256);
                if (Native.GetUserNameEx(3 /* NameDisplay */, sb, ref size)) n = sb.ToString();
            }
            catch { }
            if (string.IsNullOrWhiteSpace(n)) n = Environment.UserName;
            n = (n ?? "").Trim();
            int sp = n.IndexOfAny(new[] { ' ', '.', '_', '@' });
            if (sp > 0) n = n.Substring(0, sp);
            if (n.Length == 0) return "there";
            return char.ToUpper(n[0], CultureInfo.InvariantCulture) + (n.Length > 1 ? n.Substring(1) : "");
        }
    }

    // ===================================================================== CLEANER
    public enum ItemState { Waiting, Working, Done, Partial, Skipped }

    public class CleanTarget
    {
        public string Key, Icon, Tag;     // Tag: safe | rebuild | permanent | browser | optional
        public bool Default = true;
        public Func<IEnumerable<string>> Folders;         // folders whose CONTENTS are removed
        public Func<IEnumerable<FileInfo>> Files;         // individual files
        public Func<long> CustomSize;
        public Action Custom;                             // runs before folder clearing; throw = partial
        public Action Before, After;
        public Func<string> Guard;                        // returns a note key when this item must be skipped right now
        public bool NoFolderClear;
        public bool MeasureByDrive;                       // freed space = system drive free-space gain (component store)
        public bool Slow;                                 // can take several minutes
        public long Size;
        public bool IsDns { get { return Key == "dns"; } }
    }

    public class CleanResult
    {
        public string Key; public ItemState State; public long Freed; public int Locked, Scheduled; public List<string> Notes = new List<string>();
    }

    public class Cleaner
    {
        static volatile bool toolCancel;
        volatile bool cancel;
        public bool Cancel { get { return cancel; } set { cancel = value; toolCancel = value; } }
        public bool SkipRecentTemp = true;
        public bool RemoveLockedAtRestart = true;   // old temp files that are in use go at the next restart
        int locked, scheduled;
        static readonly string[] NeverClean;

        static Cleaner()
        {
            var list = new List<string>();
            foreach (var p in new[] {
                Environment.GetEnvironmentVariable("SystemDrive") + "\\", Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.System), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "/" })
                if (!string.IsNullOrEmpty(p)) list.Add(Norm(p));
            NeverClean = list.ToArray();
        }

        // the current wallpaper (and Windows' own wallpaper cache) must survive even when it lives in a temp folder
        static string wallPath = "";
        public static void RefreshProtected() { try { wallPath = Native.WallpaperPath() ?? ""; } catch { wallPath = ""; } }
        static bool IsProtectedFile(string full)
        {
            if (string.IsNullOrEmpty(full)) return false;
            if (wallPath.Length > 0 && string.Equals(full, wallPath, StringComparison.OrdinalIgnoreCase)) return true;
            var n = full.ToLowerInvariant();
            return n.Contains(@"\microsoft\windows\themes\") || n.EndsWith("transcodedwallpaper");
        }

        static bool IsPending(string hklmKey)
        {
            try { using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(hklmKey)) return k != null; } catch { return false; }
        }
        // never empty the Windows Update download cache while an update is waiting for a restart
        static string UpdatePendingGuard()
        {
            if (IsPending(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") ||
                IsPending(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") ||
                IsPending(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Services\Pending")) return "note_wu_pending";
            return null;
        }

        static string Norm(string p) { try { return Path.GetFullPath(p).TrimEnd('\\', '/').ToLowerInvariant(); } catch { return p.ToLowerInvariant(); } }

        public static string Win { get { return Environment.GetFolderPath(Environment.SpecialFolder.Windows); } }
        static string Local { get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); } }
        static string ProgData { get { return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData); } }
        static string Roaming { get { return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); } }
        static string LocalLow { get { return Path.Combine(Path.GetDirectoryName(Local) ?? Local, "LocalLow"); } }
        public static string WinOld { get { return Path.Combine(Path.GetPathRoot(Win) ?? "C:\\", "Windows.old"); } }

        public static List<CleanTarget> CreateTargets()
        {
            return new List<CleanTarget>
            {
                new CleanTarget { Key = "usertemp", Icon = "E8B7", Tag = "safe", Folders = () => new[] { Path.GetTempPath() } },
                new CleanTarget { Key = "wintemp", Icon = "E770", Tag = "safe", Folders = () => new[] { Path.Combine(Win, "Temp") } },
                new CleanTarget { Key = "prefetch", Icon = "E945", Tag = "rebuild", Default = false, Folders = () => new[] { Path.Combine(Win, "Prefetch") } },
                new CleanTarget { Key = "recycle", Icon = "E74D", Tag = "permanent", Default = false, NoFolderClear = true, CustomSize = RecycleBinSize,
                    Custom = () => { int r = Native.SHEmptyRecycleBin(IntPtr.Zero, null, 7); if (r != 0 && r != unchecked((int)0x8000FFFF)) throw new CleanException("err_recycle"); } },
                new CleanTarget { Key = "browser", Icon = "E774", Tag = "browser", Folders = BrowserCaches },
                new CleanTarget { Key = "thumbs", Icon = "E8B9", Tag = "safe", Files = () => SafeFiles(Path.Combine(Local, @"Microsoft\Windows\Explorer"), "thumbcache_*.db") },
                new CleanTarget { Key = "wer", Icon = "E7BA", Tag = "safe", Folders = () => new[] {
                    Path.Combine(ProgData, @"Microsoft\Windows\WER\ReportArchive"), Path.Combine(ProgData, @"Microsoft\Windows\WER\ReportQueue"),
                    Path.Combine(Local, @"Microsoft\Windows\WER"), Path.Combine(Local, "CrashDumps"), Path.Combine(Win, "Minidump") } },
                new CleanTarget { Key = "delivery", Icon = "E896", Tag = "safe", Folders = () => new[] {
                    Path.Combine(Win, @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache") } },
                new CleanTarget { Key = "logs", Icon = "E9F9", Tag = "safe", Files = OldLogs },
                new CleanTarget { Key = "winupdate", Icon = "E777", Tag = "optional", Default = false, Guard = UpdatePendingGuard,
                    Folders = () => new[] { Path.Combine(Win, @"SoftwareDistribution\Download") },
                    Before = () => { Service("stop", "wuauserv"); Service("stop", "bits"); },
                    After = () => { Service("start", "wuauserv"); Service("start", "bits"); } },
                new CleanTarget { Key = "dns", Icon = "E701", Tag = "safe", Custom = () => { Native.DnsFlushResolverCache(); } },

                // ---------------- Deep Clean (off by default, run with the Deep Clean button) ----------------
                new CleanTarget { Key = "gpucache", Icon = "E7F4", Tag = "deep", Default = false, Folders = GpuCaches },
                new CleanTarget { Key = "appcache", Icon = "E71D", Tag = "deep", Default = false, Folders = AppCaches },
                new CleanTarget { Key = "inetcache", Icon = "E12B", Tag = "deep", Default = false, Folders = () => new[] {
                    Path.Combine(Local, @"Microsoft\Windows\INetCache\IE"), Path.Combine(Local, @"Microsoft\Windows\INetCache\Low"),
                    Path.Combine(Local, @"Microsoft\Windows\INetCache\Content.Word"), Path.Combine(Local, @"Microsoft\Windows\INetCache\Content.Outlook") } },
                new CleanTarget { Key = "dumps", Icon = "EA39", Tag = "deep", Default = false, Files = MemoryDumps },
                new CleanTarget { Key = "winsxs", Icon = "E9F5", Tag = "deep", Default = false, Slow = true, NoFolderClear = true, MeasureByDrive = true,
                    Custom = () => { { int rc = RunTool("dism.exe", "/Online /Cleanup-Image /StartComponentCleanup", 45); if (rc != 0 && rc != -3) throw new CleanException("err_dism"); } } },
                new CleanTarget { Key = "winold", Icon = "E81C", Tag = "permanent", Default = false, Slow = true, NoFolderClear = true,
                    Folders = () => new[] { WinOld }, Custom = RemoveWindowsOld }
            };
        }

        public static bool IsDeep(CleanTarget t) { return t.Tag == "deep" || t.Key == "winold"; }

        static IEnumerable<string> Existing(IEnumerable<string> paths)
        {
            foreach (var p in paths) if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) yield return p;
        }

        // shader caches - every driver rebuilds these automatically
        static IEnumerable<string> GpuCaches()
        {
            return Existing(new[] {
                Path.Combine(Local, "D3DSCache"),
                Path.Combine(Local, @"NVIDIA\DXCache"), Path.Combine(Local, @"NVIDIA\GLCache"), Path.Combine(Local, @"NVIDIA Corporation\NV_Cache"),
                Path.Combine(LocalLow, @"NVIDIA\PerDriverVersion\DXCache"), Path.Combine(LocalLow, @"NVIDIA\PerDriverVersion\GLCache"),
                Path.Combine(Local, @"AMD\DxCache"), Path.Combine(Local, @"AMD\DxcCache"), Path.Combine(Local, @"AMD\GLCache"), Path.Combine(Local, @"AMD\VkCache"),
                Path.Combine(LocalLow, @"Intel\ShaderCache"), Path.Combine(Local, @"Intel\ShaderCache") }).ToList();
        }

        // cache folders only (never settings, logins or downloads) of common desktop apps
        static IEnumerable<string> AppCaches()
        {
            var list = new List<string>();
            var electron = new[] { Path.Combine(Roaming, "discord"), Path.Combine(Roaming, "Slack"), Path.Combine(Roaming, @"Microsoft\Teams"),
                Path.Combine(Roaming, "Code"), Path.Combine(Roaming, "WhatsApp"), Path.Combine(Roaming, "Spotify"),
                Path.Combine(Local, @"Packages\MSTeams_8wekyb3d8bbwe\LocalCache\Microsoft\MSTeams\EBWebView\Default") };
            foreach (var root in Existing(electron))
                foreach (var sub in new[] { "Cache", "Code Cache", "GPUCache", "DawnCache", "DawnGraphiteCache", "DawnWebGPUCache", "CachedData", @"Service Worker\CacheStorage" })
                    list.Add(Path.Combine(root, sub));
            list.Add(Path.Combine(Local, @"Spotify\Data"));
            list.Add(Path.Combine(Local, @"Steam\htmlcache"));
            list.Add(Path.Combine(Local, @"Microsoft\Office\16.0\OfficeFileCache"));
            return Existing(list).ToList();
        }

        static IEnumerable<FileInfo> MemoryDumps()
        {
            var list = new List<FileInfo>();
            try { var m = new FileInfo(Path.Combine(Win, "MEMORY.DMP")); if (m.Exists) list.Add(m); } catch { }
            var lkr = Path.Combine(Win, "LiveKernelReports");
            try { if (Directory.Exists(lkr)) list.AddRange(new DirectoryInfo(lkr).GetFiles("*.dmp", SearchOption.AllDirectories)); } catch { }
            return list;
        }

        /// Runs a Windows tool hidden and waits; returns its exit code (-1 if it could not run).
        public static int RunTool(string exe, string args, int timeoutMinutes)
        {
            try
            {
                string full = Path.Combine(Win, "System32", exe);
                if (!File.Exists(full)) { var sn = Path.Combine(Win, "Sysnative", exe); if (File.Exists(sn)) full = sn; }
                var psi = new ProcessStartInfo(full, args) { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
                using (var p = Process.Start(psi))
                {
                    var end = DateTime.UtcNow.AddMinutes(timeoutMinutes);
                    while (!p.WaitForExit(500))
                    {
                        if (toolCancel || DateTime.UtcNow > end)
                        {
                            try { Process.Start(new ProcessStartInfo("taskkill.exe", "/PID " + p.Id + " /T /F") { CreateNoWindow = true, UseShellExecute = false }).WaitForExit(5000); } catch { }
                            return toolCancel ? -3 : -2;
                        }
                    }
                    return p.ExitCode;
                }
            }
            catch { return -1; }
        }

        // Windows.old is owned by TrustedInstaller: take ownership, grant admins, then delete
        static void RemoveWindowsOld()
        {
            var dir = WinOld;
            if (!Directory.Exists(dir)) return;
            RunTool("takeown.exe", "/F \"" + dir + "\" /R /A /D Y", 30);
            RunTool("icacls.exe", "\"" + dir + "\" /grant *S-1-5-32-544:F /T /C /Q", 30);
            RunTool("cmd.exe", "/c rd /s /q \"" + dir + "\"", 30);
            if (Directory.Exists(dir)) throw new CleanException("err_winold");
        }

        static long FreeSpace()
        {
            try { return new DriveInfo(Path.GetPathRoot(Win)).AvailableFreeSpace; } catch { return 0; }
        }

        static void Service(string verb, string name)
        {
            try
            {
                var psi = new ProcessStartInfo("sc.exe", verb + " " + name) { CreateNoWindow = true, UseShellExecute = false };
                using (var p = Process.Start(psi)) p.WaitForExit(15000);
                if (verb == "stop") System.Threading.Thread.Sleep(1200);
            }
            catch { }
        }

        static long RecycleBinSize()
        {
            try
            {
                var info = new Native.SHQUERYRBINFO { cbSize = Marshal.SizeOf(typeof(Native.SHQUERYRBINFO)) };
                if (Native.SHQueryRecycleBin(null, ref info) == 0) return info.i64Size;
            }
            catch { }
            return 0;
        }

        static IEnumerable<FileInfo> SafeFiles(string dir, string pattern)
        {
            try { if (Directory.Exists(dir)) return new DirectoryInfo(dir).GetFiles(pattern); } catch { }
            return new FileInfo[0];
        }

        static IEnumerable<FileInfo> OldLogs()
        {
            // Windows setup / CBS logs older than 7 days
            var list = new List<FileInfo>();
            var cutoff = DateTime.Now.AddDays(-7);
            foreach (var d in new[] { Path.Combine(Win, @"Logs\CBS"), Path.Combine(Win, @"Logs\DISM"), Path.Combine(Win, @"Panther"), Path.Combine(Win, @"Logs\MoSetup"), Path.Combine(Win, @"Logs\NetSetup") })
                foreach (var f in SafeFiles(d, "*.log")) if (f.LastWriteTime < cutoff) list.Add(f);
            foreach (var f in SafeFiles(Path.Combine(Win, @"Logs\CBS"), "CbsPersist_*.cab")) if (f.LastWriteTime < cutoff) list.Add(f);     // compressed old CBS logs, often hundreds of MB
            foreach (var f in SafeFiles(Path.Combine(Win, @"Logs\WindowsUpdate"), "*.etl")) if (f.LastWriteTime < cutoff) list.Add(f);
            return list;
        }

        public static IEnumerable<string> BrowserCaches()
        {
            var list = new List<string>();
            var chromium = new[] { @"Google\Chrome\User Data", @"Microsoft\Edge\User Data", @"BraveSoftware\Brave-Browser\User Data", @"Vivaldi\User Data" };
            foreach (var rel in chromium)
            {
                var root = Path.Combine(Local, rel);
                if (!Directory.Exists(root)) continue;
                try
                {
                    foreach (var prof in Directory.GetDirectories(root))
                        foreach (var sub in new[] { "Cache", "Code Cache", "GPUCache" })
                        {
                            var p = Path.Combine(prof, sub);
                            if (Directory.Exists(p)) list.Add(p);
                        }
                }
                catch { }
                foreach (var sub in new[] { "ShaderCache", "GrShaderCache" })
                {
                    var p = Path.Combine(root, sub);
                    if (Directory.Exists(p)) list.Add(p);
                }
            }
            var opera = Path.Combine(Local, @"Opera Software\Opera Stable\Cache");
            if (Directory.Exists(opera)) list.Add(opera);
            var ff = Path.Combine(Local, @"Mozilla\Firefox\Profiles");
            if (Directory.Exists(ff))
                try { foreach (var prof in Directory.GetDirectories(ff)) { var p = Path.Combine(prof, "cache2"); if (Directory.Exists(p)) list.Add(p); } } catch { }
            return list;
        }

        public static bool BrowserRunning()
        {
            foreach (var n in new[] { "chrome", "msedge", "firefox", "brave", "opera", "vivaldi" })
                try { if (Process.GetProcessesByName(n).Length > 0) return true; } catch { }
            return false;
        }

        // ---- measuring ----
        public long TreeSize(string path) { return TreeSize(path, default(DateTime)); }

        /// olderThan: only count files last written before this time (what a clean will really remove).
        public long TreeSize(string path, DateTime olderThan)
        {
            long total = 0;
            if (Cancel) return 0;
            try
            {
                if (!Directory.Exists(path)) return 0;
                long fast = Native.FastTreeSize(Path.GetFullPath(path), () => Cancel, olderThan);
                if (fast >= 0) return fast;
            }
            catch { }
            try
            {
                var di = new DirectoryInfo(path);
                if (!di.Exists) return 0;
                foreach (var f in di.EnumerateFiles()) if (olderThan == default(DateTime) || f.LastWriteTime < olderThan) total += f.Length;
                foreach (var d in di.EnumerateDirectories())
                    if ((d.Attributes & FileAttributes.ReparsePoint) == 0) total += TreeSize(d.FullName, olderThan);
            }
            catch { }
            return total;
        }

        public long Measure(CleanTarget t)
        {
            long total = 0;
            try
            {
                if (t.CustomSize != null) total += t.CustomSize();
                // temp folders: files from the last 24 h are kept by the clean, so they are not counted as junk either
                var cutoff = SkipRecentTemp && (t.Key == "usertemp" || t.Key == "wintemp") ? DateTime.Now.AddHours(-24) : default(DateTime);
                if (t.Folders != null) foreach (var p in t.Folders()) if (!string.IsNullOrEmpty(p)) total += TreeSize(p, cutoff);
                if (t.Files != null) foreach (var f in t.Files()) total += f.Length;
            }
            catch { }
            return total;
        }

        // ---- deleting ----
        bool TooRecent(FileSystemInfo f, bool isTemp)
        {
            return isTemp && SkipRecentTemp && f.LastWriteTime > DateTime.Now.AddHours(-24);
        }

        void RemoveTree(DirectoryInfo di, bool isTemp)
        {
            if (Cancel) return;
            try
            {
                if ((di.Attributes & FileAttributes.ReparsePoint) != 0) { di.Delete(); return; }   // remove the link, never follow it
                foreach (var f in di.GetFiles())
                {
                    if (TooRecent(f, isTemp)) { continue; }
                    DeleteFile(f, isTemp);
                }
                foreach (var d in di.GetDirectories()) RemoveTree(d, isTemp);
                if (di.GetFileSystemInfos().Length == 0) di.Delete();
            }
            catch { }
        }

        void DeleteFile(FileInfo f, bool isTemp)
        {
            if (IsProtectedFile(f.FullName)) return;
            try { f.Attributes = FileAttributes.Normal; f.Delete(); return; }
            catch { }
            // in use right now: for temp files older than a day, let Windows remove them during the next restart
            if (isTemp && RemoveLockedAtRestart && Native.IsWindows && f.LastWriteTime < DateTime.Now.AddHours(-24))
            {
                try { if (Native.MoveFileEx(f.FullName, null, 4 /* MOVEFILE_DELAY_UNTIL_REBOOT */)) { scheduled++; return; } } catch { }
            }
            locked++;
        }

        public bool ClearFolder(string path, bool isTemp)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string full = Norm(path);
            if (full.Length <= 3 || NeverClean.Contains(full)) return false;
            var di = new DirectoryInfo(path);
            if (!di.Exists) return false;
            try
            {
                foreach (var f in di.GetFiles())
                {
                    if (Cancel) return true;
                    if (TooRecent(f, isTemp)) continue;
                    DeleteFile(f, isTemp);
                }
            }
            catch { }
            try { foreach (var d in di.GetDirectories()) RemoveTree(d, isTemp); } catch { }
            return true;
        }

        static readonly HashSet<string> AdminOnly = new HashSet<string> { "wintemp", "prefetch", "delivery", "logs", "winupdate", "dumps", "winsxs", "winold" };
        static bool IsAdmin()
        {
            try { using (var id = System.Security.Principal.WindowsIdentity.GetCurrent()) return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator); }
            catch { return true; }
        }

        public CleanResult Clean(CleanTarget t)
        {
            var r = new CleanResult { Key = t.Key, State = ItemState.Done };
            // the Microsoft Store edition runs without administrator rights, so system-wide items are skipped there (the installer/portable edition cleans them)
            if (AppInfo.IsStore && AdminOnly.Contains(t.Key) && !IsAdmin()) { r.State = ItemState.Skipped; r.Notes.Add("note_admin_needed"); return r; }
            locked = 0; scheduled = 0; toolCancel = Cancel;
            RefreshProtected();
            if (t.Guard != null)
            {
                string why = null; try { why = t.Guard(); } catch { }
                if (why != null) { r.State = ItemState.Skipped; r.Notes.Add(why); return r; }
            }
            long before = Measure(t);
            long driveBefore = t.MeasureByDrive ? FreeSpace() : 0;
            bool isTemp = t.Key == "usertemp" || t.Key == "wintemp";
            foreach (var step in new[] { t.Before, t.Custom })
            {
                if (step == null) continue;
                try { step(); }
                catch (CleanException ce) { r.State = ItemState.Partial; r.Notes.Add(ce.Message); }
                catch (Exception ex) { r.State = ItemState.Partial; r.Notes.Add(Friendly(ex)); }
            }
            if (t.Folders != null && !t.NoFolderClear) foreach (var p in t.Folders()) ClearFolder(p, isTemp);
            if (t.Files != null) foreach (var f in t.Files()) { if (IsProtectedFile(f.FullName)) continue; try { f.Delete(); } catch { locked++; } }
            if (t.After != null) { try { t.After(); } catch (Exception ex) { r.Notes.Add(Friendly(ex)); } }
            t.Size = Measure(t);
            r.Freed = Math.Max(0, before - t.Size);
            if (t.MeasureByDrive) { r.Freed = Math.Max(0, FreeSpace() - driveBefore); t.Size = 0; }
            r.Locked = locked;
            r.Scheduled = scheduled;
            if (isTemp && SkipRecentTemp && t.Size > 0) r.Notes.Add("note_recent");
            return r;
        }

        public static string Friendly(Exception ex)
        {
            if (ex is UnauthorizedAccessException) return "err_access";
            if (ex is FileNotFoundException || ex is DirectoryNotFoundException) return "err_gone";
            var io = ex as IOException;
            if (io != null) { int code = Marshal.GetHRForException(io) & 0xFFFF; if (code == 32 || code == 33) return "err_inuse"; }
            var m = ex.Message ?? "";
            return m.Length > 110 ? m.Substring(0, 107) + "..." : m;
        }
    }

    public class CleanException : Exception { public CleanException(string key) : base(key) { } }

    // ===================================================================== ORGANIZER
    public class PlanItem
    {
        public string File { get; set; }
        public string Folder { get; set; }
        public string Destination { get; set; }
        public string Size { get; set; }
        public long Bytes { get; set; }
        public string Source;
        public string DestDir;
    }

    public class OrgOutcome { public int Moved; public List<string> Skipped = new List<string>(); public string UndoFile; }

    public static class Organizer
    {
        static readonly HashSet<string> SkipExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".crdownload", ".part", ".partial", ".tmp", ".download", ".opdownload", ".!ut", ".aria2", ".lnk", ".url", ".ini", ".sys" };

        public static Dictionary<string, string> BuildMap(IEnumerable<OrgRule> rules)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in rules)
            {
                var name = CleanName(r.Name);
                if (name.Length == 0 || string.IsNullOrEmpty(r.Extensions)) continue;
                foreach (var raw in r.Extensions.Split(new[] { ',', ' ', ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var e = raw.Trim().TrimStart('*').TrimStart('.');
                    if (e.Length > 0 && !map.ContainsKey("." + e)) map["." + e] = name;
                }
            }
            return map;
        }

        public static string CleanName(string name)
        {
            if (name == null) return "";
            var bad = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (var c in name.Trim()) sb.Append(Array.IndexOf(bad, c) >= 0 ? '-' : c);
            var s = sb.ToString().Trim().TrimEnd('.');
            return s;
        }

        public static string DateFolder(DateTime dt, int format)
        {
            var inv = CultureInfo.InvariantCulture;
            switch (format)
            {
                case 1: return dt.ToString("yyyy-MM", inv);
                case 2: return dt.ToString("yyyy", inv) + Path.DirectorySeparatorChar + dt.ToString("MMMM", inv);
                default: return dt.ToString("yyyy", inv) + Path.DirectorySeparatorChar + dt.ToString("MM - MMMM", inv);
            }
        }

        public static List<PlanItem> Plan(IEnumerable<OrgFolder> folders, IEnumerable<OrgRule> rules, string othersName, int dateFormat)
        {
            var plan = new List<PlanItem>();
            var map = BuildMap(rules);
            var others = CleanName(othersName); if (others.Length == 0) others = "Others";
            var cutoff = DateTime.Now.AddMinutes(-2);
            foreach (var f in folders)
            {
                if (!f.Enabled || string.IsNullOrEmpty(f.Path) || !Directory.Exists(f.Path)) continue;
                FileInfo[] files;
                try { files = new DirectoryInfo(f.Path).GetFiles(); } catch { continue; }
                foreach (var file in files)
                {
                    if ((file.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                    if (SkipExt.Contains(file.Extension) || file.LastWriteTime > cutoff) continue;
                    string sub;
                    if (f.ByDate) sub = DateFolder(file.LastWriteTime, dateFormat);
                    else { if (!map.TryGetValue(file.Extension, out sub)) sub = others; }
                    plan.Add(new PlanItem
                    {
                        File = file.Name, Folder = f.Name, Destination = f.Name + Path.DirectorySeparatorChar + sub, Size = Fmt.Size(file.Length),
                        Bytes = file.Length, Source = file.FullName, DestDir = Path.Combine(f.Path, sub)
                    });
                }
            }
            return plan;
        }

        public static string UniquePath(string path)
        {
            if (!System.IO.File.Exists(path) && !Directory.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path), b = Path.GetFileNameWithoutExtension(path), e = Path.GetExtension(path);
            for (int n = 1; ; n++)
            {
                var c = Path.Combine(dir, b + " (" + n + ")" + e);
                if (!System.IO.File.Exists(c) && !Directory.Exists(c)) return c;
            }
        }

        public static OrgOutcome Run(List<PlanItem> plan, Func<bool> cancel, Action<int, int, PlanItem> progress)
        {
            var res = new OrgOutcome();
            var log = new StringBuilder();
            int i = 0;
            foreach (var p in plan)
            {
                if (cancel != null && cancel()) break;
                i++;
                try
                {
                    if (!System.IO.File.Exists(p.Source)) throw new FileNotFoundException();
                    Directory.CreateDirectory(p.DestDir);
                    var dest = UniquePath(Path.Combine(p.DestDir, p.File));
                    System.IO.File.Move(p.Source, dest);
                    log.Append(p.Source).Append('\t').Append(dest).Append('\n');
                    res.Moved++;
                }
                catch (Exception ex) { res.Skipped.Add(p.File + "|" + Cleaner.Friendly(ex)); }
                if (progress != null) progress(i, plan.Count, p);
            }
            if (res.Moved > 0)
            {
                Directory.CreateDirectory(AppInfo.UndoDir);
                res.UndoFile = Path.Combine(AppInfo.UndoDir, "undo_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".tsv");
                System.IO.File.WriteAllText(res.UndoFile, log.ToString(), Encoding.UTF8);
            }
            return res;
        }

        public static string LastUndoFile()
        {
            try
            {
                if (!Directory.Exists(AppInfo.UndoDir)) return null;
                return Directory.GetFiles(AppInfo.UndoDir, "undo_*.tsv").OrderByDescending(x => x).FirstOrDefault();
            }
            catch { return null; }
        }

        public static int UndoCount(string file)
        {
            try { return System.IO.File.ReadAllLines(file).Count(l => l.Contains("\t")); } catch { return 0; }
        }

        public static OrgOutcome Undo(string file, Action<int, int> progress)
        {
            var res = new OrgOutcome();
            var lines = System.IO.File.ReadAllLines(file).Where(l => l.Contains("\t")).Reverse().ToList();
            var touched = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int i = 0;
            foreach (var l in lines)
            {
                i++;
                var parts = l.Split('\t');
                string from = parts[0], to = parts[1];
                try
                {
                    if (!System.IO.File.Exists(to)) throw new CleanException("err_gone");
                    if (System.IO.File.Exists(from)) throw new CleanException("err_exists");
                    System.IO.File.Move(to, from);
                    res.Moved++;
                    touched[Path.GetDirectoryName(to)] = Path.GetDirectoryName(from);
                }
                catch (CleanException ce) { res.Skipped.Add(Path.GetFileName(from) + "|" + ce.Message); }
                catch (Exception ex) { res.Skipped.Add(Path.GetFileName(from) + "|" + Cleaner.Friendly(ex)); }
                if (progress != null) progress(i, lines.Count);
            }
            foreach (var kv in touched)
            {
                string root = kv.Value.TrimEnd('\\', '/'), cur = kv.Key;
                while (cur != null && cur.TrimEnd('\\', '/').Length > root.Length && cur.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    try { if (Directory.Exists(cur) && Directory.GetFileSystemEntries(cur).Length == 0) Directory.Delete(cur); else break; } catch { break; }
                    cur = Path.GetDirectoryName(cur);
                }
            }
            try { System.IO.File.Move(file, file + ".done"); } catch { }
            return res;
        }
    }

    // ===================================================================== MEMORY
    public class ProcRow
    {
        public string App { get; set; }
        public string RAM { get; set; }
        public double RamMB { get; set; }
        public int Procs { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public bool Protected;
    }

    public static class Memory
    {
        public static readonly HashSet<string> Protected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "idle", "system", "secure system", "registry", "memory compression", "smss", "csrss", "wininit", "winlogon", "services", "lsass",
            "lsaiso", "svchost", "dwm", "explorer", "fontdrvhost", "sihost", "ctfmon", "taskhostw", "runtimebroker", "searchhost",
            "searchindexer", "searchapp", "startmenuexperiencehost", "shellexperiencehost", "textinputhost", "securityhealthservice",
            "securityhealthsystray", "msmpeng", "nissrv", "mpdefendercoreservice", "spoolsv", "audiodg", "conhost", "dllhost", "wudfhost",
            "smartscreen", "lsm", "wmiprvse", "applicationframehost", "vmmem", "vmmemwsl", "msedgewebview2", "kemas"
        };

        // Windows Explorer and the other desktop-shell processes: never trimmed and never ended by KEMAS, whatever the list above says
        static readonly HashSet<string> Shell = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "explorer", "dwm", "sihost", "shellexperiencehost", "startmenuexperiencehost", "searchhost", "searchapp", "textinputhost", "ctfmon", "taskhostw", "applicationframehost", "runtimebroker", "winlogon", "csrss", "fontdrvhost" };
        public static bool IsShell(string name) { return !string.IsNullOrEmpty(name) && Shell.Contains(name.Replace(".exe", "")); }

        // only ordinary user apps are ever trimmed: not shell processes, and nothing that lives in the Windows folder (or whose path cannot be read)
        static bool SafeToTrim(Process p)
        {
            try
            {
                if (IsShell(SafeName(p)) || Protected.Contains(SafeName(p))) return false;
                string path = p.MainModule.FileName;
                string win = Cleaner.Win.TrimEnd('\\') + "\\";
                return !string.IsNullOrEmpty(path) && !path.StartsWith(win, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        public static void Info(out double total, out double free, out int pct)
        {
            var m = new Native.MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(Native.MEMORYSTATUSEX)) };
            if (Native.GlobalMemoryStatusEx(ref m)) { total = m.ullTotalPhys; free = m.ullAvailPhys; pct = (int)m.dwMemoryLoad; }
            else { total = 1; free = 1; pct = 0; }
        }

        public static List<ProcRow> Groups()
        {
            var rows = new List<ProcRow>();
            Process[] all;
            try { all = Process.GetProcesses(); } catch { return rows; }
            int me = Process.GetCurrentProcess().Id;
            var firstOf = new Dictionary<ProcRow, Process>();
            foreach (var g in all.Where(p => p.Id > 4).GroupBy(p => SafeName(p), StringComparer.OrdinalIgnoreCase))
            {
                long sum = 0;
                foreach (var p in g) { try { sum += p.WorkingSet64; } catch { } }
                bool prot = Protected.Contains(g.Key) || g.Any(p => p.Id == me);
                var row = new ProcRow { App = g.Key, Procs = g.Count(), RamMB = Math.Round(sum / 1048576.0, 1), RAM = Fmt.Size(sum), Description = "", Protected = prot,
                    Status = prot ? "protected" : "canclose" };
                rows.Add(row);
                firstOf[row] = g.First();
            }
            var top = rows.OrderByDescending(r => r.RamMB).Take(60).ToList();
            // reading the file description opens each module, so only do it for the rows that are shown
            foreach (var r in top) { try { r.Description = firstOf[r].MainModule.FileVersionInfo.FileDescription ?? ""; } catch { } }
            foreach (var p in all) { try { p.Dispose(); } catch { } }
            return top;
        }

        static string SafeName(Process p) { try { return p.ProcessName; } catch { return "?"; } }

        public static long Trim(Action<double> progress)
        {
            double t, before, after; int pct;
            Info(out t, out before, out pct);
            var procs = Process.GetProcesses();
            int mySession = 0; try { mySession = Process.GetCurrentProcess().SessionId; } catch { }
            for (int i = 0; i < procs.Length; i++)
            {
                // Gentle trim: only ordinary apps the user can see (a window, in this desktop session, using real memory).
                // Never the desktop shell, window manager, services, drivers or anything without a window - trimming those can blank the wallpaper.
                try
                {
                    var p = procs[i];
                    if (SafeToTrim(p) && p.SessionId == mySession && p.SessionId != 0 && p.MainWindowHandle != IntPtr.Zero && p.WorkingSet64 > 80L * 1048576)
                        Native.EmptyWorkingSet(p.Handle);
                }
                catch { }
                try { procs[i].Dispose(); } catch { }
                if (progress != null && i % 8 == 0) progress((double)i / procs.Length);
            }
            System.Threading.Thread.Sleep(600);
            Native.RefreshDesktop();
            Info(out t, out after, out pct);
            return (long)Math.Max(0, after - before);
        }

        public static int Kill(string name)
        {
            int killed = 0, me = Process.GetCurrentProcess().Id;
            if (IsShell(name) || Protected.Contains(name)) return 0;
            foreach (var p in Process.GetProcessesByName(name))
            {
                try { if (p.Id != me) { p.Kill(); killed++; } } catch { }
                finally { p.Dispose(); }
            }
            return killed;
        }
    }

    // ===================================================================== PROGRAMS
    public class AppRow
    {
        public string Name { get; set; }
        public string Publisher { get; set; }
        public string Version { get; set; }
        public string Size { get; set; }
        public double SizeMB { get; set; }
        public string Installed { get; set; }
        public string InstallSort { get; set; }
        public string Uninstall;
    }

    public static class Programs
    {
        public static List<AppRow> List()
        {
            var rows = new Dictionary<string, AppRow>(StringComparer.OrdinalIgnoreCase);
            var roots = new[] {
                Tuple.Create(RegistryHive.LocalMachine, RegistryView.Registry64), Tuple.Create(RegistryHive.LocalMachine, RegistryView.Registry32),
                Tuple.Create(RegistryHive.CurrentUser, RegistryView.Default) };
            foreach (var r in roots)
            {
                try
                {
                    using (var bk = RegistryKey.OpenBaseKey(r.Item1, r.Item2))
                    using (var k = bk.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (k == null) continue;
                        foreach (var sub in k.GetSubKeyNames())
                        {
                            try
                            {
                                using (var s = k.OpenSubKey(sub))
                                {
                                    var name = s.GetValue("DisplayName") as string;
                                    var un = s.GetValue("UninstallString") as string;
                                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(un)) continue;
                                    if (Convert.ToInt32(s.GetValue("SystemComponent", 0)) == 1 || s.GetValue("ParentKeyName") != null) continue;
                                    if (rows.ContainsKey(name)) continue;
                                    double kb = 0; try { kb = Convert.ToDouble(s.GetValue("EstimatedSize", 0)); } catch { }
                                    string date = s.GetValue("InstallDate") as string ?? "", nice = "";
                                    DateTime dt;
                                    if (date.Length == 8 && DateTime.TryParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                                        nice = dt.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
                                    else date = "";
                                    rows[name] = new AppRow { Name = name, Publisher = s.GetValue("Publisher") as string ?? "", Version = s.GetValue("DisplayVersion") as string ?? "",
                                        SizeMB = Math.Round(kb / 1024, 1), Size = kb > 0 ? Fmt.Size(kb * 1024) : "-", Installed = nice, InstallSort = date, Uninstall = un };
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
            return rows.Values.OrderByDescending(a => a.SizeMB).ToList();
        }

        public static void StartUninstall(AppRow a)
        {
            var cmd = System.Text.RegularExpressions.Regex.Replace(a.Uninstall, @"(?i)msiexec(\.exe)?\s+/I", "MsiExec.exe /X");
            var psi = new ProcessStartInfo("cmd.exe", "/c \"" + cmd + "\"") { CreateNoWindow = true, UseShellExecute = false };
            Process.Start(psi);
        }
    }

    // ===================================================================== STARTUP APPS
    public class StartupItem
    {
        public string Name { get; set; }
        public string Command { get; set; }
        public string Location { get; set; }
        public bool Enabled { get; set; }
        public RegistryHive Hive;
        public RegistryView View;
        public string ApprovedKey;     // StartupApproved\Run | Run32 | StartupFolder
        public string ValueName;
    }

    public static class Startup
    {
        const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

        public static List<StartupItem> List()
        {
            var list = new List<StartupItem>();
            AddRun(list, RegistryHive.CurrentUser, RegistryView.Default, "Run", "User");
            AddRun(list, RegistryHive.LocalMachine, RegistryView.Registry64, "Run", "All users");
            AddRun(list, RegistryHive.LocalMachine, RegistryView.Registry32, "Run32", "All users (32-bit)");
            AddFolder(list, Environment.GetFolderPath(Environment.SpecialFolder.Startup), RegistryHive.CurrentUser, "Startup folder");
            AddFolder(list, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), RegistryHive.LocalMachine, "Startup folder (all users)");
            return list.Where(i => !i.Name.Equals("KEMAS", StringComparison.OrdinalIgnoreCase)).OrderBy(i => i.Name).ToList();
        }

        static void AddRun(List<StartupItem> list, RegistryHive hive, RegistryView view, string approvedKey, string where)
        {
            try
            {
                using (var bk = RegistryKey.OpenBaseKey(hive, view))
                using (var k = bk.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (k == null) return;
                    foreach (var v in k.GetValueNames())
                    {
                        if (string.IsNullOrEmpty(v)) continue;
                        list.Add(new StartupItem { Name = v, Command = Convert.ToString(k.GetValue(v)), Location = where, Hive = hive, View = view,
                            ApprovedKey = approvedKey, ValueName = v, Enabled = IsEnabled(hive, approvedKey, v) });
                    }
                }
            }
            catch { }
        }

        static void AddFolder(List<StartupItem> list, string dir, RegistryHive hive, string where)
        {
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
                foreach (var f in Directory.GetFiles(dir))
                {
                    if (Path.GetFileName(f).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    var n = Path.GetFileName(f);
                    list.Add(new StartupItem { Name = Path.GetFileNameWithoutExtension(f), Command = f, Location = where, Hive = hive, View = RegistryView.Default,
                        ApprovedKey = "StartupFolder", ValueName = n, Enabled = IsEnabled(hive, "StartupFolder", n) });
                }
            }
            catch { }
        }

        static bool IsEnabled(RegistryHive hive, string approvedKey, string name)
        {
            try
            {
                using (var bk = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
                using (var k = bk.OpenSubKey(Approved + approvedKey))
                {
                    var b = k == null ? null : k.GetValue(name) as byte[];
                    if (b == null || b.Length == 0) return true;
                    return (b[0] & 1) == 0;   // 02 / 06 = enabled, 03 / 07 = disabled (same as Task Manager)
                }
            }
            catch { return true; }
        }

        public static void SetEnabled(StartupItem item, bool enabled)
        {
            using (var bk = RegistryKey.OpenBaseKey(item.Hive, RegistryView.Default))
            using (var k = bk.CreateSubKey(Approved + item.ApprovedKey))
            {
                var b = new byte[12];
                b[0] = (byte)(enabled ? 0x02 : 0x03);
                if (!enabled) BitConverter.GetBytes(DateTime.Now.ToFileTimeUtc()).CopyTo(b, 4);
                k.SetValue(item.ValueName, b, RegistryValueKind.Binary);
            }
            item.Enabled = enabled;
        }
    }
}
