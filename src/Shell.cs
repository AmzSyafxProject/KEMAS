// KEMAS 4.0 - application shell: category sidebar, compact mode, breadcrumb, grouped search, notifications, category landing pages
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace Kemas
{
    public partial class MainWin
    {
        // ------------------------------------------------------------ information architecture
        class Cat
        {
            public string Id, Glyph, En, Ms, Landing;     // Landing = nav name opened by the header (null = header only toggles)
            public string[] Items;
            public Border Header; public StackPanel Body; public TextBlock Chevron, Label, Icon;
        }
        List<Cat> cats;
        static readonly Dictionary<string, string[]> CoreNav = new Dictionary<string, string[]>
        {
            // nav -> glyph, en, ms, one-line purpose (en), (ms)
            { "NavDash", new[] { "E80F", "Home", "Utama", "", "" } },
            { "NavClean", new[] { "E74D", "Smart Clean", "Pembersihan Pintar", "Temp files, caches, logs and the Recycle Bin, reviewed before removal.", "Fail sementara, cache, log dan Tong Kitar Semula, disemak sebelum dibuang." } },
            { "NavOrg", new[] { "E8B7", "File Organizer", "Penyusun Fail", "Sorts a folder into tidy subfolders by rules, with undo.", "Menyusun folder ke subfolder mengikut peraturan, boleh dibatalkan." } },
            { "NavRam", new[] { "E9D9", "Memory", "Memori", "RAM in use and the apps holding the most of it.", "RAM yang digunakan dan apps yang paling banyak menggunakannya." } },
            { "NavStartup", new[] { "E7E8", "Startup Manager", "Pengurus Permulaan", "Apps that start with Windows and how much they slow boot.", "Apps yang bermula bersama Windows dan kesannya pada but." } },
            { "NavApps", new[] { "E71D", "Software Manager", "Pengurus Perisian", "Installed apps with size, version and uninstall.", "Apps dipasang dengan saiz, versi dan nyahpasang." } },
            { "NavDoctor", new[] { "E9D2", "PC Doctor", "Doktor PC", "Finds problems and the fix for each.", "Mencari masalah dan cara membaikinya." } },
            { "NavMap", new[] { "EDA2", "Storage Map", "Peta Storan", "See what fills your drive, block by block.", "Lihat apa yang memenuhkan cakera anda." } },
            { "NavRecover", new[] { "E777", "Recovery Centre", "Pusat Pemulihan", "Everything KEMAS changed, and what can be undone.", "Semua yang KEMAS ubah, dan apa yang boleh dibatalkan." } },
            { "NavSettings", new[] { "E713", "Settings", "Tetapan", "", "" } },
            { "NavAbout", new[] { "E946", "About", "Perihal", "", "" } },
        };
        static readonly Dictionary<string, string[]> ModDesc = new Dictionary<string, string[]>
        {
            { "Health", new[] { "Your score and what lowers it.", "Skor anda dan apa yang menurunkannya." } },
            { "Timeline", new[] { "How your PC health changed day by day.", "Perubahan kesihatan PC dari hari ke hari." } },
            { "DeepApp", new[] { "Cache folders left by each app.", "Folder cache yang ditinggalkan setiap app." } },
            { "Dupes", new[] { "Identical files in your folders; the original is kept.", "Fail serupa dalam folder anda; yang asal disimpan." } },
            { "Large", new[] { "The biggest files in your profile.", "Fail terbesar dalam profil anda." } },
            { "Empty", new[] { "Folders with nothing inside.", "Folder yang kosong." } },
            { "Residual", new[] { "Data left behind by uninstalled apps.", "Data yang tertinggal dari apps dinyahpasang." } },
            { "Browser", new[] { "Cache of each browser on this PC.", "Cache setiap pelayar di PC ini." } },
            { "Downloads", new[] { "Old and large downloads, never auto-selected.", "Muat turun lama dan besar, tidak dipilih secara automatik." } },
            { "Perf", new[] { "Live CPU and memory with the busiest apps.", "CPU dan memori secara langsung dengan apps paling sibuk." } },
            { "Background", new[] { "Apps running in the background.", "Apps yang berjalan di latar." } },
            { "Game", new[] { "High performance power and fewer distractions.", "Kuasa prestasi tinggi dan kurang gangguan." } },
            { "Profiles", new[] { "Switch power plans and see what changes.", "Tukar pelan kuasa dan lihat perubahannya." } },
            { "Battery", new[] { "Charge, wear and a full battery report.", "Cas, kehausan dan laporan bateri penuh." } },
            { "DiskHealth", new[] { "Drive model, type and Windows health status.", "Model cakera, jenis dan status kesihatan Windows." } },
            { "Privacy", new[] { "Windows settings that share your data.", "Tetapan Windows yang berkongsi data anda." } },
            { "WinHealth", new[] { "Windows Update, Defender, SFC and DISM.", "Windows Update, Defender, SFC dan DISM." } },
            { "Network", new[] { "Adapters, ping and DNS.", "Penyesuai, ping dan DNS." } },
            { "Drivers", new[] { "Installed drivers with version and date.", "Pemacu dipasang dengan versi dan tarikh." } },
            { "SysInfo", new[] { "Windows, CPU, GPU, memory and board.", "Windows, CPU, GPU, memori dan papan induk." } },
            { "Shredder", new[] { "Overwrite files so they cannot be recovered.", "Tindih fail supaya tidak boleh dipulihkan." } },
            { "Restore", new[] { "Windows restore points; create one before big changes.", "Titik pemulihan Windows; cipta sebelum perubahan besar." } },
        };

        string NavTitle(string nav)
        {
            string[] c; if (CoreNav.TryGetValue(nav, out c)) return Tx(c[1], c[2]);
            return ModTitle(nav.Substring(3));
        }
        string NavGlyph(string nav)
        {
            string[] c; if (CoreNav.TryGetValue(nav, out c)) return c[0];
            var m = Mods.FirstOrDefault(x => x[0] == nav.Substring(3)); return m == null ? "E8A5" : m[1];
        }
        string NavDesc(string nav)
        {
            string[] c; if (CoreNav.TryGetValue(nav, out c)) return Tx(c[3], c[4]);
            string[] d; return ModDesc.TryGetValue(nav.Substring(3), out d) ? Tx(d[0], d[1]) : "";
        }
        Cat CatOf(string nav) { return cats == null ? null : cats.FirstOrDefault(c => c.Landing == nav || c.Items.Contains(nav)); }
        void Go(string nav) { var r = E<RadioButton>(nav); if (r.IsChecked == true) ShowPage(nav); else r.IsChecked = true; }

        // ------------------------------------------------------------ sidebar
        void BuildSidebar()
        {
            cats = new List<Cat>
            {
                new Cat { Id = "ov", Glyph = "E95E", En = "Overview", Ms = "Ringkasan", Items = new[] { "NavHealth", "NavDoctor", "NavTimeline" } },
                new Cat { Id = "cl", Glyph = "E74D", En = "Cleanup", Ms = "Pembersihan", Landing = "NavHubClean", Items = new[] { "NavClean", "NavOrg", "NavDeepApp", "NavDupes", "NavLarge", "NavEmpty", "NavResidual", "NavBrowser", "NavDownloads" } },
                new Cat { Id = "pf", Glyph = "EC4A", En = "Performance", Ms = "Prestasi", Landing = "NavHubPerf", Items = new[] { "NavPerf", "NavRam", "NavStartup", "NavBackground", "NavGame", "NavProfiles", "NavBattery" } },
                new Cat { Id = "st", Glyph = "EDA2", En = "Storage", Ms = "Storan", Landing = "NavHubStore", Items = new[] { "NavMap", "NavDiskHealth", "NavApps" } },
                new Cat { Id = "pr", Glyph = "E72E", En = "Privacy & Security", Ms = "Privasi & Keselamatan", Landing = "NavHubPriv", Items = new[] { "NavPrivacy", "NavWinHealth" } },
                new Cat { Id = "ut", Glyph = "E90F", En = "Utilities", Ms = "Utiliti", Landing = "NavHubUtil", Items = new[] { "NavNetwork", "NavDrivers", "NavSysInfo", "NavShredder", "NavRestore" } },
                new Cat { Id = "rc", Glyph = "E777", En = "Recovery", Ms = "Pemulihan", Landing = "NavRecover", Items = new string[0] },
            };
            var stack = E<StackPanel>("NavStack");
            Func<string, RadioButton> take = n => { var r = E<RadioButton>(n); var p = r.Parent as Panel; if (p != null) p.Children.Remove(r); return r; };
            var home = take("NavDash");
            stack.Children.Clear();
            stack.Children.Add(home);
            var open = new HashSet<string>((S.NavOpen ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            foreach (var c in cats)
            {
                var cat = c;
                var g = new Grid { Height = 34 };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                cat.Icon = new TextBlock { Text = Glyph(c.Glyph), FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
                cat.Label = new TextBlock { FontSize = 12.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
                cat.Chevron = new TextBlock { FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0), Padding = new Thickness(6), Background = Brushes.Transparent, Cursor = Cursors.Hand };
                cat.Icon.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
                cat.Label.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
                cat.Chevron.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
                Grid.SetColumn(cat.Label, 1); Grid.SetColumn(cat.Chevron, 2);
                g.Children.Add(cat.Icon); g.Children.Add(cat.Label);
                if (c.Items.Length > 0) g.Children.Add(cat.Chevron);
                cat.Header = new Border { Child = g, CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 0, 4, 0), Margin = new Thickness(0, 10, 0, 2), Background = Brushes.Transparent, Cursor = Cursors.Hand, Focusable = true, FocusVisualStyle = null };
                System.Windows.Automation.AutomationProperties.SetName(cat.Header, c.En);
                cat.Header.MouseEnter += (s, e) => { if (!HeaderActive(cat)) cat.Header.SetResourceReference(Border.BackgroundProperty, "HoverBrush"); };
                cat.Header.MouseLeave += (s, e) => PaintHeaders();
                cat.Chevron.MouseLeftButtonUp += (s, e) => { e.Handled = true; Toggle(cat, cat.Body.Visibility != Visibility.Visible); };
                cat.Header.MouseLeftButtonUp += (s, e) => HeaderClick(cat);
                cat.Header.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter || e.Key == Key.Space) { HeaderClick(cat); e.Handled = true; }
                    else if (e.Key == Key.Right) { Toggle(cat, true); e.Handled = true; }
                    else if (e.Key == Key.Left) { Toggle(cat, false); e.Handled = true; }
                };
                stack.Children.Add(cat.Header);
                cat.Body = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
                foreach (var n in c.Items) cat.Body.Children.Add(take(n));
                stack.Children.Add(cat.Body);
                cat.Body.Visibility = open.Contains(c.Id) ? Visibility.Visible : Visibility.Collapsed;
            }
            if (string.IsNullOrEmpty(S.NavOpen)) cats[0].Body.Visibility = Visibility.Visible;
            RefreshSidebarTexts();
        }
        bool HeaderActive(Cat c) { return c.Landing != null && currentPage == "Page" + c.Landing.Substring(3); }
        void HeaderClick(Cat c)
        {
            if (compact) { Go(c.Landing ?? c.Items[0]); return; }
            if (c.Landing != null) { Go(c.Landing); Toggle(c, true); }
            else Toggle(c, c.Body.Visibility != Visibility.Visible);
        }
        void Toggle(Cat c, bool show)
        {
            if (c.Items.Length == 0) return;
            c.Body.Visibility = show && !compact ? Visibility.Visible : Visibility.Collapsed;
            c.Chevron.Text = Glyph(show ? "E70D" : "E76C");
            if (!compact)
            {
                S.NavOpen = string.Join(",", cats.Where(x => x.Body.Visibility == Visibility.Visible).Select(x => x.Id));
                QueueSave();
                if (show && !S.ReducedMotion) c.Body.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, 160));
            }
        }
        void RefreshSidebarTexts()
        {
            if (cats == null) return;
            foreach (var c in cats)
            {
                c.Label.Text = Tx(c.En, c.Ms).ToUpperInvariant();
                c.Header.ToolTip = compact ? Tx(c.En, c.Ms) : null;
                c.Chevron.Text = Glyph(c.Body.Visibility == Visibility.Visible ? "E70D" : "E76C");
            }
        }
        void PaintHeaders()
        {
            if (cats == null) return;
            var mine = CatOf("Nav" + currentPage.Substring(4));
            foreach (var c in cats)
            {
                bool act = HeaderActive(c), inside = c == mine;
                if (act) c.Header.SetResourceReference(Border.BackgroundProperty, "NavSelBrush"); else c.Header.Background = Brushes.Transparent;
                c.Label.SetResourceReference(TextBlock.ForegroundProperty, act ? "TextBrush" : "MutedBrush");
                if (inside) c.Icon.SetResourceReference(TextBlock.ForegroundProperty, "CyanBrush"); else c.Icon.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
                if (inside && !act) c.Label.SetResourceReference(TextBlock.ForegroundProperty, "CyanBrush");
            }
        }
        /// Called after every page change: keeps the active category open and the breadcrumb right.
        void OnPageShown()
        {
            var c = CatOf("Nav" + currentPage.Substring(4));
            if (c != null && !compact && c.Body.Visibility != Visibility.Visible) Toggle(c, true);
            PaintHeaders();
            UpdateCrumb();
        }

        // ------------------------------------------------------------ compact sidebar
        bool compact;
        void SetCompact(bool on)
        {
            if (cats == null || on == compact) return;
            compact = on;
            var side = E<Border>("SideBar");
            ((Grid)side.Parent).ColumnDefinitions[0].Width = new GridLength(on ? 72 : 248);
            ((DockPanel)side.Child).Margin = on ? new Thickness(10, 18, 10, 16) : new Thickness(16, 18, 16, 16);
            var brand = E<StackPanel>("Brand"); if (brand.Children.Count > 1) brand.Children[1].Visibility = on ? Visibility.Collapsed : Visibility.Visible;
            brand.Margin = on ? new Thickness(8, 4, 0, 24) : new Thickness(6, 4, 0, 30);
            foreach (var n in new[] { "SideStatus", "SideVersion" }) E<FrameworkElement>(n).Visibility = on ? Visibility.Collapsed : Visibility.Visible;
            if (on) E<FrameworkElement>("UpdateChip").Visibility = Visibility.Collapsed;
            var navs = new List<RadioButton> { E<RadioButton>("NavDash"), E<RadioButton>("NavSettings"), E<RadioButton>("NavAbout") };
            foreach (var r in navs)
            {
                r.ClipToBounds = on;
                r.ToolTip = on ? NavTitle(r.Name.Length > 0 ? r.Name : "NavDash") : null;
            }
            foreach (var c in cats)
            {
                c.Label.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
                c.Chevron.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
                c.Header.Margin = on ? new Thickness(0, 4, 0, 2) : new Thickness(0, 10, 0, 2);
                c.Header.Height = on ? 38 : double.NaN;
                if (on) c.Body.Visibility = Visibility.Collapsed;
            }
            if (!on)
            {
                var open = new HashSet<string>((S.NavOpen ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                foreach (var c in cats) c.Body.Visibility = open.Contains(c.Id) ? Visibility.Visible : Visibility.Collapsed;
                OnPageShown();
            }
            RefreshSidebarTexts();
            PaintHeaders();
        }
        void AutoCompact(double width)
        {
            bool want = S.SideCompact || width < 1000;
            if (want != compact) SetCompact(want);
        }

        // ------------------------------------------------------------ top bar: toggle, breadcrumb, bell
        TextBlock crumbCat; Button bell; Border bellDot; Popup notePop, searchPop; StackPanel noteList, searchList;
        readonly List<Tuple<DateTime, string, string>> notes = new List<Tuple<DateTime, string, string>>();
        int unread;

        void BuildTopBar()
        {
            var bar = E<Grid>("TitleBar");
            // sidebar toggle
            var tog = new Button { Style = (Style)W.FindResource("WinBtn"), Content = Glyph("E700"), FontSize = 13, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(10, 0, 0, 0), ToolTip = Tx("Collapse or expand the sidebar", "Kecilkan atau besarkan bar sisi") };
            tog.SetResourceReference(Button.TagProperty, "HoverBrush");
            System.Windows.Automation.AutomationProperties.SetName(tog, "Toggle sidebar");
            tog.Click += (s, e) => { S.SideCompact = !compact; QueueSave(); SetCompact(S.SideCompact || W.ActualWidth < 1000); };
            WindowChrome.SetIsHitTestVisibleInChrome(tog, true);
            bar.Children.Add(tog);
            // breadcrumb: first item becomes the category, clickable
            var crumbs = (StackPanel)TB("Crumb").Parent;
            crumbs.Margin = new Thickness(56, 0, 0, 0);
            crumbs.IsHitTestVisible = true;
            crumbCat = (TextBlock)crumbs.Children[0];
            crumbCat.Cursor = Cursors.Hand;
            WindowChrome.SetIsHitTestVisibleInChrome(crumbCat, true);
            crumbCat.PreviewMouseLeftButtonDown += (s, e) =>
            {
                var c = CatOf("Nav" + currentPage.Substring(4));
                if (c != null && c.Landing != null) { e.Handled = true; Go(c.Landing); }
                else if (c == null && currentPage != "PageDash") { e.Handled = true; Go("NavDash"); }
            };
            // bell with unread dot
            var right = (StackPanel)BT("BtnTheme").Parent;
            var bg = new Grid { VerticalAlignment = VerticalAlignment.Top };
            bell = new Button { Style = (Style)W.FindResource("WinBtn"), Content = Glyph("EA8F"), FontSize = 13, ToolTip = Tx("Notifications", "Pemberitahuan") };
            bell.SetResourceReference(Button.TagProperty, "HoverBrush");
            System.Windows.Automation.AutomationProperties.SetName(bell, "Notifications");
            bellDot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = Hex("#ED626B"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 11, 13, 0), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            bg.Children.Add(bell); bg.Children.Add(bellDot);
            right.Children.Insert(right.Children.IndexOf(BT("BtnTheme")), bg);
            foreach (UIElement el2 in new UIElement[] { bell, BT("BtnTheme"), (UIElement)VisualParentOf(E<TextBox>("SearchBox"), 2) })
                WindowChrome.SetIsHitTestVisibleInChrome(el2, true);
            noteList = new StackPanel();
            notePop = MakePopup(bell, 340, noteList);
            bell.Click += (s, e) => { FillNotes(); notePop.IsOpen = true; unread = 0; bellDot.Visibility = Visibility.Collapsed; };
            // search
            searchList = new StackPanel();
            var sbox = E<TextBox>("SearchBox");
            searchPop = MakePopup((FrameworkElement)VisualParentOf(sbox, 2), 380, searchList);
            searchPop.Placement = PlacementMode.Bottom; searchPop.StaysOpen = true;
            sbox.TextChanged += (s, e) => FillSearch(sbox.Text.Trim());
            sbox.LostKeyboardFocus += (s, e) => W.Dispatcher.BeginInvoke(new Action(() => { if (!searchPop.IsKeyboardFocusWithin && !searchPop.IsMouseOver) searchPop.IsOpen = false; }), DispatcherPriority.Background);
            sbox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { sbox.Text = ""; searchPop.IsOpen = false; Keyboard.ClearFocus(); e.Handled = true; }
                else if (e.Key == Key.Enter) { var first = searchList.Children.OfType<Button>().FirstOrDefault(); if (first != null) first.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled = true; }
                else if (e.Key == Key.Down) { var first = searchList.Children.OfType<Button>().FirstOrDefault(); if (first != null) first.Focus(); e.Handled = true; }
            };
            W.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) != 0) { sbox.Focus(); sbox.SelectAll(); e.Handled = true; }
            };
            W.PreviewMouseDown += (s, e) => { if (searchPop.IsOpen && !searchPop.IsMouseOver && !sbox.IsMouseOver) searchPop.IsOpen = false; };
            W.SizeChanged += (s, e) => AutoCompact(e.NewSize.Width);
            AutoCompact(W.Width);
        }
        static DependencyObject VisualParentOf(DependencyObject d, int up)
        {
            for (int i = 0; i < up && d != null; i++) d = LogicalTreeHelper.GetParent(d) ?? VisualTreeHelper.GetParent(d);
            return d;
        }
        Popup MakePopup(FrameworkElement target, double width, StackPanel content)
        {
            var b = new Border { Width = width, CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Padding = new Thickness(8), Margin = new Thickness(0, 6, 0, 0),
                Child = new ScrollViewer { Content = content, MaxHeight = 440, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
            b.SetResourceReference(Border.BackgroundProperty, "DialogBrush"); b.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
            b.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Opacity = 0.18, Direction = 270 };
            return new Popup { PlacementTarget = target, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, Child = new Border { Padding = new Thickness(0, 0, 16, 20), Child = b }, PopupAnimation = PopupAnimation.Fade };
        }

        /// Records a message for the notification history (toasts call this).
        void Note(string msg, string icon)
        {
            if (icon == "E916" || icon == "E721" || string.IsNullOrEmpty(msg)) return;      // "please wait" and search misses are not worth keeping
            notes.Insert(0, Tuple.Create(DateTime.Now, msg, icon));
            if (notes.Count > 50) notes.RemoveAt(notes.Count - 1);
            if (bellDot != null && (notePop == null || !notePop.IsOpen)) { unread++; bellDot.Visibility = Visibility.Visible; }
        }
        void FillNotes()
        {
            noteList.Children.Clear();
            var head = new Grid { Margin = new Thickness(8, 4, 8, 8) };
            head.Children.Add(Strong(Tx("Notifications", "Pemberitahuan"), 14));
            if (notes.Count > 0)
            {
                var clr = new Button { Style = (Style)W.FindResource("Link"), Content = Tx("Clear", "Kosongkan"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                clr.Click += (s, e) => { notes.Clear(); FillNotes(); };
                head.Children.Add(clr);
            }
            noteList.Children.Add(head);
            if (notes.Count == 0)
            {
                var m = Muted(Tx("Nothing yet. Scan results, cleaning reports and warnings appear here.", "Belum ada. Keputusan imbasan, laporan pembersihan dan amaran akan muncul di sini."), 12.5);
                m.Margin = new Thickness(8, 0, 8, 10); noteList.Children.Add(m); return;
            }
            foreach (var n in notes)
            {
                var g = new Grid { Margin = new Thickness(8, 6, 8, 6) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition());
                var ic = new TextBlock { Text = Glyph(n.Item3), FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 14, Margin = new Thickness(0, 2, 12, 0), VerticalAlignment = VerticalAlignment.Top };
                ic.SetResourceReference(TextBlock.ForegroundProperty, "CyanBrush");
                g.Children.Add(ic);
                var sp = new StackPanel(); Grid.SetColumn(sp, 1);
                sp.Children.Add(Strong(n.Item2, 12.5)); sp.Children.Add(Muted(n.Item1.ToString("HH:mm"), 11));
                g.Children.Add(sp);
                noteList.Children.Add(g);
            }
        }

        // ------------------------------------------------------------ grouped search
        class Hit { public string Group, Title, Sub, Glyph; public Action Do; public string Keys; }
        List<Hit> SearchIndex()
        {
            var l = new List<Hit>();
            Action<string> page = nav =>
            {
                var c = CatOf(nav);
                l.Add(new Hit { Group = "p", Title = NavTitle(nav), Sub = c == null ? "" : Tx(c.En, c.Ms), Glyph = NavGlyph(nav), Do = () => Go(nav), Keys = nav.Substring(3) + " " + ModTitle(nav.Substring(3)) + " " + NavDesc(nav) });
            };
            page("NavDash");
            foreach (var c in cats)
            {
                if (c.Landing != null && c.Landing.StartsWith("NavHub")) { var cc = c; l.Add(new Hit { Group = "p", Title = Tx(c.En, c.Ms), Sub = Tx("Overview", "Ringkasan"), Glyph = c.Glyph, Do = () => Go(cc.Landing), Keys = c.En + " " + c.Ms }); }
                foreach (var n in c.Items) page(n);
                if (c.Landing == "NavRecover") page("NavRecover");
            }
            page("NavSettings"); page("NavAbout");
            Action<string, string, string, string, string> set = (sec, en, ms, keys, glyph) =>
                l.Add(new Hit { Group = "s", Title = Tx(en, ms), Sub = Tx("Settings", "Tetapan"), Glyph = glyph, Keys = keys, Do = () => { Go("NavSettings"); W.Dispatcher.BeginInvoke(new Action(() => E<RadioButton>(sec).IsChecked = true), DispatcherPriority.Loaded); } });
            set("SetCatApp", "Appearance and theme", "Penampilan dan tema", "theme dark light mode language bahasa motion animation tema gelap cerah", "E790");
            set("SetCatGen", "Start with Windows", "Mula bersama Windows", "startup start windows tray close general umum", "E7E8");
            set("SetCatClean", "Cleaning preferences", "Pilihan pembersihan", "clean confirm schedule auto daily weekly temp jadual", "E74D");
            set("SetCatUpd", "Updates", "Kemas kini", "update version kemas kini", "E895");
            set("SetCatHelp", "Help and report a problem", "Bantuan dan lapor masalah", "help support report bug bantuan", "E897");
            set("SetCatData", "Data and history", "Data dan sejarah", "data history export reset sejarah", "E8B7");
            Action<string, string, string, Action, string> act = (en, ms, keys, a, glyph) => l.Add(new Hit { Group = "a", Title = Tx(en, ms), Sub = Tx("Action", "Tindakan"), Glyph = glyph, Keys = keys, Do = a });
            act("Scan for junk files", "Imbas fail sampah", "scan junk clean temp cache sampah imbas", () => { Go("NavClean"); StartScan(); }, "E721");
            act("Check my PC", "Semak PC saya", "check doctor diagnose problem health semak", () => Go("NavDoctor"), "E9D2");
            act("Disable startup apps", "Matikan apps permulaan", "startup boot slow disable impact permulaan", () => Go("NavStartup"), "E7E8");
            act("Free up memory", "Kosongkan memori", "ram memory free slow memori", () => Go("NavRam"), "E9D9");
            act("Find duplicate files", "Cari fail pendua", "duplicate copy same pendua", () => Go("NavDupes"), "E8C8");
            act("Switch light or dark mode", "Tukar mod cerah atau gelap", "theme dark light mode tema", () => BT("BtnTheme").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), "E793");
            act("Create a restore point", "Cipta titik pemulihan", "restore point backup pemulihan", () => Go("NavRestore"), "E777");
            act("Undo a change", "Batalkan perubahan", "undo recover history batal", () => Go("NavRecover"), "E7A7");
            return l;
        }
        void FillSearch(string q)
        {
            TB("SearchHint").Visibility = q.Length == 0 && E<TextBox>("SearchBox").Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            searchList.Children.Clear();
            if (q.Length == 0) { searchPop.IsOpen = false; return; }
            var words = q.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var hits = SearchIndex().Where(h => words.All(w => (h.Title + " " + h.Sub + " " + h.Keys).ToLowerInvariant().Contains(w)))
                .OrderBy(h => h.Title.ToLowerInvariant().StartsWith(words[0]) ? 0 : 1).ToList();
            if (hits.Count == 0)
            {
                var m = Muted(string.Format(Tx("No results for \"{0}\".", "Tiada hasil untuk \"{0}\"."), q), 12.5); m.Margin = new Thickness(10, 8, 10, 10);
                searchList.Children.Add(m);
            }
            foreach (var grp in new[] { "p", "s", "a" })
            {
                var g = hits.Where(h => h.Group == grp).Take(6).ToList(); if (g.Count == 0) continue;
                var lbl = new TextBlock { Text = grp == "p" ? Tx("PAGES", "HALAMAN") : grp == "s" ? Tx("SETTINGS", "TETAPAN") : Tx("ACTIONS", "TINDAKAN"), FontSize = 10.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(10, 8, 0, 4) };
                lbl.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
                searchList.Children.Add(lbl);
                foreach (var h in g) searchList.Children.Add(HitRow(h));
            }
            searchPop.IsOpen = true;
        }
        Button HitRow(Hit h)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(GradTile(h.Glyph, h.Group == "a" ? Palette[2] : h.Group == "s" ? Palette[1] : Palette[0], 28, 12));
            var t = Strong(h.Title, 13); t.Margin = new Thickness(12, 0, 8, 0); t.VerticalAlignment = VerticalAlignment.Center; t.TextWrapping = TextWrapping.NoWrap; t.TextTrimming = TextTrimming.CharacterEllipsis; Grid.SetColumn(t, 1); g.Children.Add(t);
            var s = Muted(h.Sub, 11.5); s.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(s, 2); g.Children.Add(s);
            var b = new Button { Content = g, Style = (Style)W.FindResource("Row"), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            b.Click += (sn, e) => { searchPop.IsOpen = false; E<TextBox>("SearchBox").Text = ""; Keyboard.ClearFocus(); h.Do(); };
            return b;
        }

        // ------------------------------------------------------------ landing pages
        void ShowHub(string id)
        {
            switch (id)
            {
                case "HubClean": HubClean(); break;
                case "HubPerf": HubPerf(); break;
                case "HubStore": HubStore(); break;
                case "HubPriv": HubPriv(); break;
                case "HubUtil": HubUtil(); break;
            }
        }
        /// Card that opens a tool: icon, name, purpose, live status.
        Border ToolCard(string nav, string status, string statusKind = "")
        {
            var g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var top = new Grid();
            int pi = Math.Abs(nav.GetHashCode()) % Palette.Length;
            top.Children.Add(new Border { HorizontalAlignment = HorizontalAlignment.Left, Child = GradTile(NavGlyph(nav), Palette[pi], 42, 17) });
            if (!string.IsNullOrEmpty(status))
            {
                var c = (Color)ColorConverter.ConvertFromString(statusKind == "good" ? "#27B77A" : statusKind == "warn" ? "#E0961F" : statusKind == "bad" ? "#ED626B" : "#788195");
                top.Children.Add(new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 3, 10, 3), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, MaxWidth = 170,
                    Background = new SolidColorBrush(Color.FromArgb(0x22, c.R, c.G, c.B)),
                    Child = new TextBlock { Text = status, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(c), TextTrimming = TextTrimming.CharacterEllipsis } });
            }
            g.Children.Add(top);
            var t = Strong(NavTitle(nav), 15); t.Margin = new Thickness(0, 14, 0, 4); Grid.SetRow(t, 1); g.Children.Add(t);
            var d = Muted(NavDesc(nav), 12.5); d.MinHeight = 34; Grid.SetRow(d, 2); g.Children.Add(d);
            var open = new TextBlock { Text = Tx("Open", "Buka") + "  " + Glyph("E72A"), FontSize = 12.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 0) };
            open.SetResourceReference(TextBlock.ForegroundProperty, "CyanBrush");
            Grid.SetRow(open, 3); g.Children.Add(open);
            var card = CardOf(g, 18); card.Margin = new Thickness(0, 0, 12, 12); card.Cursor = Cursors.Hand; card.Focusable = true;
            System.Windows.Automation.AutomationProperties.SetName(card, NavTitle(nav));
            card.MouseEnter += (s, e) => card.SetResourceReference(Border.BorderBrushProperty, "CyanBrush");
            card.MouseLeave += (s, e) => card.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
            card.MouseLeftButtonUp += (s, e) => Go(nav);
            card.KeyDown += (s, e) => { if (e.Key == Key.Enter || e.Key == Key.Space) Go(nav); };
            return card;
        }
        /// Responsive grid: 3 columns on wide windows, 2 on medium, 1 on narrow.
        UniformGrid Flow(int max, params UIElement[] items)
        {
            var u = new UniformGrid { Columns = max, Margin = new Thickness(0, 0, -12, 0) };
            foreach (var i in items) u.Children.Add(i);
            u.SizeChanged += (s, e) => { int c = e.NewSize.Width >= 1080 ? max : e.NewSize.Width >= 640 ? Math.Min(2, max) : 1; if (u.Columns != c) u.Columns = c; };
            return u;
        }
        TextBlock Section(string t)
        {
            var tb = new TextBlock { Text = t, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 12) };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush"); return tb;
        }
        void HubBody(params UIElement[] parts)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 8, 16) };
            foreach (var p in parts) if (p != null) sp.Children.Add(p);
            ModBody(new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, -8, 0) });
        }
        /// Big-number tile with a handle on its value so landing pages can update it live.
        Border LiveTile(string label, string glyph, string[] pal, out TextBlock value, out TextBlock sub)
        {
            var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(GradTile(glyph, pal, 44, 18));
            var sp = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(sp, 1); row.Children.Add(sp);
            var l = new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.SemiBold }; l.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            value = new TextBlock { Text = "…", FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis }; value.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            sub = Muted("", 11.5); sub.TextTrimming = TextTrimming.CharacterEllipsis; sub.TextWrapping = TextWrapping.NoWrap;
            sp.Children.Add(l); sp.Children.Add(value); sp.Children.Add(sub);
            var c = CardOf(row, 18); c.Margin = new Thickness(0, 0, 12, 12); return c;
        }
        UIElement RecentFor(params string[] kinds)
        {
            var items = History.Load().Where(h => kinds.Length == 0 || kinds.Contains(h.Kind)).Take(4).ToList();
            var sp = new StackPanel();
            if (items.Count == 0) { var m = Muted(Tx("No activity yet. Finished operations appear here and in Recovery Centre.", "Belum ada aktiviti. Operasi yang selesai muncul di sini dan dalam Pusat Pemulihan."), 12.5); m.Margin = new Thickness(0, 6, 0, 6); sp.Children.Add(m); }
            foreach (var h in items)
            {
                DateTime t; string when = DateTime.TryParse(h.Time, out t) ? t.ToString("d MMM, HH:mm") : "";
                sp.Children.Add(CheckRow(h.Title, (h.Detail ?? "") + (when.Length > 0 ? "  ·  " + when : ""), true));
            }
            var g = new Grid(); g.Children.Add(sp);
            var all = new Button { Style = (Style)W.FindResource("Link"), Content = Tx("Open Recovery Centre", "Buka Pusat Pemulihan"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0) };
            all.Click += (s, e) => Go("NavRecover");
            var outer = new StackPanel(); outer.Children.Add(sp); outer.Children.Add(all);
            return CardOf(outer, 20);
        }
        static string[] P(int i) { return Palette[i % Palette.Length]; }

        void HubClean()
        {
            var scan = Btn(Tx("Scan for junk", "Imbas sampah"), true, () => { Go("NavClean"); StartScan(); });
            ModBegin("HubClean", Tx("Find files you do not need and remove them safely. Every cleaner shows you the list before anything is removed.", "Cari fail yang tidak diperlukan dan buang dengan selamat. Setiap pembersih menunjukkan senarai sebelum apa-apa dibuang."), scan);
            TextBlock v1, s1, v2, s2, v3, s3, v4, s4;
            var t1 = LiveTile(Tx("JUNK FOUND", "SAMPAH DIJUMPAI"), "E74D", P(0), out v1, out s1);
            var t2 = LiveTile(Tx("LAST CLEAN", "PEMBERSIHAN TERAKHIR"), "E81C", P(2), out v2, out s2);
            var t3 = LiveTile(Tx("TOTAL FREED", "JUMLAH DIKOSONGKAN"), "E896", P(4), out v3, out s3);
            var t4 = LiveTile(Tx("CLEANS RUN", "PEMBERSIHAN DIBUAT"), "E73E", P(1), out v4, out s4);
            bool have = scanned && junkTotal >= 0;
            v1.Text = have ? Fmt.Size(junkTotal) : "—"; s1.Text = have ? Tx("ready to review in Smart Clean", "sedia disemak dalam Pembersihan Pintar") : Tx("not scanned yet", "belum diimbas");
            v2.Text = S.LastFreed > 0 ? Fmt.Size((long)S.LastFreed) : "—"; s2.Text = string.IsNullOrEmpty(S.LastClean) ? Tx("never", "belum pernah") : S.LastClean;
            v3.Text = Fmt.Size((long)S.TotalFreed); s3.Text = Tx("since you installed KEMAS", "sejak KEMAS dipasang");
            v4.Text = S.CleanCount.ToString(); s4.Text = Tx("times", "kali");
            var tools = new List<UIElement> { ToolCard("NavClean", have ? Fmt.Size(junkTotal) : Tx("Not scanned", "Belum diimbas"), have && junkTotal > 100L * 1048576 ? "warn" : have ? "good" : "") };
            foreach (var n in new[] { "NavDeepApp", "NavDupes", "NavLarge", "NavEmpty", "NavResidual", "NavBrowser", "NavDownloads" }) tools.Add(ToolCard(n, Tx("Scans when opened", "Imbas bila dibuka")));
            tools.Insert(1, ToolCard("NavOrg", S.OrgFolders == null || S.OrgFolders.Count == 0 ? Tx("No folders yet", "Belum ada folder") : string.Format(Tx("{0} folders", "{0} folder"), S.OrgFolders.Count)));
            HubBody(Flow(4, t1, t2, t3, t4), Section(Tx("Cleaning tools", "Alat pembersihan")), Flow(3, tools.ToArray()), Section(Tx("Recent cleaning", "Pembersihan terkini")), RecentFor("clean", "autoclean", "organize"));
        }

        void HubPerf()
        {
            ModBegin("HubPerf", Tx("What is using your PC right now, and the tools to keep it quick. Nothing is closed or changed without asking you.", "Apa yang menggunakan PC anda sekarang, dan alat untuk kekalkan kelajuan. Tiada apa ditutup atau diubah tanpa kebenaran anda."),
                Btn(Tx("Open Performance Monitor", "Buka Pemantau Prestasi"), true, () => Go("NavPerf")));
            TextBlock cpuV, cpuS, ramV, ramS, stV, stS, topV, topS;
            var t1 = LiveTile("CPU", "E950", P(0), out cpuV, out cpuS);
            var t2 = LiveTile("RAM", "E9D9", P(1), out ramV, out ramS);
            var t3 = LiveTile(Tx("STARTUP APPS", "APPS PERMULAAN"), "E7E8", P(3), out stV, out stS);
            var t4 = LiveTile(Tx("HEAVIEST APP", "APP PALING BERAT"), "E7C4", P(2), out topV, out topS);
            cpuS.Text = string.Format(L.T("cores"), Environment.ProcessorCount);
            var tools = new UIElement[] { ToolCard("NavPerf", Tx("Live", "Langsung"), "good"), ToolCard("NavRam", ""), ToolCard("NavStartup", ""), ToolCard("NavBackground", ""), ToolCard("NavGame", ""), ToolCard("NavProfiles", ""), ToolCard("NavBattery", "") };
            HubBody(Flow(4, t1, t2, t3, t4), Section(Tx("Performance tools", "Alat prestasi")), Flow(3, tools));
            Action tick = () =>
            {
                int c = Cpu.Sample(); if (c >= 0) cpuV.Text = c + "%";
                double total, free; int pct; Memory.Info(out total, out free, out pct);
                ramV.Text = pct + "%"; ramS.Text = Fmt.Size((long)(total - free)) + " / " + Fmt.Size((long)total);
            };
            tick();
            modTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) }; modTimer.Tick += (s, e) => tick(); modTimer.Start();
            Task.Factory.StartNew(() =>
            {
                int on = -1, all = 0; string top = null; long topB = 0;
                try { var l = Startup.List(); all = l.Count; on = l.Count(x => x.Enabled); } catch { }
                try
                {
                    var ps = Process.GetProcesses(); int me = Process.GetCurrentProcess().Id;
                    foreach (var g in ps.Where(p => p.Id > 4 && p.Id != me).GroupBy(p => { try { return p.ProcessName; } catch { return "?"; } }))
                    {
                        long sum = 0; foreach (var p in g) try { sum += p.WorkingSet64; } catch { }
                        if (sum > topB && !string.Equals(g.Key, "Memory Compression", StringComparison.OrdinalIgnoreCase)) { topB = sum; top = g.Key; }
                    }
                    foreach (var p in ps) try { p.Dispose(); } catch { }
                }
                catch { }
                Ui(() =>
                {
                    stV.Text = on >= 0 ? on.ToString() : "—"; stS.Text = on >= 0 ? string.Format(Tx("enabled of {0}", "dihidupkan daripada {0}"), all) : Tx("could not read", "tidak dapat dibaca");
                    topV.Text = top ?? "—"; topS.Text = top == null ? "" : Fmt.Size(topB) + Tx(" of memory", " memori");
                });
            });
        }

        void HubStore()
        {
            ModBegin("HubStore", Tx("How full your drives are and what fills them. Open Storage Map to explore folder by folder.", "Sejauh mana cakera anda penuh dan apa yang memenuhkannya. Buka Peta Storan untuk teroka folder demi folder."),
                Btn(Tx("Open Storage Map", "Buka Peta Storan"), true, () => Go("NavMap")));
            var drives = new List<DriveInfo>();
            try { drives = DriveInfo.GetDrives().Where(d => { try { return d.IsReady && d.DriveType == DriveType.Fixed; } catch { return false; } }).ToList(); } catch { }
            var sys = drives.FirstOrDefault(d => d.Name.Equals(Path.GetPathRoot(Environment.SystemDirectory), StringComparison.OrdinalIgnoreCase)) ?? drives.FirstOrDefault();
            UIElement top = null;
            if (sys != null)
            {
                long used = sys.TotalSize - sys.AvailableFreeSpace; double pct = used * 100.0 / sys.TotalSize;
                var hg = new Grid(); hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) }); hg.ColumnDefinitions.Add(new ColumnDefinition());
                hg.Children.Add(DonutCard(pct, pct >= 90 ? new[] { "#ED626B", "#F48FB1" } : pct >= 75 ? new[] { "#F4AE43", "#ED8F5B" } : new[] { "#3978F6", "#43C6C0" }, Math.Round(pct) + "%", Tx("used", "digunakan"),
                    string.Format(Tx("{0} free on {1}", "{0} kosong di {1}"), Fmt.Size(sys.AvailableFreeSpace), sys.Name.TrimEnd('\\')),
                    pct >= 90 ? Tx("Your system drive is almost full. Windows needs free space for updates.", "Cakera sistem hampir penuh. Windows perlukan ruang untuk kemas kini.") : pct >= 75 ? Tx("Getting full. A cleanup soon keeps Windows comfortable.", "Semakin penuh. Pembersihan nanti akan membantu Windows.") : Tx("Plenty of room for Windows and updates.", "Banyak ruang untuk Windows dan kemas kini.")));
                var st = Stats(1, Stat(Tx("USED", "DIGUNAKAN"), Fmt.Size(used), "", "#3978F6"), Stat(Tx("CAPACITY", "KAPASITI"), Fmt.Size(sys.TotalSize), sys.DriveFormat, "#43C6C0"));
                Grid.SetColumn(st, 1); hg.Children.Add(st);
                top = hg;
            }
            var list = new StackPanel();
            foreach (var d in drives)
            {
                long used = d.TotalSize - d.AvailableFreeSpace; double pct = used * 100.0 / d.TotalSize;
                var g = new Grid { Margin = new Thickness(0, 8, 0, 8) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
                string lbl = ""; try { lbl = d.VolumeLabel; } catch { }
                var nm = Strong(d.Name.TrimEnd('\\') + (string.IsNullOrEmpty(lbl) ? "" : "  " + lbl), 13.5); nm.VerticalAlignment = VerticalAlignment.Center; g.Children.Add(nm);
                var pb = new ProgressBar { Value = pct, Height = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
                if (pct >= 90) pb.Foreground = (Brush)W.FindResource("BadBrush"); else if (pct >= 75) pb.Foreground = (Brush)W.FindResource("WarnBrush");
                Grid.SetColumn(pb, 1); g.Children.Add(pb);
                var tx = Muted(Fmt.Size(used) + " / " + Fmt.Size(d.TotalSize) + "  (" + Math.Round(pct) + "%)", 12.5); tx.VerticalAlignment = VerticalAlignment.Center; tx.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(tx, 2); g.Children.Add(tx);
                list.Children.Add(g);
            }
            if (drives.Count == 0) list.Children.Add(Muted(Tx("No fixed drives could be read.", "Tiada cakera tetap dapat dibaca."), 12.5));
            var tools = new UIElement[] { ToolCard("NavMap", ""), ToolCard("NavDiskHealth", ""), ToolCard("NavApps", ""), ToolCard("NavLarge", ""), ToolCard("NavDupes", "") };
            HubBody(top, Section(Tx("Drives", "Cakera")), CardOf(list, 20), Section(Tx("Storage tools", "Alat storan")), Flow(3, tools));
        }

        void HubPriv()
        {
            var l = PrivacyChecks(); int sc = PrivacyScore(l);
            ModBegin("HubPriv", Tx("Windows settings that share your data, and the health of Windows itself. KEMAS does not replace Windows Security or your antivirus.", "Tetapan Windows yang berkongsi data anda, dan kesihatan Windows sendiri. KEMAS tidak menggantikan Windows Security atau antivirus anda."),
                Btn(Tx("Run privacy audit", "Jalankan audit privasi"), true, () => Go("NavPrivacy")));
            var hg = new Grid(); hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) }); hg.ColumnDefinitions.Add(new ColumnDefinition());
            hg.Children.Add(DonutCard(sc, new[] { "#43C6C0", "#27B77A" }, sc.ToString(), Tx("privacy", "privasi"), sc >= 70 ? Tx("Your privacy is well protected", "Privasi anda terlindung baik") : Tx("Some settings share data", "Sesetengah tetapan berkongsi data"),
                string.Format(Tx("{0} of {1} settings are private.", "{0} daripada {1} tetapan adalah peribadi."), l.Count(x => x.Item3), l.Count)));
            bool admin = IsAdmin();
            var st = Stats(1, Stat(Tx("TO REVIEW", "PERLU DISEMAK"), l.Count(x => !x.Item3).ToString(), Tx("settings", "tetapan"), "#F4AE43"),
                Stat(Tx("PERMISSIONS", "KEBENARAN"), admin ? Tx("Administrator", "Pentadbir") : Tx("Standard", "Biasa"), admin ? Tx("all checks available", "semua semakan tersedia") : Tx("SFC and DISM need administrator", "SFC dan DISM perlukan pentadbir"), "#8B78F6"));
            Grid.SetColumn(st, 1); hg.Children.Add(st);
            var review = new StackPanel();
            foreach (var c in l.Where(x => !x.Item3)) { var u = c.Item4; review.Children.Add(CheckRow(c.Item1, c.Item2, false, Tx("Open setting", "Buka tetapan"), () => OpenUri(u))); }
            if (review.Children.Count == 0) review.Children.Add(CheckRow(Tx("Nothing to review", "Tiada yang perlu disemak"), Tx("Every checked setting keeps your data private.", "Setiap tetapan yang disemak menjaga data anda."), true));
            var tools = new UIElement[] { ToolCard("NavPrivacy", sc + "/100", sc >= 70 ? "good" : "warn"), ToolCard("NavWinHealth", admin ? "" : Tx("Partly needs admin", "Sebahagian perlu pentadbir"), admin ? "" : "warn"), ToolCard("NavBrowser", ""), ToolCard("NavShredder", "") };
            HubBody(hg, Section(Tx("Needs your attention", "Perlu perhatian anda")), CardOf(review, 20), Section(Tx("Privacy and security tools", "Alat privasi dan keselamatan")), Flow(3, tools));
        }

        void HubUtil()
        {
            ModBegin("HubUtil", Tx("Windows tools gathered in one place. Each one explains what it does before it changes anything.", "Alat Windows dikumpul di satu tempat. Setiap satu menerangkan fungsinya sebelum mengubah apa-apa."));
            bool net = false; try { net = NetworkInterface.GetIsNetworkAvailable(); } catch { }
            bool admin = IsAdmin();
            var tools = new UIElement[]
            {
                ToolCard("NavNetwork", net ? Tx("Online", "Dalam talian") : Tx("Offline", "Luar talian"), net ? "good" : "bad"),
                ToolCard("NavDrivers", Tx("Read only", "Baca sahaja")),
                ToolCard("NavSysInfo", Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"),
                ToolCard("NavShredder", Tx("Permanent", "Kekal"), "bad"),
                ToolCard("NavRestore", admin ? Tx("Ready", "Sedia") : Tx("Needs admin", "Perlu pentadbir"), admin ? "good" : "warn"),
            };
            HubBody(Flow(3, tools));
        }
    }
}
