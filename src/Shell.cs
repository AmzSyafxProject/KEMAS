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
            { "Network", new[] { "Check network adapters, connection, ping and DNS.", "Semak penyesuai rangkaian, sambungan, ping dan DNS." } },
            { "Drivers", new[] { "View installed drivers and their versions.", "Lihat pemacu yang dipasang dan versinya." } },
            { "SysInfo", new[] { "View Windows version, CPU, GPU, memory and system details.", "Lihat versi Windows, CPU, GPU, memori dan butiran sistem." } },
            { "Shredder", new[] { "Securely delete selected files with explicit confirmation.", "Padam fail terpilih dengan selamat selepas pengesahan." } },
            { "Restore", new[] { "Manage Windows restore points and create one before major changes.", "Urus titik pemulihan Windows dan cipta satu sebelum perubahan besar." } },
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
        Dictionary<string, string> Hidden = new Dictionary<string, string>();
        Cat CatOf(string nav)
        {
            if (cats == null) return null;
            string h; if (Hidden.TryGetValue(nav, out h)) return cats.First(c => c.Id == h);
            return cats.FirstOrDefault(c => c.Landing == nav || c.Items.Contains(nav));
        }
        void Go(string nav) { var r = E<RadioButton>(nav); if (r.IsChecked == true) ShowPage(nav); else r.IsChecked = true; }

        // ------------------------------------------------------------ sidebar
        void BuildSidebar()
        {
            cats = new List<Cat>
            {
                new Cat { Id = "ov", Glyph = "E95E", En = "Overview", Ms = "Ringkasan", Items = new[] { "NavHealth", "NavDoctor" } },
                new Cat { Id = "cl", Glyph = "E74D", En = "Cleanup", Ms = "Pembersihan", Landing = "NavHubClean", Items = new[] { "NavHubClean", "NavClean", "NavDeepApp", "NavDupes", "NavLarge" } },
                new Cat { Id = "pf", Glyph = "EC4A", En = "Performance", Ms = "Prestasi", Landing = "NavHubPerf", Items = new[] { "NavPerf", "NavStartup", "NavBackground", "NavGame" } },
                new Cat { Id = "st", Glyph = "EDA2", En = "Storage", Ms = "Storan", Landing = "NavHubStore", Items = new[] { "NavMap", "NavDiskHealth", "NavApps" } },
                new Cat { Id = "pr", Glyph = "E72E", En = "Privacy & Security", Ms = "Privasi & Keselamatan", Landing = "NavHubPriv", Items = new[] { "NavPrivacy", "NavBrowser", "NavWinHealth" } },
                new Cat { Id = "ut", Glyph = "E90F", En = "Utilities", Ms = "Utiliti", Landing = "NavHubUtil", Items = new[] { "NavNetwork", "NavDrivers", "NavSysInfo", "NavShredder" } },
                new Cat { Id = "rc", Glyph = "E777", En = "Recovery", Ms = "Pemulihan", Items = new[] { "NavRecover", "NavRestore" } },
            };
            // pages reached from a category overview rather than the sidebar
            Hidden = new Dictionary<string, string> { { "NavOrg", "cl" }, { "NavEmpty", "cl" }, { "NavResidual", "cl" }, { "NavDownloads", "cl" }, { "NavRam", "pf" }, { "NavProfiles", "pf" }, { "NavBattery", "pf" }, { "NavTimeline", "ov" } };
            var stack = E<StackPanel>("NavStack");
            Func<string, RadioButton> take = n => { var r = E<RadioButton>(n); var p = r.Parent as Panel; if (p != null) p.Children.Remove(r); return r; };
            var home = take("NavDash");
            stack.Children.Clear();
            stack.Children.Add(home);
            var open = new HashSet<string>((S.NavOpen ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            foreach (var c in cats)
            {
                var cat = c;
                var g = new Grid { Height = 42 };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                cat.Icon = new TextBlock { Text = Glyph(c.Glyph), FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 17.5, Width = 28, VerticalAlignment = VerticalAlignment.Center };
                cat.Label = new TextBlock { FontSize = 15, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
                cat.Chevron = new TextBlock { FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0), Padding = new Thickness(6), Background = Brushes.Transparent, Cursor = Cursors.Hand };
                cat.Icon.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
                cat.Label.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
                cat.Chevron.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
                Grid.SetColumn(cat.Label, 1); Grid.SetColumn(cat.Chevron, 2);
                g.Children.Add(cat.Icon); g.Children.Add(cat.Label);
                if (c.Items.Length > 0) g.Children.Add(cat.Chevron);
                cat.Header = new Border { Child = g, CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 0, 4, 0), Margin = new Thickness(0, 0, 0, 2), Background = Brushes.Transparent, Cursor = Cursors.Hand, Focusable = true, FocusVisualStyle = null };
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
                cat.Body = new StackPanel { Margin = new Thickness(14, 0, 0, 4) };
                foreach (var n in c.Items) cat.Body.Children.Add(take(n));
                stack.Children.Add(cat.Body);
                cat.Body.Visibility = open.Contains(c.Id) ? Visibility.Visible : Visibility.Collapsed;
            }
            RefreshSidebarTexts();
        }
        bool HeaderActive(Cat c) { return c.Landing != null && !c.Items.Contains(c.Landing) && currentPage == "Page" + c.Landing.Substring(3); }
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
                c.Label.Text = Tx(c.En, c.Ms);
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
                // overview page open = solid brand blue like a selected item; a page inside = label lit, the item itself carries the fill
                bool fill = act || (compact && inside);
                if (fill) c.Header.SetResourceReference(Border.BackgroundProperty, "NavActiveBrush"); else c.Header.Background = Brushes.Transparent;
                foreach (var t in new[] { c.Label, c.Icon, c.Chevron })
                {
                    if (fill) t.Foreground = Brushes.White;
                    else t.SetResourceReference(TextBlock.ForegroundProperty, inside ? "TextBrush" : "MutedBrush");
                }
                c.Label.FontWeight = fill || inside ? FontWeights.SemiBold : FontWeights.Normal;

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
                c.Header.Margin = new Thickness(0, 0, 0, 2);
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
            var tog = new Button { Style = (Style)W.FindResource("WinBtn"), Content = Glyph("E700"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0), FontSize = 16, ToolTip = Tx("Collapse or expand the sidebar", "Kecilkan atau besarkan bar sisi") };
            tog.SetResourceReference(Button.TagProperty, "HoverBrush");
            System.Windows.Automation.AutomationProperties.SetName(tog, "Toggle sidebar");
            tog.Click += (s, e) => { S.SideCompact = !compact; QueueSave(); SetCompact(S.SideCompact || W.ActualWidth < 1000); };
            WindowChrome.SetIsHitTestVisibleInChrome(tog, true);
            bar.Children.Add(tog);
            // breadcrumb: first item becomes the category, clickable
            var crumbs = (StackPanel)TB("Crumb").Parent;
            crumbs.Margin = new Thickness(66, 0, 0, 0);
            foreach (var t in crumbs.Children.OfType<TextBlock>()) t.FontSize = 15;
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
            var bg = new Grid { VerticalAlignment = VerticalAlignment.Center };
            bell = new Button { Style = (Style)W.FindResource("WinBtn"), Content = Glyph("EA8F"), FontSize = 16, ToolTip = Tx("Notifications", "Pemberitahuan") };
            bell.SetResourceReference(Button.TagProperty, "HoverBrush");
            System.Windows.Automation.AutomationProperties.SetName(bell, "Notifications");
            bellDot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = Hex("#ED626B"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 7, 11, 0), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            bg.Children.Add(bell); bg.Children.Add(bellDot);
            right.Children.Insert(right.Children.IndexOf(BT("BtnTheme")), bg);
            foreach (UIElement el2 in new UIElement[] { bell, BT("BtnTheme"), (UIElement)VisualParentOf(E<TextBox>("SearchBox"), 2) })
                WindowChrome.SetIsHitTestVisibleInChrome(el2, true);
            noteList = new StackPanel();
            notePop = MakePopup(bell, 420, noteList);
            notePop.Placement = PlacementMode.Bottom; notePop.HorizontalOffset = -360;
            bell.Click += (s, e) => { FillNotes(); notePop.IsOpen = true; };
            // search
            searchList = new StackPanel();
            var sbox = E<TextBox>("SearchBox");
            searchPop = MakePopup((FrameworkElement)VisualParentOf(sbox, 2), 460, searchList);
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

        class NoteItem { public DateTime T; public string Cat, Title, Desc, Icon; public bool Read; }
        readonly List<NoteItem> noteItems = new List<NoteItem>();
        static readonly Dictionary<string, string[]> NoteCats = new Dictionary<string, string[]>
        {
            // category -> en, ms, colour
            { "clean", new[] { "Cleaning", "Pembersihan", "#43C6C0" } },
            { "perf", new[] { "Performance", "Prestasi", "#8B78F6" } },
            { "store", new[] { "Storage", "Storan", "#F4AE43" } },
            { "sys", new[] { "System", "Sistem", "#3978F6" } },
            { "app", new[] { "Application", "Aplikasi", "#788195" } },
        };
        /// Records a message for the notification centre (toasts call this). Repeats within 20 s are dropped.
        void Note(string msg, string icon)
        {
            if (icon == "E916" || icon == "E721" || string.IsNullOrEmpty(msg)) return;      // "please wait" and search misses are not worth keeping
            if (noteItems.Count > 0 && noteItems[0].Title == msg && (DateTime.Now - noteItems[0].T).TotalSeconds < 20) return;
            string nav = "Nav" + currentPage.Substring(4);
            var c = CatOf(nav);
            string cat = c == null ? "app" : c.Id == "cl" ? "clean" : c.Id == "pf" ? "perf" : c.Id == "st" ? "store" : "sys";
            var low = msg.ToLowerInvariant();
            if (low.Contains("freed") || low.Contains("clean") || low.Contains("dikosongkan") || low.Contains("bersih")) cat = "clean";
            else if (low.Contains("update") || low.Contains("kemas kini")) cat = "app";
            noteItems.Insert(0, new NoteItem { T = DateTime.Now, Cat = cat, Title = msg, Icon = icon, Desc = currentPage == "PageDash" ? Tx("From Home", "Dari Utama") : string.Format(Tx("From {0}", "Dari {0}"), NavTitle(nav)) });
            if (noteItems.Count > 40) noteItems.RemoveAt(noteItems.Count - 1);
            if (notePop != null && notePop.IsOpen) FillNotes();
            PaintBell();
        }
        void PaintBell()
        {
            if (bellDot == null) return;
            int n = noteItems.Count(x => !x.Read);
            bellDot.Visibility = n > 0 ? Visibility.Visible : Visibility.Collapsed;
            bell.ToolTip = n > 0 ? string.Format(Tx("Notifications ({0} unread)", "Pemberitahuan ({0} belum dibaca)"), n) : Tx("Notifications", "Pemberitahuan");
            System.Windows.Automation.AutomationProperties.SetName(bell, (string)bell.ToolTip);
        }
        static string AgoText(DateTime t)
        {
            var d = DateTime.Now - t;
            return d.TotalMinutes < 1 ? Tx("Just now", "Baru sahaja") : d.TotalHours < 1 ? string.Format(Tx("{0} min ago", "{0} min lalu"), (int)d.TotalMinutes) : d.TotalDays < 1 ? string.Format(Tx("{0} h ago", "{0} jam lalu"), (int)d.TotalHours) : t.ToString("d MMM");
        }
        void FillNotes()
        {
            noteList.Children.Clear();
            int unreadN = noteItems.Count(x => !x.Read);
            var head = new Grid { Margin = new Thickness(10, 6, 10, 12) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); head.ColumnDefinitions.Add(new ColumnDefinition()); head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var ht = new TextBlock { Text = Tx("Notifications", "Pemberitahuan"), FontSize = 19, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            ht.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush"); head.Children.Add(ht);
            if (unreadN > 0)
            {
                var pill = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 1, 8, 2), Margin = new Thickness(10, 2, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = unreadN.ToString(), FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White } };
                pill.SetResourceReference(Border.BackgroundProperty, "NavActiveBrush");
                Grid.SetColumn(pill, 1); head.Children.Add(pill);
            }
            if (noteItems.Count > 0)
            {
                var acts = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                if (unreadN > 0)
                {
                    var mark = new Button { Style = (Style)W.FindResource("Link"), Content = Tx("Mark all as read", "Tanda semua dibaca"), FontSize = 13.5 };
                    mark.Click += (s, e) => { foreach (var x in noteItems) x.Read = true; FillNotes(); PaintBell(); };
                    acts.Children.Add(mark);
                }
                var clr = new Button { Style = (Style)W.FindResource("Link"), Content = Tx("Clear all", "Kosongkan"), FontSize = 13.5, Margin = new Thickness(16, 0, 0, 0) };
                clr.Click += (s, e) => { noteItems.Clear(); FillNotes(); PaintBell(); };
                acts.Children.Add(clr);
                Grid.SetColumn(acts, 2); head.Children.Add(acts);
            }
            noteList.Children.Add(head);
            if (noteItems.Count == 0)
            {
                var empty = new StackPanel { Margin = new Thickness(10, 16, 10, 24), HorizontalAlignment = HorizontalAlignment.Center };
                var ic = new TextBlock { Text = Glyph("EA8F"), FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 28, HorizontalAlignment = HorizontalAlignment.Center };
                ic.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
                empty.Children.Add(ic);
                var t1 = Strong(Tx("You're all caught up", "Tiada pemberitahuan baharu"), 15); t1.HorizontalAlignment = HorizontalAlignment.Center; t1.Margin = new Thickness(0, 10, 0, 4); empty.Children.Add(t1);
                var t2 = Muted(Tx("Scan results, cleaning reports and warnings appear here.", "Keputusan imbasan, laporan pembersihan dan amaran muncul di sini."), 13); t2.TextAlignment = TextAlignment.Center; empty.Children.Add(t2);
                noteList.Children.Add(empty); return;
            }
            foreach (var n in noteItems)
            {
                var item = n;
                var cc = NoteCats[n.Cat];
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 7, 0, 0), Visibility = n.Read ? Visibility.Hidden : Visibility.Visible };
                dot.SetResourceReference(Border.BackgroundProperty, "NavActiveBrush");
                g.Children.Add(dot);
                var sp = new StackPanel(); Grid.SetColumn(sp, 1);
                var top = new DockPanel();
                var when = Muted(AgoText(n.T), 12); DockPanel.SetDock(when, Dock.Right); top.Children.Add(when);
                top.Children.Add(new TextBlock { Text = Tx(cc[0], cc[1]).ToUpperInvariant(), FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Hex(cc[2]) });
                sp.Children.Add(top);
                var ti = Strong(n.Title, 15); ti.Margin = new Thickness(0, 4, 0, 2); sp.Children.Add(ti);
                sp.Children.Add(Muted(n.Desc, 12.5));
                g.Children.Add(sp);
                var x = new Button { Style = (Style)W.FindResource("WinBtn"), Content = Glyph("E711"), FontSize = 11, Width = 30, Height = 30, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8, -4, -4, 0), ToolTip = Tx("Dismiss", "Tutup") };
                x.SetResourceReference(Button.TagProperty, "HoverBrush");
                System.Windows.Automation.AutomationProperties.SetName(x, "Dismiss");
                x.Click += (s, e) => { noteItems.Remove(item); FillNotes(); PaintBell(); };
                Grid.SetColumn(x, 2); g.Children.Add(x);
                var row = new Button { Content = g, Style = (Style)W.FindResource("Row"), HorizontalContentAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 2) };
                row.Click += (s, e) => { item.Read = true; FillNotes(); PaintBell(); };
                noteList.Children.Add(row);
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
                if (c.Landing != null) { var cc = c; l.Add(new Hit { Group = "p", Title = c.Landing == "NavHubClean" ? NavTitle(c.Landing) : Tx(c.En, c.Ms), Sub = Tx(c.En, c.Ms), Glyph = c.Glyph, Do = () => Go(cc.Landing), Keys = c.En + " " + c.Ms + " scan imbas" }); }
                foreach (var n in c.Items) if (n != c.Landing) page(n);
                foreach (var hp in Hidden.Where(x => x.Value == c.Id)) page(hp.Key);
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
            g.Children.Add(FlatIcon(h.Glyph, 28, 13));
            var t = Strong(h.Title, 13); t.Margin = new Thickness(12, 0, 8, 0); t.VerticalAlignment = VerticalAlignment.Center; t.TextWrapping = TextWrapping.NoWrap; t.TextTrimming = TextTrimming.CharacterEllipsis; Grid.SetColumn(t, 1); g.Children.Add(t);
            var s = Muted(h.Sub, 11.5); s.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(s, 2); g.Children.Add(s);
            var b = new Button { Content = g, Style = (Style)W.FindResource("Row"), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            b.Click += (sn, e) => { searchPop.IsOpen = false; E<TextBox>("SearchBox").Text = ""; Keyboard.ClearFocus(); h.Do(); };
            return b;
        }

        // ------------------------------------------------------------ landing pages (calm: one list, few numbers)
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
        static Border FlatIcon(string glyph, double size, double font)
        {
            var b = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size * 0.28), VerticalAlignment = VerticalAlignment.Center };
            b.SetResourceReference(Border.BackgroundProperty, "SelectBrush");
            var t = new TextBlock { Text = Glyph(glyph), FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = font, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            t.SetResourceReference(TextBlock.ForegroundProperty, "CyanBrush");
            b.Child = t; return b;
        }
        /// One horizontal row that opens a tool: icon, divider, name, one line, optional status, arrow.
        Border ToolRow(string nav, string status = null, string kind = "", string title = null)
        {
            var g = new Grid { MinHeight = 56 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(FlatIcon(NavGlyph(nav), 52, 21));
            var div = new Border { Width = 1, Margin = new Thickness(22, 4, 22, 4) }; div.SetResourceReference(Border.BackgroundProperty, "LineBrush"); Grid.SetColumn(div, 1); g.Children.Add(div);
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            var t = new TextBlock { Text = title ?? NavTitle(nav), FontSize = 18, FontWeight = FontWeights.SemiBold }; t.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush"); sp.Children.Add(t);
            var d = new TextBlock { Text = NavDesc(nav), FontSize = 15, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap }; d.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush"); sp.Children.Add(d);
            Grid.SetColumn(sp, 2); g.Children.Add(sp);
            if (!string.IsNullOrEmpty(status))
            {
                var stp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 28, 0) };
                string res = kind == "warn" ? "WarnBrush" : kind == "bad" ? "BadBrush" : kind == "good" ? "GoodBrush" : "MutedBrush";
                if (kind != "") { var dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 1, 8, 0) }; dot.SetResourceReference(Border.BackgroundProperty, res); stp.Children.Add(dot); }
                var st = new TextBlock { Text = status, FontSize = 14.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center }; st.SetResourceReference(TextBlock.ForegroundProperty, res);
                stp.Children.Add(st); Grid.SetColumn(stp, 3); g.Children.Add(stp);
            }
            var arrow = new TextBlock { Text = Glyph("E76C"), FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 15, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            arrow.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            Grid.SetColumn(arrow, 4); g.Children.Add(arrow);
            var card = new Border { Style = (Style)W.FindResource("Card"), Padding = new Thickness(28, 18, 28, 18), Margin = new Thickness(0, 0, 0, 14), Child = g, Cursor = Cursors.Hand, Focusable = true, FocusVisualStyle = null };
            System.Windows.Automation.AutomationProperties.SetName(card, title ?? NavTitle(nav));
            card.MouseEnter += (s, e) => card.SetResourceReference(Border.BorderBrushProperty, "CyanBrush");
            card.MouseLeave += (s, e) => { if (!card.IsKeyboardFocused) card.SetResourceReference(Border.BorderBrushProperty, "LineBrush"); };
            card.GotKeyboardFocus += (s, e) => card.SetResourceReference(Border.BorderBrushProperty, "CyanBrush");
            card.LostKeyboardFocus += (s, e) => card.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
            card.MouseLeftButtonUp += (s, e) => Go(nav);
            card.KeyDown += (s, e) => { if (e.Key == Key.Enter || e.Key == Key.Space) { Go(nav); e.Handled = true; } };
            return card;
        }
        TextBlock Section(string t)
        {
            var tb = new TextBlock { Text = t, FontSize = 21, FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 26, 0, 14) };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush"); return tb;
        }
        void HubBody(params UIElement[] parts)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 4, 14, 20), MaxWidth = 1240, HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var p in parts) if (p != null) sp.Children.Add(p);
            ModBody(new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, -14, 0) });
        }
        /// One metric inside a shared panel: label over a large value, with handles for live updates.
        FrameworkElement Metric(string label, out TextBlock value, out TextBlock sub)
        {
            var sp = new StackPanel { Margin = new Thickness(28, 20, 28, 20) };
            var l = new TextBlock { Text = label, FontSize = 15, FontWeight = FontWeights.SemiBold }; l.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            value = new TextBlock { Text = "…", FontSize = 36, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2), TextTrimming = TextTrimming.CharacterEllipsis }; value.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            sub = new TextBlock { FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis }; sub.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            sp.Children.Add(l); sp.Children.Add(value); sp.Children.Add(sub);
            return sp;
        }
        /// Related numbers in one panel, separated by thin dividers instead of separate cards.
        Border Metrics(params FrameworkElement[] items)
        {
            var g = new Grid();
            for (int i = 0; i < items.Length; i++)
            {
                g.ColumnDefinitions.Add(new ColumnDefinition());
                var cell = new Border { BorderThickness = new Thickness(i == 0 ? 0 : 1, 0, 0, 0), Margin = new Thickness(0, 14, 0, 14), Child = items[i] };
                cell.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
                ((FrameworkElement)items[i]).Margin = new Thickness(28, 6, 28, 6);
                Grid.SetColumn(cell, i); g.Children.Add(cell);
            }
            return new Border { Style = (Style)W.FindResource("Card"), Padding = new Thickness(0), Child = g };
        }
        StackPanel List(params UIElement[] rows) { var sp = new StackPanel(); foreach (var r in rows) sp.Children.Add(r); return sp; }

        void HubClean()
        {
            ModBegin("HubClean", Tx("Scans everything that is safe to remove. You review the list before anything is deleted.", "Mengimbas semua yang selamat dibuang. Anda semak senarai sebelum apa-apa dipadam."),
                Btn(Tx("Scan now", "Imbas sekarang"), true, () => { Go("NavClean"); StartScan(); }));
            TextBlock v1, s1, v2, s2, v3, s3;
            var m1 = Metric(Tx("Can be removed", "Boleh dibuang"), out v1, out s1);
            var m2 = Metric(Tx("Last clean", "Pembersihan terakhir"), out v2, out s2);
            var m3 = Metric(Tx("Freed in total", "Jumlah dikosongkan"), out v3, out s3);
            bool have = scanned && junkTotal >= 0;
            v1.Text = have ? Fmt.Size(junkTotal) : "—"; s1.Text = have ? Tx("from the last scan", "dari imbasan terakhir") : Tx("not scanned yet", "belum diimbas");
            v2.Text = S.LastFreed > 0 ? Fmt.Size(S.LastFreed) : "—"; s2.Text = string.IsNullOrEmpty(S.LastClean) ? Tx("never", "belum pernah") : S.LastClean;
            v3.Text = Fmt.Size(S.TotalFreed); s3.Text = string.Format(Tx("in {0} cleans", "dalam {0} pembersihan"), S.CleanCount);
            HubBody(Metrics(m1, m2, m3),
                Section(Tx("Cleaners", "Pembersih")),
                List(ToolRow("NavClean", have ? Fmt.Size(junkTotal) : null, have && junkTotal > 512L * 1048576 ? "warn" : ""), ToolRow("NavDeepApp"), ToolRow("NavDupes"), ToolRow("NavLarge")),
                Section(Tx("More tools", "Alat lain")),
                List(ToolRow("NavEmpty"), ToolRow("NavResidual"), ToolRow("NavDownloads"), ToolRow("NavOrg")));
        }

        void HubPerf()
        {
            ModBegin("HubPerf", Tx("What is using your PC right now. Nothing is closed or changed without asking you.", "Apa yang menggunakan PC anda sekarang. Tiada apa ditutup atau diubah tanpa kebenaran anda."),
                Btn(Tx("Open monitor", "Buka pemantau"), true, () => Go("NavPerf")));
            TextBlock cpuV, cpuS, ramV, ramS, stV, stS;
            var m1 = Metric("CPU", out cpuV, out cpuS);
            var m2 = Metric(Tx("Memory", "Memori"), out ramV, out ramS);
            var m3 = Metric(Tx("Startup apps", "Apps permulaan"), out stV, out stS);
            cpuS.Text = string.Format(L.T("cores"), Environment.ProcessorCount);
            var startRow = ToolRow("NavStartup");
            HubBody(Metrics(m1, m2, m3),
                Section(Tx("Tools", "Alat")),
                List(ToolRow("NavPerf"), startRow, ToolRow("NavBackground"), ToolRow("NavGame")),
                Section(Tx("More tools", "Alat lain")),
                List(ToolRow("NavRam"), ToolRow("NavProfiles"), ToolRow("NavBattery")));
            Action tick = () =>
            {
                int c = Cpu.Sample(); if (c >= 0) cpuV.Text = c + "%";
                double total, free; int pct; Memory.Info(out total, out free, out pct);
                ramV.Text = pct + "%"; ramS.Text = Fmt.Size(total - free) + " / " + Fmt.Size(total);
            };
            tick();
            modTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) }; modTimer.Tick += (s, e) => tick(); modTimer.Start();
            Task.Factory.StartNew(() =>
            {
                int on = -1, all = 0;
                try { var l = Startup.List(); all = l.Count; on = l.Count(x => x.Enabled); } catch { }
                Ui(() => { stV.Text = on >= 0 ? on.ToString() : "—"; stS.Text = on >= 0 ? string.Format(Tx("enabled of {0}", "dihidupkan daripada {0}"), all) : Tx("could not read", "tidak dapat dibaca"); });
            });
        }

        void HubStore()
        {
            ModBegin("HubStore", Tx("How full your drives are, and the tools to see what fills them.", "Sejauh mana cakera anda penuh, dan alat untuk melihat apa yang memenuhkannya."),
                Btn(Tx("Analyze storage", "Analisis storan"), true, () => Go("NavMap")));
            var drives = new List<DriveInfo>();
            try { drives = DriveInfo.GetDrives().Where(d => { try { return d.IsReady && d.DriveType == DriveType.Fixed; } catch { return false; } }).ToList(); } catch { }
            var list = new StackPanel();
            foreach (var d in drives)
            {
                long used = d.TotalSize - d.AvailableFreeSpace; double pct = used * 100.0 / d.TotalSize;
                var g = new Grid { Margin = new Thickness(0, 6, 0, 6) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
                string lbl = ""; try { lbl = d.VolumeLabel; } catch { }
                var nm = Strong(d.Name.TrimEnd('\\') + (string.IsNullOrEmpty(lbl) ? "" : "  " + lbl), 13.5); nm.VerticalAlignment = VerticalAlignment.Center; nm.TextTrimming = TextTrimming.CharacterEllipsis; nm.TextWrapping = TextWrapping.NoWrap; g.Children.Add(nm);
                var pb = new ProgressBar { Value = pct, Height = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
                if (pct >= 90) pb.Foreground = (Brush)W.FindResource("BadBrush"); else if (pct >= 80) pb.Foreground = (Brush)W.FindResource("WarnBrush");
                Grid.SetColumn(pb, 1); g.Children.Add(pb);
                var tx = Muted(string.Format(Tx("{0} free of {1}", "{0} kosong daripada {1}"), Fmt.Size(d.AvailableFreeSpace), Fmt.Size(d.TotalSize)), 12.5); tx.VerticalAlignment = VerticalAlignment.Center; tx.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(tx, 2); g.Children.Add(tx);
                list.Children.Add(g);
            }
            if (drives.Count == 0) list.Children.Add(Muted(Tx("No fixed drives could be read.", "Tiada cakera tetap dapat dibaca."), 12.5));
            HubBody(CardOf(list, 20),
                Section(Tx("Tools", "Alat")),
                List(ToolRow("NavMap"), ToolRow("NavDiskHealth"), ToolRow("NavApps")));
        }

        void HubPriv()
        {
            var l = PrivacyChecks();
            int ok = l.Count(x => x.Item3);
            ModBegin("HubPriv", string.Format(Tx("{0} of {1} Windows privacy settings keep your data private. KEMAS does not replace Windows Security or your antivirus.", "{0} daripada {1} tetapan privasi Windows menjaga data anda. KEMAS tidak menggantikan Windows Security atau antivirus anda."), ok, l.Count),
                Btn(Tx("Run privacy audit", "Jalankan audit privasi"), true, () => Go("NavPrivacy")));
            var review = new StackPanel();
            foreach (var c in l.Where(x => !x.Item3)) { var u = c.Item4; review.Children.Add(CheckRow(c.Item1, c.Item2, false, Tx("Open setting", "Buka tetapan"), () => OpenUri(u))); }
            UIElement attention = review.Children.Count == 0 ? null : (UIElement)CardOf(review, 20);
            bool admin = IsAdmin();
            HubBody(attention == null ? null : Section(Tx("Recommended", "Disyorkan")), attention,
                Section(Tx("Tools", "Alat")),
                List(ToolRow("NavPrivacy", l.Count - ok > 0 ? string.Format(Tx("{0} to review", "{0} untuk disemak"), l.Count - ok) : null, "warn"), ToolRow("NavBrowser"), ToolRow("NavWinHealth", admin ? null : Tx("Some checks need admin", "Sesetengah semakan perlu pentadbir"))));
        }

        void HubUtil()
        {
            ModBegin("HubUtil", Tx("Windows tools in one place for system information, maintenance and troubleshooting.", "Alat Windows di satu tempat untuk maklumat sistem, penyelenggaraan dan penyelesaian masalah."));
            bool net = true; try { net = NetworkInterface.GetIsNetworkAvailable(); } catch { }
            HubBody(List(
                ToolRow("NavNetwork", net ? null : Tx("Offline", "Luar talian"), "bad"),
                ToolRow("NavDrivers"),
                ToolRow("NavSysInfo"),
                ToolRow("NavShredder"),
                ToolRow("NavRestore", IsAdmin() ? null : Tx("Needs admin", "Perlu pentadbir"), "", Tx("System Restore", "Pemulihan Sistem"))));
        }
    }
}
