// KEMAS 4.0 - Game Mode: a speed dial driven by real CPU load, and a Boost switch that changes real Windows settings.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Kemas
{
    public partial class MainWin
    {
        const string PlanUltimate = "e9a42b02-d5df-448d-aa00-03f14749eb61", PlanHigh = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", PlanBalanced = "381b4222-f694-41f0-9685-ff5bb260df2e";

        // dial geometry (canvas 340 x 206, centre at the bottom middle)
        const double DW = 340, DH = 206, DCX = 170, DCY = 166, DR = 120;
        static Point DialPt(double v, double r) { double a = (180 + v * 1.8) * Math.PI / 180; return new Point(DCX + r * Math.Cos(a), DCY + r * Math.Sin(a)); }
        static Geometry DialArc(double v)
        {
            v = Math.Max(0.6, Math.Min(100, v));
            var g = new PathGeometry(new[] { new PathFigure(DialPt(0, DR), new[] { new ArcSegment(DialPt(v, DR), new Size(DR, DR), 0, false, SweepDirection.Clockwise, true) }, false) });
            g.Freeze(); return g;
        }

        static bool WinGameModeOn()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar")) { var v = k == null ? null : k.GetValue("AutoGameModeEnabled"); return v == null || Convert.ToInt32(v) == 1; } } catch { return true; }
        }
        static bool SetWinGameMode(bool on)
        {
            try { using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\GameBar")) k.SetValue("AutoGameModeEnabled", on ? 1 : 0, RegistryValueKind.DWord); return true; } catch { return false; }
        }

        void ModGame()
        {
            ModBegin("Game", Tx("Boost switches Windows to its fastest power plan and turns on Windows Game Mode. The dial shows real CPU load. FPS is not measured.",
                                "Boost menukar Windows ke pelan kuasa terpantas dan menghidupkan Mod Permainan Windows. Dial menunjukkan beban CPU sebenar. FPS tidak diukur."),
                Btn(Tx("Game Bar settings", "Tetapan Game Bar"), false, () => OpenUri("ms-settings:gaming-gamebar")));
            ModBody(Loading(Tx("Reading power plans and running apps...", "Membaca pelan kuasa dan apps yang berjalan...")));
            List<Tuple<string, string, bool>> plans = null; List<ProcRow> heavy = null; bool gm = true;
            ModLoad("Game", () =>
            {
                plans = PowerPlans();
                gm = WinGameModeOn();
                try { heavy = Memory.Groups().Where(g => !g.Protected && g.RamMB > 300).OrderByDescending(g => g.RamMB).Take(6).ToList(); } catch { heavy = new List<ProcRow>(); }
            }, () => BuildGame(plans, heavy, gm));
        }

        void BuildGame(List<Tuple<string, string, bool>> plans, List<ProcRow> heavy, bool gm)
        {
            var act = plans.FirstOrDefault(p => p.Item3);
            var fast = plans.FirstOrDefault(p => p.Item1.ToLowerInvariant() == PlanUltimate) ?? plans.FirstOrDefault(p => p.Item1.ToLowerInvariant() == PlanHigh);
            bool fastOn = fast != null && act != null && act.Item1 == fast.Item1;
            bool boosted = fastOn && gm;

            // ---------------- the arena: dark panel, speed lines, dial, status and the Boost switch
            var arena = new Border { CornerRadius = new CornerRadius(22), ClipToBounds = true, Margin = new Thickness(0, 0, 0, 16), MinHeight = 270 };
            arena.Background = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
                GradientStops = { new GradientStop(Col("#0A1022"), 0), new GradientStop(Col("#101C44"), 0.6), new GradientStop(Col("#1B3A9E"), 1) } };
            var root = new Grid();
            arena.Child = root;
            root.Children.Add(new Rectangle { IsHitTestVisible = false, Fill = new RadialGradientBrush { Center = new Point(0.25, 0.8), GradientOrigin = new Point(0.25, 0.8), RadiusX = 0.6, RadiusY = 0.9,
                GradientStops = { new GradientStop(A("#2F6BF0", 0x40), 0), new GradientStop(A("#2F6BF0", 0), 1) } } });
            root.Children.Add(SpeedLines(boosted));

            var row = new Grid { Margin = new Thickness(28, 22, 28, 22) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            root.Children.Add(row);

            // dial
            var dial = new Canvas { Width = DW, Height = DH };
            dial.Children.Add(new Path { Data = DialArc(100), Stroke = new SolidColorBrush(A("#FFFFFF", 0x16)), StrokeThickness = 18, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            var heat = new LinearGradientBrush { MappingMode = BrushMappingMode.Absolute, StartPoint = new Point(DCX - DR, 0), EndPoint = new Point(DCX + DR, 0),
                GradientStops = { new GradientStop(Col("#24C8F4"), 0), new GradientStop(Col("#3478F6"), 0.45), new GradientStop(Col("#8B5CF6"), 0.75), new GradientStop(Col("#F0466B"), 1) } };
            var glow = new Path { Stroke = heat, StrokeThickness = 30, Opacity = 0.18, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            var arc = new Path { Stroke = heat, StrokeThickness = 18, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            dial.Children.Add(glow); dial.Children.Add(arc);
            var ticks = new StreamGeometry();
            using (var c = ticks.Open())
                for (int i = 0; i <= 20; i++)
                {
                    double v = i * 5, r2 = i % 5 == 0 ? DR + 22 : DR + 17;
                    c.BeginFigure(DialPt(v, DR + 12), false, false); c.LineTo(DialPt(v, r2), true, false);
                }
            ticks.Freeze();
            dial.Children.Add(new Path { Data = ticks, Stroke = new SolidColorBrush(A("#BFD4FF", 0x66)), StrokeThickness = 1.5 });
            foreach (var v in new[] { 0, 25, 50, 75, 100 })
            {
                var p = DialPt(v, DR + 36);
                var t = new TextBlock { Text = v.ToString(), FontSize = 12.5, Foreground = new SolidColorBrush(Col("#7F93B8")), Width = 30, TextAlignment = TextAlignment.Center };
                Canvas.SetLeft(t, p.X - 15); Canvas.SetTop(t, p.Y - 9); dial.Children.Add(t);
            }
            var needleRot = new RotateTransform(180, DCX, DCY);
            // pointer rides just inside the arc, so the reading in the middle stays clear
            var needle = new Canvas { Width = DW, Height = DH, RenderTransform = needleRot };
            needle.Children.Add(new Polygon { Points = new PointCollection { new Point(DCX + DR - 50, DCY - 6), new Point(DCX + DR - 13, DCY), new Point(DCX + DR - 50, DCY + 6) }, Fill = new LinearGradientBrush(A("#FFFFFF", 0x55), Colors.White, 0) });
            needle.Children.Add(Place(new Ellipse { Width = 26, Height = 26, Fill = new RadialGradientBrush(A("#BFF3FF", 0xAA), A("#7FE3FF", 0)) }, DCX + DR - 22, DCY - 13));
            dial.Children.Add(needle);
            var val = new TextBlock { Text = "-", FontSize = 44, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Width = 160, TextAlignment = TextAlignment.Center };
            Canvas.SetLeft(val, DCX - 80); Canvas.SetTop(val, DCY - 74); dial.Children.Add(val);
            var cap = new TextBlock { Text = Tx("CPU LOAD", "BEBAN CPU"), FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Col("#9FB0D0")), Width = 160, TextAlignment = TextAlignment.Center };
            Canvas.SetLeft(cap, DCX - 80); Canvas.SetTop(cap, DCY - 16); dial.Children.Add(cap);
            row.Children.Add(new Viewbox { Child = dial, MaxWidth = DW, StretchDirection = StretchDirection.DownOnly, VerticalAlignment = VerticalAlignment.Center });

            // status column
            var side = new StackPanel { Margin = new Thickness(32, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(side, 1); row.Children.Add(side);
            var chip = new Border { CornerRadius = new CornerRadius(13), Padding = new Thickness(12, 4, 14, 5), HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(A(boosted ? "#22B573" : "#3478F6", 0x26)), BorderBrush = new SolidColorBrush(A(boosted ? "#22B573" : "#7FB0FF", 0x77)), BorderThickness = new Thickness(1) };
            var chipSp = new StackPanel { Orientation = Orientation.Horizontal };
            var cdot = new Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(Col(boosted ? "#34D399" : "#7FB0FF")), VerticalAlignment = VerticalAlignment.Center };
            if (!S.ReducedMotion && boosted) cdot.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0.3, TimeSpan.FromSeconds(0.8)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            chipSp.Children.Add(cdot);
            chipSp.Children.Add(new TextBlock { Text = boosted ? Tx("BOOST ON", "BOOST HIDUP") : Tx("STANDARD", "BIASA"), FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Col(boosted ? "#A7F3D0" : "#CFE0FF")), Margin = new Thickness(8, 0, 0, 0) });
            chip.Child = chipSp; side.Children.Add(chip);
            side.Children.Add(new TextBlock { Text = boosted ? Tx("Ready to play", "Sedia untuk bermain") : Tx("Boost your PC for games", "Boost PC anda untuk permainan"), FontSize = 26, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 12, 0, 4), TextWrapping = TextWrapping.Wrap });
            side.Children.Add(new TextBlock { Text = boosted ? Tx("Fastest power plan and Windows Game Mode are on. Turn Boost off to go back to your usual plan.", "Pelan kuasa terpantas dan Mod Permainan Windows hidup. Matikan Boost untuk kembali ke pelan biasa.")
                                                             : Tx("One switch: fastest power plan and Windows Game Mode. Nothing is closed for you.", "Satu suis: pelan kuasa terpantas dan Mod Permainan Windows. Tiada apps ditutup untuk anda."),
                FontSize = 14, Foreground = new SolidColorBrush(Col("#9FB0D0")), TextWrapping = TextWrapping.Wrap, MaxWidth = 460, HorizontalAlignment = HorizontalAlignment.Left });

            // live meters
            var meters = new Grid { Margin = new Thickness(0, 18, 0, 0), MaxWidth = 460, HorizontalAlignment = HorizontalAlignment.Left };
            meters.ColumnDefinitions.Add(new ColumnDefinition()); meters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); meters.ColumnDefinitions.Add(new ColumnDefinition());
            TextBlock ramVal; Border ramFill;
            var ramBox = Meter(Tx("Memory", "Memori"), "E964", out ramVal, out ramFill);
            meters.Children.Add(ramBox);
            var planBox = InfoTile(Tx("Power plan", "Pelan kuasa"), act == null ? "-" : act.Item2, fastOn ? "#34D399" : "#F3A83B");
            Grid.SetColumn(planBox, 2); meters.Children.Add(planBox);
            side.Children.Add(meters);

            var btns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 0) };
            if (fast == null)
                btns.Children.Add(new TextBlock { Text = Tx("Windows did not list a High or Ultimate performance plan on this PC.", "Windows tidak menyenaraikan pelan High atau Ultimate performance pada PC ini."), Foreground = new SolidColorBrush(Col("#F3A83B")), FontSize = 14, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 });
            else if (!boosted)
            {
                var b = Btn("", true, () => AskBoost(fast, act, gm)); b.Content = MakeBtnContent("E945", Tx("Turn on Boost", "Hidupkan Boost")); b.Height = 48; b.Padding = new Thickness(28, 0, 28, 0);
                btns.Children.Add(b);
            }
            else
            {
                var b = new Button { Style = (Style)W.FindResource("DarkGhost"), Content = MakeBtnContent("E777", Tx("Turn off Boost", "Matikan Boost")), Height = 48, Padding = new Thickness(24, 0, 24, 0) };
                b.Click += (s, e) => EndBoost(plans);
                btns.Children.Add(b);
            }
            side.Children.Add(btns);

            // ---------------- readiness list (real checks)
            var list = new StackPanel();
            list.Children.Add(CheckRow(Tx("Windows Game Mode", "Mod Permainan Windows"), gm ? Tx("On. Windows gives the game you are playing priority.", "Hidup. Windows mengutamakan permainan yang sedang dimainkan.") : Tx("Off.", "Mati."), gm, gm ? null : Tx("Turn on", "Hidupkan"), () =>
                { if (SetWinGameMode(true)) History.Add("startup", ModTitle("Game"), Tx("Windows Game Mode on", "Mod Permainan Windows hidup")); ModGame(); }));
            list.Children.Add(CheckRow(Tx("Power plan", "Pelan kuasa"), act == null ? "-" : act.Item2 + (fastOn ? "" : Tx(" (not the fastest plan)", " (bukan pelan terpantas)")), fastOn, null, null));
            list.Children.Add(CheckRow(Tx("Heavy background apps", "Apps latar berat"), heavy.Count == 0 ? Tx("None using over 300 MB.", "Tiada yang guna lebih 300 MB.") : string.Join(", ", heavy.Select(h => h.App + " " + h.RAM)), heavy.Count == 0,
                heavy.Count == 0 ? null : Tx("Review in Memory", "Semak di Memori"), () => E<RadioButton>("NavRam").IsChecked = true));
            list.Children.Add(CheckRow(Tx("Game Bar and captures", "Game Bar dan rakaman"), Tx("Recording in the background can cost frames. Manage it in Windows settings.", "Rakaman di latar boleh mengurangkan prestasi. Urus dalam tetapan Windows."), true, Tx("Open", "Buka"), () => OpenUri("ms-settings:gaming-captures")));
            var listCard = CardOf(list, 22);
            var head = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            head.Children.Add(Strong(Tx("Readiness check", "Semakan kesediaan"), 17));
            list.Children.Insert(0, head);

            ModBody(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new StackPanel { Children = { arena, listCard } } });

            // ---------------- live: needle and memory meter follow real samples
            Cpu.Sample();
            double shown = 0;
            Action tick = () =>
            {
                int cpu = Cpu.Sample();
                if (cpu >= 0)
                {
                    val.Text = cpu + "%";
                    double to = 180 + cpu * 1.8;
                    if (S.ReducedMotion) needleRot.Angle = to;
                    else needleRot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(needleRot.Angle, to, TimeSpan.FromMilliseconds(650)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                    shown = cpu; arc.Data = DialArc(shown); glow.Data = arc.Data;
                }
                double total, free; int rp; Memory.Info(out total, out free, out rp);
                ramVal.Text = rp + "%"; ramFill.Width = Math.Max(4, 1.6 * rp);
                ramFill.Background = new SolidColorBrush(Col(rp >= 85 ? "#F0466B" : rp >= 70 ? "#F3A83B" : "#24C8F4"));
            };
            arc.Data = DialArc(0); glow.Data = arc.Data;
            modTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            modTimer.Tick += (s, e) => { if (currentPage != "PageGame") { modTimer.Stop(); return; } tick(); };
            modTimer.Start();
            Delay(250, () => { if (currentPage == "PageGame") tick(); });
        }

        /// Diagonal light streaks drifting left: faster while Boost is on. Decoration only.
        UIElement SpeedLines(bool fast)
        {
            var cv = new Canvas { IsHitTestVisible = false, ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Right, Width = 520, Height = 270, VerticalAlignment = VerticalAlignment.Top };
            var rnd = new Random(3);
            for (int i = 0; i < 9; i++)
            {
                double w = 70 + rnd.Next(120), y = 18 + i * 26 + rnd.Next(10);
                var r = new Rectangle { Width = w, Height = i % 3 == 0 ? 2.5 : 1.5, RadiusX = 1, RadiusY = 1,
                    Fill = new LinearGradientBrush(A("#7FE3FF", 0), A(i % 2 == 0 ? "#7FE3FF" : "#8B5CF6", (byte)(fast ? 0x99 : 0x55)), 0) };
                var tt = new TranslateTransform(); r.RenderTransform = tt;
                Canvas.SetLeft(r, 120 + rnd.Next(380)); Canvas.SetTop(r, y);
                cv.Children.Add(r);
                if (!S.ReducedMotion)
                {
                    double secs = (fast ? 0.9 : 2.6) + rnd.NextDouble() * (fast ? 0.6 : 1.6);
                    var begin = TimeSpan.FromSeconds(rnd.NextDouble() * secs);
                    r.Opacity = 0;
                    tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(80, -260, TimeSpan.FromSeconds(secs)) { RepeatBehavior = RepeatBehavior.Forever, BeginTime = begin });
                    r.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(secs), RepeatBehavior = RepeatBehavior.Forever, BeginTime = begin,
                        KeyFrames = { new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)), new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.3)), new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.7)), new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)) } });
                }
            }
            return cv;
        }

        Border Meter(string label, string glyph, out TextBlock value, out Border fill)
        {
            var b = new Border { CornerRadius = new CornerRadius(14), Background = new SolidColorBrush(A("#FFFFFF", 0x0D)), BorderBrush = new SolidColorBrush(A("#FFFFFF", 0x1A)), BorderThickness = new Thickness(1), Padding = new Thickness(14, 10, 14, 12) };
            var sp = new StackPanel();
            var top = new Grid();
            top.Children.Add(new TextBlock { Text = label, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Col("#9FB0D0")) });
            value = new TextBlock { Text = "-", FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Right };
            top.Children.Add(value); sp.Children.Add(top);
            var track = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(A("#FFFFFF", 0x1A)), Margin = new Thickness(0, 10, 0, 0), Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
            fill = new Border { Height = 6, CornerRadius = new CornerRadius(3), Width = 4, HorizontalAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(Col("#24C8F4")) };
            track.Child = fill; sp.Children.Add(track);
            b.Child = sp; return b;
        }
        Border InfoTile(string label, string value, string dotHex)
        {
            var b = new Border { CornerRadius = new CornerRadius(14), Background = new SolidColorBrush(A("#FFFFFF", 0x0D)), BorderBrush = new SolidColorBrush(A("#FFFFFF", 0x1A)), BorderThickness = new Thickness(1), Padding = new Thickness(14, 10, 14, 12) };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = label, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Col("#9FB0D0")) });
            var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            r.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(Col(dotHex)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            r.Children.Add(new TextBlock { Text = value, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 170 });
            sp.Children.Add(r);
            b.Child = sp; return b;
        }

        void AskBoost(Tuple<string, string, bool> fast, Tuple<string, string, bool> current, bool gm)
        {
            var what = new List<string> { string.Format(Tx("Power plan: {0} → {1}", "Pelan kuasa: {0} → {1}"), current == null ? "-" : current.Item2, fast.Item2) };
            if (!gm) what.Add(Tx("Windows Game Mode: off → on", "Mod Permainan Windows: mati → hidup"));
            Ask(Tx("Turn on Boost?", "Hidupkan Boost?"),
                string.Join("\n", what) + "\n\n" + Tx("Your PC will use more power and may run warmer. Turn Boost off here to go back to your current plan. No apps are closed.", "PC akan guna lebih kuasa dan mungkin lebih panas. Matikan Boost di sini untuk kembali ke pelan semasa. Tiada apps ditutup."),
                Tx("Turn on", "Hidupkan"), "E945", false, () =>
                {
                    if (current != null && current.Item1 != fast.Item1) { S.BoostPrevPlan = current.Item1; QueueSave(); }
                    bool ok = SetPlan(fast.Item1);
                    if (!gm) SetWinGameMode(true);
                    if (ok) { History.Add("startup", ModTitle("Game"), Tx("Boost on: ", "Boost hidup: ") + fast.Item2); Toast(Tx("Boost is on.", "Boost hidup."), "E945"); }
                    else Toast(Tx("Windows did not allow the change.", "Windows tidak membenarkan perubahan."), "E7BA");
                    ModGame();
                });
        }

        void EndBoost(List<Tuple<string, string, bool>> plans)
        {
            var back = plans.FirstOrDefault(p => p.Item1.Equals(S.BoostPrevPlan ?? "", StringComparison.OrdinalIgnoreCase)) ?? plans.FirstOrDefault(p => p.Item1.ToLowerInvariant() == PlanBalanced);
            if (back == null) { Toast(Tx("No plan to go back to. Choose one in Optimization Profiles.", "Tiada pelan untuk kembali. Pilih dalam Profil Pengoptimuman."), "E7BA"); return; }
            if (SetPlan(back.Item1)) { History.Add("startup", ModTitle("Game"), Tx("Boost off: ", "Boost mati: ") + back.Item2); Toast(back.Item2, "E73E"); S.BoostPrevPlan = ""; QueueSave(); }
            else Toast(Tx("Windows did not allow the change.", "Windows tidak membenarkan perubahan."), "E7BA");
            ModGame();
        }
    }
}
