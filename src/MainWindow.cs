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

            var chrome = new WindowChrome { CaptionHeight = 42, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0, 0, 0, 1), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false };
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
            SetupAurora();

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
            if (!S.ReducedMotion) aurora.Begin(W, true);

            var pulse = new DoubleAnimation(1, 1.035, TimeSpan.FromMilliseconds(1200)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
            var st = (ScaleTransform)BT("BtnSmart").RenderTransform;
            if (!S.ReducedMotion) { st.BeginAnimation(ScaleTransform.ScaleXProperty, pulse); st.BeginAnimation(ScaleTransform.ScaleYProperty, pulse); }

            var bob = new DoubleAnimation(0, -7, TimeSpan.FromSeconds(2.2)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
            if (!S.ReducedMotion) ((TranslateTransform)E<Image>("AboutLogo").RenderTransform).BeginAnimation(TranslateTransform.YProperty, bob);
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
                aurora.Begin(W, true);
                var pulse = new DoubleAnimation(1, 1.035, TimeSpan.FromMilliseconds(1200)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
                st.BeginAnimation(ScaleTransform.ScaleXProperty, pulse); st.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
                var bob = new DoubleAnimation(0, -7, TimeSpan.FromSeconds(2.2)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
                logoT.BeginAnimation(TranslateTransform.YProperty, bob);
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
            UpdateStats();
            E<TextBox>("LogBox").Clear();
            statTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            statTimer.Tick += (s, a) => { if (!busy && W.IsVisible && W.WindowState != WindowState.Minimized) UpdateStats(); };
            statTimer.Start();
            bool welcome = CheckWhatsNew();
            if (welcome) { junkTotal = 0; TB("JunkText").Text = "-"; TB("JunkSub").Text = L.T("not_scanned"); }   // the first scan starts when the tour ends
            else if (S.ScanOnStart) StartScan(); else { junkTotal = 0; TB("JunkText").Text = "-"; TB("JunkSub").Text = L.T("not_scanned"); }
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
            BT("BtnMin").Click += (s, e) => W.WindowState = WindowState.Minimized;
            BT("BtnMax").Click += (s, e) => W.WindowState = W.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            BT("BtnClose").Click += (s, e) => W.Close();

            foreach (var n in new[] { "NavDash", "NavClean", "NavOrg", "NavRam", "NavStartup", "NavApps", "NavSettings", "NavAbout" })
            {
                var name = n;
                E<RadioButton>(n).Checked += (s, e) => ShowPage(name);
            }

            BT("BtnSmart").Click += (s, e) => RequestClean(true);
            BT("BtnDashScan").Click += (s, e) => StartScan();
            BT("BtnScan").Click += (s, e) => StartScan();
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
            BT("BtnFinish").Click += (s, e) => HideOverlay();
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
                foreach (var t in targets) targetChecks[t.Key].IsChecked = t.Default;
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
                targetChecks[t.Key].Content = L.T("t_" + t.Key);
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
        void ShowPage(string nav)
        {
            string page = "Page" + nav.Substring(3);
            if (page == currentPage) return;
            var old = E<FrameworkElement>(currentPage);
            var nw = E<FrameworkElement>(page);
            old.Visibility = Visibility.Collapsed;
            nw.Visibility = Visibility.Visible;
            currentPage = page;
            if (!S.ReducedMotion)
            {
                nw.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, 240));
                var tt = nw.RenderTransform as TranslateTransform;
                if (tt == null) { tt = new TranslateTransform(); nw.RenderTransform = tt; }
                tt.BeginAnimation(TranslateTransform.YProperty, Anim(14, 0, 320, new QuinticEase { EasingMode = EasingMode.EaseOut }));
            }
            if (!busy) TB("StatusText").Text = "";      // an old message from another page must not linger
            if (page == "PageRam" && !busy) LoadRam();
            if (page == "PageApps" && !appsLoaded && !busy) LoadApps();
            if (page == "PageStartup" && !startupLoaded && !busy) LoadStartup();
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
                TB("DiskLabel").Text = L.T("storage") + "  " + d.Name.TrimEnd('\\');
                TB("DiskUsedText").Text = Fmt.Size(used) + " " + L.T("used");
                E<ProgressBar>("DiskBar").Value = diskPct;
                TB("DiskFreeText").Text = string.Format(L.T("free_of"), Fmt.Size(d.AvailableFreeSpace), Fmt.Size(d.TotalSize));
                E<ProgressBar>("DiskBar").Foreground = diskPct >= 90 ? (Brush)W.FindResource("BadBrush") : (Brush)W.FindResource("AccentGrad");

                double total, free; Memory.Info(out total, out free, out ramPct);
                TB("RamUsedText").Text = Fmt.Size(total - free) + " " + L.T("used");
                E<ProgressBar>("RamBar").Value = ramPct;
                TB("RamFreeText").Text = string.Format(L.T("free_of"), Fmt.Size(free), Fmt.Size(total));
                E<ProgressBar>("RamBar").Foreground = ramPct >= 85 ? (Brush)W.FindResource("WarnBrush") : (Brush)W.FindResource("AccentGrad");
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
