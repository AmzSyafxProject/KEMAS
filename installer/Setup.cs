using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("KEMAS Setup")]
[assembly: AssemblyProduct("KEMAS - Your PC, Kemas.")]
[assembly: AssemblyCompany("Amzar Syafiq")]
[assembly: AssemblyVersion("3.3.3.0")]

namespace KemasSetup
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool silent = args.Any(a => a.Equals("/S", StringComparison.OrdinalIgnoreCase) || a.Equals("--silent", StringComparison.OrdinalIgnoreCase));
            if (silent)
            {
                try { Installer.Run(null, true); return 0; } catch { return 1; }
            }
            Application.Run(new Wizard());
            return 0;
        }
    }

    static class Installer
    {
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\KEMAS";
        public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "KEMAS"); } }
        public static string Exe { get { return Path.Combine(Dir, "KEMAS.exe"); } }

        static byte[] Read(string name)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null) throw new InvalidOperationException("Setup package is damaged.");
                var ms = new MemoryStream(); s.CopyTo(ms); return ms.ToArray();
            }
        }

        static string Hex(byte[] b) { return string.Concat(b.Select(x => x.ToString("x2"))); }

        // progress: 0..100 with status text
        public static void Run(Action<int, string> progress, bool desktopShortcut)
        {
            Action<int, string> p = progress ?? ((a, b) => { });
            p(5, "Checking the setup package");
            byte[] exe = Read("KEMAS.exe");
            string want = System.Text.Encoding.ASCII.GetString(Read("KEMAS.exe.sha256")).Trim().ToLowerInvariant();
            string got = Hex(SHA256.Create().ComputeHash(exe));
            if (want != got) throw new InvalidOperationException("The setup package failed its integrity check. Download it again from the official KEMAS page.");

            p(18, "Closing KEMAS if it is running");
            foreach (var pr in Process.GetProcessesByName("KEMAS"))
            {
                try { if (pr.Id != Process.GetCurrentProcess().Id) { pr.CloseMainWindow(); if (!pr.WaitForExit(2500)) pr.Kill(); } } catch { }
            }
            Thread.Sleep(300);

            p(35, "Copying files");
            Directory.CreateDirectory(Dir);
            string tmp = Exe + ".new";
            File.WriteAllBytes(tmp, exe);
            if (File.Exists(Exe)) { try { File.Delete(Exe); } catch { File.Move(Exe, Exe + ".old"); } }
            File.Move(tmp, Exe);

            p(60, "Creating shortcuts");
            Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "KEMAS.lnk"), Exe);
            string dsk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "KEMAS.lnk");
            if (desktopShortcut) Shortcut(dsk, Exe);

            p(80, "Registering with Windows");
            using (var k = Registry.LocalMachine.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", "KEMAS");
                k.SetValue("DisplayVersion", "3.3.3");
                k.SetValue("Publisher", "Amzar Syafiq");
                k.SetValue("DisplayIcon", Exe + ",0");
                k.SetValue("InstallLocation", Dir);
                k.SetValue("UninstallString", "\"" + Exe + "\" --uninstall");
                k.SetValue("QuietUninstallString", "\"" + Exe + "\" --uninstall --quiet");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", exe.Length / 1024 + 1, RegistryValueKind.DWord);
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("URLInfoAbout", "https://github.com/AmzSyafxProject/KEMAS");
            }
            p(100, "Done");
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
                st.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { "KEMAS - Your PC, Kemas." });
                st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { target + ",0" });
                st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            }
            catch { }
        }
    }

    class Wizard : Form
    {
        static readonly Color Bg = Color.FromArgb(14, 17, 23), Panel2 = Color.FromArgb(22, 27, 36), Fg = Color.FromArgb(235, 239, 245),
            Dim = Color.FromArgb(139, 148, 163), Accent = Color.FromArgb(52, 211, 153);
        Panel body; Button next, cancel; Label title, sub; ProgressBar bar; Label status;
        CheckBox cDesk, cRun; int step; bool busy; string error;

        public Wizard()
        {
            Text = "KEMAS Setup"; ClientSize = new Size(560, 380); StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; BackColor = Bg; ForeColor = Fg; Font = new Font("Segoe UI", 9.5f);
            try { Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); } catch { }

            var side = new Panel { Left = 0, Top = 0, Width = 170, Height = 380, BackColor = Panel2 };
            Control mark;
            try
            {
                using (var rs = Assembly.GetExecutingAssembly().GetManifestResourceStream("logo.png"))
                    mark = new PictureBox { Image = Image.FromStream(new MemoryStream(ReadAll(rs))), SizeMode = PictureBoxSizeMode.Zoom, Left = 40, Top = 40, Width = 90, Height = 90, BackColor = Color.Transparent };
            }
            catch { mark = new Label { Text = "K", Left = 45, Top = 50, Width = 80, Height = 80 }; }
            var brand = new Label { Text = "KEMAS", Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = Fg, Left = 0, Top = 145, Width = 170, TextAlign = ContentAlignment.MiddleCenter, Height = 32 };
            var tag = new Label { Text = "Your PC, Kemas.", ForeColor = Dim, Left = 0, Top = 176, Width = 170, TextAlign = ContentAlignment.MiddleCenter, Height = 22 };
            var ver = new Label { Text = "v3.3.3 Beta", ForeColor = Dim, Left = 0, Top = 340, Width = 170, TextAlign = ContentAlignment.MiddleCenter, Height = 22 };
            side.Controls.AddRange(new Control[] { mark, brand, tag, ver });
            Controls.Add(side);

            title = new Label { Left = 200, Top = 28, Width = 340, Height = 34, Font = new Font("Segoe UI", 16f, FontStyle.Bold) };
            sub = new Label { Left = 200, Top = 66, Width = 340, Height = 40, ForeColor = Dim };
            body = new Panel { Left = 200, Top = 118, Width = 340, Height = 190, BackColor = Bg };
            next = new Button { Left = 340, Top = 328, Width = 100, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Accent, ForeColor = Bg, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            cancel = new Button { Left = 448, Top = 328, Width = 92, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Panel2, ForeColor = Fg, Text = "Cancel" };
            next.FlatAppearance.BorderSize = 0; cancel.FlatAppearance.BorderSize = 0;
            next.Click += (s, e) => Advance(); cancel.Click += (s, e) => { if (!busy) Close(); };
            Controls.AddRange(new Control[] { title, sub, body, next, cancel });
            FormClosing += (s, e) => { if (busy) e.Cancel = true; };
            Show(0);
        }

        static byte[] ReadAll(Stream st) { var ms = new MemoryStream(); st.CopyTo(ms); return ms.ToArray(); }

        Label L(string t, int top, bool dim = false) { return new Label { Text = t, Left = 0, Top = top, Width = 340, Height = 44, ForeColor = dim ? Dim : Fg }; }

        void Show(int s)
        {
            step = s; body.Controls.Clear(); cancel.Visible = true;
            if (s == 0)
            {
                title.Text = "Welcome to KEMAS"; sub.Text = "This wizard installs KEMAS on your PC.";
                body.Controls.Add(L("KEMAS is a safe, simple PC cleaner. It cleans junk files, frees memory and keeps your desktop untouched.", 0));
                body.Controls.Add(L("This is the public Beta Test. Thank you for being one of the first to try it.", 70, true));
                next.Text = "Next";
            }
            else if (s == 1)
            {
                title.Text = "Choose options"; sub.Text = "KEMAS installs to:\n" + Installer.Dir;
                cDesk = new CheckBox { Text = "Create a desktop shortcut", Checked = true, Left = 0, Top = 6, Width = 330, ForeColor = Fg };
                body.Controls.Add(cDesk);
                body.Controls.Add(L("A Start menu shortcut and an uninstall entry are always added. Administrator permission is required.", 44, true));
                next.Text = "Install";
            }
            else if (s == 2)
            {
                title.Text = "Installing"; sub.Text = "Please wait a moment.";
                bar = new ProgressBar { Left = 0, Top = 30, Width = 330, Height = 14, Style = ProgressBarStyle.Continuous };
                status = new Label { Left = 0, Top = 56, Width = 330, Height = 24, ForeColor = Dim, Text = "Starting" };
                body.Controls.AddRange(new Control[] { bar, status });
                next.Enabled = false; cancel.Visible = false; next.Text = "Install";
                StartInstall();
            }
            else
            {
                cancel.Visible = false; next.Enabled = true; next.Text = "Finish";
                if (error == null)
                {
                    title.Text = "All done"; sub.Text = "KEMAS is installed. Your PC, Kemas.";
                    cRun = new CheckBox { Text = "Open KEMAS now", Checked = true, Left = 0, Top = 6, Width = 330, ForeColor = Fg };
                    body.Controls.Add(cRun);
                    body.Controls.Add(L("To remove KEMAS later, use Settings > Apps > Installed apps.", 44, true));
                }
                else
                {
                    title.Text = "Setup could not finish"; sub.Text = "Nothing was changed on your PC beyond what is listed.";
                    body.Controls.Add(L(error, 0));
                }
            }
        }

        void Advance()
        {
            if (step == 3)
            {
                if (error == null && cRun != null && cRun.Checked)
                    try { Process.Start(new ProcessStartInfo(Installer.Exe) { UseShellExecute = true, WorkingDirectory = Installer.Dir }); } catch { }
                Close(); return;
            }
            Show(step + 1);
        }

        void StartInstall()
        {
            busy = true; bool desk = cDesk != null && cDesk.Checked;
            var th = new Thread(() =>
            {
                try { Installer.Run((pct, msg) => BeginInvoke((Action)(() => { bar.Value = pct; status.Text = msg; })), desk); }
                catch (UnauthorizedAccessException) { error = "KEMAS Setup needs administrator permission. Close this window, right-click KEMAS-Setup.exe and choose Run as administrator, then try again."; }
                catch (Exception ex) { error = ex.Message; }
                Thread.Sleep(350);
                BeginInvoke((Action)(() => { busy = false; Show(3); }));
            });
            th.IsBackground = true; th.Start();
        }
    }
}
