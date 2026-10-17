// KEMAS - entry point: single instance, animated splash, command-line modes.
// Designed & developed by Amzar Syafiq (Ceritareka Studio). Engineered with Claude by Anthropic.
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

[assembly: AssemblyTitle("KEMAS")]
[assembly: AssemblyDescription("KEMAS - Your PC, Kemas. PC cleaner, file organizer and memory booster.")]
[assembly: AssemblyCompany("Amzar Syafiq")]
[assembly: AssemblyProduct("KEMAS")]
[assembly: AssemblyCopyright("(c) 2026 Amzar Syafiq")]
[assembly: AssemblyVersion("3.2.0.0")]
[assembly: AssemblyFileVersion("3.2.0.0")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

namespace Kemas
{
    public static class App
    {
        public static int RefreshRate = 60;
        public static bool Portable;
        public static bool JustUpdated;
        static Mutex mutex;
        static EventWaitHandle showSignal;
        static MainWin main;

        public static void ReleaseMutex()
        {
            try { if (mutex != null) { mutex.ReleaseMutex(); mutex.Dispose(); mutex = null; } } catch { }
        }

        [STAThread]
        public static int Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Error("Unhandled", e.ExceptionObject as Exception);
            Func<string, bool> has = a => args.Any(x => string.Equals(x, a, StringComparison.OrdinalIgnoreCase));
            Portable = has("--portable");

            Settings.Current = Settings.Load();
            L.Set(Settings.Current.Language);

            if (has("--uninstall")) return UninstallMode(has("--quiet"));
            if (has("--autoclean")) return AutoClean.Run();

            bool created;
            mutex = new Mutex(true, "Local\\KEMAS_Instance_v3", out created);
            showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\KEMAS_Show_v3");
            if (!created)
            {
                // already running: bring the existing window forward (unless we are a fresh update restarting)
                if (!has("--updated")) { showSignal.Set(); return 0; }
                for (int i = 0; i < 40 && !created; i++) { Thread.Sleep(150); try { created = mutex.WaitOne(0); } catch (AbandonedMutexException) { created = true; } }
            }

            Installer.CleanupOldFiles();
            bool tray = has("--tray");

            // match the screen's refresh rate (60 / 120 / 144 / 165 / 240 Hz) for every animation
            RefreshRate = Display.RefreshRate();
            Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata { DefaultValue = RefreshRate });

            Splash splash = tray ? null : Splash.ShowAsync();

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.DispatcherUnhandledException += (s, e) =>
            {
                Log.Error("UI", e.Exception);
                e.Handled = true;
            };

            main = new MainWin();
            try { main.Build(); }
            catch (Exception ex)
            {
                Log.Error("Build", ex);
                if (splash != null) splash.Close();
                MessageBox.Show("KEMAS could not start:\n\n" + ex.Message + "\n\nDetails: " + AppInfo.ErrorLog, "KEMAS", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }

            if (has("--updated")) { Settings.Current.InstallOffered = true; JustUpdated = true; }
            main.W.ContentRendered += (s, e) => { if (splash != null) splash.Close(); };

            var listener = new Thread(() =>
            {
                while (true)
                {
                    try { showSignal.WaitOne(); } catch { return; }
                    app.Dispatcher.BeginInvoke(new Action(() => main.ShowFromTray()));
                }
            }) { IsBackground = true };
            listener.Start();

            if (tray)
            {
                // started with Windows: stay in the tray, build the window only when needed
                app.Dispatcher.BeginInvoke(new Action(Native.TrimSelf), DispatcherPriority.ApplicationIdle);
            }
            else
            {
                if (splash != null) splash.WaitShown(1200);
                main.W.Show();
                main.W.Activate();
            }
            app.Run();
            ReleaseMutex();
            return 0;
        }

        static int UninstallMode(bool quiet)
        {
            bool removeData = false;
            if (!quiet)
            {
                var r = MessageBox.Show(L.T("uninstall_mode_q"), "KEMAS", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (r == MessageBoxResult.Cancel) return 1;
                removeData = r == MessageBoxResult.Yes;
            }
            try
            {
                foreach (var p in System.Diagnostics.Process.GetProcessesByName("KEMAS"))
                    try { if (p.Id != System.Diagnostics.Process.GetCurrentProcess().Id) p.Kill(); } catch { }
            }
            catch { }
            Installer.Uninstall(removeData);
            if (!quiet) MessageBox.Show(L.T("uninstalled"), "KEMAS", MessageBoxButton.OK, MessageBoxImage.Information);
            return 0;
        }
    }

    // ===================================================================== splash (own UI thread, so it animates while the main window loads)
    public class Splash
    {
        Window w;
        Dispatcher disp;
        readonly ManualResetEventSlim shown = new ManualResetEventSlim(false);

        public static Splash ShowAsync()
        {
            var s = new Splash();
            var t = new Thread(s.ThreadMain) { IsBackground = true, Name = "Splash" };
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            return s;
        }

        public void WaitShown(int ms) { shown.Wait(ms); }

        void ThreadMain()
        {
            try
            {
                disp = Dispatcher.CurrentDispatcher;
                bool dark = Settings.Current.Theme != "light" && (Settings.Current.Theme != "system" || Theme.SystemPrefersDark());
                w = new Window
                {
                    Width = 340, Height = 340, WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent,
                    ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true, WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                var root = new Grid();
                var card = new Border
                {
                    CornerRadius = new CornerRadius(40), Margin = new Thickness(20),
                    Background = new SolidColorBrush(dark ? Color.FromArgb(0xF2, 0x10, 0x12, 0x1C) : Color.FromArgb(0xF7, 0xFF, 0xFF, 0xFF)),
                    BorderBrush = new SolidColorBrush(dark ? Color.FromArgb(0x26, 255, 255, 255) : Color.FromArgb(0x1A, 0x1B, 0x21, 0x40)), BorderThickness = new Thickness(1)
                };
                root.Children.Add(card);
                var glow = new Ellipse { Width = 210, Height = 210, Fill = new RadialGradientBrush(Color.FromArgb(0x66, 0x7B, 0x5C, 0xFF), Color.FromArgb(0, 0x7B, 0x5C, 0xFF)), Margin = new Thickness(0, 0, 0, 40) };
                root.Children.Add(glow);

                // spinning gradient arc around the logo
                var arc = new Path
                {
                    Width = 168, Height = 168, Stretch = Stretch.None, StrokeThickness = 4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                    Data = Geometry.Parse("M 84,4 A 80,80 0 0 1 164,84"), Margin = new Thickness(0, 0, 0, 40), RenderTransformOrigin = new Point(0.5, 0.5),
                    Stroke = new LinearGradientBrush(Color.FromRgb(0x7B, 0x5C, 0xFF), Color.FromRgb(0x2F, 0xD4, 0xFF), 45)
                };
                var rot = new RotateTransform(); arc.RenderTransform = rot;
                root.Children.Add(arc);
                var arc2 = new Path
                {
                    Width = 168, Height = 168, Stretch = Stretch.None, StrokeThickness = 4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                    Data = Geometry.Parse("M 84,164 A 80,80 0 0 1 4,84"), Margin = new Thickness(0, 0, 0, 40), RenderTransformOrigin = new Point(0.5, 0.5), Opacity = 0.35,
                    Stroke = new SolidColorBrush(Color.FromRgb(0x2F, 0xD4, 0xFF)), RenderTransform = rot
                };
                root.Children.Add(arc2);

                var logo = new Image { Source = Logo.Image(), Width = 112, Height = 112, Margin = new Thickness(0, 0, 0, 40), RenderTransformOrigin = new Point(0.5, 0.5) };
                var sc = new ScaleTransform(0.6, 0.6); logo.RenderTransform = sc;
                root.Children.Add(logo);

                var txt = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 52), Opacity = 0 };
                txt.Children.Add(new TextBlock { Text = "KEMAS", FontSize = 26, FontWeight = FontWeights.Black, HorizontalAlignment = HorizontalAlignment.Center, FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
                    Foreground = new SolidColorBrush(dark ? Color.FromRgb(0xEE, 0xF0, 0xF7) : Color.FromRgb(0x14, 0x17, 0x26)) });
                txt.Children.Add(new TextBlock { Text = L.T("tagline"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0), FontFamily = new FontFamily("Segoe UI"),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x8C, 0x93, 0xAB)) });
                root.Children.Add(txt);
                w.Content = root;

                w.Loaded += (s, e) =>
                {
                    var ease = new BackEase { Amplitude = 0.45, EasingMode = EasingMode.EaseOut };
                    sc.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.6, 1, TimeSpan.FromMilliseconds(650)) { EasingFunction = ease });
                    sc.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.6, 1, TimeSpan.FromMilliseconds(650)) { EasingFunction = ease });
                    rot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.1)) { RepeatBehavior = RepeatBehavior.Forever });
                    glow.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.55, 1, TimeSpan.FromSeconds(0.9)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
                    txt.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500)) { BeginTime = TimeSpan.FromMilliseconds(250) });
                    w.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
                };
                w.ContentRendered += (s, e) => shown.Set();
                w.Show();
                Dispatcher.Run();
            }
            catch (Exception ex) { Log.Error("Splash", ex); shown.Set(); }
        }

        public void Close()
        {
            if (disp == null) return;
            disp.BeginInvoke(new Action(() =>
            {
                try
                {
                    var a = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
                    a.Completed += (s, e) => { w.Close(); disp.InvokeShutdown(); };
                    w.BeginAnimation(UIElement.OpacityProperty, a);
                }
                catch { disp.InvokeShutdown(); }
            }));
        }
    }

    // ===================================================================== scheduled auto clean (runs without a window)
    public static class AutoClean
    {
        public static int Run()
        {
            long freed = 0;
            try
            {
                var s = Settings.Current;
                var c = new Cleaner { SkipRecentTemp = s.SkipRecentTemp, RemoveLockedAtRestart = s.RemoveLockedAtRestart };
                var sel = s.CleanSelection;
                foreach (var t in Cleaner.CreateTargets())
                {
                    bool on = sel != null ? sel.Contains(t.Key) : t.Default;
                    if (!on || t.Key == "winupdate" || Cleaner.IsDeep(t)) continue;   // never touch update files or deep items unattended
                    try { freed += c.Clean(t).Freed; } catch (Exception ex) { Log.Error("AutoClean " + t.Key, ex); }
                }
                s.LastClean = DateTime.Now.ToString("dd MMM, HH:mm", L.Culture);
                s.LastFreed = freed; s.TotalFreed += freed; s.CleanCount++;
                s.Save();
                using (var tray = new System.Windows.Forms.NotifyIcon())
                {
                    using (var st = Assembly.GetExecutingAssembly().GetManifestResourceStream("kemas.ico")) tray.Icon = new System.Drawing.Icon(st, 32, 32);
                    tray.Visible = true;
                    tray.ShowBalloonTip(5000, "KEMAS", string.Format(L.T("autoclean_done"), Fmt.Size(freed)), System.Windows.Forms.ToolTipIcon.Info);
                    var end = DateTime.Now.AddSeconds(6);
                    while (DateTime.Now < end) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(50); }
                    tray.Visible = false;
                }
            }
            catch (Exception ex) { Log.Error("AutoClean", ex); return 1; }
            return 0;
        }
    }
}
