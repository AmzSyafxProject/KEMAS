using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Kemas
{
    /// KEMAS Scan Engine: the full-window screen shown while a scan, clean or other long task runs.
    /// The orbital scanner is drawn and animated in code (no images or video). Every state change comes
    /// from the real operation: SetOvItem / OvProgress / ShowDone call EngStage, never a timer.
    public partial class MainWin
    {
        string engMode = "op";              // scan | clean | op
        string engStage = "";
        DateTime engStart;
        DispatcherTimer engClock;
        Action engFinish, engAgain;
        int engDone, engTotal, engIssues;
        readonly List<Tuple<string, string, string>> engDetails = new List<Tuple<string, string, string>>();   // name, value, state

        // scene parts
        Canvas scn;
        readonly List<Tuple<RotateTransform, double>> scnSpins = new List<Tuple<RotateTransform, double>>();
        RotateTransform scnBeamRot; Canvas scnBeam;
        Ellipse scnTint, scnGlow; ScaleTransform scnGlowScale; TranslateTransform scnFloat;
        Image scnLogo; TextBlock scnMark;
        readonly List<UIElement> scnRings = new List<UIElement>();
        readonly List<UIElement> scnParticles = new List<UIElement>();
        const double SC = 220;               // scene centre (scene is 440 x 440)

        static Color Col(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
        static Color A(string hex, byte a) { var c = Col(hex); return Color.FromArgb(a, c.R, c.G, c.B); }

        // ------------------------------------------------------------ scene
        void StartScene()
        {
            scnSpins.Clear(); scnRings.Clear(); scnParticles.Clear();
            scn = new Canvas { Width = 440, Height = 440, IsHitTestVisible = false };
            bool still = S.ReducedMotion;

            // back glow
            scnGlowScale = new ScaleTransform(1, 1, 150, 150);
            scnGlow = new Ellipse { Width = 300, Height = 300, RenderTransform = scnGlowScale,
                Fill = new RadialGradientBrush { GradientStops = { new GradientStop(A("#2F6BF0", 0x66), 0), new GradientStop(A("#24C8F4", 0x22), 0.55), new GradientStop(A("#24C8F4", 0), 1) } } };
            Place(scnGlow, SC - 150, SC - 150); scn.Children.Add(scnGlow);

            // outer scale ring with ticks, very slow
            var ticks = new StreamGeometry();
            using (var g = ticks.Open())
                for (int i = 0; i < 72; i++)
                {
                    double a = i * Math.PI * 2 / 72, r1 = 204, r2 = i % 6 == 0 ? 214 : 209;
                    g.BeginFigure(new Point(SC + r1 * Math.Cos(a), SC + r1 * Math.Sin(a)), false, false);
                    g.LineTo(new Point(SC + r2 * Math.Cos(a), SC + r2 * Math.Sin(a)), true, false);
                }
            ticks.Freeze();
            AddRing(new Path { Data = ticks, Stroke = new SolidColorBrush(A("#9FC4FF", 0x40)), StrokeThickness = 1.2 }, 90);
            AddRing(Circle(198, new SolidColorBrush(A("#9FC4FF", 0x1C)), 1), 0);

            // tilted orbits give the scanner depth
            AddOrbit(186, -22, 0.32, 9, "#24C8F4");
            AddOrbit(168, 28, 0.30, -12, "#7FB0FF");

            // main gradient arc with a light at its head
            var arc = new Canvas { Width = 440, Height = 440 };
            var arcPath = new Path { Data = Arc(170, -200, 40), StrokeThickness = 3, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                Stroke = new LinearGradientBrush(A("#3478F6", 0), A("#7FE3FF", 0xFF), new Point(0, 1), new Point(1, 0)) };
            arc.Children.Add(arcPath);
            arc.Children.Add(Dot(SC + 170 * Math.Cos(40 * Math.PI / 180), SC + 170 * Math.Sin(40 * Math.PI / 180), 5, "#BFF3FF"));
            AddRing(arc, 14);

            // dashed ring, opposite direction
            var dashed = Circle(146, new SolidColorBrush(A("#24C8F4", 0x66)), 2); dashed.StrokeDashArray = new DoubleCollection { 1, 5 };
            AddRing(dashed, -22);

            // inner ring with three data points
            var inner = new Canvas { Width = 440, Height = 440 };
            inner.Children.Add(Circle(112, new SolidColorBrush(A("#7FB0FF", 0x38)), 1.2));
            for (int i = 0; i < 3; i++) { double a = i * 2 * Math.PI / 3; inner.Children.Add(Dot(SC + 112 * Math.Cos(a), SC + 112 * Math.Sin(a), 3.5, "#7FE3FF")); }
            AddRing(inner, 8);

            // scanning beam: a soft wedge with a bright leading edge
            scnBeam = new Canvas { Width = 440, Height = 440, Opacity = 0 };
            double R = 178, span = 64;
            var lead = new Point(SC + R, SC);
            var tail = new Point(SC + R * Math.Cos(-span * Math.PI / 180), SC + R * Math.Sin(-span * Math.PI / 180));
            var wedge = new PathGeometry(new[] { new PathFigure(new Point(SC, SC), new PathSegment[] { new LineSegment(tail, false), new ArcSegment(lead, new Size(R, R), 0, false, SweepDirection.Clockwise, false) }, true) });
            wedge.Freeze();
            scnBeam.Children.Add(new Path { Data = wedge, Fill = new LinearGradientBrush(A("#24C8F4", 0), A("#24C8F4", 0x55), 0) { MappingMode = BrushMappingMode.Absolute, StartPoint = tail, EndPoint = lead } });
            scnBeam.Children.Add(new Line { X1 = SC, Y1 = SC, X2 = lead.X, Y2 = lead.Y, StrokeThickness = 2, StrokeEndLineCap = PenLineCap.Round,
                Stroke = new LinearGradientBrush(A("#7FE3FF", 0x10), A("#DFF8FF", 0xEE), 0) });
            scnBeamRot = new RotateTransform(0, SC, SC); scnBeam.RenderTransform = scnBeamRot;
            scn.Children.Add(scnBeam);

            // floating data particles (few, slow)
            var rnd = new Random(7);
            for (int i = 0; i < 16; i++)
            {
                double a = rnd.NextDouble() * Math.PI * 2, r = 84 + rnd.NextDouble() * 120, size = 1.6 + rnd.NextDouble() * 2.2;
                var d = Dot(SC + r * Math.Cos(a), SC + r * Math.Sin(a), size, i % 3 == 0 ? "#BFD4FF" : "#7FE3FF");
                d.Opacity = 0.25;
                var tt = new TranslateTransform(); d.RenderTransform = tt;
                scn.Children.Add(d); scnParticles.Add(d);
                if (!still)
                {
                    d.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.12, 0.85, TimeSpan.FromSeconds(1.8 + rnd.NextDouble() * 2.4)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(rnd.NextDouble() * 2) });
                    tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -6 - rnd.NextDouble() * 8, TimeSpan.FromSeconds(3 + rnd.NextDouble() * 3)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
                }
            }

            // the KEMAS core
            var core = new Canvas { Width = 440, Height = 440 };
            scnFloat = new TranslateTransform(); core.RenderTransform = scnFloat;
            core.Children.Add(Place(new Ellipse { Width = 150, Height = 150, Fill = new RadialGradientBrush(A("#24C8F4", 0x55), A("#24C8F4", 0)) }, SC - 75, SC - 75));
            var body = new Ellipse { Width = 124, Height = 124, StrokeThickness = 1.5, Stroke = new LinearGradientBrush(A("#BFE6FF", 0xAA), A("#3478F6", 0x40), 90),
                Fill = new RadialGradientBrush { GradientOrigin = new Point(0.35, 0.28), Center = new Point(0.42, 0.38), RadiusX = 0.7, RadiusY = 0.7,
                    GradientStops = { new GradientStop(Col("#6FB4FF"), 0), new GradientStop(Col("#2F6BF0"), 0.55), new GradientStop(Col("#173A9E"), 1) } } };
            core.Children.Add(Place(body, SC - 62, SC - 62));
            scnTint = new Ellipse { Width = 124, Height = 124, Opacity = 0, Fill = new SolidColorBrush(Col("#24C8F4")) };
            core.Children.Add(Place(scnTint, SC - 62, SC - 62));
            core.Children.Add(Place(new Ellipse { Width = 86, Height = 40, Fill = new LinearGradientBrush(A("#FFFFFF", 0x5A), A("#FFFFFF", 0), 90) }, SC - 43, SC - 56));
            scnLogo = new Image { Width = 58, Height = 58, Source = Logo.Image() };
            core.Children.Add(Place(scnLogo, SC - 29, SC - 29));
            scnMark = new TextBlock { FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 44, Foreground = Brushes.White, Width = 124, TextAlignment = TextAlignment.Center, Opacity = 0 };
            core.Children.Add(Place(scnMark, SC - 62, SC - 24));
            scn.Children.Add(core);

            E<ContentControl>("SceneHost").Content = scn;

            // intro: core lights up, rings appear one after another
            if (!still)
            {
                scnFloat.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(3, -4, TimeSpan.FromSeconds(2.6)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
                core.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, 380));
                int k = 0;
                foreach (var r in scnRings) { r.Opacity = 0; var a = Anim(0, 1, 420); a.BeginTime = TimeSpan.FromMilliseconds(120 + 90 * k++); r.BeginAnimation(UIElement.OpacityProperty, a); }
            }
            foreach (var sp in scnSpins) Spin(sp.Item1, sp.Item2);
            Pulse(2.6);
        }

        void StopScene()
        {
            if (engClock != null) engClock.Stop();
            E<ContentControl>("SceneHost").Content = null;     // drops the scene and its animations from memory
            scn = null; scnSpins.Clear(); scnRings.Clear(); scnParticles.Clear();
            var sh = E<Border>("EngShimmer"); sh.BeginAnimation(UIElement.OpacityProperty, null); sh.Opacity = 0;
            ((TranslateTransform)sh.RenderTransform).BeginAnimation(TranslateTransform.XProperty, null);
        }

        static T Place<T>(T el, double x, double y) where T : UIElement { Canvas.SetLeft(el, x); Canvas.SetTop(el, y); return el; }
        static Ellipse Circle(double r, Brush stroke, double th) { return Place(new Ellipse { Width = r * 2, Height = r * 2, Stroke = stroke, StrokeThickness = th }, SC - r, SC - r); }
        static Ellipse Dot(double x, double y, double r, string hex)
        {
            var c = Col(hex);
            return Place(new Ellipse { Width = r * 2, Height = r * 2, Fill = new RadialGradientBrush(c, Color.FromArgb(0x30, c.R, c.G, c.B)) }, x - r, y - r);
        }
        static Geometry Arc(double r, double fromDeg, double toDeg)
        {
            Func<double, Point> p = d => new Point(SC + r * Math.Cos(d * Math.PI / 180), SC + r * Math.Sin(d * Math.PI / 180));
            var g = new PathGeometry(new[] { new PathFigure(p(fromDeg), new[] { new ArcSegment(p(toDeg), new Size(r, r), 0, toDeg - fromDeg > 180, SweepDirection.Clockwise, true) }, false) });
            g.Freeze(); return g;
        }
        void AddRing(UIElement el, double period)
        {
            var host = el as Canvas ?? new Canvas { Width = 440, Height = 440, Children = { el } };
            var rt = new RotateTransform(0, SC, SC); host.RenderTransform = rt;
            scn.Children.Add(host); scnRings.Add(host);
            if (period != 0) scnSpins.Add(Tuple.Create(rt, period));
        }
        /// An orbit seen at an angle: a flattened ring whose particle travels along the ellipse.
        void AddOrbit(double r, double tilt, double flat, double period, string hex)
        {
            var spin = new Canvas { Width = 440, Height = 440 };
            spin.Children.Add(Circle(r, new SolidColorBrush(A(hex, 0x3A)), 1.4));
            spin.Children.Add(Dot(SC + r, SC, 5, hex));
            var rt = new RotateTransform(0, SC, SC); spin.RenderTransform = rt;
            var tiltHost = new Canvas { Width = 440, Height = 440, Children = { spin } };
            tiltHost.RenderTransform = new TransformGroup { Children = { new ScaleTransform(1, flat, SC, SC), new RotateTransform(tilt, SC, SC) } };
            scn.Children.Add(tiltHost); scnRings.Add(tiltHost);
            scnSpins.Add(Tuple.Create(rt, period));
        }
        /// Continuous rotation that starts from wherever the ring is now, so speed changes never jump.
        void Spin(RotateTransform rt, double period)
        {
            double a = rt.Angle % 360;
            if (S.ReducedMotion || period == 0) { rt.BeginAnimation(RotateTransform.AngleProperty, null); rt.Angle = a; return; }
            rt.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(a, a + 360 * Math.Sign(period), TimeSpan.FromSeconds(Math.Abs(period))) { RepeatBehavior = RepeatBehavior.Forever });
        }
        void Pulse(double seconds)
        {
            if (scnGlowScale == null) return;
            if (S.ReducedMotion || seconds <= 0) { scnGlowScale.BeginAnimation(ScaleTransform.ScaleXProperty, null); scnGlowScale.BeginAnimation(ScaleTransform.ScaleYProperty, null); return; }
            var a = new DoubleAnimation(0.94, 1.08, TimeSpan.FromSeconds(seconds / 2)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
            scnGlowScale.BeginAnimation(ScaleTransform.ScaleXProperty, a); scnGlowScale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
        }
        void Beam(bool on, double period)
        {
            if (scnBeam == null) return;
            scnBeam.BeginAnimation(UIElement.OpacityProperty, Anim(scnBeam.Opacity, on ? (S.ReducedMotion ? 0.6 : 1) : 0, on ? 300 : 500));
            if (on) Spin(scnBeamRot, period); else Spin(scnBeamRot, 0);
        }
        void SlowRings(double factor) { foreach (var sp in scnSpins) Spin(sp.Item1, sp.Item2 * factor); }
        void CoreTint(string hex, double op, string mark)
        {
            if (scnTint == null) return;
            scnTint.Fill = new SolidColorBrush(Col(hex));
            scnTint.BeginAnimation(UIElement.OpacityProperty, Anim(scnTint.Opacity, op, 450));
            scnLogo.BeginAnimation(UIElement.OpacityProperty, Anim(scnLogo.Opacity, mark == null ? 1 : 0, 300));
            if (mark != null) scnMark.Text = Glyph(mark);
            scnMark.BeginAnimation(UIElement.OpacityProperty, Anim(scnMark.Opacity, mark == null ? 0 : 1, 350));
        }

        // ------------------------------------------------------------ lifecycle
        /// init | scan | clean | op | analyze | done | partial | cancel | error
        void EngStage(string st)
        {
            if (st == engStage) return;
            engStage = st;
            string label, dot, pill; string[] name;
            switch (st)
            {
                case "init": label = Tx("INITIALIZING", "MEMULAKAN"); dot = "#4A93FF"; pill = "#3478F6"; name = new[] { "Preparing", "Bersedia" }; break;
                case "scan": label = Tx("SCANNING", "MENGIMBAS"); dot = "#7FE3FF"; pill = "#24C8F4"; name = new[] { "Scanning", "Mengimbas" }; break;
                case "clean": label = Tx("CLEANING", "MEMBERSIH"); dot = "#7FE3FF"; pill = "#24C8F4"; name = new[] { "Cleaning", "Membersih" }; break;
                case "analyze": label = Tx("ANALYZING", "MENGANALISIS"); dot = "#B7A8FF"; pill = "#8B78F6"; name = new[] { "Analysing", "Menganalisis" }; break;
                case "done": label = Tx("COMPLETED", "SELESAI"); dot = "#22B573"; pill = "#22B573"; name = new[] { "Completed", "Selesai" }; break;
                case "partial": label = Tx("PARTIAL RESULT", "HASIL SEPARA"); dot = "#F3A83B"; pill = "#F3A83B"; name = new[] { "Partly done", "Sebahagian" }; break;
                case "cancel": label = Tx("CANCELLED", "DIBATALKAN"); dot = "#9AA4B8"; pill = "#7D8BA5"; name = new[] { "Stopped", "Dihentikan" }; break;
                case "error": label = Tx("ERROR", "RALAT"); dot = "#E85D6A"; pill = "#E85D6A"; name = new[] { "Failed", "Gagal" }; break;
                default: label = Tx("WORKING", "BEKERJA"); dot = "#7FE3FF"; pill = "#24C8F4"; name = new[] { "Working", "Bekerja" }; break;
            }
            TB("EngStatus").Text = label;
            TB("EngStageName").Text = Tx(name[0], name[1]);
            var d = E<Ellipse>("EngDot"); d.Fill = new SolidColorBrush(Col(dot));
            var p = E<Border>("EngPill"); p.Background = new SolidColorBrush(A(pill, 0x1F)); p.BorderBrush = new SolidColorBrush(A(pill, 0x66));
            bool live = st == "init" || st == "scan" || st == "clean" || st == "op" || st == "analyze";
            if (live && !S.ReducedMotion) d.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0.3, TimeSpan.FromSeconds(0.8)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            else { d.BeginAnimation(UIElement.OpacityProperty, null); d.Opacity = 1; }

            switch (st)
            {
                case "init": Beam(false, 0); CoreTint("#24C8F4", 0, null); Pulse(2.6); break;
                case "scan": case "clean": case "op": Beam(true, 3.2); CoreTint("#24C8F4", 0, null); Pulse(2.6); break;
                case "analyze": Beam(true, 7); CoreTint("#24C8F4", 0.35, null); Pulse(1.3); break;
                case "done": Beam(false, 0); SlowRings(3); CoreTint("#22B573", 0.9, "E73E"); Pulse(4); break;
                case "partial": Beam(false, 0); SlowRings(3); CoreTint("#F3A83B", 0.85, "E7BA"); Pulse(4); break;
                case "cancel": Beam(false, 0); SlowRings(4); CoreTint("#7D8BA5", 0.85, "E71A"); Pulse(0); break;
                case "error": Beam(false, 0); SlowRings(4); CoreTint("#E85D6A", 0.85, "E783"); Pulse(0); break;
            }
            if (!live) { if (engClock != null) engClock.Stop(); Shimmer(false); }
        }

        /// Called by ShowOverlay: resets counters, starts the clock.
        void EngBegin(int total)
        {
            engStage = ""; engDone = 0; engIssues = 0; engTotal = total; engStart = DateTime.Now;
            E<Image>("EngLogo").Source = Logo.Image();
            TB("EngDevice").Text = Environment.MachineName;
            TB("EngTagline").Text = Tx("Intelligent PC Analysis", "Analisis PC Pintar");
            TB("EngItems").Text = "0 / " + total;
            TB("EngItemsLbl").Text = engMode == "scan" ? Tx("Categories scanned", "Kategori diimbas") : Tx("Items processed", "Item diproses");
            TB("EngFound").Text = engMode == "op" ? "0" : "-";
            TB("EngFoundLbl").Text = engMode == "scan" ? Tx("Identified", "Dikenal pasti") : engMode == "clean" ? Tx("Cleaned", "Dibersihkan") : Tx("Issues", "Isu");
            TB("EngElapsed").Text = "0:00"; TB("EngElapsedLbl").Text = Tx("Elapsed", "Masa berlalu");
            TB("EngCatLbl").Text = engMode == "op" ? Tx("STEPS", "LANGKAH") : Tx("CATEGORIES", "KATEGORI");
            TB("EngActLbl").Text = Tx("LIVE ACTIVITY", "AKTIVITI LANGSUNG");
            TB("DoneDetLbl").Text = Tx("SUMMARY", "RINGKASAN");
            E<StackPanel>("EngActivity").Children.Clear();
            engDetails.Clear();
            Activity(engMode == "scan" ? Tx("Preparing system analysis...", "Menyediakan analisis sistem...") : Tx("Preparing...", "Bersedia..."), "info");
            if (engClock == null)
            {
                engClock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                engClock.Tick += (s, e) => TB("EngElapsed").Text = Clock(DateTime.Now - engStart);
            }
            engClock.Start();
            EngStage("init");
        }
        static string Clock(TimeSpan t) { return (int)t.TotalMinutes + ":" + t.Seconds.ToString("00"); }

        /// One line in the live activity panel; newest on top, five kept.
        void Activity(string text, string kind)
        {
            var list = E<StackPanel>("EngActivity");
            string hex = kind == "good" ? "#22B573" : kind == "warn" ? "#F3A83B" : kind == "bad" ? "#E85D6A" : "#4A93FF";
            var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new Ellipse { Width = 6, Height = 6, Fill = new SolidColorBrush(Col(hex)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 10, 0) });
            var t = new TextBlock { Text = text, FontSize = 12.5, Foreground = new SolidColorBrush(Col("#C8D4EE")), TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(t, 1); g.Children.Add(t);
            var w = new TextBlock { Text = Clock(DateTime.Now - engStart), FontSize = 12.5, Foreground = new SolidColorBrush(Col("#6F82A8")), Margin = new Thickness(10, 0, 0, 0) };
            Grid.SetColumn(w, 2); g.Children.Add(w);
            list.Children.Insert(0, g);
            while (list.Children.Count > 5) list.Children.RemoveAt(list.Children.Count - 1);
            for (int i = 0; i < list.Children.Count; i++) list.Children[i].Opacity = 1 - i * 0.16;
            if (!S.ReducedMotion) g.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, 250));
        }

        /// Honest "still working" signal for steps whose length is unknown: a light sweep, not a fake percentage.
        void Shimmer(bool on)
        {
            var sh = E<Border>("EngShimmer"); var tt = (TranslateTransform)sh.RenderTransform;
            if (!on || S.ReducedMotion) { sh.BeginAnimation(UIElement.OpacityProperty, null); sh.Opacity = 0; tt.BeginAnimation(TranslateTransform.XProperty, null); return; }
            sh.Opacity = 1;
            double w = Math.Max(200, E<ProgressBar>("OvBar").ActualWidth);
            tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-90, w, TimeSpan.FromSeconds(1.6)) { RepeatBehavior = RepeatBehavior.Forever });
        }

        void EngCount(int done, string found)
        {
            engDone = done;
            TB("EngItems").Text = done + " / " + engTotal;
            if (found != null) TB("EngFound").Text = found;
        }

        static readonly Dictionary<string, string> OpGlyph = new Dictionary<string, string> { { "ram", "E9D9" }, { "undo", "E7A7" }, { "update", "E896" } };
        string ItemGlyph(string key)
        {
            var t = targets == null ? null : targets.FirstOrDefault(x => x.Key == key);
            if (t != null && !string.IsNullOrEmpty(t.Icon)) return t.Icon;
            string g; if (OpGlyph.TryGetValue(key, out g)) return g;
            return key.StartsWith("org_") ? "E8B7" : "E9F5";
        }

        /// Result reveal: the panel slides up and the actions are set for this kind of task.
        void EngResult(string outcome)
        {
            EngStage(outcome);
            var dv = E<Grid>("DoneView");
            if (!S.ReducedMotion) ((TranslateTransform)dv.RenderTransform).BeginAnimation(TranslateTransform.YProperty, Anim(14, 0, 420, new QuinticEase { EasingMode = EasingMode.EaseOut }));
            E<Ellipse>("DoneDisc").Fill = new SolidColorBrush(A(outcome == "done" ? "#22B573" : outcome == "error" ? "#E85D6A" : outcome == "cancel" ? "#7D8BA5" : "#F3A83B", 0x22));
            bool tick = outcome == "done";
            E<Ellipse>("DoneCircle").Visibility = tick ? Visibility.Visible : Visibility.Collapsed;
            E<Path>("DoneCheck").Visibility = tick ? Visibility.Visible : Visibility.Collapsed;
            var mk = TB("DoneMark");
            mk.Visibility = tick ? Visibility.Collapsed : Visibility.Visible;
            mk.Text = Glyph(outcome == "error" ? "E783" : outcome == "cancel" ? "E71A" : "E7BA");
            mk.Foreground = new SolidColorBrush(Col(outcome == "error" ? "#E85D6A" : outcome == "cancel" ? "#9AA4B8" : "#F3A83B"));
            // details list (per category), hidden until asked for
            var det = E<StackPanel>("DoneDetails"); det.Children.Clear(); det.Visibility = Visibility.Collapsed;
            foreach (var d in engDetails)
            {
                var g = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.Children.Add(new TextBlock { Text = d.Item1, FontSize = 14, Foreground = new SolidColorBrush(Col("#C8D4EE")), TextTrimming = TextTrimming.CharacterEllipsis });
                var v = new TextBlock { Text = d.Item2, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(12, 0, 0, 0),
                    Foreground = new SolidColorBrush(Col(d.Item3 == "skip" ? "#F3A83B" : d.Item3 == "zero" ? "#7F93B8" : "#EEF3FF")) };
                Grid.SetColumn(v, 1); g.Children.Add(v); det.Children.Add(g);
            }
            var bd = BT("BtnDoneDetails"); bd.Visibility = engDetails.Count > 0 ? Visibility.Visible : Visibility.Collapsed; bd.Content = Tx("View details", "Lihat butiran");
            var ag = BT("BtnDoneAgain"); ag.Visibility = engAgain != null ? Visibility.Visible : Visibility.Collapsed;
            ag.Content = outcome == "error" ? Tx("Retry", "Cuba lagi") : Tx("Scan again", "Imbas semula");
            var fin = BT("BtnFinish");
            bool review = engMode == "scan" && outcome != "error" && junkTotal > 0;
            fin.Content = MakeBtnContent(review ? "E74D" : "E73E", review ? Tx("Review & clean", "Semak & bersihkan") : L.T("finish"));
            engFinish = review ? (Action)(() => { HideOverlay(); Go("NavClean"); }) : null;
            fin.Focus();
        }
        static StackPanel MakeBtnContent(string glyph, string text)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = Glyph(glyph), FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 14.5, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
            return sp;
        }

        void SetupEngine()
        {
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(BT("BtnEngMin"), true);
            BT("BtnEngMin").Click += (s, e) => W.WindowState = WindowState.Minimized;
            BT("BtnEngMin").ToolTip = Tx("Minimize", "Kecilkan");
            BT("BtnDoneDetails").Click += (s, e) =>
            {
                var det = E<StackPanel>("DoneDetails"); bool show = det.Visibility != Visibility.Visible;
                det.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                BT("BtnDoneDetails").Content = show ? Tx("Hide details", "Sorok butiran") : Tx("View details", "Lihat butiran");
            };
            BT("BtnDoneAgain").Click += (s, e) => { var a = engAgain; HideOverlay(); if (a != null) Delay(340, a); };
        }
    }
}
