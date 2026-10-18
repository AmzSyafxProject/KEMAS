// KEMAS - main window: features (cleaner, organizer, memory, startup, programs, updates, overlay, dialogs)
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Kemas
{
    public partial class MainWin
    {
        static Brush Hex(string h) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(h)); b.Freeze(); return b; }
        static string Glyph(string hex) { return ((char)Convert.ToInt32(hex, 16)).ToString(); }

        // ================================================================ cleaner rows
        void BuildCleanerRows()
        {
            targets = Cleaner.CreateTargets();
            var list = E<StackPanel>("CleanList");
            list.Children.Clear();
            var sel = S.CleanSelection;
            bool deepHeader = false;
            foreach (var t in targets)
            {
                if (!deepHeader && Cleaner.IsDeep(t))
                {
                    deepHeader = true;
                    var head = new StackPanel { Margin = new Thickness(4, 14, 0, 12) };
                    var hl = new StackPanel { Orientation = Orientation.Horizontal };
                    hl.Children.Add(new TextBlock { Text = Glyph("E945"), FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 15, Foreground = Hex("#9C8BFF"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                    var ht = new TextBlock { FontSize = 16, FontWeight = FontWeights.SemiBold };
                    ht.SetResourceReference(TextBlock.TextProperty, "L_deep_head");
                    ht.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
                    hl.Children.Add(ht);
                    head.Children.Add(hl);
                    var hd = new TextBlock { FontSize = 12.5, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap };
                    hd.SetResourceReference(TextBlock.TextProperty, "L_deep_head_d");
                    hd.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
                    head.Children.Add(hd);
                    list.Children.Add(head);
                }
                var row = new Border { CornerRadius = new CornerRadius(16), Padding = new Thickness(16, 13, 16, 13), Margin = new Thickness(0, 0, 0, 10), BorderThickness = new Thickness(1) };
                row.SetResourceReference(Border.BackgroundProperty, "CardBrush");
                row.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var tile = new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius(13), VerticalAlignment = VerticalAlignment.Center };
                tile.SetResourceReference(Border.BackgroundProperty, "IconTile");
                tile.Child = new TextBlock { Text = Glyph(t.Icon), FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 18, Foreground = Hex("#9C8BFF"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                g.Children.Add(tile);
                var mid = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                var cb = new CheckBox { FontSize = 14.5, FontWeight = FontWeights.SemiBold, Style = (Style)W.FindResource("CB") };
                cb.IsChecked = sel != null ? sel.Contains(t.Key) : t.Default;
                cb.Checked += (s, e) => { UpdateSelectedTotal(); QueueSave(); };
                cb.Unchecked += (s, e) => { UpdateSelectedTotal(); QueueSave(); };
                var desc = new TextBlock { FontSize = 12.5, Margin = new Thickness(30, 3, 0, 0), TextWrapping = TextWrapping.Wrap };
                desc.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
                mid.Children.Add(cb); mid.Children.Add(desc);
                Grid.SetColumn(mid, 1); g.Children.Add(mid);
                var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0), MinWidth = 110 };
                var size = new TextBlock { Text = "-", FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = (Brush)W.FindResource("CyanBrush"), HorizontalAlignment = HorizontalAlignment.Right };
                var tag = new TextBlock { FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 2, 0, 0) };
                tag.Foreground = t.Tag == "safe" ? (Brush)W.FindResource("GoodBrush") : t.Tag == "permanent" ? (Brush)W.FindResource("BadBrush") : t.Tag == "deep" ? Hex("#9C8BFF") : (Brush)W.FindResource("WarnBrush");
                right.Children.Add(size); right.Children.Add(tag);
                Grid.SetColumn(right, 2); g.Children.Add(right);
                row.Child = g;
                NameScope.SetNameScope(row, new NameScope());
                row.RegisterName("desc", desc); row.RegisterName("tagtext", tag);
                cb.Tag = row;
                targetChecks[t.Key] = cb;
                targetSizes[t.Key] = size;
                list.Children.Add(row);
            }
        }

        void SetTargetSize(CleanTarget t)
        {
            var l = targetSizes[t.Key];
            if (t.IsDns) l.Text = L.T("refresh_word");
            else if (t.MeasureByDrive) l.Text = L.T("varies_word");
            else if (!scanned) l.Text = "-";
            else l.Text = t.Size <= 0 ? L.T("clean_word") : Fmt.Size(t.Size);
        }

        void UpdateSelectedTotal()
        {
            if (targets == null) return;
            long sum = 0; int n = 0;
            foreach (var t in targets) if (targetChecks[t.Key].IsChecked == true) { sum += t.Size; n++; }
            TB("SelTotal").Text = string.Format(L.T("selected_total"), Fmt.Size(sum));
            TB("SelCount").Text = string.Format(L.T("selected_count"), n, targets.Count);
            bool allStd = targets.Where(t => !Cleaner.IsDeep(t)).All(t => targetChecks[t.Key].IsChecked == true);
            BT("BtnSelAll").Content = allStd ? L.T("select_none") : L.T("select_all");
        }

        // ================================================================ scan
        void StartScan()
        {
            if (busy) { Toast(L.T("please_wait"), "E916"); return; }
            TB("JunkText").Text = "...";
            TB("JunkSub").Text = L.T("scanning");
            foreach (var t in targets) targetSizes[t.Key].Text = L.T("scanning_short");
            Log(L.T("scan_start"));
            var c = new Cleaner { SkipRecentTemp = S.SkipRecentTemp };
            activeCleaner = c;
            int done = 0;
            Run(() =>
            {
                // measure several locations at once: most of the time is spent waiting on the disk, not the CPU
                var opts = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Math.Min(4, Environment.ProcessorCount)) };
                System.Threading.Tasks.Parallel.ForEach(targets, opts, (t, state) =>
                {
                    if (cancelFlag) { state.Stop(); return; }
                    long size = c.Measure(t);
                    var tt = t; int k = System.Threading.Interlocked.Increment(ref done);
                    Ui(() =>
                    {
                        tt.Size = size;
                        scanned = true;
                        SetTargetSize(tt);
                        E<ProgressBar>("MainProgress").Value = k * 100.0 / targets.Count;
                        TB("StatusText").Text = L.T("scanning") + "  " + L.T("t_" + tt.Key);
                    });
                });
            }, () =>
            {
                activeCleaner = null;
                junkTotal = targets.Sum(t => t.Size);
                scanned = true;
                TB("JunkText").Text = Fmt.Size(junkTotal);
                TB("JunkSub").Text = string.Format(L.T("across"), targets.Count);
                UpdateSelectedTotal();
                UpdateStats();
                Log(string.Format(L.T("scan_done"), Fmt.Size(junkTotal)));
            });
        }

        // ================================================================ clean
        public void RequestClean(bool smart)
        {
            if (busy) { Toast(L.T("please_wait"), "E916"); return; }
            var sel = targets.Where(t => targetChecks[t.Key].IsChecked == true).ToList();
            bool trim = smart && S.TrimRamOnSmart;
            if (sel.Count == 0 && !trim) { Toast(L.T("nothing_ticked"), "E7BA"); return; }
            if (!S.ConfirmClean) { StartClean(sel, trim); return; }
            string names = string.Join(", ", sel.Select(t => L.T("t_" + t.Key)));
            string msg = string.Format(L.T("confirm_clean_d"), names);
            if (trim) msg += " " + L.T("confirm_trim");
            if (trim || sel.Any(t => Cleaner.IsDeep(t))) msg += "\n\n" + L.T("known_wall");
            if (sel.Any(t => t.Key == "recycle")) msg += "\n\n" + L.T("confirm_recycle");
            if (sel.Any(t => t.Key == "winold") && System.IO.Directory.Exists(Cleaner.WinOld)) msg += "\n\n" + L.T("deep_confirm_winold");
            if (sel.Any(t => t.Slow)) msg += "\n\n" + L.T("slow_note");
            if (sel.Any(t => t.Key == "browser") && Cleaner.BrowserRunning()) msg += "\n\n" + L.T("confirm_browser");
            msg += "\n\n" + L.T("confirm_safe");
            Ask(smart ? L.T("confirm_smart") : L.T("confirm_clean"), msg, L.T("clean_now"), "E74D", false, () => StartClean(sel, trim));
        }

        public void RequestDeepClean()
        {
            if (busy) { Toast(L.T("please_wait"), "E916"); return; }
            // everything except the Windows Update download cache (it can break an update that is installing)
            foreach (var t in targets) targetChecks[t.Key].IsChecked = t.Key != "winupdate";
            var sel = targets.Where(t => targetChecks[t.Key].IsChecked == true).ToList();
            string msg = L.T("deep_confirm_d");
            if (System.IO.Directory.Exists(Cleaner.WinOld)) msg += "\n\n" + L.T("deep_confirm_winold");
            if (Cleaner.BrowserRunning()) msg += "\n\n" + L.T("confirm_browser");
            msg += "\n\n" + L.T("known_wall");
            msg += "\n\n" + L.T("confirm_safe");
            Ask(L.T("deep_confirm"), msg, L.T("deep_go"), "E945", false, () => StartClean(sel, S.TrimRamOnSmart));
        }

        // ================================================================ what's new
        static readonly string[][] WhatsNewItems =
        {
            new[] { "E9D2", "wn0" },   // calmer cleaning screen
            new[] { "E7BA", "wn1" },   // known issue: black wallpaper
            new[] { "E72E", "wn2" },   // update security
            new[] { "E7C3", "wn3" },   // public beta
            new[] { "E897", "wn4" },   // help form
        };

        /// Returns true when the welcome screen was opened (first run).
        bool CheckWhatsNew()
        {
            string seen = S.LastSeenVersion ?? "";
            bool used = S.CleanCount > 0 || !string.IsNullOrEmpty(S.LastClean);
            bool upgraded = seen != AppInfo.Version && (seen.Length > 0 || App.JustUpdated || used);
            if (seen != AppInfo.Version) { S.LastSeenVersion = AppInfo.Version; QueueSave(); }
            if (!S.WelcomeDone && !upgraded && !used && seen.Length == 0) { ShowWelcome(); return true; }
            if (!S.WelcomeDone) { S.WelcomeDone = true; QueueSave(); }     // existing users never see the first-run tour
            if (upgraded) Delay(900, ShowWhatsNew);
            return false;
        }

        // ================================================================ welcome (first run)
        int welcomeStep;

        void ShowWelcome()
        {
            loadingUi = true;
            E<RadioButton>(S.Language == "ms" ? "WLangMs" : "WLangEn").IsChecked = true;
            E<RadioButton>(S.Theme == "light" ? "WThemeLight" : S.Theme == "system" ? "WThemeSystem" : "WThemeDark").IsChecked = true;
            E<CheckBox>("WStartup").IsChecked = true;
            E<CheckBox>("WAuto").IsChecked = false;
            loadingUi = false;
            E<Image>("WLogo").Source = Logo.Image();
            WelcomeTexts();
            welcomeStep = 0;
            ShowWelcomeStep(0, 0);
            var layer = E<Grid>("WelcomeLayer");
            layer.Visibility = Visibility.Visible;
            layer.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, 260));
            var sc = (ScaleTransform)E<Border>("WelcomeCard").RenderTransform;
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(0.92, 1, 420, new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }));
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(0.92, 1, 420, new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }));
            if (!S.ReducedMotion)
            {
                var bob = new DoubleAnimation(0, -6, TimeSpan.FromSeconds(1.8)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
                ((TranslateTransform)E<Image>("WLogo").RenderTransform).BeginAnimation(TranslateTransform.YProperty, bob);
            }
            BT("BtnWNext").Focus();
        }

        void WelcomeTexts()
        {
            string name = string.IsNullOrWhiteSpace(S.DisplayName) ? UserName.Detect() : S.DisplayName.Trim();
            TB("WHello").Text = string.Format(L.T("greet"), name);
            BT("BtnWNext").Content = welcomeStep == 0 ? L.T("wel_start") : welcomeStep == 1 ? L.T("wel_continue") : L.T("wel_finish");
        }

        void ShowWelcomeStep(int i, int dir)
        {
            welcomeStep = i;
            for (int k = 0; k < 3; k++)
            {
                var p = E<FrameworkElement>("WStep" + k);
                p.Visibility = k == i ? Visibility.Visible : Visibility.Collapsed;
                var dot = E<Border>("WDot" + k);
                dot.Width = k == i ? 22 : 7;
                if (k == i) dot.Background = (Brush)W.FindResource("AccentGrad"); else dot.SetResourceReference(Border.BackgroundProperty, "LineBrush");
            }
            var cur = E<FrameworkElement>("WStep" + i);
            if (!S.ReducedMotion && dir != 0)
            {
                var tt = cur.RenderTransform as TranslateTransform;
                if (tt == null) { tt = new TranslateTransform(); cur.RenderTransform = tt; }
                tt.BeginAnimation(TranslateTransform.XProperty, Anim(28 * dir, 0, 360, new QuinticEase { EasingMode = EasingMode.EaseOut }));
                cur.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, 260));
            }
            if (i == 2 && !S.ReducedMotion)
            {
                var sc = (ScaleTransform)E<Border>("WDoneBadge").RenderTransform;
                sc.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(0.4, 1, 520, new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut }));
                sc.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(0.4, 1, 520, new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut }));
            }
            BT("BtnWBack").Visibility = i == 0 ? Visibility.Collapsed : Visibility.Visible;
            BT("BtnWSkip").Visibility = i == 0 ? Visibility.Visible : Visibility.Collapsed;
            WelcomeTexts();
        }

        void WelcomeNext()
        {
            if (welcomeStep < 2) { ShowWelcomeStep(welcomeStep + 1, 1); return; }
            FinishWelcome(true);
        }

        void FinishWelcome(bool applyChoices)
        {
            if (applyChoices)
            {
                bool startup = E<CheckBox>("WStartup").IsChecked == true;
                if (startup != S.StartWithWindows && Tasks.SetStartup(startup)) S.StartWithWindows = startup;
                if (E<CheckBox>("WAuto").IsChecked == true && S.AutoClean == "off" && Tasks.SetAutoClean("weekly")) S.AutoClean = "weekly";
            }
            S.WelcomeDone = true;
            SaveNow();
            LoadSettingsIntoUi();
            var layer = E<Grid>("WelcomeLayer");
            var a = Anim(1, 0, 240);
            a.Completed += (s, e) =>
            {
                layer.Visibility = Visibility.Collapsed;
                ((TranslateTransform)E<Image>("WLogo").RenderTransform).BeginAnimation(TranslateTransform.YProperty, null);
            };
            layer.BeginAnimation(UIElement.OpacityProperty, a);
            Delay(350, () => { if (!scanned && !busy) StartScan(); });
            Delay(1600, OfferInstall);
        }

        void ShowWhatsNew()
        {
            TB("WnVersion").Text = "v" + AppInfo.Version;
            TB("WnTitle").Text = string.Format(L.T("wn_title"), AppInfo.Version);
            TB("WnSub").Text = L.T("wn_sub");
            var list = E<StackPanel>("WnList");
            list.Children.Clear();
            int i = 0;
            foreach (var it in WhatsNewItems)
            {
                var row = new Grid { Margin = new Thickness(0, 12, 0, 0), Opacity = S.ReducedMotion ? 1 : 0 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                var tile = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(12), VerticalAlignment = VerticalAlignment.Top };
                tile.SetResourceReference(Border.BackgroundProperty, "IconTile");
                tile.Child = new TextBlock { Text = Glyph(it[0]), FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 17, Foreground = Hex("#9C8BFF"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                row.Children.Add(tile);
                var txt = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                var t1 = new TextBlock { Text = L.T(it[1] + "_t"), FontSize = 14.5, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
                t1.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
                var t2 = new TextBlock { Text = L.T(it[1] + "_d"), FontSize = 12.5, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap };
                t2.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
                txt.Children.Add(t1); txt.Children.Add(t2);
                Grid.SetColumn(txt, 1); row.Children.Add(txt);
                list.Children.Add(row);
                if (!S.ReducedMotion)
                {
                    var tt = new TranslateTransform(0, 12); row.RenderTransform = tt;
                    var fade = Anim(0, 1, 380); fade.BeginTime = TimeSpan.FromMilliseconds(260 + i * 110);
                    var rise = Anim(12, 0, 460, new QuinticEase { EasingMode = EasingMode.EaseOut }); rise.BeginTime = fade.BeginTime;
                    row.BeginAnimation(UIElement.OpacityProperty, fade);
                    tt.BeginAnimation(TranslateTransform.YProperty, rise);
                }
                i++;
            }
            var layer = E<Grid>("WhatsNewLayer");
            layer.Visibility = Visibility.Visible;
            layer.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, 200));
            var sc = (ScaleTransform)E<Border>("WhatsNewCard").RenderTransform;
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(0.9, 1, 360, new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut }));
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(0.9, 1, 360, new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut }));
            if (!S.ReducedMotion)
            {
                var rot = (RotateTransform)TB("WnSpark").RenderTransform;
                var spin = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(900)) { BeginTime = TimeSpan.FromMilliseconds(150), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                rot.BeginAnimation(RotateTransform.AngleProperty, spin);
            }
            BT("BtnWnTry").Focus();
        }

        void CloseWhatsNew(bool tryDeep)
        {
            var layer = E<Grid>("WhatsNewLayer");
            var a = Anim(1, 0, 180);
            a.Completed += (s, e) => layer.Visibility = Visibility.Collapsed;
            layer.BeginAnimation(UIElement.OpacityProperty, a);
            if (tryDeep) { E<RadioButton>("NavClean").IsChecked = true; if (!busy) StartScan(); }
        }

        // ================================================================ road to 100
        string roadSig;

        void UpdateRoad(int score)
        {
            string sig = score + "|" + penJunkStd + "|" + penJunkDeep + "|" + penDisk + "|" + penRam + "|" + Fmt.Size(junkStd) + "|" + Fmt.Size(junkDeep) + "|" + busy + "|" + S.Language;
            if (sig == roadSig) return;
            roadSig = sig;
            int toGo = 100 - score;
            TB("RoadToGo").Text = toGo <= 0 ? L.T("road_perfect_chip") : string.Format(L.T("road_togo"), toGo);
            var list = E<StackPanel>("RoadList");
            list.Children.Clear();
            if (!scanned)
            {
                list.Children.Add(RoadRow("E721", L.T("road_scan"), L.T("road_scan_d"), 0, L.T("scan_again"), () => StartScan(), false));
                return;
            }
            list.Children.Add(RoadRow("E74D", L.T("road_junk"), penJunkStd > 0 ? string.Format(L.T("road_junk_d"), Fmt.Size(junkStd)) : L.T("road_junk_ok"),
                penJunkStd, L.T("smart"), () => RequestClean(true), penJunkStd == 0));
            list.Children.Add(RoadRow("E945", L.T("road_deep"), penJunkDeep > 0 ? string.Format(L.T("road_deep_d"), Fmt.Size(junkDeep)) : L.T("road_deep_ok"),
                penJunkDeep, L.T("deep_btn"), () => { E<RadioButton>("NavClean").IsChecked = true; RequestDeepClean(); }, penJunkDeep == 0));
            list.Children.Add(RoadRow("EDA2", L.T("road_disk"), penDisk > 0 ? string.Format(L.T("road_disk_d"), diskPct) : string.Format(L.T("road_disk_ok"), diskPct),
                penDisk, L.T("road_disk_btn"), () => E<RadioButton>("NavApps").IsChecked = true, penDisk == 0));
            list.Children.Add(RoadRow("E9D9", L.T("road_ram"), penRam > 0 ? string.Format(L.T("road_ram_d"), ramPct) : string.Format(L.T("road_ram_ok"), ramPct),
                penRam, L.T("road_ram_btn"), () => E<RadioButton>("NavRam").IsChecked = true, penRam == 0));
            if (toGo <= 0)
            {
                var done = new TextBlock { Text = L.T("road_perfect"), FontSize = 12.5, Margin = new Thickness(2, 10, 0, 0), TextWrapping = TextWrapping.Wrap };
                done.Foreground = (Brush)W.FindResource("GoodBrush");
                list.Children.Add(done);
            }
        }

        FrameworkElement RoadRow(string icon, string title, string desc, int points, string action, Action onClick, bool ok)
        {
            var g = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var tile = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), VerticalAlignment = VerticalAlignment.Center };
            if (ok) tile.Background = Hex("#2621C98A"); else tile.SetResourceReference(Border.BackgroundProperty, "IconTile");
            tile.Child = new TextBlock { Text = Glyph(ok ? "E73E" : icon), FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 14,
                Foreground = ok ? (Brush)W.FindResource("GoodBrush") : Hex("#9C8BFF"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            g.Children.Add(tile);
            var mid = new StackPanel { Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            var t1 = new TextBlock { Text = title, FontSize = 13.5, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
            t1.SetResourceReference(TextBlock.ForegroundProperty, ok ? "MutedBrush" : "TextBrush");
            var t2 = new TextBlock { Text = desc, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 0) };
            t2.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            mid.Children.Add(t1); mid.Children.Add(t2);
            Grid.SetColumn(mid, 1); g.Children.Add(mid);
            if (!ok)
            {
                var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                if (points > 0)
                    right.Children.Add(new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(9, 3, 9, 3), Margin = new Thickness(0, 0, 10, 0), Background = Hex("#2622C4F0"), VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock { Text = "+" + points, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)W.FindResource("CyanBrush") } });
                var b = new Button { Content = action, Style = (Style)W.FindResource("Ghost"), Height = 32, Padding = new Thickness(14, 0, 14, 0), FontSize = 12.5, IsEnabled = !busy };
                b.Click += (s, e) => onClick();
                right.Children.Add(b);
                Grid.SetColumn(right, 2); g.Children.Add(right);
            }
            return g;
        }

        // ================================================================ help & support
        void ShowSupport()
        {
            TB("RepEmailLine").Text = string.Format(L.T("help_email_line"), Support.Email);
            var layer = E<Grid>("SupportLayer");
            layer.Visibility = Visibility.Visible;
            layer.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, 180));
            var sc = (ScaleTransform)E<Border>("SupportCard").RenderTransform;
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(0.94, 1, 280, new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }));
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(0.94, 1, 280, new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }));
            E<TextBox>("RepMessage").Focus();
        }

        void CloseSupport()
        {
            var layer = E<Grid>("SupportLayer");
            var a = Anim(1, 0, 160);
            a.Completed += (s, e) => layer.Visibility = Visibility.Collapsed;
            layer.BeginAnimation(UIElement.OpacityProperty, a);
        }

        string RepCategory
        {
            get { return E<RadioButton>("RepIdea").IsChecked == true ? "Suggestion" : E<RadioButton>("RepQuestion").IsChecked == true ? "Question" : "Problem report"; }
        }

        void SendReport(bool gmail)
        {
            string msg = E<TextBox>("RepMessage").Text;
            bool diag = E<CheckBox>("RepDiag").IsChecked == true;
            if (string.IsNullOrWhiteSpace(msg)) { Toast(L.T("help_empty"), "E7BA"); E<TextBox>("RepMessage").Focus(); return; }
            string url = gmail ? Support.GmailUrl(RepCategory, msg, diag) : Support.MailtoUrl(RepCategory, msg, diag);
            if (Support.Open(url))
            {
                Toast(gmail ? L.T("help_opened_gmail") : L.T("help_opened_mail"), "E73E");
                E<TextBox>("RepMessage").Clear();
                CloseSupport();
            }
            else
            {
                CopyReport();
                Toast(L.T("help_no_mail"), "E7BA");
            }
        }

        void CopyReport()
        {
            string text = "To: " + Support.Email + "\r\nSubject: " + Support.Subject(RepCategory) + "\r\n\r\n" +
                          Support.Body(RepCategory, E<TextBox>("RepMessage").Text, E<CheckBox>("RepDiag").IsChecked == true, 2500).Replace("\n", "\r\n");
            try { Clipboard.SetText(text); Toast(L.T("help_copied"), "E8C8"); }
            catch (Exception ex) { global::Kemas.Log.Error("Clipboard", ex); }
        }

        void StartClean(List<CleanTarget> sel, bool trim)
        {
            var items = sel.Select(t => Tuple.Create(t.Key, L.T("t_" + t.Key))).ToList();
            if (trim) items.Add(Tuple.Create("ram", L.T("free_ram_item")));
            ShowOverlay(L.T("ov_clean"), L.T("ov_clean_d"), items);
            var c = new Cleaner { SkipRecentTemp = S.SkipRecentTemp, RemoveLockedAtRestart = S.RemoveLockedAtRestart };
            activeCleaner = c;
            var results = new List<CleanResult>();
            long ram = -1;
            bool browserOpen = sel.Any(t => t.Key == "browser") && Cleaner.BrowserRunning();
            int total = items.Count, i = 0;
            var watch = Stopwatch.StartNew();
            Log(L.T("clean_start"));
            Run(() =>
            {
                foreach (var t in sel)
                {
                    var tt = t;
                    if (cancelFlag)
                    {
                        results.Add(new CleanResult { Key = t.Key, State = ItemState.Skipped, Notes = { "note_stopped" } });
                        Ui(() => SetOvItem(tt.Key, ItemState.Skipped, L.T("stopped")));
                        continue;
                    }
                    Ui(() => { SetOvItem(tt.Key, ItemState.Working, L.T("cleaning")); OvTrack(tt.Key, i * 100.0 / total, 100.0 / total, tt.Slow); OvStep(L.T("cleaning") + " " + L.T("t_" + tt.Key) + (tt.Slow ? "  ·  " + L.T("slow_note") : "")); });
                    CleanResult r;
                    try { r = c.Clean(t); }
                    catch (Exception ex) { r = new CleanResult { Key = t.Key, State = ItemState.Skipped }; r.Notes.Add(Cleaner.Friendly(ex)); }
                    if (t.Key == "browser" && browserOpen) r.Notes.Add("note_browser");
                    results.Add(r);
                    int k = ++i;
                    var rr = r;
                    Ui(() =>
                    {
                        string detail = tt.IsDns ? L.T("refreshed") : string.Format(L.T("freed_x"), Fmt.Size(rr.Freed));
                        if (rr.State == ItemState.Skipped) detail = L.T("skipped");
                        SetOvItem(tt.Key, rr.State == ItemState.Done && rr.Locked > 0 ? ItemState.Done : rr.State, detail);
                        OvProgress(k * 100.0 / total);
                        SetTargetSize(tt);
                        string line = "  " + L.T("t_" + tt.Key) + ": " + detail;
                        if (rr.Locked > 0) line += "  (" + string.Format(L.T("note_locked"), rr.Locked) + ")";
                        if (rr.Scheduled > 0) line += "  (" + string.Format(L.T("note_restart"), rr.Scheduled) + ")";
                        Log(line);
                    });
                }
                if (trim)
                {
                    if (cancelFlag) Ui(() => SetOvItem("ram", ItemState.Skipped, L.T("stopped")));
                    else
                    {
                        Ui(() => { SetOvItem("ram", ItemState.Working, L.T("freeing")); OvStep(L.T("freeing")); });
                        try
                        {
                            ram = Memory.Trim(p => Ui(() => OvProgress((i + p) * 100.0 / total)));
                            var g = ram;
                            Ui(() => SetOvItem("ram", ItemState.Done, string.Format(L.T("released_x"), Fmt.Size(g))));
                        }
                        catch (Exception ex) { Ui(() => SetOvItem("ram", ItemState.Skipped, L.T("skipped"))); global::Kemas.Log.Error("Trim", ex); }
                    }
                    Ui(() => OvProgress(100));
                }
            }, () =>
            {
                activeCleaner = null;
                long freed = results.Sum(r => r.Freed);
                int cleaned = results.Count(r => r.State != ItemState.Skipped);
                int skipped = results.Count(r => r.State == ItemState.Skipped);
                var notes = new List<Tuple<string, bool>>();
                foreach (var r in results)
                {
                    string name = L.T("t_" + r.Key);
                    var parts = r.Notes.Select(NoteText).ToList();
                    if (r.Locked > 0) parts.Insert(0, string.Format(L.T("note_locked"), r.Locked));
                    if (r.Scheduled > 0) parts.Insert(0, string.Format(L.T("note_restart"), r.Scheduled));
                    if (parts.Count == 0) continue;
                    bool warn = r.State != ItemState.Done;
                    string head = r.State == ItemState.Skipped ? L.T("was_skipped") : r.State == ItemState.Partial ? L.T("partly") : "";
                    notes.Add(Tuple.Create(name + (head.Length > 0 ? " " + head : "") + ": " + string.Join("; ", parts), warn));
                }
                if (cancelFlag) notes.Add(Tuple.Create(L.T("you_stopped"), false));
                var chips = new List<Tuple<string, string>> { Tuple.Create(string.Format(L.T("chip_cleaned"), cleaned), "good") };
                if (skipped > 0) chips.Add(Tuple.Create(string.Format(L.T("chip_skipped"), skipped), "warn"));
                if (ram >= 0) chips.Add(Tuple.Create("RAM +" + Fmt.Size(ram), "cyan"));
                chips.Add(Tuple.Create(watch.Elapsed.TotalSeconds.ToString("0.0") + "s", "plain"));
                string title = L.T("done_title");
                if (cleaned == 0 && skipped > 0) title = L.T("done_none");
                else if (skipped > 0 || notes.Any(n => n.Item2)) title = L.T("done_skips");
                ShowDone(title, L.T("done_sub"), freed, false, L.T("freed_up"), chips, notes);
                junkTotal = targets.Sum(t => t.Size);
                TB("JunkText").Text = Fmt.Size(junkTotal);
                TB("JunkSub").Text = L.T("left_in_use");
                S.LastClean = DateTime.Now.ToString("dd MMM, HH:mm", L.Culture);
                S.LastFreed = freed; S.TotalFreed += freed; S.CleanCount++;
                SaveNow();
                UpdateLastClean();
                UpdateSelectedTotal();
                Log(string.Format(L.T("clean_done"), Fmt.Size(freed)));
            });
        }

        string NoteText(string key)
        {
            if (key == null) return "";
            if (key.StartsWith("err_") || key.StartsWith("note_")) return L.T(key);
            return key;
        }

        // ================================================================ overlay
        readonly Dictionary<string, Tuple<TextBlock, TextBlock>> ovRows = new Dictionary<string, Tuple<TextBlock, TextBlock>>();

        void ShowOverlay(string title, string sub, List<Tuple<string, string>> items)
        {
            TB("OvTitle").Text = title; TB("OvSub").Text = sub;
            OvProgress(0); OvStep(L.T("starting"));
            var list = E<StackPanel>("OvList");
            list.Children.Clear(); ovRows.Clear();
            foreach (var it in items)
            {
                var b = new Border { CornerRadius = new CornerRadius(11), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 6) };
                b.SetResourceReference(Border.BackgroundProperty, "GhostBrush");
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var ic = new TextBlock { Text = "○", FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform() };
                ic.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
                var nm = new TextBlock { Text = it.Item2, FontSize = 13.5, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                nm.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
                var dt = new TextBlock { Text = L.T("waiting"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
                dt.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
                Grid.SetColumn(nm, 1); Grid.SetColumn(dt, 2);
                g.Children.Add(ic); g.Children.Add(nm); g.Children.Add(dt);
                b.Child = g;
                list.Children.Add(b);
                ovRows[it.Item1] = Tuple.Create(ic, dt);
            }
            E<Grid>("WorkView").Visibility = Visibility.Visible;
            E<Grid>("DoneView").Visibility = Visibility.Collapsed;
            BT("BtnStop").IsEnabled = true; BT("BtnStop").Content = L.T("stop");
            var ov = E<Grid>("Overlay");
            ov.Visibility = Visibility.Visible;
            ov.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, 220));
            var sc = (ScaleTransform)E<Border>("OverlayCard").RenderTransform;
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(0.94, 1, 320, new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }));
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(0.94, 1, 320, new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }));
            StartScene();
        }

        void OvProgress(double p)
        {
            E<ProgressBar>("OvBar").Value = p;
            TB("OvPct").Text = p.ToString("0") + "%";
            E<ProgressBar>("MainProgress").Value = p;
        }
        DispatcherTimer ovTimer; string ovKey; DateTime ovStart; double ovBase, ovSpan, ovExpect = 8;
        void OvTrack(string key, double baseP, double span, bool slow)
        {
            ovKey = key; ovStart = DateTime.UtcNow; ovBase = baseP; ovSpan = span; ovExpect = slow ? 600 : 8;
            if (ovTimer == null) { ovTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; ovTimer.Tick += OvTick; }
            ovTimer.Start();
        }
        void OvUntrack() { ovKey = null; if (ovTimer != null) ovTimer.Stop(); }
        void OvTick(object s, EventArgs e)
        {
            if (ovKey == null) return;
            var el = DateTime.UtcNow - ovStart;
            Tuple<TextBlock, TextBlock> r;
            if (ovRows.TryGetValue(ovKey, out r)) r.Item2.Text = L.T("cleaning") + " " + ((int)el.TotalMinutes) + ":" + el.Seconds.ToString("00");
            if (ovSpan > 0) { double f = 1 - Math.Exp(-el.TotalSeconds / ovExpect); OvProgress(ovBase + ovSpan * 0.92 * f); }
        }
        void OvStep(string s) { TB("OvStep").Text = s; TB("StatusText").Text = s; }

        void SetOvItem(string key, ItemState state, string detail)
        {
            Tuple<TextBlock, TextBlock> r;
            if (!ovRows.TryGetValue(key, out r)) return;
            var ic = r.Item1;
            var rot = (RotateTransform)ic.RenderTransform;
            rot.BeginAnimation(RotateTransform.AngleProperty, null);
            if (state != ItemState.Working && key == ovKey) OvUntrack();
            ic.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            switch (state)
            {
                case ItemState.Working:
                    ic.Text = Glyph("E895"); ic.Foreground = (Brush)W.FindResource("CyanBrush");
                    rot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1)) { RepeatBehavior = RepeatBehavior.Forever });
                    r.Item2.Foreground = (Brush)W.FindResource("CyanBrush"); break;
                case ItemState.Done: ic.Text = Glyph("E73E"); ic.Foreground = (Brush)W.FindResource("GoodBrush"); r.Item2.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush"); break;
                case ItemState.Partial: ic.Text = Glyph("E7BA"); ic.Foreground = (Brush)W.FindResource("WarnBrush"); r.Item2.Foreground = (Brush)W.FindResource("WarnBrush"); break;
                case ItemState.Skipped: ic.Text = Glyph("E7BA"); ic.Foreground = (Brush)W.FindResource("BadBrush"); r.Item2.Foreground = (Brush)W.FindResource("BadBrush"); break;
            }
            if (state != ItemState.Working) ic.BeginAnimation(UIElement.OpacityProperty, Anim(0.2, 1, 250));
            r.Item2.Text = detail;
        }

        double countTarget; bool countFiles; DateTime countStart;
        void ShowDone(string title, string sub, double value, bool files, string bigSub, List<Tuple<string, string>> chips, List<Tuple<string, bool>> notes)
        {
            E<Grid>("WorkView").Visibility = Visibility.Collapsed;
            var dv = E<Grid>("DoneView");
            dv.Visibility = Visibility.Visible;
            dv.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, 260));
            TB("DoneTitle").Text = title; TB("DoneSub").Text = sub; TB("DoneBigSub").Text = bigSub;
            var wrap = E<WrapPanel>("DoneChips"); wrap.Children.Clear();
            foreach (var c in chips)
            {
                string fg = c.Item2 == "good" ? "#21C98A" : c.Item2 == "warn" ? "#F5A524" : c.Item2 == "cyan" ? "#22C4F0" : null;
                var b = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(11, 5, 11, 5), Margin = new Thickness(0, 0, 8, 6) };
                var t = new TextBlock { Text = c.Item1, FontSize = 12.5 };
                if (fg != null) { t.Foreground = Hex(fg); b.Background = Hex("#26" + fg.Substring(1)); }
                else { t.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush"); b.SetResourceReference(Border.BackgroundProperty, "GhostBrush"); }
                b.Child = t; wrap.Children.Add(b);
            }
            var np = E<StackPanel>("DoneNotes"); np.Children.Clear();
            if (notes.Count == 0) notes.Add(Tuple.Create(L.T("all_good"), false));
            foreach (var n in notes)
            {
                var g = new Grid { Margin = new Thickness(0, 0, 0, 7) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                var ic = new TextBlock { Text = Glyph(n.Item2 ? "E7BA" : "E946"), FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 12, Margin = new Thickness(0, 2, 0, 0),
                    Foreground = (Brush)W.FindResource(n.Item2 ? "WarnBrush" : "CyanBrush") };
                var tx = new TextBlock { Text = n.Item1, TextWrapping = TextWrapping.Wrap, FontSize = 12.5 };
                tx.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
                Grid.SetColumn(tx, 1); g.Children.Add(ic); g.Children.Add(tx); np.Children.Add(g);
            }
            E<Ellipse>("DoneCircle").BeginAnimation(Shape.StrokeDashOffsetProperty, Anim(51, 0, 600));
            var a2 = Anim(9, 0, 380); a2.BeginTime = TimeSpan.FromMilliseconds(450);
            E<System.Windows.Shapes.Path>("DoneCheck").BeginAnimation(Shape.StrokeDashOffsetProperty, a2);
            countTarget = value; countFiles = files; countStart = DateTime.UtcNow;
            CompositionTarget.Rendering -= CountFrame; CompositionTarget.Rendering += CountFrame;
        }

        void CountFrame(object s, EventArgs e)
        {
            double p = Math.Min(1, (DateTime.UtcNow - countStart).TotalMilliseconds / 1100);
            double v = countTarget * (1 - Math.Pow(1 - p, 3));
            TB("DoneBig").Text = countFiles ? v.ToString("N0") : Fmt.Size(v);
            if (p >= 1) CompositionTarget.Rendering -= CountFrame;
        }

        void HideOverlay()
        {
            OvUntrack();
            StopScene();
            var ov = E<Grid>("Overlay");
            var a = Anim(1, 0, 180);
            a.Completed += (s, e) => { ov.Visibility = Visibility.Collapsed; UpdateStats(); if (currentPage == "PageRam") LoadRam(); Native.TrimSelf(); };
            ov.BeginAnimation(UIElement.OpacityProperty, a);
        }

        // ---- the maintenance animation: a self-contained vector scene (no downloads, seamless loops)
        void StartScene()
        {
            var host = E<ContentControl>("SceneHost");
            try
            {
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Scene.xaml"))
                    host.Content = XamlReader.Load(s);
            }
            catch (Exception ex) { global::Kemas.Log.Error("Scene", ex); }
        }

        void StopScene()
        {
            E<ContentControl>("SceneHost").Content = null;   // drops the scene and its animations from memory
        }

        /// Removes the video that older versions downloaded; KEMAS no longer fetches anything for its animation.
        // Beta badge + live countdown to the official public release (25 Oct 2026, 00:00 Malaysia time).
        void StartBetaCountdown()
        {
            var target = new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.FromHours(8));
            var tb = E<TextBlock>("BetaCountdown"); var pill = E<FrameworkElement>("BetaPill");
            Action tick = null;
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            tick = () =>
            {
                var left = target - DateTimeOffset.Now;
                if (left <= TimeSpan.Zero) { tb.Text = "Official release"; t.Stop(); return; }
                tb.Text = string.Format("{0}d {1:00}:{2:00}:{3:00}", left.Days, left.Hours, left.Minutes, left.Seconds);
            };
            t.Tick += (s, a) => tick(); tick(); t.Start();
        }

        void RemoveLegacyAnimation()
        {
            try { var f = System.IO.Path.Combine(AppInfo.DataDir, "kemas_anim.mp4"); if (File.Exists(f)) File.Delete(f); } catch { }
        }

        // ================================================================ dialog
        Action dialogYes;
        void Ask(string title, string msg, string yes, string icon, bool danger, Action onYes, string checkText = null)
        {
            TB("DialogTitle").Text = title;
            TB("DialogMsg").Text = msg;
            BT("BtnDialogYes").Content = yes;
            BT("BtnDialogNo").Visibility = onYes == null ? Visibility.Collapsed : Visibility.Visible;
            TB("DialogIcon").Text = Glyph(icon);
            E<Border>("DialogBadge").Background = danger ? (Brush)W.FindResource("BadBrush") : (Brush)W.FindResource("AccentGrad");
            var cb = E<CheckBox>("DialogCheck");
            cb.Visibility = checkText == null ? Visibility.Collapsed : Visibility.Visible;
            cb.Content = checkText; cb.IsChecked = false;
            dialogYes = onYes;
            var layer = E<Grid>("DialogLayer");
            layer.Visibility = Visibility.Visible;
            layer.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, 160));
            var sc = (ScaleTransform)E<Border>("DialogCard").RenderTransform;
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(0.92, 1, 260, new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut }));
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(0.92, 1, 260, new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut }));
            BT("BtnDialogYes").Focus();
        }
        bool DialogChecked { get { return E<CheckBox>("DialogCheck").IsChecked == true; } }

        void CloseDialog(bool yes)
        {
            var act = dialogYes; dialogYes = null;
            var checkedState = DialogChecked;
            E<Grid>("DialogLayer").Visibility = Visibility.Collapsed;
            if (yes && act != null) { lastDialogChecked = checkedState; act(); }
        }
        bool lastDialogChecked;

        // ================================================================ organizer
        void BuildOrganizer()
        {
            folders = new ObservableCollection<FolderVM>(S.OrgFolders.Select(f => new FolderVM(f)));
            rules = new ObservableCollection<RuleVM>(S.OrgRules.Select(r => new RuleVM(r)));
            E<ItemsControl>("OrgFolderList").ItemsSource = folders;
            E<ItemsControl>("OrgRuleList").ItemsSource = rules;
            loadingUi = true;
            E<TextBox>("OthersName").Text = S.OrgOthersName;
            loadingUi = false;
            plan = null;
        }

        void OnTemplateClick(object sender, RoutedEventArgs e)
        {
            var fe = e.OriginalSource as FrameworkElement;
            if (fe == null) return;
            string tag = fe.Tag as string;
            if (tag == "removefolder")
            {
                var vm = fe.DataContext as FolderVM;
                if (vm == null) return;
                folders.Remove(vm); S.OrgFolders.Remove(vm.M); plan = null; QueueSave();
            }
            else if (tag == "removerule")
            {
                var vm = fe.DataContext as RuleVM;
                if (vm == null) return;
                rules.Remove(vm); S.OrgRules.Remove(vm.M); plan = null; QueueSave();
            }
            else if (tag == "startup")
            {
                var item = fe.DataContext as StartupItem;
                var cb = fe as CheckBox;
                if (item == null || cb == null) return;
                bool want = !item.Enabled;
                try { Startup.SetEnabled(item, want); Toast(string.Format(want ? L.T("startup_enabled") : L.T("startup_disabled"), item.Name), "E73E"); }
                catch (Exception ex) { Toast(L.T("startup_fail_item"), "E7BA"); global::Kemas.Log.Error("Startup", ex); }
                cb.IsChecked = item.Enabled;
                UpdateStartupSummary();
            }
            else if (tag == "changed") { plan = null; }
        }

        void AddFolder()
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = L.T("pick_folder"), ShowNewFolderButton = false })
            {
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                string p = dlg.SelectedPath;
                if (S.OrgFolders.Any(x => string.Equals(x.Path, p, StringComparison.OrdinalIgnoreCase))) { Toast(L.T("folder_exists"), "E7BA"); return; }
                string root = System.IO.Path.GetPathRoot(p);
                if (string.Equals(root, p, StringComparison.OrdinalIgnoreCase) || p.StartsWith(Cleaner.Win, StringComparison.OrdinalIgnoreCase)
                    || p.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase))
                { Toast(L.T("folder_unsafe"), "E7BA"); return; }
                var f = new OrgFolder(new DirectoryInfo(p).Name, p, true, false);
                S.OrgFolders.Add(f); folders.Add(new FolderVM(f)); plan = null; QueueSave();
            }
        }

        void ExportRules()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { FileName = "KEMAS-organizer-rules.json", Filter = "KEMAS rules (*.json)|*.json", DefaultExt = ".json" };
            if (dlg.ShowDialog(W) != true) return;
            try
            {
                var prof = new OrgProfile { Rules = S.OrgRules, OthersName = S.OrgOthersName, DateFormat = S.OrgDateFormat };
                File.WriteAllText(dlg.FileName, Json.Write(prof), System.Text.Encoding.UTF8);
                Toast(L.T("exported"), "E73E");
            }
            catch (Exception ex) { Toast(L.T("export_fail") + " " + ex.Message, "E7BA"); }
        }

        void ImportRules()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "KEMAS rules (*.json)|*.json|All files|*.*" };
            if (dlg.ShowDialog(W) != true) return;
            try
            {
                var prof = Json.Read<OrgProfile>(File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8));
                if (prof == null || prof.Rules == null || prof.Rules.Count == 0) throw new InvalidDataException(L.T("import_invalid"));
                S.OrgRules = prof.Rules.Where(r => r != null && !string.IsNullOrWhiteSpace(r.Name)).Take(200).ToList();
                if (!string.IsNullOrWhiteSpace(prof.OthersName)) S.OrgOthersName = prof.OthersName;
                S.OrgDateFormat = Math.Max(0, Math.Min(2, prof.DateFormat));
                BuildOrganizer(); LoadSettingsIntoUi(); QueueSave();
                Toast(string.Format(L.T("imported"), S.OrgRules.Count), "E73E");
            }
            catch (Exception ex) { Toast(L.T("import_fail") + " " + ex.Message, "E7BA"); }
        }

        void StartPreview(Action then)
        {
            if (busy) { Toast(L.T("please_wait"), "E916"); return; }
            E<RadioButton>("OrgTabPreview").IsChecked = true;
            TB("OrgSummary").Text = L.T("looking");
            var fl = S.OrgFolders.ToList(); var rl = S.OrgRules.ToList(); string others = S.OrgOthersName; int fmt = S.OrgDateFormat;
            List<PlanItem> result = null;
            Run(() => { result = Organizer.Plan(fl, rl, others, fmt); }, () =>
            {
                plan = result ?? new List<PlanItem>();
                E<DataGrid>("OrgGrid").ItemsSource = plan;
                if (plan.Count == 0) { TB("OrgSummary").Text = L.T("already_tidy"); Log(L.T("already_tidy")); return; }
                var parts = plan.GroupBy(p => p.Folder).Select(g => string.Format(L.T("n_in"), g.Count(), g.Key));
                TB("OrgSummary").Text = string.Format(L.T("will_move"), plan.Count) + "  " + string.Join("  |  ", parts);
                Log(string.Format(L.T("preview_ready"), plan.Count));
                if (then != null) then();
            });
        }

        void RequestOrganize()
        {
            Action ask = () => Ask(L.T("org_q"), string.Format(L.T("org_q_d"), plan.Count), L.T("organize"), "E8B7", false, StartOrganize);
            if (plan == null) StartPreview(() => { if (plan.Count > 0) ask(); }); else if (plan.Count > 0) ask(); else Toast(L.T("already_tidy"), "E73E");
        }

        void StartOrganize()
        {
            var p = plan.ToList();
            var groups = p.GroupBy(x => x.Folder).ToList();
            ShowOverlay(L.T("ov_org"), L.T("ov_org_d"), groups.Select(g => Tuple.Create("org_" + g.Key, string.Format(L.T("n_files"), g.Key, g.Count()))).ToList());
            OrgOutcome res = null;
            var watch = Stopwatch.StartNew();
            Run(() =>
            {
                string lastFolder = null;
                res = Organizer.Run(p, () => cancelFlag, (i, n, item) =>
                {
                    if (item.Folder != lastFolder)
                    {
                        string prev = lastFolder; lastFolder = item.Folder;
                        Ui(() => { if (prev != null) SetOvItem("org_" + prev, ItemState.Done, L.T("done_word")); SetOvItem("org_" + item.Folder, ItemState.Working, L.T("moving")); });
                    }
                    if (i % 3 == 0 || i == n) Ui(() => { OvProgress(i * 100.0 / n); OvStep(L.T("moving") + " " + item.File); });
                });
                if (lastFolder != null) { var lf = lastFolder; Ui(() => SetOvItem("org_" + lf, ItemState.Done, L.T("done_word"))); }
            }, () =>
            {
                if (res == null) res = new OrgOutcome();
                foreach (var g in groups)
                {
                    int bad = res.Skipped.Count(s => g.Any(x => s.StartsWith(x.File + "|")));
                    if (bad > 0) SetOvItem("org_" + g.Key, ItemState.Partial, string.Format(L.T("x_skipped"), bad));
                }
                var notes = res.Skipped.Take(25).Select(s => { var a = s.Split('|'); return Tuple.Create(a[0] + ": " + NoteText(a.Length > 1 ? a[1] : ""), true); }).ToList();
                if (res.Skipped.Count > 25) notes.Add(Tuple.Create(string.Format(L.T("more_skipped"), res.Skipped.Count - 25), true));
                if (cancelFlag) notes.Add(Tuple.Create(L.T("you_stopped"), false));
                if (res.Moved > 0) notes.Add(Tuple.Create(L.T("undo_hint"), false));
                var chips = new List<Tuple<string, string>> { Tuple.Create(string.Format(L.T("chip_moved"), res.Moved), "good") };
                if (res.Skipped.Count > 0) chips.Add(Tuple.Create(string.Format(L.T("chip_skipped"), res.Skipped.Count), "warn"));
                chips.Add(Tuple.Create(watch.Elapsed.TotalSeconds.ToString("0.0") + "s", "plain"));
                ShowDone(res.Skipped.Count > 0 ? L.T("org_done_skips") : L.T("org_done"), L.T("org_done_sub"), res.Moved, true, L.T("files_tidied"), chips, notes);
                plan = null; E<DataGrid>("OrgGrid").ItemsSource = null;
                TB("OrgSummary").Text = string.Format(L.T("organized_n"), res.Moved);
                Log(string.Format(L.T("organized_n"), res.Moved));
            });
        }

        void RequestUndo()
        {
            var f = Organizer.LastUndoFile();
            if (f == null) { Ask(L.T("nothing_undo"), L.T("nothing_undo_d"), "OK", "E946", false, null); return; }
            int n = Organizer.UndoCount(f);
            Ask(L.T("undo_q"), string.Format(L.T("undo_q_d"), n), L.T("undo"), "E7A7", false, () =>
            {
                ShowOverlay(L.T("ov_undo"), L.T("ov_undo_d"), new List<Tuple<string, string>> { Tuple.Create("undo", L.T("restore_item")) });
                OrgOutcome res = null;
                Run(() =>
                {
                    Ui(() => SetOvItem("undo", ItemState.Working, L.T("restoring")));
                    res = Organizer.Undo(f, (i, total) => { if (i % 3 == 0 || i == total) Ui(() => OvProgress(i * 100.0 / total)); });
                }, () =>
                {
                    if (res == null) res = new OrgOutcome();
                    SetOvItem("undo", res.Skipped.Count > 0 ? ItemState.Partial : ItemState.Done, string.Format(L.T("restored_n"), res.Moved));
                    var notes = res.Skipped.Take(25).Select(s => { var a = s.Split('|'); return Tuple.Create(a[0] + ": " + NoteText(a.Length > 1 ? a[1] : ""), true); }).ToList();
                    var chips = new List<Tuple<string, string>> { Tuple.Create(string.Format(L.T("chip_restored"), res.Moved), "good") };
                    if (res.Skipped.Count > 0) chips.Add(Tuple.Create(string.Format(L.T("chip_skipped"), res.Skipped.Count), "warn"));
                    ShowDone(L.T("undo_done"), L.T("undo_done_sub"), res.Moved, true, L.T("files_restored"), chips, notes);
                    Log(string.Format(L.T("restored_n"), res.Moved));
                });
            });
        }

        // ================================================================ memory
        void LoadRam()
        {
            List<ProcRow> rows = null;
            Run(() => { rows = Memory.Groups(); }, () =>
            {
                foreach (var r in rows) r.Status = r.Protected ? L.T("st_protected") : L.T("st_canclose");
                E<DataGrid>("ProcGrid").ItemsSource = rows;
                UpdateStats();
            });
        }

        void StartTrim()
        {
            ShowOverlay(L.T("ov_ram"), L.T("ov_ram_d"), new List<Tuple<string, string>> { Tuple.Create("ram", L.T("free_ram_item")) });
            long gain = 0;
            var watch = Stopwatch.StartNew();
            Run(() =>
            {
                Ui(() => SetOvItem("ram", ItemState.Working, L.T("freeing")));
                gain = Memory.Trim(p => Ui(() => OvProgress(p * 100)));
            }, () =>
            {
                OvProgress(100);
                SetOvItem("ram", ItemState.Done, string.Format(L.T("released_x"), Fmt.Size(gain)));
                ShowDone(L.T("ram_done"), L.T("ram_done_sub"), gain, false, L.T("ram_released"),
                    new List<Tuple<string, string>> { Tuple.Create(watch.Elapsed.TotalSeconds.ToString("0.0") + "s", "plain") },
                    new List<Tuple<string, bool>> { Tuple.Create(L.T("ram_note"), false) });
                Log(string.Format(L.T("released_x"), Fmt.Size(gain)));
            });
        }

        void RequestKill()
        {
            var sel = E<DataGrid>("ProcGrid").SelectedItems.Cast<ProcRow>().ToList();
            if (sel.Count == 0) { Toast(L.T("select_app"), "E7BA"); return; }
            var ok = sel.Where(r => !r.Protected).ToList();
            if (ok.Count == 0) { Ask(L.T("protected_q"), L.T("protected_d"), "OK", "E72E", false, null); return; }
            string names = string.Join(", ", ok.Select(r => r.App + " (" + r.RAM + ")"));
            string msg = string.Format(L.T("kill_d"), names);
            if (ok.Count < sel.Count) msg += "\n\n" + L.T("kill_protected");
            Ask(L.T("kill_q"), msg, L.T("end_apps"), "E7BA", true, () =>
            {
                foreach (var r in ok)
                {
                    int n = Memory.Kill(r.App);
                    Log(n > 0 ? string.Format(L.T("ended"), r.App, r.RAM) : string.Format(L.T("end_fail"), r.App));
                }
                Delay(500, LoadRam);
            });
        }

        // ================================================================ startup apps
        List<StartupItem> startupItems;
        void LoadStartup()
        {
            List<StartupItem> items = null;
            Run(() => { items = Startup.List(); }, () =>
            {
                startupItems = items;
                startupLoaded = true;
                E<ItemsControl>("StartupList").ItemsSource = items;
                UpdateStartupSummary();
            });
        }

        void UpdateStartupSummary()
        {
            if (startupItems == null) return;
            TB("StartupSummary").Text = string.Format(L.T("startup_summary"), startupItems.Count(i => i.Enabled), startupItems.Count);
        }

        // ================================================================ programs
        void LoadApps()
        {
            List<AppRow> rows = null;
            Run(() => { rows = Programs.List(); }, () =>
            {
                allApps = rows; appsLoaded = true;
                FilterApps();
                TB("AppSummary").Text = string.Format(L.T("apps_summary"), rows.Count, Fmt.Size(rows.Sum(a => a.SizeMB) * 1048576));
            });
        }

        void FilterApps()
        {
            if (allApps == null) return;
            string q = E<TextBox>("AppSearch").Text.Trim();
            E<DataGrid>("AppGrid").ItemsSource = q.Length == 0 ? allApps
                : allApps.Where(a => a.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || (a.Publisher ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        void RequestUninstallApp()
        {
            var a = E<DataGrid>("AppGrid").SelectedItem as AppRow;
            if (a == null) { Toast(L.T("select_program"), "E7BA"); return; }
            Ask(string.Format(L.T("uninstall_q"), a.Name), L.T("uninstall_d"), L.T("uninstall"), "E74D", true, () =>
            {
                try { Programs.StartUninstall(a); Log(string.Format(L.T("uninstall_started"), a.Name)); }
                catch (Exception ex) { Toast(L.T("uninstall_fail") + " " + ex.Message, "E7BA"); }
            });
        }

        // ================================================================ updates
        void CheckUpdate(bool manual)
        {
            if (!Updater.Configured) { if (manual) Toast(L.T("update_not_configured"), "E946"); return; }
            TB("UpdateStatus").Text = L.T("update_checking");
            UpdateInfo info = null; Exception err = null;
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try { info = Updater.Check(); } catch (Exception ex) { err = ex; }
                Ui(() =>
                {
                    if (err != null) { TB("UpdateStatus").Text = L.T("update_offline"); if (manual) Toast(L.T("update_offline"), "E7BA"); return; }
                    if (info == null) { TB("UpdateStatus").Text = string.Format(L.T("update_latest"), DateTime.Now.ToString("HH:mm")); if (manual) Toast(L.T("update_none"), "E73E"); return; }
                    pendingUpdate = info;
                    TB("UpdateStatus").Text = string.Format(L.T("update_found"), info.Version);
                    TB("UpdateChipText").Text = "v" + info.Version;
                    var chip = E<Border>("UpdateChip");
                    chip.Visibility = Visibility.Visible;
                    chip.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, 300));
                    if (manual || S.SkippedVersion != info.Version.ToString()) OfferUpdate(info);
                });
            });
        }

        void OfferUpdate(UpdateInfo info)
        {
            string notes = (info.Notes ?? "").Trim();
            if (notes.Length > 600) notes = notes.Substring(0, 597) + "...";
            string msg = string.Format(L.T("update_q_d"), AppInfo.Version, info.Version) + (notes.Length > 0 ? "\n\n" + notes : "");
            Ask(string.Format(L.T("update_q"), info.Version), msg, L.T("update_now"), "E896", false, () =>
            {
                if (lastDialogChecked) { S.SkippedVersion = info.Version.ToString(); QueueSave(); return; }
                ApplyUpdate(info);
            }, L.T("skip_version"));
        }

        void ApplyUpdate(UpdateInfo info)
        {
            ShowOverlay(L.T("ov_update"), string.Format(L.T("ov_update_d"), info.Version), new List<Tuple<string, string>>
                { Tuple.Create("dl", L.T("upd_download")), Tuple.Create("verify", L.T("upd_verify")), Tuple.Create("install", L.T("upd_install")) });
            BT("BtnStop").IsEnabled = false;
            string exe = null;
            Run(() =>
            {
                Ui(() => SetOvItem("dl", ItemState.Working, "0%"));
                exe = Updater.DownloadAndApply(info, p => Ui(() => { OvProgress(p * 90); SetOvItem("dl", ItemState.Working, (p * 100).ToString("0") + "%"); }));
            }, () =>
            {
                if (exe == null)
                {
                    SetOvItem("dl", ItemState.Skipped, L.T("failed"));
                    ShowDone(L.T("update_failed"), L.T("update_failed_d"), 0, true, "", new List<Tuple<string, string>>(), new List<Tuple<string, bool>> { Tuple.Create(L.T("update_failed_note"), true) });
                    return;
                }
                SetOvItem("dl", ItemState.Done, L.T("done_word")); SetOvItem("verify", ItemState.Done, L.T("done_word")); SetOvItem("install", ItemState.Done, L.T("done_word"));
                OvProgress(100); OvStep(L.T("restarting"));
                Installer.UpdateRegistryVersion();
                Delay(900, () =>
                {
                    App.ReleaseMutex();
                    try { Process.Start(new ProcessStartInfo(exe, "--updated") { UseShellExecute = true }); } catch { }
                    QuitRequested = true; W.Close();
                });
            });
        }

        // ================================================================ install / uninstall
        void OfferInstall()
        {
            if (AppInfo.IsStore || Installer.IsInstalledCopy || S.InstallOffered || App.Portable) return;
            S.InstallOffered = true; QueueSave();
            Ask(L.T("install_q"), L.T("install_q_d"), L.T("install"), "E896", false, RequestInstallNow);
        }

        void RequestInstall() { Ask(L.T("install_q"), L.T("install_q_d"), L.T("install"), "E896", false, RequestInstallNow); }

        void RequestInstallNow()
        {
            try
            {
                Installer.Install();
                if (S.StartWithWindows) Tasks.SetStartup(true);
                if (S.AutoClean != "off") Tasks.SetAutoClean(S.AutoClean);
                Ask(L.T("installed_title"), L.T("installed_d"), L.T("open_installed"), "E73E", false, () =>
                {
                    App.ReleaseMutex();
                    try { Process.Start(new ProcessStartInfo(Installer.InstalledExe, "--updated") { UseShellExecute = true }); } catch { }
                    QuitRequested = true; W.Close();
                });
            }
            catch (Exception ex) { Toast(L.T("install_fail") + " " + ex.Message, "E7BA"); global::Kemas.Log.Error("Install", ex); }
        }

        void RequestSelfUninstall()
        {
            Ask(L.T("self_uninstall_q"), L.T("self_uninstall_d"), L.T("uninstall"), "E74D", true, () =>
            {
                Installer.Uninstall(lastDialogChecked);
                QuitRequested = true; W.Close();
            }, L.T("remove_data"));
        }
    }
}
