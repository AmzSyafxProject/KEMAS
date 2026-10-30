// KEMAS 4.0 - extra modules (cleanup, performance, storage, privacy, utilities), built in code on a shared page kit
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Kemas
{
    public class ModRow
    {
        public bool Sel { get; set; }
        public string C0 { get; set; }
        public string C1 { get; set; }
        public string C2 { get; set; }
        public string C3 { get; set; }
        public string C4 { get; set; }
        public object Tag;
        public long Num;
        public string Glyph { get; set; }
        public Brush Tile { get; set; }
        public Brush PillBg { get; set; }
        public Brush PillFg { get; set; }
    }

    [DataContract] public class HealthDay { [DataMember] public string Day = ""; [DataMember] public int Score; }
    [DataContract] public class HealthFile { [DataMember] public List<HealthDay> Days = new List<HealthDay>(); }

    public static class FileOps
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHFILEOPSTRUCT { public IntPtr hwnd; public uint wFunc; public string pFrom; public string pTo; public ushort fFlags; public bool fAnyOperationsAborted; public IntPtr hNameMappings; public string lpszProgressTitle; }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHFileOperation(ref SHFILEOPSTRUCT op);
        /// Moves files or folders to the Recycle Bin (can be restored from there).
        public static bool Recycle(IEnumerable<string> paths)
        {
            var list = paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
            if (list.Count == 0) return true;
            var op = new SHFILEOPSTRUCT { wFunc = 3, pFrom = string.Join("\0", list) + "\0\0", fFlags = 0x40 | 0x10 | 0x4 | 0x400 };
            try { return SHFileOperation(ref op) == 0 && !op.fAnyOperationsAborted; } catch { return false; }
        }
        static readonly string[] SkipDirs = { "AppData", "OneDrive - ", "$Recycle.Bin" };
        /// Walks the user profile (not AppData), skipping links and unreadable folders.
        public static IEnumerable<FileInfo> UserFiles(Func<bool> cancel)
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var stack = new Stack<string>(); stack.Push(root);
            while (stack.Count > 0)
            {
                if (cancel()) yield break;
                var d = stack.Pop(); DirectoryInfo di; FileInfo[] files = null; DirectoryInfo[] subs = null;
                try { di = new DirectoryInfo(d); files = di.GetFiles(); subs = di.GetDirectories(); } catch { continue; }
                foreach (var f in files) if ((f.Attributes & (FileAttributes.ReparsePoint | FileAttributes.System)) == 0) yield return f;
                foreach (var s in subs)
                {
                    if ((s.Attributes & (FileAttributes.ReparsePoint | FileAttributes.System)) != 0) continue;
                    if (d == root && (s.Name == "AppData" || s.Name.StartsWith("."))) continue;
                    stack.Push(s.FullName);
                }
            }
        }
        public static long DirSize(string dir)
        {
            long n = 0;
            try { foreach (var f in new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories)) try { n += f.Length; } catch { } } catch { }
            return n;
        }
        /// Deletes the contents of a cache folder; files in use are skipped. Returns bytes freed.
        public static long EmptyDir(string dir)
        {
            long freed = 0;
            try
            {
                foreach (var f in new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories))
                    try { long l = f.Length; f.Attributes = FileAttributes.Normal; f.Delete(); freed += l; } catch { }
                foreach (var s in new DirectoryInfo(dir).GetDirectories()) try { s.Delete(true); } catch { }
            }
            catch { }
            return freed;
        }
    }

    public static class Wmi
    {
        public static List<ManagementObject> Q(string scope, string query)
        {
            var r = new List<ManagementObject>();
            try { using (var s = new ManagementObjectSearcher(scope, query)) foreach (ManagementObject o in s.Get()) r.Add(o); } catch { }
            return r;
        }
        public static string S(ManagementObject o, string p) { try { var v = o[p]; return v == null ? "" : v.ToString().Trim(); } catch { return ""; } }
    }

    public partial class MainWin
    {
        static string Tx(string en, string ms) { return Settings.Current.Language == "ms" ? ms : en; }
        static bool IsAdmin() { try { return new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator); } catch { return false; } }

        // ------------------------------------------------------------ module registry
        // id, icon, en, ms, group
        static readonly string[][] Mods =
        {
            new[] { "Health", "E95E", "PC Health", "Kesihatan PC", "ov" },
            new[] { "Timeline", "E81C", "Health Timeline", "Garis Masa Kesihatan", "ov" },
            new[] { "DeepApp", "E7B8", "Deep App Cleaner", "Pembersih Apps Mendalam", "cl" },
            new[] { "Dupes", "E8C8", "Duplicate Finder", "Pencari Pendua", "cl" },
            new[] { "Large", "E8B9", "Large File Finder", "Pencari Fail Besar", "cl" },
            new[] { "Empty", "E8D5", "Empty Folders", "Folder Kosong", "cl" },
            new[] { "Residual", "E8F1", "Residual Files", "Fail Sisa", "cl" },
            new[] { "Browser", "E774", "Browser Cleaner", "Pembersih Pelayar", "cl" },
            new[] { "Downloads", "E896", "Download Cleaner", "Pembersih Muat Turun", "cl" },
            new[] { "Perf", "E9D9", "Performance Monitor", "Pemantau Prestasi", "pf" },
            new[] { "Background", "E7C4", "Background Apps", "Apps Latar", "pf" },
            new[] { "Game", "E7FC", "Game Mode", "Mod Permainan", "pf" },
            new[] { "Profiles", "E945", "Optimization Profiles", "Profil Pengoptimuman", "pf" },
            new[] { "Battery", "E83F", "Battery Insights", "Maklumat Bateri", "pf" },
            new[] { "DiskHealth", "EDA2", "Disk Health", "Kesihatan Cakera", "st" },
            new[] { "Privacy", "E72E", "Privacy Audit", "Audit Privasi", "pr" },
            new[] { "WinHealth", "E83D", "Windows Health Check", "Semakan Kesihatan Windows", "pr" },
            new[] { "Network", "E968", "Network Diagnostics", "Diagnostik Rangkaian", "ut" },
            new[] { "Drivers", "E964", "Driver Information", "Maklumat Pemacu", "ut" },
            new[] { "SysInfo", "E7F4", "System Information", "Maklumat Sistem", "ut" },
            new[] { "Shredder", "E74D", "File Shredder", "Pemusnah Fail", "ut" },
            new[] { "Restore", "E777", "System Restore", "Pemulihan Sistem", "ut" },
        };
        readonly Dictionary<string, Grid> modPages = new Dictionary<string, Grid>();
        readonly List<Tuple<RadioButton, string, string>> modNavs = new List<Tuple<RadioButton, string, string>>();
        readonly List<Tuple<TextBlock, string, string>> grpLabels = new List<Tuple<TextBlock, string, string>>();
        public static string ModTitle(string id) { var m = Mods.FirstOrDefault(x => x[0] == id); return m == null ? id : Tx(m[2], m[3]); }

        void SetupModules()
        {
            var host = E<Grid>("Pages");
            foreach (var m in Mods)
            {
                var g = new Grid { Visibility = Visibility.Collapsed };
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                g.RowDefinitions.Add(new RowDefinition());
                host.Children.Add(g);
                el["Page" + m[0]] = g; modPages[m[0]] = g;
                var nav = new RadioButton { Style = (Style)W.FindResource("Nav"), Tag = Glyph(m[1]), Content = Tx(m[2], m[3]) };
                var id = m[0];
                nav.Checked += (s, e) => ShowPage("Nav" + id);
                el["Nav" + id] = nav;
                modNavs.Add(Tuple.Create(nav, m[2], m[3]));
            }
            // rebuild the sidebar in module order
            var stack = E<StackPanel>("NavStack");
            Func<string, RadioButton> ex = n => { var r = E<RadioButton>(n); var p = r.Parent as Panel; if (p != null) p.Children.Remove(r); return r; };
            var order = new List<object>
            {
                "NavDash", "#OVERVIEW|RINGKASAN", "NavHealth", "NavDoctor", "NavTimeline",
                "#CLEANUP|PEMBERSIHAN", "NavClean", "NavOrg", "NavDeepApp", "NavDupes", "NavLarge", "NavEmpty", "NavResidual", "NavBrowser", "NavDownloads",
                "#PERFORMANCE|PRESTASI", "NavPerf", "NavRam", "NavBackground", "NavStartup", "NavGame", "NavProfiles", "NavBattery",
                "#STORAGE|STORAN", "NavMap", "NavDiskHealth", "NavApps",
                "#PRIVACY & SECURITY|PRIVASI & KESELAMATAN", "NavPrivacy", "NavWinHealth",
                "#UTILITIES|UTILITI", "NavNetwork", "NavDrivers", "NavSysInfo", "NavShredder", "NavRestore", "NavRecover",
            };
            var items = order.Select(o => (string)o).ToList();
            var built = new List<UIElement>();
            foreach (var it in items)
            {
                if (it.StartsWith("#"))
                {
                    var a = it.Substring(1).Split('|');
                    var t = new TextBlock { Style = (Style)W.FindResource("NavGroup"), Text = Tx(a[0], a[1]) };
                    grpLabels.Add(Tuple.Create(t, a[0], a[1])); built.Add(t);
                }
                else built.Add(ex(it));
            }
            stack.Children.Clear();
            foreach (var b in built) stack.Children.Add(b);
        }

        void RefreshModuleTexts()
        {
            foreach (var n in modNavs) n.Item1.Content = Tx(n.Item2, n.Item3);
            foreach (var g in grpLabels) g.Item1.Text = Tx(g.Item2, g.Item3);
        }

        // ------------------------------------------------------------ page kit
        Grid curMod; string curModId;
        DispatcherTimer modTimer;
        Grid ModBegin(string id, string sub, params Button[] buttons)
        {
            var g = modPages[id]; g.Children.Clear(); curMod = g; curModId = id;
            var head = new Grid { Margin = new Thickness(0, 0, 0, 18) };
            head.ColumnDefinitions.Add(new ColumnDefinition()); head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var hp = new Grid { Margin = new Thickness(0, 0, 16, 0) };
            hp.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); hp.ColumnDefinitions.Add(new ColumnDefinition());
            var m = Mods.First(x => x[0] == id);
            hp.Children.Add(GradTile(m[1], ModColor(id), 52, 22));
            var sp = new StackPanel { Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = ModTitle(id), Style = (Style)W.FindResource("H1") });
            sp.Children.Add(new TextBlock { Text = sub, Style = (Style)W.FindResource("Sub") });
            Grid.SetColumn(sp, 1); hp.Children.Add(sp);
            head.Children.Add(hp);
            var bp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            for (int i = 0; i < buttons.Length; i++) { if (i > 0) buttons[i].Margin = new Thickness(10, 0, 0, 0); bp.Children.Add(buttons[i]); }
            Grid.SetColumn(bp, 1); head.Children.Add(bp);
            g.Children.Add(head);
            return g;
        }
        void ModBody(UIElement body) { Grid.SetRow(body, 1); curMod.Children.Add(body); }

        static readonly string[][] Palette = { new[] { "#3978F6", "#5BA8FF" }, new[] { "#8B78F6", "#C08BFF" }, new[] { "#43C6C0", "#3978F6" }, new[] { "#F4AE43", "#ED8F5B" }, new[] { "#27B77A", "#43C6C0" }, new[] { "#ED626B", "#F48FB1" } };
        static string[] ModColor(string id) { int i = Array.FindIndex(Mods, x => x[0] == id); return Palette[Math.Max(0, i) % Palette.Length]; }
        static Brush Grad(string[] c) { var b = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(c[0]), (Color)ColorConverter.ConvertFromString(c[1]), new Point(0, 0), new Point(1, 1)); b.Freeze(); return b; }
        static Border GradTile(string glyph, string[] c, double size, double font)
        {
            return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size * 0.28), Background = Grad(c), VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = Glyph(glyph), FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = font, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        }
        /// Ring chart: pct 0..100 with a centre value and caption.
        static Grid Donut(double pct, string[] c, string center, string caption, double size = 150)
        {
            var g = new Grid { Width = size, Height = size };
            double th = size * 0.09, r = (size - th) / 2;
            var track = new System.Windows.Shapes.Ellipse { Width = size - th, Height = size - th, StrokeThickness = th }; track.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "TrackBrush");
            g.Children.Add(track);
            double v = Math.Max(0.5, Math.Min(99.9, pct)), a = (v / 100 * 360 - 90) * Math.PI / 180, cx = size / 2;
            var fig = new PathFigure { StartPoint = new Point(cx, th / 2) };
            fig.Segments.Add(new ArcSegment(new Point(cx + r * Math.Cos(a), cx + r * Math.Sin(a)), new Size(r, r), 0, v > 50, SweepDirection.Clockwise, true));
            var geo = new PathGeometry(); geo.Figures.Add(fig);
            g.Children.Add(new System.Windows.Shapes.Path { Data = geo, Stroke = Grad(c), StrokeThickness = th, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            var t = new TextBlock { Text = center, FontSize = size * 0.22, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center }; t.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            var cp = new TextBlock { Text = caption, FontSize = Math.Max(10, size * 0.075), HorizontalAlignment = HorizontalAlignment.Center }; cp.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            sp.Children.Add(t); sp.Children.Add(cp); g.Children.Add(sp);
            return g;
        }
        UIElement DonutCard(double pct, string[] c, string center, string caption, string title, string detail)
        {
            var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(Donut(pct, c, center, caption, 130));
            var sp = new StackPanel { Margin = new Thickness(22, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(Strong(title, 18)); var d = Muted(detail, 13); d.Margin = new Thickness(0, 6, 0, 0); sp.Children.Add(d);
            Grid.SetColumn(sp, 1); g.Children.Add(sp);
            var card = CardOf(g, 22); card.Margin = new Thickness(0, 0, 12, 12); return card;
        }
        Button Btn(string text, bool primary, Action onClick, bool danger = false)
        {
            var b = new Button { Content = text, Style = (Style)W.FindResource(danger ? "Danger" : primary ? "Primary" : "Ghost") };
            b.Click += (s, e) => onClick();
            return b;
        }
        Border CardOf(UIElement child, double pad = 20)
        {
            return new Border { Style = (Style)W.FindResource("Card"), Padding = new Thickness(pad, pad - 4, pad, pad - 4), Child = child };
        }
        TextBlock Muted(string t, double size = 12.5)
        {
            var tb = new TextBlock { Text = t, FontSize = size, TextWrapping = TextWrapping.Wrap };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush"); return tb;
        }
        TextBlock Strong(string t, double size = 14)
        {
            var tb = new TextBlock { Text = t, FontSize = size, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush"); return tb;
        }
        DataGrid Table(bool check, params string[] cols)
        {
            var dg = new DataGrid { SelectionMode = DataGridSelectionMode.Extended };
            if (check)
            {
                var f = new FrameworkElementFactory(typeof(CheckBox));
                f.SetBinding(ToggleButton.IsCheckedProperty, new Binding("Sel") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                f.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                dg.Columns.Add(new DataGridTemplateColumn { CellTemplate = new DataTemplate { VisualTree = f }, Width = new DataGridLength(44) });
            }
            dg.RowHeight = 48;
            for (int i = 0; i < cols.Length; i++)
            {
                var parts = cols[i].Split('|');
                bool pill = parts[0].StartsWith("!"); string head = pill ? parts[0].Substring(1) : parts[0];
                var width = parts.Length > 1 ? new DataGridLength(double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), DataGridLengthUnitType.Star) : new DataGridLength(1, DataGridLengthUnitType.Auto);
                if (i == 0)
                {
                    var sp = new FrameworkElementFactory(typeof(StackPanel)); sp.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
                    var tile = new FrameworkElementFactory(typeof(Border));
                    tile.SetValue(FrameworkElement.WidthProperty, 30.0); tile.SetValue(FrameworkElement.HeightProperty, 30.0); tile.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
                    tile.SetBinding(Border.BackgroundProperty, new Binding("Tile")); tile.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 10, 0));
                    var gl = new FrameworkElementFactory(typeof(TextBlock)); gl.SetBinding(TextBlock.TextProperty, new Binding("Glyph"));
                    gl.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets")); gl.SetValue(TextBlock.FontSizeProperty, 13.0); gl.SetValue(TextBlock.ForegroundProperty, Brushes.White);
                    gl.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); gl.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                    tile.AppendChild(gl); sp.AppendChild(tile);
                    var tx = new FrameworkElementFactory(typeof(TextBlock)); tx.SetBinding(TextBlock.TextProperty, new Binding("C0")); tx.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
                    tx.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); tx.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
                    sp.AppendChild(tx);
                    dg.Columns.Add(new DataGridTemplateColumn { Header = head, CellTemplate = new DataTemplate { VisualTree = sp }, Width = width, SortMemberPath = "C0" });
                }
                else if (pill)
                {
                    var bd = new FrameworkElementFactory(typeof(Border)); bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(10)); bd.SetValue(Border.PaddingProperty, new Thickness(10, 3, 10, 3));
                    bd.SetBinding(Border.BackgroundProperty, new Binding("PillBg")); bd.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
                    var tx = new FrameworkElementFactory(typeof(TextBlock)); tx.SetBinding(TextBlock.TextProperty, new Binding("C" + i)); tx.SetBinding(TextBlock.ForegroundProperty, new Binding("PillFg"));
                    tx.SetValue(TextBlock.FontSizeProperty, 11.5); tx.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold); bd.AppendChild(tx);
                    dg.Columns.Add(new DataGridTemplateColumn { Header = head, CellTemplate = new DataTemplate { VisualTree = bd }, Width = width, SortMemberPath = "C" + i });
                    dg.Tag = i;
                }
                else dg.Columns.Add(new DataGridTextColumn { Header = head, Binding = new Binding("C" + i), IsReadOnly = true, Width = width });
            }
            return dg;
        }
        static readonly string[] GoodWords = { "Great", "Keadaan Baik", "Healthy", "Sihat", "Connected", "Bersambung", "Online", "Original", "Asal", "Good", "Baik", "On", "Hidup", "Safe", "Selamat" };
        /// Fills icon tiles and status pills for every row before the table is shown.
        void Decorate(DataGrid dg, string id)
        {
            var rows = dg.ItemsSource as System.Collections.IEnumerable; if (rows == null) return;
            var m = Mods.FirstOrDefault(x => x[0] == id); string glyph = m == null ? "E8A5" : m[1];
            int pc = dg.Tag is int ? (int)dg.Tag : -1, k = 0;
            foreach (ModRow r in rows)
            {
                if (r.Glyph == null) r.Glyph = Glyph(glyph);
                if (r.Tile == null) r.Tile = Grad(Palette[Math.Abs((r.C0 ?? "").GetHashCode()) % Palette.Length]);
                if (pc >= 0)
                {
                    string v = (string)typeof(ModRow).GetProperty("C" + pc).GetValue(r, null) ?? "";
                    bool good = GoodWords.Any(w => v.StartsWith(w, StringComparison.OrdinalIgnoreCase));
                    bool bad = v.IndexOf("Unhealthy", StringComparison.OrdinalIgnoreCase) >= 0 || v.IndexOf("Tidak sihat", StringComparison.OrdinalIgnoreCase) >= 0 || v.StartsWith("Off") || v.StartsWith("Mati");
                    var c = (Color)ColorConverter.ConvertFromString(good ? "#27B77A" : bad ? "#ED626B" : "#E0961F");
                    r.PillFg = new SolidColorBrush(c); r.PillBg = new SolidColorBrush(Color.FromArgb(0x24, c.R, c.G, c.B));
                }
                k++;
            }
        }
        Border TableCard(DataGrid dg, UIElement footer = null)
        {
            Decorate(dg, curModId);
            var dp = new DockPanel();
            if (footer != null) { DockPanel.SetDock(footer, Dock.Bottom); dp.Children.Add(footer); }
            dp.Children.Add(dg);
            return new Border { Style = (Style)W.FindResource("Card"), Padding = new Thickness(6), Child = dp };
        }
        UIElement Stat(string label, string value, string sub, string color)
        {
            var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new ColumnDefinition());
            var pal = Palette.FirstOrDefault(p => p[0] == color) ?? new[] { color, color };
            row.Children.Add(GradTile(curModId == null ? "E9D2" : Mods.First(x => x[0] == curModId)[1], pal, 44, 18));
            var sp = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(sp, 1); row.Children.Add(sp);
            var l = new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.SemiBold }; l.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            var v = new TextBlock { Text = value, FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis }; v.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            sp.Children.Add(l); sp.Children.Add(v);
            if (!string.IsNullOrEmpty(sub)) sp.Children.Add(Muted(sub, 11.5));
            var c = CardOf(row, 18); c.Margin = new Thickness(0, 0, 12, 12); return c;
        }
        UniformGrid Stats(int cols, params UIElement[] items) { var u = new UniformGrid { Columns = cols, Margin = new Thickness(0, 0, -12, 4) }; foreach (var i in items) u.Children.Add(i); return u; }
        Grid Rows(params UIElement[] items)
        {
            var g = new Grid();
            for (int i = 0; i < items.Length; i++)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = i == items.Length - 1 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
                Grid.SetRow(items[i], i); g.Children.Add(items[i]);
            }
            return g;
        }
        UIElement Loading(string t) { var g = new Grid(); var m = Muted(t, 13.5); m.HorizontalAlignment = HorizontalAlignment.Center; m.VerticalAlignment = VerticalAlignment.Center; g.Children.Add(m); return CardOf(g); }
        UIElement CheckRow(string title, string detail, bool ok, string action = null, Action onClick = null)
        {
            var g = new Grid { Margin = new Thickness(0, 8, 0, 8) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var col = ok ? "#27B77A" : "#F4AE43";
            var tile = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Background = Hex(ok ? "#2227B77A" : "#26F4AE43"), VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = Glyph(ok ? "E73E" : "E7BA"), FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 13, Foreground = Hex(col), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            g.Children.Add(tile);
            var mid = new StackPanel { Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            mid.Children.Add(Strong(title, 13.5)); mid.Children.Add(Muted(detail, 12));
            Grid.SetColumn(mid, 1); g.Children.Add(mid);
            if (action != null) { var b = Btn(action, false, onClick); b.Height = 32; b.FontSize = 12.5; b.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(b, 2); g.Children.Add(b); }
            var line = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = g }; line.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
            return line;
        }
        static void OpenPath(string p) { try { if (File.Exists(p)) Process.Start("explorer.exe", "/select,\"" + p + "\""); else Process.Start("explorer.exe", "\"" + p + "\""); } catch { } }
        static void OpenUri(string u) { try { Process.Start(new ProcessStartInfo(u) { UseShellExecute = true }); } catch { } }
        static string Ago(DateTime t) { var d = DateTime.Now - t; return d.TotalDays >= 365 ? (int)(d.TotalDays / 365) + "y" : d.TotalDays >= 30 ? (int)(d.TotalDays / 30) + "mo" : (int)d.TotalDays + "d"; }

        void ShowModule(string id)
        {
            if (modTimer != null) { modTimer.Stop(); modTimer = null; }
            switch (id)
            {
                case "Health": ModHealth(); break;
                case "Timeline": ModTimeline(); break;
                case "DeepApp": ModDeepApp(); break;
                case "Dupes": ModDupes(); break;
                case "Large": ModLarge(); break;
                case "Empty": ModEmpty(); break;
                case "Residual": ModResidual(); break;
                case "Browser": ModBrowser(); break;
                case "Downloads": ModDownloads(); break;
                case "Perf": ModPerf(); break;
                case "Background": ModBackground(); break;
                case "Game": ModGame(); break;
                case "Profiles": ModProfiles(); break;
                case "Battery": ModBattery(); break;
                case "DiskHealth": ModDiskHealth(); break;
                case "Privacy": ModPrivacy(); break;
                case "WinHealth": ModWinHealth(); break;
                case "Network": ModNetwork(); break;
                case "Drivers": ModDrivers(); break;
                case "SysInfo": ModSysInfo(); break;
                case "Shredder": ModShredder(); break;
                case "Restore": ModRestore(); break;
            }
        }

        // shared: a scan that fills a checkable table, with a Recycle Bin action
        void FileTable(string id, string sub, string scanText, string[] cols, Func<Func<bool>, List<ModRow>> scan, string actText, string confirmText, Func<List<ModRow>, int> act, bool selectAllDefault = false)
        {
            DataGrid dg = null; TextBlock foot = null;
            Action doScan = null;
            Action doAct = () =>
            {
                if (dg == null || dg.ItemsSource == null) return;
                var sel = ((List<ModRow>)dg.ItemsSource).Where(r => r.Sel).ToList();
                if (sel.Count == 0) { Toast(Tx("Tick the items first.", "Tandakan item dahulu."), "E7BA"); return; }
                long sz = sel.Sum(r => r.Num);
                Ask(actText + "?", string.Format(confirmText, sel.Count, Fmt.Size(sz)), actText, "E74D", true, () =>
                {
                    int done = 0;
                    Run(() => { done = act(sel); }, () =>
                    {
                        Toast(string.Format(Tx("{0} of {1} done.", "{0} daripada {1} selesai."), done, sel.Count), "E73E");
                        History.Add("clean", ModTitle(id), string.Format(Tx("{0} items, {1}", "{0} item, {1}"), done, Fmt.Size(sz)), string.Join("\n", sel.Take(40).Select(r => r.C0 + "  " + Fmt.Size(r.Num))));
                        RefreshHistory();
                        doScan();
                    });
                });
            };
            doScan = () =>
            {
                ModBegin(id, sub, Btn(Tx("Scan", "Imbas"), false, () => doScan()), Btn(actText, true, doAct));
                ModBody(Loading(scanText));
                List<ModRow> rows = null;
                Run(() => { rows = scan(() => cancelFlag); }, () =>
                {
                    if (rows == null) rows = new List<ModRow>();
                    if (selectAllDefault) foreach (var r in rows) r.Sel = true;
                    dg = Table(true, cols); dg.ItemsSource = rows;
                    dg.MouseDoubleClick += (s, e) => { var r = dg.SelectedItem as ModRow; if (r != null && r.Tag is string) OpenPath((string)r.Tag); };
                    foot = Muted(rows.Count == 0 ? Tx("Nothing found.", "Tiada apa ditemui.") : string.Format(Tx("{0} found, {1} in total. Double-click a row to open its location.", "{0} ditemui, {1} keseluruhan. Klik dua kali untuk buka lokasi."), rows.Count, Fmt.Size(rows.Sum(r => r.Num))), 12);
                    foot.Margin = new Thickness(10, 8, 10, 6);
                    curMod = modPages[id];
                    var old = modPages[id].Children.OfType<UIElement>().Where(c => Grid.GetRow(c) == 1).ToList();
                    foreach (var o in old) modPages[id].Children.Remove(o);
                    ModBody(TableCard(dg, foot));
                });
            };
            doScan();
        }

        // ============================================================ CLEANUP
        void ModLarge()
        {
            FileTable("Large", Tx("Files over 100 MB in your personal folders, biggest first. Removed files go to the Recycle Bin.", "Fail melebihi 100 MB dalam folder peribadi, terbesar dahulu. Fail dibuang ke Tong Kitar Semula."),
                Tx("Looking for large files...", "Mencari fail besar..."),
                new[] { Tx("Name", "Nama") + "|3", Tx("Folder", "Folder") + "|4", Tx("Size", "Saiz"), Tx("Type", "Jenis"), Tx("Modified", "Diubah") },
                cancel =>
                {
                    var l = new List<ModRow>();
                    foreach (var f in FileOps.UserFiles(cancel)) { long len; try { len = f.Length; } catch { continue; } if (len >= 100L * 1024 * 1024) l.Add(new ModRow { C0 = f.Name, C1 = f.DirectoryName, C2 = Fmt.Size(len), C3 = f.Extension.TrimStart('.').ToUpperInvariant(), C4 = f.LastWriteTime.ToString("d MMM yyyy"), Num = len, Tag = f.FullName }); }
                    return l.OrderByDescending(r => r.Num).Take(300).ToList();
                }, Tx("Move to Recycle Bin", "Pindah ke Tong Kitar"), Tx("Move {0} files ({1}) to the Recycle Bin? You can restore them from there.", "Pindahkan {0} fail ({1}) ke Tong Kitar Semula? Boleh dipulihkan dari sana."),
                sel => sel.Count(r => FileOps.Recycle(new[] { (string)r.Tag })));
        }

        void ModDupes()
        {
            FileTable("Dupes", Tx("Identical files (same size and same content hash). The first copy in each group is never ticked. Removed copies go to the Recycle Bin.", "Fail serupa (saiz dan hash kandungan sama). Salinan pertama setiap kumpulan tidak ditanda. Salinan dibuang ke Tong Kitar Semula."),
                Tx("Comparing files...", "Membandingkan fail..."),
                new[] { Tx("Name", "Nama") + "|3", Tx("Folder", "Folder") + "|4", Tx("Size", "Saiz"), Tx("Group", "Kumpulan"), "!" + Tx("Copy", "Salinan") },
                cancel =>
                {
                    var bySize = new Dictionary<long, List<FileInfo>>();
                    foreach (var f in FileOps.UserFiles(cancel)) { long len; try { len = f.Length; } catch { continue; } if (len < 1024 * 1024) continue; List<FileInfo> l; if (!bySize.TryGetValue(len, out l)) bySize[len] = l = new List<FileInfo>(); l.Add(f); }
                    var rows = new List<ModRow>(); int grp = 0;
                    using (var sha = SHA1.Create())
                        foreach (var kv in bySize.Where(k => k.Value.Count > 1).OrderByDescending(k => k.Key))
                        {
                            if (cancel()) break;
                            var byHash = new Dictionary<string, List<FileInfo>>();
                            foreach (var f in kv.Value) { try { string h; using (var s = f.OpenRead()) h = Convert.ToBase64String(sha.ComputeHash(s)); List<FileInfo> l; if (!byHash.TryGetValue(h, out l)) byHash[h] = l = new List<FileInfo>(); l.Add(f); } catch { } }
                            foreach (var g in byHash.Values.Where(v => v.Count > 1))
                            {
                                grp++; int i = 0;
                                foreach (var f in g.OrderBy(x => x.LastWriteTime))
                                    rows.Add(new ModRow { C0 = f.Name, C1 = f.DirectoryName, C2 = Fmt.Size(kv.Key), C3 = "#" + grp, C4 = i++ == 0 ? Tx("Original (oldest)", "Asal (tertua)") : Tx("Duplicate", "Pendua"), Num = kv.Key, Tag = f.FullName });
                            }
                            if (grp > 400) break;
                        }
                    return rows;
                }, Tx("Move to Recycle Bin", "Pindah ke Tong Kitar"), Tx("Move {0} duplicate files ({1}) to the Recycle Bin?", "Pindahkan {0} fail pendua ({1}) ke Tong Kitar Semula?"),
                sel => sel.Count(r => FileOps.Recycle(new[] { (string)r.Tag })));
        }

        void ModEmpty()
        {
            FileTable("Empty", Tx("Folders in your personal folders that contain no files at all. System and app folders are skipped.", "Folder peribadi yang langsung tiada fail. Folder sistem dan apps dilangkau."),
                Tx("Looking for empty folders...", "Mencari folder kosong..."),
                new[] { Tx("Folder", "Folder") + "|2", Tx("Location", "Lokasi") + "|5", Tx("Created", "Dicipta") },
                cancel =>
                {
                    var rows = new List<ModRow>();
                    var root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var dirs = new Stack<string>(); dirs.Push(root);
                    while (dirs.Count > 0 && !cancel() && rows.Count < 2000)
                    {
                        var d = dirs.Pop(); string[] subs;
                        try { subs = Directory.GetDirectories(d); } catch { continue; }
                        foreach (var s in subs)
                        {
                            DirectoryInfo di; try { di = new DirectoryInfo(s); if ((di.Attributes & (FileAttributes.ReparsePoint | FileAttributes.System | FileAttributes.Hidden)) != 0) continue; } catch { continue; }
                            if (d == root && (di.Name == "AppData" || di.Name.StartsWith("."))) continue;
                            bool empty; try { empty = !Directory.EnumerateFiles(s, "*", SearchOption.AllDirectories).Any(); } catch { continue; }
                            if (empty) rows.Add(new ModRow { C0 = di.Name, C1 = di.Parent.FullName, C2 = di.CreationTime.ToString("d MMM yyyy"), Tag = s });
                            else dirs.Push(s);
                        }
                    }
                    return rows;
                }, Tx("Move to Recycle Bin", "Pindah ke Tong Kitar"), Tx("Move {0} empty folders to the Recycle Bin?", "Pindahkan {0} folder kosong ke Tong Kitar Semula?"),
                sel => sel.Count(r => { var p = (string)r.Tag; try { if (Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories).Any()) return false; } catch { return false; } return FileOps.Recycle(new[] { p }); }));
        }

        void ModDownloads()
        {
            FileTable("Downloads", Tx("Old installers, large downloads and files you have not touched in 90 days. Nothing is ticked for you.", "Pemasang lama, muat turun besar dan fail yang tidak disentuh 90 hari. Tiada apa ditanda secara automatik."),
                Tx("Reading Downloads...", "Membaca Muat Turun..."),
                new[] { Tx("Name", "Nama") + "|4", "!" + Tx("Reason", "Sebab") + "|3", Tx("Size", "Saiz"), Tx("Modified", "Diubah") },
                cancel =>
                {
                    var rows = new List<ModRow>();
                    var exts = new[] { ".exe", ".msi", ".msix", ".zip", ".rar", ".7z", ".iso" };
                    try
                    {
                        foreach (var f in new DirectoryInfo(KnownFolders.Downloads).GetFiles())
                        {
                            string why = null; long len = f.Length; var age = DateTime.Now - f.LastWriteTime;
                            if (exts.Contains(f.Extension.ToLowerInvariant()) && age.TotalDays > 30) why = Tx("Installer or archive, ", "Pemasang atau arkib, ") + Ago(f.LastWriteTime);
                            else if (len > 500L * 1024 * 1024) why = Tx("Large download", "Muat turun besar");
                            else if (age.TotalDays > 90) why = Tx("Not changed for ", "Tidak diubah selama ") + Ago(f.LastWriteTime);
                            if (why != null) rows.Add(new ModRow { C0 = f.Name, C1 = why, C2 = Fmt.Size(len), C3 = f.LastWriteTime.ToString("d MMM yyyy"), Num = len, Tag = f.FullName });
                        }
                    }
                    catch { }
                    return rows.OrderByDescending(r => r.Num).ToList();
                }, Tx("Move to Recycle Bin", "Pindah ke Tong Kitar"), Tx("Move {0} downloads ({1}) to the Recycle Bin?", "Pindahkan {0} muat turun ({1}) ke Tong Kitar Semula?"),
                sel => sel.Count(r => FileOps.Recycle(new[] { (string)r.Tag })));
        }

        static List<Tuple<string, string>> CacheDirs()
        {
            var la = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var ra = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cache", "Code Cache", "GPUCache", "CachedData", "ShaderCache", "GrShaderCache", "Service Worker\\CacheStorage", "DawnCache", "INetCache" };
            var res = new List<Tuple<string, string>>();
            foreach (var root in new[] { la, ra })
            {
                string[] apps; try { apps = Directory.GetDirectories(root); } catch { continue; }
                foreach (var app in apps)
                {
                    var an = Path.GetFileName(app);
                    if (an.Equals("Temp", StringComparison.OrdinalIgnoreCase) || an.Equals("Packages", StringComparison.OrdinalIgnoreCase)) continue;
                    var stack = new Stack<Tuple<string, int>>(); stack.Push(Tuple.Create(app, 0));
                    while (stack.Count > 0)
                    {
                        var t = stack.Pop(); string[] subs; try { subs = Directory.GetDirectories(t.Item1); } catch { continue; }
                        foreach (var s in subs)
                        {
                            var n = Path.GetFileName(s);
                            if (names.Contains(n)) res.Add(Tuple.Create(an, s));
                            else if (t.Item2 < 4) stack.Push(Tuple.Create(s, t.Item2 + 1));
                        }
                    }
                }
            }
            return res;
        }

        void ModDeepApp()
        {
            FileTable("DeepApp", Tx("Cache folders that apps rebuild by themselves (Cache, Code Cache, GPU cache). Settings, logins and your data are not touched.", "Folder cache yang dibina semula oleh apps (Cache, Code Cache, cache GPU). Tetapan, log masuk dan data anda tidak disentuh."),
                Tx("Measuring app caches...", "Mengukur cache apps..."),
                new[] { Tx("App", "Apps") + "|2", Tx("Cache folder", "Folder cache") + "|5", Tx("Size", "Saiz") },
                cancel => CacheDirs().Select(t => { long n = FileOps.DirSize(t.Item2); return new ModRow { C0 = t.Item1, C1 = t.Item2, C2 = Fmt.Size(n), Num = n, Tag = t.Item2 }; }).Where(r => r.Num > 1024 * 1024).OrderByDescending(r => r.Num).ToList(),
                Tx("Clean selected", "Bersihkan dipilih"), Tx("Empty {0} cache folders ({1})? Close those apps first; files in use are skipped.", "Kosongkan {0} folder cache ({1})? Tutup apps itu dahulu; fail yang digunakan dilangkau."),
                sel => sel.Count(r => FileOps.EmptyDir((string)r.Tag) > 0));
        }

        void ModResidual()
        {
            FileTable("Residual", Tx("App data folders that do not match any installed program. These may be leftovers of removed apps. Review each one: nothing is ticked, and removal goes to the Recycle Bin.", "Folder data apps yang tidak sepadan dengan program dipasang. Mungkin sisa apps yang dibuang. Semak setiap satu: tiada apa ditanda, dan pembuangan ke Tong Kitar Semula."),
                Tx("Comparing with installed programs...", "Membandingkan dengan program dipasang..."),
                new[] { Tx("Folder", "Folder") + "|2", Tx("Location", "Lokasi") + "|5", Tx("Size", "Saiz"), Tx("Last used", "Terakhir digunakan") },
                cancel =>
                {
                    var known = new List<string>();
                    try { foreach (var a in Programs.List()) { known.Add((a.Name ?? "").ToLowerInvariant()); known.Add((a.Publisher ?? "").ToLowerInvariant()); } } catch { }
                    var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Microsoft", "Packages", "Temp", "Comms", "ConnectedDevicesPlatform", "D3DSCache", "CrashDumps", "Programs", "PlaceholderTileLogoFolder", "Publishers", "Windows", "KEMAS", "Adobe", "Google", "Mozilla", "NVIDIA", "NVIDIA Corporation", "Intel", "AMD", "Package Cache", "VirtualStore", "History", "IsolatedStorage", "SquirrelTemp", "pip", "npm", "npm-cache", "Python", "IconCache.db", "Apple", "Sun", "Oracle", "Lenovo", "Realtek" };
                    var rows = new List<ModRow>();
                    foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) })
                    {
                        string[] dirs; try { dirs = Directory.GetDirectories(root); } catch { continue; }
                        foreach (var d in dirs)
                        {
                            if (cancel()) break;
                            var n = Path.GetFileName(d); if (skip.Contains(n) || n.StartsWith(".")) continue;
                            var key = n.ToLowerInvariant().Replace(" ", "");
                            if (key.Length < 3 || known.Any(k => k.Replace(" ", "").Contains(key) || (key.Length > 4 && key.Contains(k.Replace(" ", "")) && k.Length > 3))) continue;
                            DateTime last; try { last = Directory.GetLastWriteTime(d); } catch { continue; }
                            if ((DateTime.Now - last).TotalDays < 30) continue;   // recently used, probably still in use
                            long sz = FileOps.DirSize(d); if (sz < 512 * 1024) continue;
                            rows.Add(new ModRow { C0 = n, C1 = d, C2 = Fmt.Size(sz), C3 = last.ToString("d MMM yyyy"), Num = sz, Tag = d });
                        }
                    }
                    return rows.OrderByDescending(r => r.Num).ToList();
                }, Tx("Move to Recycle Bin", "Pindah ke Tong Kitar"), Tx("Move {0} folders ({1}) to the Recycle Bin? Only do this if you know the app is gone.", "Pindahkan {0} folder ({1}) ke Tong Kitar Semula? Lakukan hanya jika anda pasti apps itu sudah tiada."),
                sel => sel.Count(r => FileOps.Recycle(new[] { (string)r.Tag })));
        }

        void ModBrowser()
        {
            var la = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var browsers = new[] {
                Tuple.Create("Microsoft Edge", Path.Combine(la, @"Microsoft\Edge\User Data")),
                Tuple.Create("Google Chrome", Path.Combine(la, @"Google\Chrome\User Data")),
                Tuple.Create("Brave", Path.Combine(la, @"BraveSoftware\Brave-Browser\User Data")),
                Tuple.Create("Opera", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Opera Software\Opera Stable")),
                Tuple.Create("Firefox", Path.Combine(la, @"Mozilla\Firefox\Profiles")) };
            FileTable("Browser", Tx("Cached web files per browser. History, cookies, passwords and logins are not touched. Close the browser first.", "Fail web cache bagi setiap pelayar. Sejarah, kuki, kata laluan dan log masuk tidak disentuh. Tutup pelayar dahulu."),
                Tx("Measuring browser caches...", "Mengukur cache pelayar..."),
                new[] { Tx("Browser", "Pelayar") + "|2", Tx("Item", "Item") + "|2", Tx("Folder", "Folder") + "|5", Tx("Size", "Saiz") },
                cancel =>
                {
                    var rows = new List<ModRow>();
                    foreach (var b in browsers)
                    {
                        if (!Directory.Exists(b.Item2)) continue;
                        var dirs = new List<string>();
                        try
                        {
                            foreach (var prof in Directory.GetDirectories(b.Item2))
                                foreach (var c in new[] { "Cache", "Code Cache", "GPUCache", "cache2", "Service Worker\\CacheStorage" })
                                { var p = Path.Combine(prof, c); if (Directory.Exists(p)) dirs.Add(p); }
                            foreach (var c in new[] { "Cache", "Code Cache", "GPUCache" }) { var p = Path.Combine(b.Item2, c); if (Directory.Exists(p)) dirs.Add(p); }
                        }
                        catch { }
                        if (b.Item1 == "Firefox") { var fc = Path.Combine(la, @"Mozilla\Firefox\Profiles"); try { foreach (var prof in Directory.GetDirectories(fc)) { var p = Path.Combine(prof, "cache2"); if (Directory.Exists(p) && !dirs.Contains(p)) dirs.Add(p); } } catch { } }
                        foreach (var d in dirs.Distinct()) { long n = FileOps.DirSize(d); if (n > 0) rows.Add(new ModRow { C0 = b.Item1, C1 = Tx("Cache", "Cache") + " · " + Path.GetFileName(d), C2 = d, C3 = Fmt.Size(n), Num = n, Tag = d }); }
                    }
                    return rows.OrderByDescending(r => r.Num).ToList();
                }, Tx("Clean selected", "Bersihkan dipilih"), Tx("Empty {0} browser cache folders ({1})? Pages may load a little slower once.", "Kosongkan {0} folder cache pelayar ({1})? Halaman mungkin lambat sedikit sekali."),
                sel => sel.Count(r => FileOps.EmptyDir((string)r.Tag) > 0), true);
        }

        // ============================================================ OVERVIEW
        int CurrentScore() { ComputePenalties(); return (int)Math.Max(5, Math.Min(100, 100 - penDisk - penRam - penJunkStd - penJunkDeep)); }

        void RecordHealthDay()
        {
            try
            {
                var path = Path.Combine(AppInfo.DataDir, "health.json"); var f = new HealthFile();
                if (File.Exists(path)) f = Json.Read<HealthFile>(File.ReadAllText(path)) ?? new HealthFile();
                if (f.Days == null) f.Days = new List<HealthDay>();
                var today = DateTime.Today.ToString("yyyy-MM-dd"); int sc = CurrentScore();
                f.Days.RemoveAll(d => d.Day == today); f.Days.Add(new HealthDay { Day = today, Score = sc });
                f.Days.Sort((a, b) => string.CompareOrdinal(a.Day, b.Day)); if (f.Days.Count > 120) f.Days.RemoveRange(0, f.Days.Count - 120);
                Directory.CreateDirectory(AppInfo.DataDir); File.WriteAllText(path, Json.Write(f));
            }
            catch { }
        }

        void ModHealth()
        {
            ModBegin("Health", Tx("One overview of performance, storage, privacy and security, from real checks on this PC.", "Gambaran prestasi, storan, privasi dan keselamatan daripada semakan sebenar pada PC ini."),
                Btn(Tx("Open PC Doctor", "Buka PC Doctor"), true, () => E<RadioButton>("NavDoctor").IsChecked = true));
            ModBody(Loading(Tx("Checking...", "Menyemak...")));
            int priv = -1; string av = null; int startN = 0;
            Run(() =>
            {
                priv = PrivacyScore(PrivacyChecks());
                var a = Wmi.Q(@"root\SecurityCenter2", "SELECT displayName FROM AntiVirusProduct"); av = a.Count > 0 ? Wmi.S(a[0], "displayName") : null;
                try { startN = Startup.List().Count(x => x.Enabled); } catch { }
            }, () =>
            {
                int sc = CurrentScore();
                var list = new StackPanel();
                list.Children.Add(CheckRow(Tx("Performance", "Prestasi"), string.Format(Tx("Memory {0}% in use.", "Memori {0}% digunakan."), ramPct), ramPct < 85, Tx("Memory", "Memori"), () => E<RadioButton>("NavRam").IsChecked = true));
                list.Children.Add(CheckRow(Tx("Storage", "Storan"), string.Format(Tx("Drive is {0}% full, {1} of junk found.", "Cakera {0}% penuh, {1} sampah ditemui."), diskPct, Fmt.Size(Math.Max(0, junkTotal))), diskPct < 85 && junkTotal < 1073741824L, Tx("Storage Map", "Peta Storan"), () => E<RadioButton>("NavMap").IsChecked = true));
                list.Children.Add(CheckRow(Tx("Startup", "Permulaan"), string.Format(Tx("{0} apps start with Windows.", "{0} apps bermula dengan Windows."), startN), startN < 6, Tx("Startup", "Permulaan"), () => E<RadioButton>("NavStartup").IsChecked = true));
                list.Children.Add(CheckRow(Tx("Privacy", "Privasi"), string.Format(Tx("Privacy score {0}/100 from your Windows settings.", "Skor privasi {0}/100 daripada tetapan Windows."), priv), priv >= 70, Tx("Privacy Audit", "Audit Privasi"), () => E<RadioButton>("NavPrivacy").IsChecked = true));
                list.Children.Add(CheckRow(Tx("Security", "Keselamatan"), av != null ? Tx("Protection reported by Windows: ", "Perlindungan dilaporkan Windows: ") + av : Tx("No antivirus reported by Windows Security Center.", "Tiada antivirus dilaporkan oleh Windows Security Center."), av != null, Tx("Health Check", "Semakan"), () => E<RadioButton>("NavWinHealth").IsChecked = true));
                var hero = new Grid(); hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) }); hero.ColumnDefinitions.Add(new ColumnDefinition());
                hero.Children.Add(DonutCard(sc, new[] { "#43C6C0", "#3978F6" }, sc.ToString(), Tx("Health score", "Skor kesihatan"), L.T(sc >= 85 ? "pill_great" : sc >= 60 ? "pill_ok" : "pill_bad"), Tx("Based on memory, storage, junk, startup, privacy and protection on this PC.", "Berdasarkan memori, storan, sampah, permulaan, privasi dan perlindungan pada PC ini.")));
                var mini = Stats(2, Stat(Tx("MEMORY", "MEMORI"), ramPct + "%", Tx("in use", "digunakan"), "#8B78F6"), Stat(Tx("DISK", "CAKERA"), diskPct + "%", Tx("full", "penuh"), "#3978F6"), Stat(Tx("PRIVACY", "PRIVASI"), priv + "/100", "", "#43C6C0"), Stat(Tx("STARTUP", "PERMULAAN"), startN.ToString(), Tx("apps", "apps"), "#F4AE43"));
                Grid.SetColumn(mini, 1); hero.Children.Add(mini);
                var top = hero;
                curMod = modPages["Health"]; foreach (var o in curMod.Children.OfType<UIElement>().Where(c => Grid.GetRow(c) == 1).ToList()) curMod.Children.Remove(o);
                ModBody(Rows(top, CardOf(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto })));
            });
        }

        void ModTimeline()
        {
            RecordHealthDay();
            ModBegin("Timeline", Tx("Your PC health score day by day. A point is saved each day you open KEMAS.", "Skor kesihatan PC setiap hari. Satu titik disimpan setiap hari anda buka KEMAS."));
            var f = new HealthFile();
            try { var p = Path.Combine(AppInfo.DataDir, "health.json"); if (File.Exists(p)) f = Json.Read<HealthFile>(File.ReadAllText(p)) ?? f; } catch { }
            var days = f.Days ?? new List<HealthDay>();
            var cv = new Canvas { Height = 180, ClipToBounds = true };
            cv.SizeChanged += (s, e) =>
            {
                cv.Children.Clear(); if (days.Count == 0) return;
                double w = cv.ActualWidth, h = cv.ActualHeight;
                for (int k = 0; k <= 4; k++) { var ln = new System.Windows.Shapes.Line { X1 = 0, X2 = w, Y1 = h - k * h / 4, Y2 = h - k * h / 4, StrokeThickness = 1 }; ln.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "LineBrush"); cv.Children.Add(ln); }
                var pl = new System.Windows.Shapes.Polyline { StrokeThickness = 3, Stroke = Hex("#3978F6"), StrokeLineJoin = PenLineJoin.Round };
                for (int i = 0; i < days.Count; i++) pl.Points.Add(new Point(days.Count == 1 ? w / 2 : 6 + i * (w - 12) / (days.Count - 1), h - days[i].Score / 100.0 * (h - 8) - 4));
                cv.Children.Add(pl);
                foreach (var pt in pl.Points) { var dot = new System.Windows.Shapes.Ellipse { Width = 8, Height = 8, Fill = Hex("#3978F6") }; Canvas.SetLeft(dot, pt.X - 4); Canvas.SetTop(dot, pt.Y - 4); cv.Children.Add(dot); }
            };
            var dg = Table(false, Tx("Day", "Hari") + "|2", Tx("Score", "Skor") + "|1", "!" + Tx("Status", "Status") + "|2");
            dg.ItemsSource = days.AsEnumerable().Reverse().Select(d => new ModRow { C0 = d.Day, C1 = d.Score + "/100", C2 = L.T(d.Score >= 85 ? "pill_great" : d.Score >= 60 ? "pill_ok" : "pill_bad") }).ToList();
            var chart = CardOf(new StackPanel { Children = { Strong(Tx("Health score", "Skor kesihatan")), cv } }); chart.Margin = new Thickness(0, 0, 0, 14);
            ModBody(Rows(chart, TableCard(dg)));
        }

        // ============================================================ PERFORMANCE
        void ModPerf()
        {
            ModBegin("Perf", Tx("Live CPU, memory, disk and network, plus the apps using the most CPU right now.", "CPU, memori, cakera dan rangkaian secara langsung, serta apps yang paling banyak guna CPU sekarang."));
            var vals = new TextBlock[4]; var subs = new TextBlock[4]; var lines = new System.Windows.Shapes.Polyline[4]; var hist = new List<double>[4];
            var names = new[] { "CPU", Tx("Memory", "Memori"), Tx("Disk", "Cakera"), Tx("Network", "Rangkaian") }; var cols = new[] { "#3978F6", "#8B78F6", "#F4AE43", "#43C6C0" };
            var tiles = new UIElement[4];
            for (int i = 0; i < 4; i++)
            {
                hist[i] = new List<double>();
                var sp = new StackPanel();
                var l = Muted(names[i], 12); l.FontWeight = FontWeights.SemiBold; sp.Children.Add(l);
                vals[i] = new TextBlock { FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Hex(cols[i]), Text = "-" }; sp.Children.Add(vals[i]);
                subs[i] = Muted("", 11.5); sp.Children.Add(subs[i]);
                var c = new Canvas { Width = 200, Height = 40, ClipToBounds = true }; lines[i] = new System.Windows.Shapes.Polyline { Stroke = Hex(cols[i]), StrokeThickness = 2 }; c.Children.Add(lines[i]);
                sp.Children.Add(new Viewbox { Stretch = Stretch.Fill, Height = 40, Margin = new Thickness(0, 8, 0, 0), Child = c });
                var card = CardOf(sp, 18); card.Margin = new Thickness(0, 0, 12, 12); tiles[i] = card;
            }
            var dg = Table(false, Tx("App", "Apps") + "|3", "CPU|1", Tx("Memory", "Memori") + "|1", Tx("Processes", "Proses") + "|1");
            ModBody(Rows(Stats(4, tiles), TableCard(dg)));
            Cpu.Sample();
            long lastNet = NetBytes(); var lastT = DateTime.Now;
            var lastCpu = new Dictionary<int, TimeSpan>();
            PerformanceCounter disk = null; try { disk = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total"); disk.NextValue(); } catch { disk = null; }
            Action draw = () =>
            {
                for (int i = 0; i < 4; i++)
                {
                    var h = hist[i]; var pts = new PointCollection(); double mx = i == 3 ? Math.Max(1, h.DefaultIfEmpty(1).Max()) : 100;
                    for (int k = 0; k < h.Count; k++) pts.Add(new Point(k * 200.0 / 29, 40 - Math.Min(1, h[k] / mx) * 36 - 2));
                    lines[i].Points = pts;
                }
            };
            modTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            int tick = 0;
            modTimer.Tick += (s, e) =>
            {
                if (currentPage != "PagePerf") { modTimer.Stop(); return; }
                int cpu = Cpu.Sample(); if (cpu >= 0) { vals[0].Text = cpu + "%"; Push30(hist[0], cpu); }
                subs[0].Text = string.Format(L.T("cores"), Environment.ProcessorCount);
                double total, free; int rp; Memory.Info(out total, out free, out rp); vals[1].Text = rp + "%"; subs[1].Text = Fmt.Size(total - free) + " / " + Fmt.Size(total); Push30(hist[1], rp);
                if (disk != null) { try { var dv = Math.Min(100, disk.NextValue()); vals[2].Text = dv.ToString("0") + "%"; subs[2].Text = Tx("active time", "masa aktif"); Push30(hist[2], dv); } catch { } } else { vals[2].Text = diskPct + "%"; subs[2].Text = Tx("full", "penuh"); }
                long nb = NetBytes(); var now = DateTime.Now; double kbs = (nb - lastNet) / 1024.0 / Math.Max(0.1, (now - lastT).TotalSeconds); lastNet = nb; lastT = now;
                vals[3].Text = kbs >= 1024 ? (kbs / 1024).ToString("0.0") + " MB/s" : kbs.ToString("0") + " KB/s"; subs[3].Text = Tx("send + receive", "hantar + terima"); Push30(hist[3], kbs);
                draw();
                if (tick++ % 2 == 0)
                {
                    var rows = new List<ModRow>(); double secs = 3.0 * Environment.ProcessorCount;
                    var groups = new Dictionary<string, Tuple<double, long, int>>();
                    foreach (var p in Process.GetProcesses())
                    {
                        try
                        {
                            var t = p.TotalProcessorTime; TimeSpan prev; double d = lastCpu.TryGetValue(p.Id, out prev) ? (t - prev).TotalSeconds : 0; lastCpu[p.Id] = t;
                            Tuple<double, long, int> g; groups.TryGetValue(p.ProcessName, out g);
                            groups[p.ProcessName] = Tuple.Create((g == null ? 0 : g.Item1) + d, (g == null ? 0 : g.Item2) + p.WorkingSet64, (g == null ? 0 : g.Item3) + 1);
                        }
                        catch { }
                        finally { p.Dispose(); }
                    }
                    dg.ItemsSource = groups.OrderByDescending(g => g.Value.Item1).Take(25).Select(g => new ModRow { C0 = g.Key, C1 = Math.Min(100, g.Value.Item1 / secs * 100).ToString("0.0") + "%", C2 = Fmt.Size(g.Value.Item2), C3 = g.Value.Item3.ToString() }).ToList();
                }
            };
            modTimer.Start();
        }
        static void Push30(List<double> l, double v) { l.Add(v); if (l.Count > 30) l.RemoveAt(0); }
        static long NetBytes()
        {
            long n = 0;
            try { foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()) if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback) { var s = ni.GetIPv4Statistics(); n += s.BytesReceived + s.BytesSent; } } catch { }
            return n;
        }

        void ModBackground()
        {
            ModBegin("Background", Tx("Apps running without a window. Windows system processes are protected and cannot be ended here.", "Apps yang berjalan tanpa tetingkap. Proses sistem Windows dilindungi dan tidak boleh ditamatkan di sini."),
                Btn(Tx("Refresh", "Muat semula"), false, ModBackground));
            ModBody(Loading(Tx("Reading running apps...", "Membaca apps yang berjalan...")));
            List<ModRow> rows = null;
            Run(() =>
            {
                var withWin = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in Process.GetProcesses()) { try { if (p.MainWindowHandle != IntPtr.Zero) withWin.Add(p.ProcessName); } catch { } finally { p.Dispose(); } }
                rows = Memory.Groups().Where(g => !g.Protected && !withWin.Contains(g.App)).Select(g => new ModRow { C0 = g.App, C1 = g.Description, C2 = g.RAM, C3 = g.Procs.ToString(), Num = (long)(g.RamMB * 1048576), Tag = g.App }).ToList();
            }, () =>
            {
                var dg = Table(true, Tx("App", "Apps") + "|2", Tx("Description", "Penerangan") + "|3", Tx("Memory", "Memori") + "|1", Tx("Processes", "Proses") + "|1");
                dg.ItemsSource = rows;
                var end = Btn(Tx("End selected", "Tamatkan dipilih"), false, () =>
                {
                    var sel = rows.Where(r => r.Sel).ToList(); if (sel.Count == 0) { Toast(Tx("Tick the apps first.", "Tandakan apps dahulu."), "E7BA"); return; }
                    Ask(L.T("kill_q"), string.Format(L.T("kill_d"), string.Join(", ", sel.Select(r => r.C0))), L.T("end_apps"), "E7BA", true, () => { foreach (var r in sel) Memory.Kill((string)r.Tag); Delay(600, ModBackground); });
                }, true);
                end.HorizontalAlignment = HorizontalAlignment.Left; end.Margin = new Thickness(8);
                curMod = modPages["Background"]; foreach (var o in curMod.Children.OfType<UIElement>().Where(c => Grid.GetRow(c) == 1).ToList()) curMod.Children.Remove(o);
                ModBody(TableCard(dg, end));
            });
        }

        static List<Tuple<string, string, bool>> PowerPlans()
        {
            var r = new List<Tuple<string, string, bool>>();
            try
            {
                var psi = new ProcessStartInfo("powercfg", "/list") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
                using (var p = Process.Start(psi))
                {
                    var o = p.StandardOutput.ReadToEnd(); p.WaitForExit(5000);
                    foreach (var line in o.Split('\n'))
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(line, @"([0-9a-fA-F\-]{36})\s+\((.+?)\)(\s*\*)?");
                        if (m.Success) r.Add(Tuple.Create(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Success));
                    }
                }
            }
            catch { }
            return r;
        }
        static bool SetPlan(string guid)
        {
            try { var p = Process.Start(new ProcessStartInfo("powercfg", "/setactive " + guid) { UseShellExecute = false, CreateNoWindow = true }); p.WaitForExit(5000); return p.ExitCode == 0; } catch { return false; }
        }

        void ModProfiles()
        {
            ModBegin("Profiles", Tx("Choose how Windows balances speed and battery. KEMAS switches the Windows power plan; you can switch back any time.", "Pilih cara Windows mengimbangi kelajuan dan bateri. KEMAS menukar pelan kuasa Windows; anda boleh tukar semula bila-bila masa."),
                Btn(Tx("Open power settings", "Buka tetapan kuasa"), false, () => OpenUri("ms-settings:powersleep")));
            var plans = PowerPlans();
            var desc = new Dictionary<string, string> {
                { "381b4222-f694-41f0-9685-ff5bb260df2e", Tx("Normal performance with sensible power use. Best for most people.", "Prestasi biasa dengan penggunaan kuasa munasabah. Terbaik untuk kebanyakan orang.") },
                { "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", Tx("Keeps the CPU at higher speeds. Faster, uses more power and heat.", "Mengekalkan CPU pada kelajuan tinggi. Lebih pantas, guna lebih kuasa dan haba.") },
                { "a1841308-3541-4fab-bc81-f71556f20b4a", Tx("Lower speeds and less background work to stretch battery life.", "Kelajuan rendah dan kurang kerja latar untuk memanjangkan bateri.") },
                { "e9a42b02-d5df-448d-aa00-03f14749eb61", Tx("Maximum performance for demanding work and games.", "Prestasi maksimum untuk kerja berat dan permainan.") } };
            var wrap = new UniformGrid { Columns = Math.Max(1, Math.Min(4, plans.Count)), Margin = new Thickness(0, 0, -12, 0), VerticalAlignment = VerticalAlignment.Top };
            var cols = new[] { "#3978F6", "#8B78F6", "#43C6C0", "#F4AE43", "#ED626B" }; int ci = 0;
            foreach (var p in plans)
            {
                var sp = new StackPanel();
                var tile = new Border { Width = 46, Height = 46, CornerRadius = new CornerRadius(13), Background = Hex(cols[ci++ % cols.Length]), HorizontalAlignment = HorizontalAlignment.Left,
                    Child = new TextBlock { Text = Glyph("E945"), FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 20, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                sp.Children.Add(tile);
                var t = Strong(p.Item2, 15); t.Margin = new Thickness(0, 12, 0, 4); sp.Children.Add(t);
                string d; sp.Children.Add(Muted(desc.TryGetValue(p.Item1.ToLowerInvariant(), out d) ? d : Tx("A custom Windows power plan.", "Pelan kuasa Windows tersuai."), 12));
                var guid = p.Item1;
                var b = p.Item3 ? Btn(Tx("Active", "Aktif"), false, () => { }) : Btn(Tx("Apply", "Guna"), true, () =>
                {
                    Ask(Tx("Switch power plan?", "Tukar pelan kuasa?"), Tx("Windows will use this plan from now on. You can switch back here any time.", "Windows akan guna pelan ini mulai sekarang. Anda boleh tukar semula di sini."), Tx("Apply", "Guna"), "E945", false, () =>
                    {
                        if (SetPlan(guid)) { History.Add("startup", ModTitle("Profiles"), p.Item2); Toast(p.Item2, "E73E"); } else Toast(Tx("Windows did not allow the change.", "Windows tidak membenarkan perubahan."), "E7BA");
                        ModProfiles();
                    });
                });
                b.Margin = new Thickness(0, 14, 0, 0); b.HorizontalAlignment = HorizontalAlignment.Left; sp.Children.Add(b);
                var card = CardOf(sp); card.Margin = new Thickness(0, 0, 12, 12); wrap.Children.Add(card);
            }
            if (plans.Count == 0) ModBody(Loading(Tx("Windows did not list any power plans.", "Windows tidak menyenaraikan pelan kuasa.")));
            else ModBody(new ScrollViewer { Content = wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        }

        void ModGame()
        {
            ModBegin("Game", Tx("Get ready to play: a faster power plan, Windows Game Mode and fewer background apps. FPS is not measured.", "Sedia untuk bermain: pelan kuasa lebih pantas, Mod Permainan Windows dan kurang apps latar. FPS tidak diukur."));
            bool gm = true;
            try { using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar")) { var v = k == null ? null : k.GetValue("AutoGameModeEnabled"); gm = v == null || Convert.ToInt32(v) == 1; } } catch { }
            var plans = PowerPlans(); var act = plans.FirstOrDefault(p => p.Item3);
            var fast = plans.FirstOrDefault(p => p.Item1.ToLowerInvariant() == "e9a42b02-d5df-448d-aa00-03f14749eb61") ?? plans.FirstOrDefault(p => p.Item1.ToLowerInvariant() == "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
            var list = new StackPanel();
            list.Children.Add(CheckRow(Tx("Windows Game Mode", "Mod Permainan Windows"), gm ? Tx("On. Windows gives games priority.", "Hidup. Windows mengutamakan permainan.") : Tx("Off.", "Mati."), gm, gm ? null : Tx("Turn on", "Hidupkan"), () =>
            { try { using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\GameBar")) k.SetValue("AutoGameModeEnabled", 1, RegistryValueKind.DWord); History.Add("startup", ModTitle("Game"), Tx("Windows Game Mode on", "Mod Permainan Windows hidup")); } catch { } ModGame(); }));
            bool fastOn = fast != null && act != null && act.Item1 == fast.Item1;
            list.Children.Add(CheckRow(Tx("Power plan", "Pelan kuasa"), act == null ? "-" : act.Item2, fastOn, fast == null || fastOn ? null : Tx("Use ", "Guna ") + fast.Item2, () => { if (SetPlan(fast.Item1)) History.Add("startup", ModTitle("Game"), fast.Item2); ModGame(); }));
            var heavy = Memory.Groups().Where(g => !g.Protected && g.RamMB > 300).OrderByDescending(g => g.RamMB).Take(6).ToList();
            list.Children.Add(CheckRow(Tx("Heavy background apps", "Apps latar berat"), heavy.Count == 0 ? Tx("None using over 300 MB.", "Tiada yang guna lebih 300 MB.") : string.Join(", ", heavy.Select(h => h.App + " " + h.RAM)), heavy.Count == 0, heavy.Count == 0 ? null : Tx("Review in Memory", "Semak di Memori"), () => E<RadioButton>("NavRam").IsChecked = true));
            list.Children.Add(CheckRow(Tx("Game Bar and captures", "Game Bar dan rakaman"), Tx("Manage recording and the Xbox Game Bar in Windows settings.", "Urus rakaman dan Xbox Game Bar dalam tetapan Windows."), true, Tx("Open", "Buka"), () => OpenUri("ms-settings:gaming-gamebar")));
            ModBody(CardOf(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }));
        }

        void ModBattery()
        {
            ModBegin("Battery", Tx("Battery level, charging and health from Windows. A full report can be created by Windows.", "Paras bateri, pengecasan dan kesihatan daripada Windows. Laporan penuh boleh dicipta oleh Windows."),
                Btn(Tx("Create battery report", "Cipta laporan bateri"), true, () =>
                {
                    var outp = Path.Combine(AppInfo.DataDir, "battery-report.html");
                    Run(() => { try { Directory.CreateDirectory(AppInfo.DataDir); var p = Process.Start(new ProcessStartInfo("powercfg", "/batteryreport /output \"" + outp + "\"") { UseShellExecute = false, CreateNoWindow = true }); p.WaitForExit(20000); } catch { } },
                        () => { if (File.Exists(outp)) OpenUri(outp); else Toast(Tx("Windows could not create the report.", "Windows tidak dapat mencipta laporan."), "E7BA"); });
                }));
            var ps = System.Windows.Forms.SystemInformation.PowerStatus;
            if (ps.BatteryChargeStatus.HasFlag(System.Windows.Forms.BatteryChargeStatus.NoSystemBattery))
            { ModBody(Loading(Tx("This PC has no battery (desktop or battery not detected).", "PC ini tiada bateri (desktop atau bateri tidak dikesan)."))); return; }
            long design = 0, full = 0; int cycles = -1;
            foreach (var o in Wmi.Q(@"root\WMI", "SELECT DesignedCapacity FROM BatteryStaticData")) long.TryParse(Wmi.S(o, "DesignedCapacity"), out design);
            foreach (var o in Wmi.Q(@"root\WMI", "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity")) long.TryParse(Wmi.S(o, "FullChargedCapacity"), out full);
            foreach (var o in Wmi.Q(@"root\WMI", "SELECT CycleCount FROM BatteryCycleCount")) int.TryParse(Wmi.S(o, "CycleCount"), out cycles);
            int pct = (int)Math.Round(ps.BatteryLifePercent * 100);
            string left = ps.BatteryLifeRemaining > 0 ? TimeSpan.FromSeconds(ps.BatteryLifeRemaining).ToString(@"h\:mm") : "-";
            int health = design > 0 && full > 0 ? (int)Math.Round(full * 100.0 / design) : -1;
            var bh = DonutCard(pct, new[] { "#27B77A", "#43C6C0" }, pct + "%", ps.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online ? Tx("Charging", "Mengecas") : Tx("On battery", "Guna bateri"), Tx("Battery level", "Paras bateri"), health > 0 ? string.Format(Tx("Battery health {0}% of its original capacity.", "Kesihatan bateri {0}% daripada kapasiti asal."), Math.Min(100, health)) : Tx("Health is not reported by this battery.", "Kesihatan tidak dilaporkan oleh bateri ini."));
            ModBody(Rows(bh, Stats(4,
                Stat(Tx("BATTERY", "BATERI"), pct + "%", ps.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online ? Tx("Plugged in", "Dicas") : Tx("On battery", "Guna bateri"), "#27B77A"),
                Stat(Tx("TIME LEFT", "BAKI MASA"), left, Tx("estimated by Windows", "anggaran Windows"), "#3978F6"),
                Stat(Tx("HEALTH", "KESIHATAN"), health > 0 ? Math.Min(100, health) + "%" : "-", health > 0 ? string.Format(Tx("{0} of {1} mWh", "{0} daripada {1} mWh"), full, design) : Tx("not reported", "tidak dilaporkan"), "#8B78F6"),
                Stat(Tx("CYCLES", "KITARAN"), cycles >= 0 ? cycles.ToString() : "-", cycles >= 0 ? Tx("charge cycles", "kitaran cas") : Tx("not reported", "tidak dilaporkan"), "#F4AE43")),
                CardOf(Muted(Tx("Health compares today's full charge with the capacity the battery was designed for. Values come from the battery's own firmware and may not be reported on every laptop.", "Kesihatan membandingkan cas penuh hari ini dengan kapasiti asal bateri. Nilai datang daripada perisian tegar bateri dan mungkin tidak dilaporkan pada setiap komputer riba."), 13))));
        }

        // ============================================================ STORAGE / SECURITY
        void ModDiskHealth()
        {
            ModBegin("DiskHealth", Tx("Drive type, size and the health status Windows reports for each drive.", "Jenis cakera, saiz dan status kesihatan yang dilaporkan Windows bagi setiap cakera."));
            ModBody(Loading(Tx("Reading drives...", "Membaca cakera...")));
            List<ModRow> rows = new List<ModRow>(); bool trim = false;
            Run(() =>
            {
                foreach (var o in Wmi.Q(@"root\Microsoft\Windows\Storage", "SELECT FriendlyName, MediaType, Size, HealthStatus, BusType, SpindleSpeed FROM MSFT_PhysicalDisk"))
                {
                    string mt = Wmi.S(o, "MediaType"), hs = Wmi.S(o, "HealthStatus"), bt = Wmi.S(o, "BusType"); long sz; long.TryParse(Wmi.S(o, "Size"), out sz);
                    rows.Add(new ModRow { C0 = Wmi.S(o, "FriendlyName"), C1 = mt == "4" ? "SSD" : mt == "3" ? "HDD" : Tx("Unknown", "Tidak diketahui"), C2 = Fmt.Size(sz), C3 = bt == "17" ? "NVMe" : bt == "11" ? "SATA" : bt == "7" ? "USB" : bt, C4 = hs == "0" ? Tx("Healthy", "Sihat") : hs == "1" ? Tx("Warning", "Amaran") : hs == "2" ? Tx("Unhealthy", "Tidak sihat") : Tx("Unknown", "Tidak diketahui") });
                }
                try { var p = Process.Start(new ProcessStartInfo("fsutil", "behavior query DisableDeleteNotify") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true }); var o2 = p.StandardOutput.ReadToEnd(); p.WaitForExit(4000); trim = o2.Contains("= 0"); } catch { }
            }, () =>
            {
                var dg = Table(false, Tx("Drive", "Cakera") + "|4", Tx("Type", "Jenis") + "|1", Tx("Size", "Saiz") + "|1", Tx("Bus", "Bas") + "|1", "!" + Tx("Health", "Kesihatan") + "|1"); dg.ItemsSource = rows;
                bool ok = rows.Count > 0 && rows.All(r => r.C4 == Tx("Healthy", "Sihat"));
                var top = Stats(3, Stat(Tx("DRIVES", "CAKERA"), rows.Count.ToString(), "", "#3978F6"), Stat(Tx("STATUS", "STATUS"), rows.Count == 0 ? "-" : ok ? Tx("Good", "Baik") : Tx("Check", "Semak"), Tx("as reported by Windows", "seperti dilaporkan Windows"), ok ? "#27B77A" : "#F4AE43"), Stat("TRIM", trim ? Tx("On", "Hidup") : Tx("Off / unknown", "Mati / tidak diketahui"), Tx("SSD maintenance", "penyelenggaraan SSD"), "#8B78F6"));
                var note = Muted(Tx("Temperature and detailed SMART counters need administrator rights and are not shown.", "Suhu dan kaunter SMART terperinci perlukan hak pentadbir dan tidak ditunjukkan."), 12); note.Margin = new Thickness(10, 8, 10, 6);
                curMod = modPages["DiskHealth"]; foreach (var o in curMod.Children.OfType<UIElement>().Where(c => Grid.GetRow(c) == 1).ToList()) curMod.Children.Remove(o);
                ModBody(Rows(top, TableCard(dg, note)));
            });
        }

        static List<Tuple<string, string, bool, string>> PrivacyChecks()
        {
            Func<RegistryKey, string, string, int> rd = (hive, path, name) => { try { using (var k = hive.OpenSubKey(path)) { var v = k == null ? null : k.GetValue(name); return v == null ? -1 : Convert.ToInt32(v); } } catch { return -1; } };
            Func<string, string> cs = cap => { try { using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\" + cap)) return k == null ? "" : (k.GetValue("Value") as string ?? ""); } catch { return ""; } };
            var l = new List<Tuple<string, string, bool, string>>();
            int ad = rd(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled");
            l.Add(Tuple.Create(Tx("Advertising ID", "ID Pengiklanan"), ad == 1 ? Tx("On: apps can show personalised ads.", "Hidup: apps boleh tunjuk iklan peribadi.") : Tx("Off.", "Mati."), ad != 1, "ms-settings:privacy-general"));
            int te = rd(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled");
            l.Add(Tuple.Create(Tx("Tailored experiences", "Pengalaman disesuaikan"), te == 1 ? Tx("On: diagnostic data is used for tips and ads.", "Hidup: data diagnostik digunakan untuk tip dan iklan.") : Tx("Off.", "Mati."), te != 1, "ms-settings:privacy-feedback"));
            int sp = rd(Registry.CurrentUser, @"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted");
            l.Add(Tuple.Create(Tx("Online speech recognition", "Pengecaman suara dalam talian"), sp == 1 ? Tx("On: voice is sent to Microsoft.", "Hidup: suara dihantar ke Microsoft.") : Tx("Off.", "Mati."), sp != 1, "ms-settings:privacy-speech"));
            int ah = rd(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities");
            l.Add(Tuple.Create(Tx("Activity history", "Sejarah aktiviti"), ah == 0 ? Tx("Off by policy.", "Dimatikan oleh polisi.") : Tx("May be stored on this device.", "Mungkin disimpan pada peranti ini."), ah == 0, "ms-settings:privacy-activityhistory"));
            string loc = cs("location"), cam = cs("webcam"), mic = cs("microphone");
            l.Add(Tuple.Create(Tx("Location", "Lokasi"), loc == "Allow" ? Tx("Apps may use your location.", "Apps boleh guna lokasi anda.") : Tx("Blocked for apps.", "Disekat untuk apps."), loc != "Allow", "ms-settings:privacy-location"));
            l.Add(Tuple.Create(Tx("Camera", "Kamera"), cam == "Allow" ? Tx("Apps may ask to use the camera.", "Apps boleh minta guna kamera.") : Tx("Blocked for apps.", "Disekat untuk apps."), true, "ms-settings:privacy-webcam"));
            l.Add(Tuple.Create(Tx("Microphone", "Mikrofon"), mic == "Allow" ? Tx("Apps may ask to use the microphone.", "Apps boleh minta guna mikrofon.") : Tx("Blocked for apps.", "Disekat untuk apps."), true, "ms-settings:privacy-microphone"));
            int dd = rd(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry");
            l.Add(Tuple.Create(Tx("Diagnostic data", "Data diagnostik"), dd >= 3 ? Tx("Optional data is sent.", "Data pilihan dihantar.") : Tx("Required data only.", "Data wajib sahaja."), dd < 3, "ms-settings:privacy-feedback"));
            return l;
        }
        static int PrivacyScore(List<Tuple<string, string, bool, string>> l) { return l.Count == 0 ? 0 : (int)Math.Round(l.Count(x => x.Item3) * 100.0 / l.Count); }

        void ModPrivacy()
        {
            var l = PrivacyChecks(); int sc = PrivacyScore(l);
            ModBegin("Privacy", Tx("Reads your Windows privacy settings and explains each one. KEMAS changes nothing; each item opens the right Windows setting.", "Membaca tetapan privasi Windows dan menerangkannya. KEMAS tidak mengubah apa-apa; setiap item membuka tetapan Windows yang betul."));
            var list = new StackPanel();
            foreach (var c in l) { var u = c.Item4; list.Children.Add(CheckRow(c.Item1, c.Item2, c.Item3, Tx("Open setting", "Buka tetapan"), () => OpenUri(u))); }
            var ph = new Grid(); ph.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) }); ph.ColumnDefinitions.Add(new ColumnDefinition());
            ph.Children.Add(DonutCard(sc, new[] { "#43C6C0", "#27B77A" }, sc.ToString(), Tx("Privacy score", "Skor privasi"), sc >= 70 ? Tx("Your privacy is well protected", "Privasi anda terlindung baik") : Tx("Some settings share data", "Sesetengah tetapan berkongsi data"), Tx("Each setting below opens the right page in Windows Settings.", "Setiap tetapan di bawah membuka halaman yang betul dalam Tetapan Windows.")));
            var pm = Stats(1, Stat(Tx("PRIVATE", "PERSENDIRIAN"), l.Count(x => x.Item3).ToString(), Tx("settings", "tetapan"), "#3978F6"), Stat(Tx("TO REVIEW", "PERLU DISEMAK"), l.Count(x => !x.Item3).ToString(), Tx("settings", "tetapan"), "#F4AE43"));
            Grid.SetColumn(pm, 1); ph.Children.Add(pm);
            ModBody(Rows(ph,
                CardOf(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto })));
        }

        void ModWinHealth()
        {
            ModBegin("WinHealth", Tx("Windows protection, updates and system file checks. This is not an antivirus.", "Perlindungan Windows, kemas kini dan semakan fail sistem. Ini bukan antivirus."));
            ModBody(Loading(Tx("Checking Windows...", "Menyemak Windows...")));
            List<string> av = new List<string>(); string lastFix = null; bool reboot = false;
            Run(() =>
            {
                foreach (var o in Wmi.Q(@"root\SecurityCenter2", "SELECT displayName, productState FROM AntiVirusProduct")) { int st; int.TryParse(Wmi.S(o, "productState"), out st); av.Add(Wmi.S(o, "displayName") + (((st >> 12) & 0xF) == 1 ? "" : " (" + Tx("off", "mati") + ")")); }
                DateTime best = DateTime.MinValue;
                foreach (var o in Wmi.Q(@"root\cimv2", "SELECT InstalledOn FROM Win32_QuickFixEngineering")) { DateTime d; if (DateTime.TryParse(Wmi.S(o, "InstalledOn"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d) && d > best) best = d; }
                if (best > DateTime.MinValue) lastFix = best.ToString("d MMM yyyy");
                try { using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired")) reboot = k != null; } catch { }
            }, () =>
            {
                var list = new StackPanel();
                list.Children.Add(CheckRow(Tx("Antivirus", "Antivirus"), av.Count > 0 ? string.Join(", ", av) : Tx("None reported by Windows Security Center.", "Tiada dilaporkan oleh Windows Security Center."), av.Count > 0 && av.Any(a => !a.Contains("(")), Tx("Windows Security", "Windows Security"), () => OpenUri("windowsdefender:")));
                list.Children.Add(CheckRow(Tx("Windows Update", "Windows Update"), (lastFix != null ? Tx("Latest update installed ", "Kemas kini terakhir dipasang ") + lastFix + ". " : "") + (reboot ? Tx("A restart is needed to finish updates.", "Mula semula diperlukan untuk menyiapkan kemas kini.") : Tx("No restart pending.", "Tiada mula semula tertunda.")), !reboot, Tx("Open", "Buka"), () => OpenUri("ms-settings:windowsupdate")));
                Action sfc = () =>
                {
                    if (!IsAdmin()) { Ask(Tx("Administrator needed", "Perlukan pentadbir"), Tx("The system file check (SFC) needs administrator rights. Open KEMAS as administrator, then run it again.", "Semakan fail sistem (SFC) perlukan hak pentadbir. Buka KEMAS sebagai pentadbir, kemudian jalankan semula."), L.T("admin_run_btn"), "E7EF", false, () => { if (Cleaner.RelaunchAsAdmin()) Application.Current.Shutdown(); }); return; }
                    try { Process.Start(new ProcessStartInfo("cmd.exe", "/k sfc /scannow & echo. & echo DISM check: & DISM /Online /Cleanup-Image /CheckHealth") { UseShellExecute = true }); History.Add("clean", ModTitle("WinHealth"), "SFC + DISM"); } catch { }
                };
                list.Children.Add(CheckRow(Tx("System files (SFC and DISM)", "Fail sistem (SFC dan DISM)"), Tx("Checks Windows files and repairs them with Windows' own tools. Runs in a separate window and can take 10 to 20 minutes.", "Menyemak fail Windows dan membaikinya dengan alat Windows sendiri. Berjalan dalam tetingkap berasingan, 10 hingga 20 minit."), true, Tx("Run check", "Jalankan semakan"), sfc));
                list.Children.Add(CheckRow(Tx("Reliability history", "Sejarah kebolehpercayaan"), Tx("See recent app crashes and Windows errors.", "Lihat ranap apps dan ralat Windows terkini."), true, Tx("Open", "Buka"), () => { try { Process.Start("perfmon.exe", "/rel"); } catch { } }));
                curMod = modPages["WinHealth"]; foreach (var o in curMod.Children.OfType<UIElement>().Where(c => Grid.GetRow(c) == 1).ToList()) curMod.Children.Remove(o);
                ModBody(CardOf(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }));
            });
        }

        // ============================================================ UTILITIES
        void ModNetwork()
        {
            ModBegin("Network", Tx("Connection, latency, packet loss, DNS and your network adapters.", "Sambungan, kependaman, kehilangan paket, DNS dan penyesuai rangkaian."), Btn(Tx("Test again", "Uji semula"), true, ModNetwork));
            ModBody(Loading(Tx("Testing connection...", "Menguji sambungan...")));
            long avg = -1; int lost = 0, sent = 8; long dns = -1; var rows = new List<ModRow>();
            Run(() =>
            {
                var times = new List<long>();
                using (var p = new Ping()) for (int i = 0; i < sent; i++) { try { var r = p.Send("1.1.1.1", 1500); if (r.Status == IPStatus.Success) times.Add(r.RoundtripTime); else lost++; } catch { lost++; } }
                if (times.Count > 0) avg = (long)times.Average();
                try { var sw = Stopwatch.StartNew(); Dns.GetHostAddresses("www.microsoft.com"); dns = sw.ElapsedMilliseconds; } catch { }
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    string ip = ""; try { ip = string.Join(", ", ni.GetIPProperties().UnicastAddresses.Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).Select(a => a.Address.ToString())); } catch { }
                    rows.Add(new ModRow { C0 = ni.Name, C1 = ni.NetworkInterfaceType.ToString(), C2 = ni.OperationalStatus == OperationalStatus.Up ? Tx("Connected", "Bersambung") : Tx("Off", "Mati"), C3 = ni.Speed > 0 ? (ni.Speed / 1000000) + " Mbps" : "-", C4 = ip });
                }
            }, () =>
            {
                bool online = avg >= 0;
                var top = Stats(4, Stat(Tx("INTERNET", "INTERNET"), online ? Tx("Online", "Dalam talian") : Tx("Offline", "Luar talian"), "", online ? "#27B77A" : "#ED626B"),
                    Stat(Tx("LATENCY", "KEPENDAMAN"), online ? avg + " ms" : "-", "1.1.1.1", "#3978F6"), Stat(Tx("PACKET LOSS", "KEHILANGAN PAKET"), (lost * 100 / sent) + "%", string.Format(Tx("{0} of {1} lost", "{0} daripada {1} hilang"), lost, sent), lost == 0 ? "#27B77A" : "#F4AE43"),
                    Stat("DNS", dns >= 0 ? dns + " ms" : "-", "www.microsoft.com", "#8B78F6"));
                var dg = Table(false, Tx("Adapter", "Penyesuai") + "|3", Tx("Type", "Jenis") + "|2", "!" + Tx("Status", "Status") + "|1", Tx("Speed", "Kelajuan") + "|1", "IPv4|2"); dg.ItemsSource = rows;
                curMod = modPages["Network"]; foreach (var o in curMod.Children.OfType<UIElement>().Where(c => Grid.GetRow(c) == 1).ToList()) curMod.Children.Remove(o);
                ModBody(Rows(top, TableCard(dg)));
            });
        }

        void ModDrivers()
        {
            ModBegin("Drivers", Tx("Installed device drivers with version and date. KEMAS does not install or replace drivers.", "Pemacu peranti yang dipasang dengan versi dan tarikh. KEMAS tidak memasang atau mengganti pemacu."),
                Btn(Tx("Device Manager", "Pengurus Peranti"), false, () => { try { Process.Start("devmgmt.msc"); } catch { } }), Btn(Tx("Check Windows Update", "Semak Windows Update"), true, () => OpenUri("ms-settings:windowsupdate-optionalupdates")));
            ModBody(Loading(Tx("Reading drivers...", "Membaca pemacu...")));
            List<ModRow> rows = new List<ModRow>();
            Run(() =>
            {
                foreach (var o in Wmi.Q(@"root\cimv2", "SELECT DeviceName, Manufacturer, DriverVersion, DriverDate, DeviceClass FROM Win32_PnPSignedDriver"))
                {
                    var n = Wmi.S(o, "DeviceName"); if (n.Length == 0) continue;
                    var dd = Wmi.S(o, "DriverDate"); string date = dd.Length >= 8 ? dd.Substring(6, 2) + "/" + dd.Substring(4, 2) + "/" + dd.Substring(0, 4) : "";
                    rows.Add(new ModRow { C0 = n, C1 = Wmi.S(o, "Manufacturer"), C2 = Wmi.S(o, "DeviceClass"), C3 = Wmi.S(o, "DriverVersion"), C4 = date, Num = dd.Length >= 8 ? long.Parse(dd.Substring(0, 8)) : 0 });
                }
            }, () =>
            {
                var dg = Table(false, Tx("Device", "Peranti") + "|4", Tx("Manufacturer", "Pengeluar") + "|2", Tx("Class", "Kelas") + "|1", Tx("Version", "Versi") + "|2", Tx("Date", "Tarikh") + "|1");
                dg.ItemsSource = rows.OrderBy(r => r.C2).ThenBy(r => r.C0).ToList();
                var note = Muted(string.Format(Tx("{0} drivers. Updates come from Windows Update or the device maker.", "{0} pemacu. Kemas kini datang daripada Windows Update atau pengeluar peranti."), rows.Count), 12); note.Margin = new Thickness(10, 8, 10, 6);
                curMod = modPages["Drivers"]; foreach (var o in curMod.Children.OfType<UIElement>().Where(c => Grid.GetRow(c) == 1).ToList()) curMod.Children.Remove(o);
                ModBody(TableCard(dg, note));
            });
        }

        void ModSysInfo()
        {
            ModBegin("SysInfo", Tx("Your PC's hardware and Windows details.", "Butiran perkakasan dan Windows PC anda."), Btn(Tx("Copy all", "Salin semua"), false, () => { try { Clipboard.SetText(sysInfoText ?? ""); Toast(Tx("Copied.", "Disalin."), "E73E"); } catch { } }));
            ModBody(Loading(Tx("Reading hardware...", "Membaca perkakasan...")));
            var rows = new List<ModRow>();
            Run(() =>
            {
                Action<string, string> add = (k, v) => { if (!string.IsNullOrEmpty(v)) rows.Add(new ModRow { C0 = k, C1 = v }); };
                add("Windows", Support.WindowsVersion());
                add(Tx("Computer name", "Nama komputer"), Environment.MachineName);
                try { using (var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0")) add("CPU", (k.GetValue("ProcessorNameString") as string ?? "").Trim() + "  ·  " + string.Format(L.T("cores"), Environment.ProcessorCount)); } catch { }
                double total, free; int rp; Memory.Info(out total, out free, out rp); add("RAM", Fmt.Size(total));
                foreach (var o in Wmi.Q(@"root\cimv2", "SELECT Name, AdapterRAM, DriverVersion FROM Win32_VideoController")) add("GPU", Wmi.S(o, "Name") + "  ·  " + Tx("driver ", "pemacu ") + Wmi.S(o, "DriverVersion"));
                try { using (var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS")) { add(Tx("Motherboard", "Papan induk"), (k.GetValue("BaseBoardManufacturer") + " " + k.GetValue("BaseBoardProduct")).Trim()); add(Tx("Computer model", "Model komputer"), (k.GetValue("SystemManufacturer") + " " + k.GetValue("SystemProductName")).Trim()); add("BIOS", (k.GetValue("BIOSVendor") + " " + k.GetValue("BIOSVersion") + "  ·  " + k.GetValue("BIOSReleaseDate")).Trim()); } } catch { }
                foreach (var d in DriveInfo.GetDrives()) try { if (d.IsReady) add(Tx("Drive ", "Cakera ") + d.Name.TrimEnd('\\'), Fmt.Size(d.TotalSize) + "  ·  " + d.DriveFormat + "  ·  " + Fmt.Size(d.AvailableFreeSpace) + Tx(" free", " kosong")); } catch { }
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()) if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback) add(Tx("Network", "Rangkaian"), ni.Description);
                add(Tx("Uptime", "Masa hidup"), TimeSpan.FromMilliseconds(Environment.TickCount & int.MaxValue).ToString(@"d\d\ h\h\ m\m"));
            }, () =>
            {
                sysInfoText = string.Join("\r\n", rows.Select(r => r.C0 + ": " + r.C1));
                var dg = Table(false, Tx("Item", "Item") + "|1", Tx("Details", "Butiran") + "|4"); dg.ItemsSource = rows;
                curMod = modPages["SysInfo"]; foreach (var o in curMod.Children.OfType<UIElement>().Where(c => Grid.GetRow(c) == 1).ToList()) curMod.Children.Remove(o);
                ModBody(TableCard(dg));
            });
        }
        string sysInfoText;

        List<ModRow> shredList = new List<ModRow>();
        void ModShredder()
        {
            DataGrid dg = Table(false, Tx("File", "Fail") + "|3", Tx("Folder", "Folder") + "|4", Tx("Size", "Saiz") + "|1");
            dg.ItemsSource = shredList.ToList();
            ModBegin("Shredder", Tx("Permanently erase files: the content is overwritten before deletion, so it cannot be recovered from the Recycle Bin. On SSDs Windows may still keep old copies internally.", "Padam fail selamanya: kandungan ditulis ganti sebelum dipadam, jadi tidak boleh dipulihkan dari Tong Kitar Semula. Pada SSD, Windows mungkin masih menyimpan salinan lama secara dalaman."),
                Btn(Tx("Add files", "Tambah fail"), false, () =>
                {
                    var dlg = new Microsoft.Win32.OpenFileDialog { Multiselect = true };
                    if (dlg.ShowDialog() == true) { foreach (var f in dlg.FileNames) if (!shredList.Any(r => (string)r.Tag == f)) { var fi = new FileInfo(f); shredList.Add(new ModRow { C0 = fi.Name, C1 = fi.DirectoryName, C2 = Fmt.Size(fi.Length), Num = fi.Length, Tag = f }); } ModShredder(); }
                }),
                Btn(Tx("Clear list", "Kosongkan senarai"), false, () => { shredList.Clear(); ModShredder(); }),
                Btn(Tx("Shred permanently", "Musnahkan selamanya"), false, () =>
                {
                    if (shredList.Count == 0) { Toast(Tx("Add files first.", "Tambah fail dahulu."), "E7BA"); return; }
                    var files = shredList.ToList();
                    Ask(Tx("Shred permanently?", "Musnahkan selamanya?"), string.Format(Tx("{0} files ({1}) will be overwritten and deleted. This cannot be undone and they will not go to the Recycle Bin.", "{0} fail ({1}) akan ditulis ganti dan dipadam. Ini tidak boleh dibatalkan dan tidak masuk Tong Kitar Semula."), files.Count, Fmt.Size(files.Sum(f => f.Num))), Tx("Shred", "Musnahkan"), "E74D", true, () =>
                    {
                        int ok = 0;
                        Run(() =>
                        {
                            var rnd = RandomNumberGenerator.Create(); var buf = new byte[1 << 16];
                            foreach (var f in files)
                            {
                                var p = (string)f.Tag;
                                try
                                {
                                    File.SetAttributes(p, FileAttributes.Normal);
                                    using (var fs = new FileStream(p, FileMode.Open, FileAccess.Write, FileShare.None)) { long left = fs.Length; while (left > 0) { rnd.GetBytes(buf); int n = (int)Math.Min(buf.Length, left); fs.Write(buf, 0, n); left -= n; } fs.Flush(true); }
                                    var np = Path.Combine(Path.GetDirectoryName(p), Guid.NewGuid().ToString("N")); File.Move(p, np); File.Delete(np); ok++;
                                }
                                catch { }
                            }
                        }, () =>
                        {
                            History.Add("clean", ModTitle("Shredder"), string.Format(Tx("{0} files shredded", "{0} fail dimusnahkan"), ok), string.Join("\n", files.Select(f => f.C0)));
                            shredList.Clear(); Toast(string.Format(Tx("{0} of {1} shredded.", "{0} daripada {1} dimusnahkan."), ok, files.Count), "E73E"); ModShredder();
                        });
                    });
                }, true));
            var note = Muted(shredList.Count == 0 ? Tx("No files yet. Use \"Add files\".", "Belum ada fail. Guna \"Tambah fail\".") : string.Format(Tx("{0} files, {1}.", "{0} fail, {1}."), shredList.Count, Fmt.Size(shredList.Sum(f => f.Num))), 12); note.Margin = new Thickness(10, 8, 10, 6);
            ModBody(TableCard(dg, note));
        }

        void ModRestore()
        {
            ModBegin("Restore", Tx("Windows restore points let you roll back system changes. Creating one needs administrator rights; Windows will ask.", "Titik pemulihan Windows membolehkan perubahan sistem dibatalkan. Mencipta satu perlukan hak pentadbir; Windows akan bertanya."),
                Btn(Tx("System Protection", "Perlindungan Sistem"), false, () => { try { Process.Start("SystemPropertiesProtection.exe"); } catch { } }),
                Btn(Tx("Create restore point", "Cipta titik pemulihan"), true, () =>
                {
                    Ask(Tx("Create a restore point?", "Cipta titik pemulihan?"), Tx("Windows will ask for administrator permission, then save a restore point named \"KEMAS\". It takes about a minute.", "Windows akan meminta kebenaran pentadbir, kemudian menyimpan titik pemulihan bernama \"KEMAS\". Kira-kira seminit."), Tx("Create", "Cipta"), "E777", false, () =>
                    {
                        try
                        {
                            var p = Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -WindowStyle Hidden -Command \"Checkpoint-Computer -Description 'KEMAS' -RestorePointType MODIFY_SETTINGS\"") { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden });
                            History.Add("clean", ModTitle("Restore"), Tx("Restore point requested", "Titik pemulihan diminta"));
                            Toast(Tx("Creating restore point...", "Mencipta titik pemulihan..."), "E777");
                            Delay(60000, () => { if (currentPage == "PageRestore") ModRestore(); });
                        }
                        catch { Toast(Tx("Cancelled.", "Dibatalkan."), "E7BA"); }
                    });
                }));
            ModBody(Loading(Tx("Reading restore points...", "Membaca titik pemulihan...")));
            var rows = new List<ModRow>(); bool denied = false;
            Run(() =>
            {
                var q = Wmi.Q(@"root\default", "SELECT Description, CreationTime, SequenceNumber FROM SystemRestore");
                if (q.Count == 0 && !IsAdmin()) denied = true;
                foreach (var o in q) { var ct = Wmi.S(o, "CreationTime"); string d = ct.Length >= 12 ? ct.Substring(6, 2) + "/" + ct.Substring(4, 2) + "/" + ct.Substring(0, 4) + " " + ct.Substring(8, 2) + ":" + ct.Substring(10, 2) : ct; rows.Add(new ModRow { C0 = Wmi.S(o, "Description"), C1 = d, C2 = "#" + Wmi.S(o, "SequenceNumber") }); }
            }, () =>
            {
                var dg = Table(false, Tx("Description", "Penerangan") + "|4", Tx("Created", "Dicipta") + "|2", Tx("Number", "Nombor") + "|1"); dg.ItemsSource = rows.AsEnumerable().Reverse().ToList();
                var note = Muted(denied ? Tx("Windows only lists restore points to administrators. Open KEMAS as administrator to see them, or use System Protection.", "Windows hanya menyenaraikan titik pemulihan kepada pentadbir. Buka KEMAS sebagai pentadbir, atau guna Perlindungan Sistem.") : rows.Count == 0 ? Tx("No restore points yet. System Protection may be off for drive C:.", "Belum ada titik pemulihan. Perlindungan Sistem mungkin mati untuk cakera C:.") : string.Format(Tx("{0} restore points. Restoring is done from System Protection.", "{0} titik pemulihan. Pemulihan dibuat melalui Perlindungan Sistem."), rows.Count), 12);
                note.Margin = new Thickness(10, 8, 10, 6);
                curMod = modPages["Restore"]; foreach (var o in curMod.Children.OfType<UIElement>().Where(c => Grid.GetRow(c) == 1).ToList()) curMod.Children.Remove(o);
                ModBody(TableCard(dg, note));
            });
        }
    }
}
