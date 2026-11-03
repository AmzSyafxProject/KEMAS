// KEMAS - main window logic
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;

namespace Kemas
{
    // ---------------------------------------------------------------- view models
    public abstract class Notify : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void Raise(string p) { var h = PropertyChanged; if (h != null) h(this, new PropertyChangedEventArgs(p)); }
        public static Action Changed;
        protected void Save() { if (Changed != null) Changed(); }
    }

    public class FolderVM : Notify
    {
        public OrgFolder M;
        public FolderVM(OrgFolder m) { M = m; Group = "g" + Guid.NewGuid().ToString("N"); }
        public string Group { get; private set; }
        public string Name { get { return M.Name; } }
        public string Path { get { return M.Path; } }
        public bool Enabled { get { return M.Enabled; } set { M.Enabled = value; Raise("Enabled"); Save(); } }
        public bool ByType { get { return !M.ByDate; } set { if (value && M.ByDate) { M.ByDate = false; Raise("ByDate"); Raise("ByType"); Save(); } } }
        public bool ByDate { get { return M.ByDate; } set { if (value && !M.ByDate) { M.ByDate = true; Raise("ByDate"); Raise("ByType"); Save(); } } }
    }

    public class RuleVM : Notify
    {
        public OrgRule M;
        public RuleVM(OrgRule m) { M = m; }
        public string Name { get { return M.Name; } set { M.Name = value; Raise("Name"); Save(); } }
        public string Extensions { get { return M.Extensions; } set { M.Extensions = value; Raise("Extensions"); Save(); } }
    }

    // ---------------------------------------------------------------- window
    public partial class MainWin
    {
        public Window W;
        Settings S { get { return Settings.Current; } }
        readonly Dictionary<string, object> el = new Dictionary<string, object>();
        T E<T>(string name) where T : class
        {
            object o;
            if (!el.TryGetValue(name, out o)) { o = W.FindName(name); el[name] = o; }
            return o as T;
        }
        TextBlock TB(string n) { return E<TextBlock>(n); }
        Button BT(string n) { return E<Button>(n); }

        List<CleanTarget> targets;
        readonly Dictionary<string, CheckBox> targetChecks = new Dictionary<string, CheckBox>();
        readonly Dictionary<string, TextBlock> targetSizes = new Dictionary<string, TextBlock>();
        ObservableCollection<FolderVM> folders;
        ObservableCollection<RuleVM> rules;
        List<PlanItem> plan;
        List<AppRow> allApps;
        bool appsLoaded, busy, startupLoaded, scanned;
        long junkTotal = -1;
        int diskPct, ramPct;
        double ringCur, ringTarget;
        Storyboard aurora;
        DispatcherTimer statTimer, saveTimer;
        UpdateInfo pendingUpdate;
        public bool StartHidden;
        public bool QuitRequested;
        System.Windows.Forms.NotifyIcon tray;
        Cleaner activeCleaner;
        volatile bool cancelFlag;

        public void Build()
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Main.xaml"))
                W = (Window)XamlReader.Load(s);

            Theme.Apply(W.Resources, S.Theme);
            L.Apply(W.Resources, S.Language);

            var chrome = new WindowChrome { CaptionHeight = 64, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0, 0, 0, 1), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false };
            WindowChrome.SetWindowChrome(W, chrome);
            foreach (var n in new[] { "BtnHelp", "BtnMin", "BtnMax", "BtnClose", "Brand" })
                WindowChrome.SetIsHitTestVisibleInChrome(E<IInputElement>(n), n != "Brand");

            E<Image>("SideLogo").Source = Logo.Image();
            E<Image>("AboutLogo").Source = Logo.Image();

            BuildCleanerRows();
            BuildOrganizer();
            WireEvents();
            LoadSettingsIntoUi();
            RefreshTexts();
            SetRing(0.5);
            E<Canvas>("Aurora").Visibility = Visibility.Collapsed;   // 4.0: flat background, no animated glow (saves GPU every frame)

            W.SourceInitialized += (s, e) => Win11Corners();
            W.ContentRendered += OnFirstRender;
            W.StateChanged += (s, e) =>
            {
                BT("BtnMax").Content = W.WindowState == WindowState.Maximized ? "" : "";
                E<Grid>("Root").Margin = W.WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
                if (W.WindowState == WindowState.Minimized) TrimSoon();
                PauseAnimations(W.WindowState == WindowState.Minimized);
            };
            W.Closing += OnClosing;
            // "System" theme follows Windows live: switching Windows to light or dark switches KEMAS too, overlays included
            Microsoft.Win32.UserPreferenceChangedEventHandler themeHook = (s, e) =>
            {
                if (e.Category != Microsoft.Win32.UserPreferenceCategory.General) return;
                W.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (S.Theme != "system" || Theme.SystemPrefersDark() == Theme.IsDark) return;
                    ApplyThemeAndLanguage();
                    try { int dark = Theme.IsDark ? 1 : 0; DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(W).Handle, 20, ref dark, 4); } catch { }
                }));
            };
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += themeHook;
            W.Closed += (s, e) => Microsoft.Win32.SystemEvents.UserPreferenceChanged -= themeHook;
            Notify.Changed = QueueSave;
            SetupTray();
        }

        // ================================================================ window chrome / perf
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        void Win11Corners()
        {
            try
            {
                var h = new WindowInteropHelper(W).Handle;
                int round = 2; DwmSetWindowAttribute(h, 33, ref round, 4);            // rounded corners (Windows 11)
                int dark = Theme.IsDark ? 1 : 0; DwmSetWindowAttribute(h, 20, ref dark, 4);
            }
            catch { }
        }

        void SetupAurora()
        {
            aurora = new Storyboard { RepeatBehavior = RepeatBehavior.Forever, AutoReverse = true };
            Action<string, string, double, double> add = (name, prop, to, secs) =>
            {
                var a = new DoubleAnimation { To = to, Duration = TimeSpan.FromSeconds(secs), EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
                Storyboard.SetTarget(a, E<UIElement>(name));
                Storyboard.SetTargetProperty(a, new PropertyPath("(0).(1)", UIElement.RenderTransformProperty, prop == "X" ? TranslateTransform.XProperty : TranslateTransform.YProperty));
                aurora.Children.Add(a);
            };
            add("AuroraA", "X", -160, 16); add("AuroraA", "Y", 60, 16);
            add("AuroraB", "X", -220, 16); add("AuroraB", "Y", -90, 16);
            add("AuroraC", "X", 200, 16);
            // not started: the aurora layers are fully transparent in both themes, so it only cost frames


            // the About logo floats only while About is on screen (see AboutBob)
        }

        bool smoothScrolling;
        /// Eased scroll (macOS feel). Jumps straight there when motion is reduced.
        void SmoothScroll(ScrollViewer sv, double to)
        {
            if (S.ReducedMotion) { sv.ScrollToVerticalOffset(to); return; }
            double from = sv.VerticalOffset; var watch = Stopwatch.StartNew(); const double ms = 320;
            EventHandler step = null;
            smoothScrolling = true;
            step = (s, e) =>
            {
                double t = Math.Min(1, watch.ElapsedMilliseconds / ms), k = 1 - Math.Pow(1 - t, 3);
                sv.ScrollToVerticalOffset(from + (to - from) * k);
                if (t >= 1) { CompositionTarget.Rendering -= step; smoothScrolling = false; }
            };
            CompositionTarget.Rendering += step;
        }

        void AboutBob(bool on)
        {
            var t = (TranslateTransform)E<Image>("AboutLogo").RenderTransform;
            if (!on || S.ReducedMotion) { t.BeginAnimation(TranslateTransform.YProperty, null); t.Y = 0; return; }
            t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -7, TimeSpan.FromSeconds(2.2)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        }

        void PauseAnimations(bool pause)
        {
            if (aurora == null || S.ReducedMotion) return;
            try { if (pause) aurora.Pause(W); else aurora.Resume(W); } catch { }
        }

        void ApplyMotion()
        {
            var st = (ScaleTransform)BT("BtnSmart").RenderTransform;
            var logoT = (TranslateTransform)E<Image>("AboutLogo").RenderTransform;
            if (S.ReducedMotion)
            {
                try { aurora.Stop(W); } catch { }
                st.BeginAnimation(ScaleTransform.ScaleXProperty, null); st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                logoT.BeginAnimation(TranslateTransform.YProperty, null);
            }
            else
            {
                AboutBob(currentPage == "PageAbout");
            }
        }

        void TrimSoon()
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            t.Tick += (s, e) => { t.Stop(); if (W.WindowState == WindowState.Minimized || !W.IsVisible) Native.TrimSelf(); };
            t.Start();
        }

        static DoubleAnimation Anim(double from, double to, double ms, IEasingFunction ease = null)
        {
            return new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease ?? new CubicEase { EasingMode = EasingMode.EaseOut } };
        }

        // ================================================================ first render
        void OnFirstRender(object sender, EventArgs e)
        {
            W.ContentRendered -= OnFirstRender;
            PerfLog.Mark("startup: first frame", PerfLog.Now);
            ShowIntro();
            UpdateStats();
            E<TextBox>("LogBox").Clear();
            statTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            statTimer.Tick += (s, a) => { if (W.IsVisible && W.WindowState != WindowState.Minimized) UpdateStats(); };
            statTimer.Start();
            bool welcome = CheckWhatsNew();
            if (welcome) { junkTotal = 0; TB("JunkText").Text = "-"; TB("JunkSub").Text = L.T("not_scanned"); }   // the first scan starts when the tour ends
            else
            {
                bool cached = ApplyScanCache();
                if (S.ScanOnStart) Delay(cached ? 2500 : 900, () => { if (!busy) StartScan(); });    // let the window settle first; cached numbers show meanwhile
                else if (!cached) { junkTotal = 0; TB("JunkText").Text = "-"; TB("JunkSub").Text = L.T("not_scanned"); }
            }
            RemoveLegacyAnimation();
            StartBetaCountdown();
            if (S.AutoUpdate && Updater.Configured) Delay(4000, () => CheckUpdate(false));
            if (!welcome) Delay(1800, OfferInstall);
            Delay(6000, Native.TrimSelf);
        }

        void Delay(int ms, Action a)
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
            t.Tick += (s, e) => { t.Stop(); try { a(); } catch (Exception ex) { global::Kemas.Log.Error("Delay", ex); } };
            t.Start();
        }

        void OnClosing(object sender, CancelEventArgs e)
        {
            if (!QuitRequested && S.CloseToTray && tray != null)
            {
                e.Cancel = true;
                W.Hide();
                if (!trayHinted) { trayHinted = true; tray.ShowBalloonTip(3000, "KEMAS", L.T("tray_hint"), System.Windows.Forms.ToolTipIcon.Info); }
                TrimSoon();
                return;
            }
            cancelFlag = true;
            if (activeCleaner != null) activeCleaner.Cancel = true;
            SaveNow();
            if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
            Application.Current.Shutdown();
        }
        bool trayHinted;

        // ================================================================ tray
        void SetupTray()
        {
            try
            {
                tray = new System.Windows.Forms.NotifyIcon();
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("kemas.ico")) tray.Icon = new System.Drawing.Icon(s, 32, 32);
                tray.Text = "KEMAS";
                var menu = new System.Windows.Forms.ContextMenuStrip();
                menu.Items.Add(L.T("tray_open"), null, (s, e) => ShowFromTray());
                menu.Items.Add(L.T("smart"), null, (s, e) => { ShowFromTray(); RequestClean(true); });
                menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
                menu.Items.Add(L.T("tray_exit"), null, (s, e) => { QuitRequested = true; W.Close(); });
                tray.ContextMenuStrip = menu;
                tray.DoubleClick += (s, e) => ShowFromTray();
                tray.Visible = true;
            }
            catch (Exception ex) { global::Kemas.Log.Error("Tray", ex); tray = null; }
        }

        public void ShowFromTray()
        {
            W.Show();
            if (W.WindowState == WindowState.Minimized) W.WindowState = WindowState.Normal;
            W.Activate(); W.Topmost = true; W.Topmost = false;
        }

        // ================================================================ events
        void WireEvents()
        {
            try
            {
                System.Windows.Automation.AutomationProperties.SetName(BT("BtnHelp"), "Help");
                System.Windows.Automation.AutomationProperties.SetName(BT("BtnMin"), "Minimize");
                System.Windows.Automation.AutomationProperties.SetName(BT("BtnMax"), "Maximize or restore");
                System.Windows.Automation.AutomationProperties.SetName(BT("BtnClose"), "Close");
            }
            catch { }
            BT("BtnMin").Click += (s, e) => W.WindowState = WindowState.Minimized;
            BT("BtnMax").Click += (s, e) => W.WindowState = W.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            BT("BtnClose").Click += (s, e) => W.Close();

            SetupModules();
            BuildTopBar();
            foreach (var n in new[] { "NavDash", "NavClean", "NavOrg", "NavRam", "NavStartup", "NavApps", "NavDoctor", "NavMap", "NavRecover", "NavSettings", "NavAbout" })
            {
                var name = n;
                E<RadioButton>(n).Checked += (s, e) => ShowPage(name);
            }

            BT("BtnSmart").Click += (s, e) => RequestClean(true);
            BT("BtnDashScan").Click += (s, e) => StartScan(true);
            BT("QaClean").Click += (s, e) => E<RadioButton>("NavClean").IsChecked = true;
            BT("QaOrg").Click += (s, e) => E<RadioButton>("NavMap").IsChecked = true;
            E<FrameworkElement>("DashRoot").SizeChanged += (s2, e2) => LayoutDash(e2.NewSize.Width);
            var setPairs = new[] { "SetCatApp:SecApp", "SetCatGen:SecGen", "SetCatClean:SecClean", "SetCatUpd:SecUpd", "SetCatHelp:SecHelp", "SetCatData:SecData" };
            bool spying = false;
            foreach (var pr in setPairs)
            {
                var a = pr.Split(':');
                var rb = E<RadioButton>(a[0]);
                var tag = (rb.Tag as string ?? "E713|#3478F6").Split('|');
                Border tb; TextBlock ti;
                rb.Tag = NavTile(tag[0], tag[1], 28, 14, out tb, out ti);       // coloured KEMAS icon tile
                rb.Checked += (s, e) =>
                {
                    if (spying) return;
                    var target = E<FrameworkElement>(a[1]); var sv = E<ScrollViewer>("SetScroll");
                    var pt = target.TransformToAncestor((Visual)sv.Content).Transform(new Point(0, 0));
                    SmoothScroll(sv, Math.Min(pt.Y, sv.ScrollableHeight));
                };
            }
            // scroll-spy: the sidebar follows the section you are reading
            E<ScrollViewer>("SetScroll").ScrollChanged += (s, e) =>
            {
                if (smoothScrolling) return;
                var sv = (ScrollViewer)s; string pick = null;
                foreach (var pr in setPairs)
                {
                    var a = pr.Split(':');
                    var y = E<FrameworkElement>(a[1]).TransformToAncestor((Visual)sv.Content).Transform(new Point(0, 0)).Y;
                    if (y <= sv.VerticalOffset + 60) pick = a[0];
                }
                if (sv.VerticalOffset >= sv.ScrollableHeight - 2) pick = setPairs[setPairs.Length - 1].Split(':')[0];
                if (pick != null && E<RadioButton>(pick).IsChecked != true) { spying = true; E<RadioButton>(pick).IsChecked = true; spying = false; }
            };
            BT("BtnAssist").Click += (s, e) => E<RadioButton>("NavDoctor").IsChecked = true;
            BT("BtnOvMap").Click += (s, e) => E<RadioButton>("NavMap").IsChecked = true;
            E<Border>("SideStatus").MouseLeftButtonUp += (s, e) => E<RadioButton>("NavDoctor").IsChecked = true;
            var sb = E<TextBox>("SearchBox");
            sb.TextChanged += (s, e) => TB("SearchHint").Visibility = sb.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            BT("QaRam").Click += (s, e) => E<RadioButton>("NavRam").IsChecked = true;
            BT("QaStartup").Click += (s, e) => E<RadioButton>("NavStartup").IsChecked = true;
            BT("BtnTheme").Click += (s, e) =>
            {
                S.Theme = Theme.IsDark ? "light" : "dark";
                ApplyThemeAndLanguage(); QueueSave();
                loadingUi = true; E<RadioButton>(S.Theme == "light" ? "ThemeLight" : "ThemeDark").IsChecked = true; loadingUi = false;
            };
            TB("SideVersion").Text = "Version " + AppInfo.Version;
            BT("BtnDocCheck").Click += (s, e) => RunDoctor();
            BT("BtnMapScan").Click += (s, e) => ScanMap();
            E<Canvas>("MapCanvas").SizeChanged += (s, e) => DrawMap();
            try { var dv = new DriveInfo(System.IO.Path.GetPathRoot(Environment.SystemDirectory)); StorageHistory.Record(dv.TotalSize - dv.AvailableFreeSpace); } catch { }
            BT("BtnViewAll").Click += (s, e) => E<RadioButton>("NavDoctor").IsChecked = true;
            RefreshHistory();
            UpdateCrumb();
            BT("BtnScan").Click += (s, e) => StartScan(true);
            BT("BtnClean").Click += (s, e) => RequestClean(false);
            BT("BtnDeep").Click += (s, e) => RequestDeepClean();
            BT("BtnHelp").Click += (s, e) => ShowSupport();
            BT("BtnWNext").Click += (s, e) => WelcomeNext();
            BT("BtnWBack").Click += (s, e) => { if (welcomeStep > 0) ShowWelcomeStep(welcomeStep - 1, -1); };
            BT("BtnWSkip").Click += (s, e) => FinishWelcome(false);
            Action<string, Action> wradio = (name, act) => E<RadioButton>(name).Checked += (s, e) => { if (!loadingUi) { act(); ApplyThemeAndLanguage(); WelcomeTexts(); QueueSave(); } };
            wradio("WLangEn", () => S.Language = "en");
            wradio("WLangMs", () => S.Language = "ms");
            wradio("WThemeDark", () => S.Theme = "dark");
            wradio("WThemeLight", () => S.Theme = "light");
            wradio("WThemeSystem", () => S.Theme = "system");
            BT("BtnReport").Click += (s, e) => ShowSupport();
            BT("BtnAboutReport").Click += (s, e) => ShowSupport();
            BT("BtnSup5").Click += (s, e) => OpenSupportLink("https://checkout.hit-pay.com/payment-request/@nuraqsa-network/a316b34d-c051-432c-b5bb-fa07743847a9/checkout");
            BT("BtnSup10").Click += (s, e) => OpenSupportLink("https://checkout.hit-pay.com/payment-request/@nuraqsa-network/a316b395-d1db-47fe-bc2b-016ca6279e68/checkout");
            BT("BtnSup15").Click += (s, e) => OpenSupportLink("https://checkout.hit-pay.com/payment-request/@nuraqsa-network/a316b3bd-40e0-49b2-ae72-a852dda4b2f4/checkout");
            BT("BtnSupportClose").Click += (s, e) => CloseSupport();
            BT("BtnRepMail").Click += (s, e) => SendReport(false);
            BT("BtnRepGmail").Click += (s, e) => SendReport(true);
            BT("BtnRepCopy").Click += (s, e) => CopyReport();
            BT("BtnRepForm").Click += (s, e) => { if (!Support.Open(AppInfo.FeedbackUrl)) Toast(L.T("help_no_mail"), "E7BA"); };
            E<TextBox>("RepMessage").TextChanged += (s, e) => TB("RepHint").Visibility = E<TextBox>("RepMessage").Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
            BT("BtnWnClose").Click += (s, e) => CloseWhatsNew(false);
            BT("BtnWnTry").Click += (s, e) => CloseWhatsNew(true);
            BT("BtnSelAll").Click += (s, e) =>
            {
                // standard items only - deep items are picked one by one or through Deep Clean
                var std = targets.Where(t => !Cleaner.IsDeep(t)).Select(t => targetChecks[t.Key]).ToList();
                bool all = std.All(c => c.IsChecked == true);
                foreach (var c in std) c.IsChecked = !all;
            };

            BT("BtnPreview").Click += (s, e) => StartPreview(null);
            BT("BtnOrganize").Click += (s, e) => RequestOrganize();
            BT("BtnUndo").Click += (s, e) => RequestUndo();
            E<RadioButton>("OrgTabFolders").Checked += (s, e) => OrgTab(0);
            E<RadioButton>("OrgTabRules").Checked += (s, e) => OrgTab(1);
            E<RadioButton>("OrgTabPreview").Checked += (s, e) => OrgTab(2);
            BT("BtnAddFolder").Click += (s, e) => AddFolder();
            BT("BtnAddRule").Click += (s, e) => { var r = new OrgRule(L.T("new_folder"), ""); S.OrgRules.Add(r); rules.Add(new RuleVM(r)); QueueSave(); };
            BT("BtnResetRules").Click += (s, e) => Ask(L.T("reset_rules_q"), L.T("reset_rules_d"), L.T("reset"), "E72C", false, () =>
            {
                S.OrgRules = Settings.DefaultRules(); S.OrgOthersName = "Others"; BuildOrganizer(); QueueSave(); Toast(L.T("rules_reset"), "E73E");
            });
            BT("BtnExport").Click += (s, e) => ExportRules();
            BT("BtnImport").Click += (s, e) => ImportRules();
            E<TextBox>("OthersName").TextChanged += (s, e) => { if (loadingUi) return; S.OrgOthersName = E<TextBox>("OthersName").Text; QueueSave(); };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                E<RadioButton>("DateFmt" + i).Checked += (s, e) => { if (loadingUi) return; S.OrgDateFormat = k; QueueSave(); };
            }
            // buttons / checkboxes inside templates
            E<ItemsControl>("OrgFolderList").AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnTemplateClick));
            E<ItemsControl>("OrgRuleList").AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnTemplateClick));
            E<ItemsControl>("StartupList").AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnTemplateClick));

            BT("BtnRamRefresh").Click += (s, e) => LoadRam();
            BT("BtnTrim").Click += (s, e) => StartTrim();
            BT("BtnKill").Click += (s, e) => RequestKill();
            BT("BtnStartupRefresh").Click += (s, e) => LoadStartup();
            BT("BtnAppsRefresh").Click += (s, e) => LoadApps();
            BT("BtnUninstall").Click += (s, e) => RequestUninstallApp();
            E<TextBox>("AppSearch").TextChanged += (s, e) =>
            {
                E<TextBlock>("AppSearchHint").Visibility = E<TextBox>("AppSearch").Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
                FilterApps();
            };

            BT("BtnStop").Click += (s, e) => { cancelFlag = true; if (activeCleaner != null) activeCleaner.Cancel = true; BT("BtnStop").IsEnabled = false; BT("BtnStop").Content = L.T("stopping"); };
            BT("BtnFinish").Click += (s, e) => { if (engFinish != null) engFinish(); else HideOverlay(); };
            SetupEngine();
            BT("BtnDialogNo").Click += (s, e) => CloseDialog(false);
            BT("BtnDialogYes").Click += (s, e) => CloseDialog(true);
            E<Border>("UpdateChip").MouseLeftButtonUp += (s, e) => { if (pendingUpdate != null) OfferUpdate(pendingUpdate); };
            BT("BtnCheckUpdate").Click += (s, e) => CheckUpdate(true);
            BT("BtnAboutUpdate").Click += (s, e) => CheckUpdate(true);
            BT("BtnInstall").Click += (s, e) => { if (Installer.IsInstalledCopy) RequestSelfUninstall(); else RequestInstall(); };
            BT("BtnDataFolder").Click += (s, e) => { try { Directory.CreateDirectory(AppInfo.DataDir); Process.Start("explorer.exe", "\"" + AppInfo.DataDir + "\""); } catch { } };
            BT("BtnErrLog").Click += (s, e) =>
            {
                try
                {
                    if (!File.Exists(AppInfo.ErrorLog)) { Directory.CreateDirectory(AppInfo.DataDir); File.WriteAllText(AppInfo.ErrorLog, L.T("no_errors") + Environment.NewLine); }
                    Process.Start("notepad.exe", "\"" + AppInfo.ErrorLog + "\"");
                }
                catch { }
            };
            BT("BtnResetSettings").Click += (s, e) => Ask(L.T("reset_q"), L.T("reset_d"), L.T("reset"), "E72C", true, () =>
            {
                var keepStats = S;
                var fresh = new Settings { TotalFreed = keepStats.TotalFreed, CleanCount = keepStats.CleanCount, LastClean = keepStats.LastClean, LastFreed = keepStats.LastFreed, InstallOffered = true };
                fresh.FillDefaults();
                Settings.Current = fresh;
                Tasks.SetStartup(false); Tasks.SetAutoClean("off");
                ApplyThemeAndLanguage();
                BuildOrganizer();
                LoadSettingsIntoUi();
                foreach (var t in targets) targetChecks[t.Key].IsChecked = t.Default && !Cleaner.StoreLocked(t.Key);
                SaveNow();
                Toast(L.T("settings_reset"), "E73E");
            });

            // settings controls
            Action<string, Action> radio = (name, act) => E<RadioButton>(name).Checked += (s, e) => { if (!loadingUi) act(); };
            radio("ThemeDark", () => { S.Theme = "dark"; ApplyThemeAndLanguage(); QueueSave(); });
            radio("ThemeLight", () => { S.Theme = "light"; ApplyThemeAndLanguage(); QueueSave(); });
            radio("ThemeSystem", () => { S.Theme = "system"; ApplyThemeAndLanguage(); QueueSave(); });
            radio("LangEn", () => { S.Language = "en"; ApplyThemeAndLanguage(); QueueSave(); });
            radio("LangMs", () => { S.Language = "ms"; ApplyThemeAndLanguage(); QueueSave(); });
            radio("AutoOff", () => SetAutoClean("off"));
            radio("AutoDaily", () => SetAutoClean("daily"));
            radio("AutoWeekly", () => SetAutoClean("weekly"));
            Action<string, Action<bool>> sw = (name, act) =>
            {
                var cb = E<CheckBox>(name);
                cb.Click += (s, e) => { if (!loadingUi) act(cb.IsChecked == true); };
            };
            sw("SetMotion", v => { S.ReducedMotion = !v; ApplyMotion(); RefreshTexts(); QueueSave(); });
            sw("SetStartup", v =>
            {
                if (Tasks.SetStartup(v)) { S.StartWithWindows = v; Toast(v ? L.T("startup_on") : L.T("startup_off"), "E73E"); }
                else { E<CheckBox>("SetStartup").IsChecked = !v; Toast(L.T("startup_fail"), "E7BA"); }
                QueueSave();
            });
            sw("SetTray", v => { S.CloseToTray = v; QueueSave(); });
            sw("SetScanStart", v => { S.ScanOnStart = v; QueueSave(); });
            sw("SetConfirm", v => { S.ConfirmClean = v; QueueSave(); });
            sw("SetRecent", v => { S.SkipRecentTemp = v; QueueSave(); });
            sw("SetRestartDel", v => { S.RemoveLockedAtRestart = v; QueueSave(); });
            sw("SetSmartRam", v => { S.TrimRamOnSmart = v; QueueSave(); });
            sw("SetAutoUpdate", v => { S.AutoUpdate = v; QueueSave(); });
            E<TextBox>("SetName").TextChanged += (s, e) =>
            {
                E<TextBlock>("SetNameHint").Visibility = E<TextBox>("SetName").Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
                if (loadingUi) return;
                S.DisplayName = E<TextBox>("SetName").Text.Trim();
                UpdateGreeting();
                QueueSave();
            };

            W.PreviewKeyDown += (s, e) =>
            {
                // Ctrl+1..6 jump between pages, F5 scans again
                bool overlay = E<Grid>("DialogLayer").Visibility == Visibility.Visible || E<Grid>("SupportLayer").Visibility == Visibility.Visible || E<Grid>("WhatsNewLayer").Visibility == Visibility.Visible;
                if (!overlay && !busy && (Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key >= Key.D1 && e.Key <= Key.D6)
                {
                    var navs = new[] { "NavDash", "NavClean", "NavOrg", "NavRam", "NavStartup", "NavDoctor", "NavMap", "NavApps", "NavRecover" };
                    E<RadioButton>(navs[e.Key - Key.D1]).IsChecked = true; e.Handled = true; return;
                }
                if (!overlay && !busy && e.Key == Key.F5) { StartScan(true); e.Handled = true; return; }
                if (e.Key == Key.Escape && E<Grid>("DialogLayer").Visibility == Visibility.Visible) { CloseDialog(false); e.Handled = true; }
                else if (e.Key == Key.Escape && E<Grid>("SupportLayer").Visibility == Visibility.Visible) { CloseSupport(); e.Handled = true; }
                else if (e.Key == Key.Escape && E<Grid>("WhatsNewLayer").Visibility == Visibility.Visible) { CloseWhatsNew(false); e.Handled = true; }
            };
        }

        void SetAutoClean(string mode)
        {
            if (Tasks.SetAutoClean(mode)) { S.AutoClean = mode; Toast(mode == "off" ? L.T("auto_off") : L.T("auto_on"), "E73E"); }
            else Toast(L.T("auto_fail"), "E7BA");
            SaveSelection();
            QueueSave();
        }

        bool loadingUi;
        void LoadSettingsIntoUi()
        {
            loadingUi = true;
            if (AppInfo.IsStore)
            {
                // Microsoft Store build: the Store installs, updates and removes KEMAS
                foreach (var n in new[] { "AutoUpdateRow", "AutoUpdateLine", "InstallRow", "InstallRowLine", "BtnCheckUpdate", "BtnAboutUpdate" })
                    E<FrameworkElement>(n).Visibility = Visibility.Collapsed;
            }
            E<RadioButton>(S.Theme == "light" ? "ThemeLight" : S.Theme == "system" ? "ThemeSystem" : "ThemeDark").IsChecked = true;
            E<RadioButton>(S.Language == "ms" ? "LangMs" : "LangEn").IsChecked = true;
            E<RadioButton>(S.AutoClean == "daily" ? "AutoDaily" : S.AutoClean == "weekly" ? "AutoWeekly" : "AutoOff").IsChecked = true;
            E<CheckBox>("SetMotion").IsChecked = !S.ReducedMotion;
            E<CheckBox>("SetStartup").IsChecked = S.StartWithWindows;
            E<CheckBox>("SetTray").IsChecked = S.CloseToTray;
            E<CheckBox>("SetScanStart").IsChecked = S.ScanOnStart;
            E<CheckBox>("SetConfirm").IsChecked = S.ConfirmClean;
            E<CheckBox>("SetRecent").IsChecked = S.SkipRecentTemp;
            E<CheckBox>("SetRestartDel").IsChecked = S.RemoveLockedAtRestart;
            E<CheckBox>("SetSmartRam").IsChecked = S.TrimRamOnSmart;
            E<CheckBox>("SetAutoUpdate").IsChecked = S.AutoUpdate;
            E<TextBox>("SetName").Text = S.DisplayName ?? "";
            E<TextBlock>("SetNameHint").Text = UserName.Detect();
            E<TextBlock>("SetNameHint").Visibility = string.IsNullOrEmpty(S.DisplayName) ? Visibility.Visible : Visibility.Collapsed;
            E<TextBox>("OthersName").Text = S.OrgOthersName;
            E<RadioButton>("DateFmt" + Math.Max(0, Math.Min(2, S.OrgDateFormat))).IsChecked = true;
            loadingUi = false;
        }

        void ApplyThemeAndLanguage()
        {
            Theme.Apply(W.Resources, S.Theme);
            L.Apply(W.Resources, S.Language);
            Win11Corners();
            RefreshTexts();
            UpdateCrumb();
            try { RefreshHistory(); RefreshModuleTexts(); } catch { }
            RebuildTrayMenu();
        }

        void RebuildTrayMenu()
        {
            if (tray == null) return;
            var m = tray.ContextMenuStrip;
            if (m == null || m.Items.Count < 4) return;
            m.Items[0].Text = L.T("tray_open"); m.Items[1].Text = L.T("smart"); m.Items[3].Text = L.T("tray_exit");
        }

        // texts set from code (re-run on language change)
        void RefreshTexts()
        {
            UpdateGreeting();
            var ci = L.Culture;
            TB("DateText").Text = DateTime.Now.ToString("dddd, d MMMM yyyy", ci) + "  ·  " + L.T("tagline");
            TB("MotionDesc").Text = S.ReducedMotion ? L.T("motion_reduced") : string.Format(L.T("motion_full"), App.RefreshRate);
            TB("VersionLine").Text = string.Format(L.T("version_line"), AppInfo.Version);
            if (TB("UpdateStatus").Text.Length == 0 || !Updater.Configured) TB("UpdateStatus").Text = AppInfo.IsStore ? L.T("update_store") : Updater.Configured ? L.T("update_idle") : L.T("update_not_configured");
            TB("AboutVersion").Text = string.Format(L.T("about_version"), AppInfo.Version, App.RefreshRate);
            TB("CopyrightText").Text = "© 2026 Amzar Syafiq. " + L.T("rights");
            bool installedCopy = Installer.IsInstalledCopy;
            TB("InstallTitle").Text = installedCopy ? L.T("uninstall_app") : L.T("install_app");
            TB("InstallDesc").Text = installedCopy ? L.T("uninstall_app_d") : L.T("install_app_d");
            BT("BtnInstall").Content = installedCopy ? L.T("uninstall") : L.T("install");
            foreach (var t in targets)
            {
                targetChecks[t.Key].Content = (Cleaner.StoreLocked(t.Key) ? "\uD83D\uDD12 " : "") + L.T("t_" + t.Key);
                var row = (FrameworkElement)targetChecks[t.Key].Tag;
                ((TextBlock)row.FindName("desc")).Text = L.T("t_" + t.Key + "_d");
                var tag = (TextBlock)row.FindName("tagtext");
                tag.Text = L.T("tag_" + t.Tag);
                if (!busy) SetTargetSize(t);
            }
            Headers(E<DataGrid>("OrgGrid"), "col_file", "col_from", "col_to", "col_size");
            Headers(E<DataGrid>("ProcGrid"), "col_app", "col_memory", "col_procs", "col_desc", "col_status");
            Headers(E<DataGrid>("AppGrid"), "col_program", "col_publisher", "col_size", "col_version", "col_installed");
            UpdateSelectedTotal();
            UpdateLastClean();
            if (junkTotal >= 0 && scanned) { TB("JunkSub").Text = string.Format(L.T("across"), targets.Count); UpdateHealth(); }
            UpdateStats();
        }

        static void Headers(DataGrid g, params string[] keys)
        {
            for (int i = 0; i < keys.Length && i < g.Columns.Count; i++) g.Columns[i].Header = L.T(keys[i]);
        }

        void UpdateGreeting()
        {
            string name = string.IsNullOrWhiteSpace(S.DisplayName) ? UserName.Detect() : S.DisplayName.Trim();
            int h = DateTime.Now.Hour;
            string part = h < 12 ? L.T("greet_morning") : h < 18 ? L.T("greet_afternoon") : L.T("greet_evening");
            TB("Greeting").Text = part.Replace(" \u2600", "") + ", " + name;
        }

        // ================================================================ save
        void QueueSave()
        {
            if (saveTimer == null)
            {
                saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                saveTimer.Tick += (s, e) => { saveTimer.Stop(); SaveNow(); FlashSaved(); };
            }
            saveTimer.Stop(); saveTimer.Start();
        }

        void SaveNow() { SaveSelection(); S.Save(); }

        void SaveSelection()
        {
            if (targets == null) return;
            S.CleanSelection = targets.Where(t => targetChecks[t.Key].IsChecked == true).Select(t => t.Key).ToList();
        }

        void FlashSaved()
        {
            if (E<Grid>("PageOrg").Visibility != Visibility.Visible) return;
            var sh = E<StackPanel>("SavedHint");
            var a = new DoubleAnimationUsingKeyFrames();
            a.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))));
            a.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1400))));
            a.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1800))));
            sh.BeginAnimation(UIElement.OpacityProperty, a);
        }

        // ================================================================ navigation
        string currentPage = "PageDash";
        void UpdateCrumb()
        {
            var map = new System.Collections.Generic.Dictionary<string, string> { { "PageDash", "nav_dash" }, { "PageClean", "nav_clean" }, { "PageOrg", "nav_org" }, { "PageRam", "nav_ram" }, { "PageStartup", "nav_startup" }, { "PageApps", "nav_apps" }, { "PageDoctor", "nav_doctor" }, { "PageMap", "nav_map" }, { "PageRecover", "nav_recover" }, { "PageSettings", "nav_settings" }, { "PageAbout", "nav_about" } };
            string k; if (map.TryGetValue(currentPage, out k)) TB("Crumb").Text = L.T(k);
            else if (currentPage.StartsWith("Page") && modPages.ContainsKey(currentPage.Substring(4))) TB("Crumb").Text = ModTitle(currentPage.Substring(4));
            if (crumbCat != null)
            {
                var c = CatOf("Nav" + currentPage.Substring(4));
                bool landing = c != null && c.Landing == "Nav" + currentPage.Substring(4);
                crumbCat.Text = c == null || landing ? "KEMAS" : Tx(c.En, c.Ms);
            }
        }
        void ShowPage(string nav)
        {
            string page = "Page" + nav.Substring(3);
            if (page == currentPage) return;
            var pageWatch = Stopwatch.StartNew();
            W.Dispatcher.BeginInvoke(new Action(() => PerfLog.Mark("page " + page, pageWatch.ElapsedMilliseconds)), DispatcherPriority.ContextIdle);
            var old = E<FrameworkElement>(currentPage);
            var nw = E<FrameworkElement>(page);
            old.Visibility = Visibility.Collapsed;
            nw.Visibility = Visibility.Visible;
            currentPage = page;
            UpdateCrumb();
            if (!S.ReducedMotion)
            {
                nw.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, 140));
                var tt = nw.RenderTransform as TranslateTransform;
                if (tt == null) { tt = new TranslateTransform(); nw.RenderTransform = tt; }
                tt.BeginAnimation(TranslateTransform.YProperty, Anim(8, 0, 180, new QuinticEase { EasingMode = EasingMode.EaseOut }));
            }
            if (!busy) TB("StatusText").Text = "";      // an old message from another page must not linger
            AboutBob(page == "PageAbout");
            if (page == "PageRam") LoadRam();
            if (page == "PageApps" && !appsLoaded) LoadApps();
            if (page == "PageRecover") RefreshHistory();
            if (modPages.ContainsKey(page.Substring(4))) ShowModule(page.Substring(4));
            if (page == "PageDoctor") RunDoctor();
            if (page == "PageMap") ShowMapHistory();
            if (page == "PageStartup" && !startupLoaded) LoadStartup();
            OnPageShown();
        }

        void OrgTab(int i)
        {
            var panels = new[] { "OrgFoldersPanel", "OrgRulesPanel", "OrgPreviewPanel" };
            for (int k = 0; k < 3; k++)
            {
                var p = E<FrameworkElement>(panels[k]);
                p.Visibility = k == i ? Visibility.Visible : Visibility.Collapsed;
                if (k == i && !S.ReducedMotion) p.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, 200));
            }
        }

        // ================================================================ log / toast / status
        void Log(string msg)
        {
            var box = E<TextBox>("LogBox");
            if (box.LineCount > 300) box.Clear();
            box.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "]  " + msg + Environment.NewLine);
            box.ScrollToEnd();
            TB("StatusText").Text = msg;
        }

        DispatcherTimer toastTimer;
        void Toast(string msg, string icon = "E946")
        {
            var t = E<Border>("Toast");
            Note(msg, icon);
            TB("ToastText").Text = msg;
            TB("ToastIcon").Text = ((char)Convert.ToInt32(icon, 16)).ToString();
            t.Visibility = Visibility.Visible;
            t.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, 180));
            ((TranslateTransform)t.RenderTransform).BeginAnimation(TranslateTransform.YProperty, Anim(16, 0, 260));
            if (toastTimer == null)
            {
                toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
                toastTimer.Tick += (s, e) =>
                {
                    toastTimer.Stop();
                    var a = Anim(1, 0, 220);
                    a.Completed += (s2, e2) => E<Border>("Toast").Visibility = Visibility.Collapsed;
                    E<Border>("Toast").BeginAnimation(UIElement.OpacityProperty, a);
                };
            }
            toastTimer.Stop(); toastTimer.Start();
        }

        void SetBusy(bool on)
        {
            busy = on;
            foreach (var n in new[] { "BtnDashScan", "BtnSmart", "BtnScan", "BtnClean", "BtnDeep", "BtnSelAll", "BtnPreview", "BtnOrganize", "BtnUndo",
                                      "BtnRamRefresh", "BtnTrim", "BtnKill", "BtnAppsRefresh", "BtnUninstall", "BtnStartupRefresh" })
                BT(n).IsEnabled = !on;
            if (!on) E<ProgressBar>("MainProgress").Value = 0;
            if (junkTotal >= 0) UpdateHealth();
        }

        void Ui(Action a) { W.Dispatcher.BeginInvoke(a); }

        /// Read-only background load (page data, lists, checks). Unlike Run it never blocks other work
        /// and is never refused while a scan or clean is running, so a page can't get stuck on "Loading".
        void Bg(string what, Action work, Action done)
        {
            var sw = Stopwatch.StartNew();
            Task.Factory.StartNew(() =>
            {
                Exception err = null;
                try { work(); } catch (Exception ex) { err = ex; global::Kemas.Log.Error("Bg " + what, ex); }
                Ui(() =>
                {
                    PerfLog.Mark("load " + what, sw.ElapsedMilliseconds);
                    try { done(); } catch (Exception ex) { global::Kemas.Log.Error("BgDone " + what, ex); }
                });
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// Runs work off the UI thread so animations stay smooth.
        void Run(Action work, Action done)
        {
            if (busy) { Toast(L.T("please_wait"), "E916"); return; }
            SetBusy(true);
            cancelFlag = false;
            Task.Factory.StartNew(() =>
            {
                Exception err = null;
                try { work(); } catch (Exception ex) { err = ex; global::Kemas.Log.Error("Task", ex); }
                Ui(() =>
                {
                    SetBusy(false);
                    if (err != null) { Log(L.T("task_error") + " " + err.Message); }
                    try { done(); } catch (Exception ex) { global::Kemas.Log.Error("Done", ex); }
                });
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        // ================================================================ dashboard
        void UpdateStats()
        {
            try
            {
                var d = new DriveInfo(System.IO.Path.GetPathRoot(Environment.SystemDirectory));
                long used = d.TotalSize - d.AvailableFreeSpace;
                diskPct = (int)Math.Round(used * 100.0 / d.TotalSize);
                TB("DiskLabel").Text = "Disk " + d.Name.TrimEnd('\\');
                TB("DiskUsedText").Text = diskPct + "%";
                E<ProgressBar>("DiskBar").Value = diskPct;
                TB("DiskFreeText").Text = Fmt.Size(used) + " / " + Fmt.Size(d.TotalSize);
                diskUsedB = used; diskTotalB = d.TotalSize; DrawOverview();
                E<ProgressBar>("DiskBar").Foreground = diskPct >= 90 ? (Brush)W.FindResource("BadBrush") : (Brush)W.FindResource("AccentGrad");

                double total, free; Memory.Info(out total, out free, out ramPct);
                TB("RamUsedText").Text = ramPct + "%";
                E<ProgressBar>("RamBar").Value = ramPct;
                TB("RamFreeText").Text = Fmt.Size(total - free) + " / " + Fmt.Size(total);
                int cpu = Cpu.Sample();
                if (cpu >= 0) { TB("CpuText").Text = cpu + "%"; Push(cpuHist, cpu); DrawSpark("SparkCpu", cpuHist); }
                TB("CpuSub").Text = string.Format(L.T("cores"), Environment.ProcessorCount);
                if (lastScanAt != DateTime.MinValue) { var ago = DateTime.Now - lastScanAt; TB("LastScanText").Text = string.Format(L.T("last_scan_ago"), ago.TotalMinutes < 1 ? L.T("just_now") : ago.TotalHours < 1 ? string.Format(L.T("min_ago"), (int)ago.TotalMinutes) : string.Format(L.T("hr_ago"), (int)ago.TotalHours)); }
                E<ProgressBar>("RamBar").Foreground = ramPct >= 85 ? (Brush)W.FindResource("WarnBrush") : new SolidColorBrush(Color.FromRgb(0x8B, 0x78, 0xF6));
                Push(diskHist, used / 1073741824.0); Push(ramHist, total - free);
                DrawSpark("SparkDisk", diskHist); DrawSpark("SparkRam", ramHist);
                if (currentPage == "PageRam")
                {
                    TB("RamPageText").Text = string.Format(L.T("in_use_of"), Fmt.Size(total - free), Fmt.Size(total));
                    TB("RamPagePct").Text = ramPct + "%";
                    E<ProgressBar>("RamPageBar").Value = ramPct;
                }
                UpdateHealth();
            }
            catch { }
        }

        // Health score: 100 minus whole-point penalties. Each penalty is one step on the dashboard's "Road to 100".
        const long JunkGrace = 32L * 1024 * 1024;     // a little leftover junk (files in use) never costs points
        int penDisk, penRam, penJunkStd, penJunkDeep;
        long junkStd, junkDeep;

        void ComputePenalties()
        {
            junkStd = 0; junkDeep = 0;
            if (targets != null && scanned)
                foreach (var t in targets)
                {
                    if (t.Key == "winupdate" || t.Size <= 0) continue;            // optional item, never counted against the score
                    if (Cleaner.IsDeep(t)) junkDeep += t.Size; else junkStd += t.Size;
                }
            double junkPen = Math.Min(35, Math.Max(0, junkStd + junkDeep - JunkGrace) / 1073741824.0 * 8);
            double share = junkStd + junkDeep > 0 ? (double)junkStd / (junkStd + junkDeep) : 0;
            penJunkStd = (int)Math.Round(junkPen * share);
            penJunkDeep = (int)Math.Round(junkPen) - penJunkStd;
            penDisk = diskPct > 70 ? (int)Math.Round((diskPct - 70) * 1.2) : 0;
            penRam = ramPct > 70 ? (int)Math.Round((ramPct - 70) * 0.5) : 0;
        }

        void UpdateHealth()
        {
            if (junkTotal < 0) return;
            ComputePenalties();
            double score = Math.Max(5, Math.Min(100, 100 - penDisk - penRam - penJunkStd - penJunkDeep));
            AnimateRing(score);
            UpdateRoad((int)score);
            TB("ScoreTitle").Text = score >= 85 ? L.T("score_great") : score >= 60 ? L.T("score_ok") : L.T("score_bad");
            TB("ScoreDesc").Text = string.Format(L.T("score_desc"), Fmt.Size(junkTotal), diskPct, ramPct);
            string pk = score >= 85 ? "Good" : score >= 60 ? "Warn" : "Bad";
            TB("ScorePillText").Text = L.T(score >= 85 ? "pill_great" : score >= 60 ? "pill_ok" : "pill_bad");
            var pc = (SolidColorBrush)W.FindResource(pk + "Brush");
            TB("ScorePillText").Foreground = pc; TB("ScoreIcon").Foreground = pc;
            TB("ScoreIcon").Text = score >= 85 ? "\uE73E" : "\uE7BA";
            TB("SideStatusText").Text = L.T(score >= 85 ? "side_great" : "side_ok");
            TB("SideStatusSub").Text = L.T(score >= 85 ? "side_sub" : "side_sub2");
            TB("AssistText").Text = penJunkStd + penJunkDeep > 0 && junkTotal > JunkGrace ? string.Format(L.T("assist_junk"), Fmt.Size(junkTotal))
                : penDisk > 0 ? string.Format(L.T("assist_disk"), diskPct) : penRam > 0 ? string.Format(L.T("assist_ram"), ramPct) : L.T("assist_ok");
            TB("TileJunk").Text = Fmt.Size(junkTotal);
            TB("TileDisk").Text = L.T(diskPct < 75 ? "st_good" : diskPct < 90 ? "st_full" : "st_crit");
            TB("TileRam").Text = L.T(ramPct < 75 ? "st_good" : "st_busy");
            if (junkTotal != lastJunkSample) { lastJunkSample = junkTotal; Push(junkHist, junkTotal); DrawSpark("SparkJunk", junkHist); }
        }

        // tiny live history for the dashboard sparklines (this session only)
        readonly System.Collections.Generic.List<double> cpuHist = new System.Collections.Generic.List<double>();
        DateTime lastScanAt = DateTime.MinValue;
        long diskUsedB, diskTotalB;
        int dashMode = -1;
        /// Home reflows with the real width instead of scaling text down: wide = hero in one row and two columns below,
        /// narrow = actions under the text and everything in one column. Text sizes never change.
        void LayoutDash(double w)
        {
            if (w <= 0) return;
            int mode = w >= 1000 ? 2 : w >= 760 ? 1 : 0;
            if (mode == dashMode) return; dashMode = mode;
            var actions = E<FrameworkElement>("HeroActions");
            var text = E<FrameworkElement>("HeroText");
            if (mode == 2) { Grid.SetRow(actions, 0); Grid.SetColumn(actions, 2); actions.Margin = new Thickness(0); actions.HorizontalAlignment = HorizontalAlignment.Stretch; }
            else { Grid.SetRow(actions, 1); Grid.SetColumn(actions, 1); actions.Margin = new Thickness(28, 18, 0, 0); actions.HorizontalAlignment = HorizontalAlignment.Left; }
            text.Margin = new Thickness(28, 0, mode == 2 ? 20 : 0, 0);
            E<FrameworkElement>("HeroOrbit").Visibility = mode == 0 ? Visibility.Collapsed : Visibility.Visible;
            E<UniformGrid>("DashMetrics").Columns = mode == 0 ? 1 : 2;
            var sto = E<FrameworkElement>("StoCard"); var tips = E<FrameworkElement>("TipsCard"); var bottom = E<Grid>("DashBottom");
            if (mode == 0)
            {
                bottom.ColumnDefinitions[1].Width = new GridLength(0); bottom.ColumnDefinitions[2].Width = new GridLength(0);
                Grid.SetRow(tips, 1); Grid.SetColumn(tips, 0); tips.Margin = new Thickness(0, 16, 0, 0);
            }
            else
            {
                bottom.ColumnDefinitions[1].Width = new GridLength(16); bottom.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
                Grid.SetRow(tips, 0); Grid.SetColumn(tips, 2); tips.Margin = new Thickness(0);
            }
            Grid.SetRow(sto, 0); Grid.SetColumn(sto, 0);
        }

        void DrawOverview()
        {
            if (diskTotalB <= 0) return;
            TB("OvDrive").Text = Fmt.Size(diskTotalB) + " " + (System.IO.Path.GetPathRoot(Environment.SystemDirectory) ?? "").TrimEnd('\\');
            TB("OvUsed").Text = string.Format(L.T("ov_used"), Fmt.Size(diskUsedB), Fmt.Size(diskTotalB));
            var bar = E<Grid>("StoBar"); var leg = E<WrapPanel>("OvLegend");
            bar.Children.Clear(); bar.ColumnDefinitions.Clear(); leg.Children.Clear();
            var parts = new System.Collections.Generic.List<Tuple<string, double, string>>();
            if (mapRep != null) { int i = 0; foreach (var b in mapRep.Blocks.Take(6)) parts.Add(Tuple.Create(L.T("mb_" + b.Key), (double)b.Bytes, MapColors[i++ % MapColors.Length])); }
            else parts.Add(Tuple.Create(L.T("used_word"), (double)diskUsedB, "#3978F6"));
            double sumParts = parts.Sum(x => x.Item2);
            parts.Add(Tuple.Create(L.T("mb_free"), (double)Math.Max(0, diskTotalB - Math.Max(diskUsedB, (long)sumParts)), (string)null));
            foreach (var pt in parts)
            {
                bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.0001, pt.Item2), GridUnitType.Star) });
                if (pt.Item3 != null) { var r = new Border { Background = Hex(pt.Item3) }; Grid.SetColumn(r, bar.ColumnDefinitions.Count - 1); bar.Children.Add(r); }
                var lg = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 6) };
                var dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
                if (pt.Item3 != null) dot.Background = Hex(pt.Item3); else dot.SetResourceReference(Border.BackgroundProperty, "TrackBrush");
                var tx = new TextBlock { Text = pt.Item1 + "  " + Fmt.Size(pt.Item2), FontSize = 12.5 }; tx.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
                lg.Children.Add(dot); lg.Children.Add(tx); leg.Children.Add(lg);
            }
        }
        readonly System.Collections.Generic.List<double> diskHist = new System.Collections.Generic.List<double>(), ramHist = new System.Collections.Generic.List<double>(), junkHist = new System.Collections.Generic.List<double>();
        long lastJunkSample = -1;
        static void Push(System.Collections.Generic.List<double> l, double v) { l.Add(v); if (l.Count > 24) l.RemoveAt(0); }
        void DrawSpark(string name, System.Collections.Generic.List<double> h)
        {
            var pl = E<System.Windows.Shapes.Polyline>(name);
            var pts = new PointCollection();
            if (h.Count >= 2)
            {
                double mn = double.MaxValue, mx = double.MinValue;
                foreach (var v in h) { mn = Math.Min(mn, v); mx = Math.Max(mx, v); }
                double span = mx - mn; if (span < 1e-9) span = 1;
                for (int i = 0; i < h.Count; i++)
                    pts.Add(new Point(2 + i * 92.0 / (h.Count - 1), span == 1 && mx == mn ? 24 : 40 - (h[i] - mn) / span * 32));
            }
            pl.Points = pts;
            var area = W.FindName("Area" + name.Substring(5)) as System.Windows.Shapes.Polygon;
            if (area != null)
            {
                var ap = new PointCollection(pts);
                if (pts.Count > 1) { ap.Add(new Point(pts[pts.Count - 1].X, 44)); ap.Add(new Point(pts[0].X, 44)); }
                area.Points = ap;
            }
        }

        bool ringHooked;
        void AnimateRing(double target)
        {
            ringTarget = target;
            if (S.ReducedMotion) { ringCur = target; SetRing(target); TB("ScoreText").Text = ((int)target).ToString(); return; }
            if (!ringHooked) { ringHooked = true; CompositionTarget.Rendering += RingFrame; }
        }

        DateTime lastFrame = DateTime.MinValue;
        void RingFrame(object s, EventArgs e)
        {
            // frame-rate independent easing (runs at the screen refresh rate)
            var now = DateTime.UtcNow;
            double dt = lastFrame == DateTime.MinValue ? 1.0 / 60 : Math.Min(0.1, (now - lastFrame).TotalSeconds);
            lastFrame = now;
            double diff = ringTarget - ringCur;
            if (Math.Abs(diff) < 0.3) { ringCur = ringTarget; CompositionTarget.Rendering -= RingFrame; ringHooked = false; lastFrame = DateTime.MinValue; }
            else ringCur += diff * (1 - Math.Exp(-6.0 * dt));
            SetRing(ringCur);
            TB("ScoreText").Text = ((int)Math.Round(ringCur)).ToString();
        }

        void SetRing(double v)
        {
            v = Math.Max(0.5, Math.Min(99.95, v));
            double r = 85.5, cx = 98, cy = 98;
            double a = (v / 100 * 360 - 90) * Math.PI / 180;
            var end = new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
            var fig = new PathFigure { StartPoint = new Point(cx, cy - r), IsClosed = false };
            fig.Segments.Add(new ArcSegment(end, new Size(r, r), 0, v > 50, SweepDirection.Clockwise, true));
            var geo = new PathGeometry(); geo.Figures.Add(fig); geo.Freeze();
            E<System.Windows.Shapes.Path>("RingArc").Data = geo;
        }

        void UpdateLastClean()
        {
            TB("LastCleanText").Text = string.IsNullOrEmpty(S.LastClean) ? "" : string.Format(L.T("last_clean"), S.LastClean, Fmt.Size(S.LastFreed));
        }
    }
}
