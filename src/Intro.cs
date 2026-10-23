using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Kemas
{
    // Launch film shown once, on the first open of the official release. Click, Esc or Space skips it.
    public partial class MainWin
    {
        Grid intro;

        void ShowIntro()
        {
            if (S.IntroSeen) return;
            S.IntroSeen = true; QueueSave();
            if (S.ReducedMotion) return;
            bool ms = S.Language == "ms";
            var root = E<Grid>("Root");
            intro = new Grid { Background = new SolidColorBrush(Color.FromRgb(2, 3, 10)), ClipToBounds = true, Cursor = Cursors.Arrow };
            Panel.SetZIndex(intro, 1000);
            Grid.SetRowSpan(intro, 8); Grid.SetColumnSpan(intro, 8);

            // horizon glow
            var glow = new Ellipse { Width = 1800, Height = 900, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, -520), Opacity = 0, IsHitTestVisible = false };
            var gb = new RadialGradientBrush { Center = new Point(.5, .5), RadiusX = .5, RadiusY = .5 };
            gb.GradientStops.Add(new GradientStop(Color.FromArgb(150, 110, 130, 255), 0));
            gb.GradientStops.Add(new GradientStop(Color.FromArgb(60, 123, 92, 255), .55));
            gb.GradientStops.Add(new GradientStop(Color.FromArgb(0, 2, 3, 10), 1));
            glow.Fill = gb; intro.Children.Add(glow);

            // drifting particles
            var cv = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
            var rnd = new Random(7);
            for (int i = 0; i < 70; i++)
            {
                double sz = 1.2 + rnd.NextDouble() * 2.2, z = .3 + rnd.NextDouble() * .7;
                var dot = new Ellipse { Width = sz, Height = sz, Opacity = .15 + .55 * z, Fill = new SolidColorBrush(i % 5 == 0 ? Color.FromRgb(0x22, 0xD3, 0xF5) : i % 7 == 0 ? Color.FromRgb(0x9B, 0x7B, 0xFF) : Color.FromRgb(0xCF, 0xE0, 0xFF)) };
                Canvas.SetLeft(dot, rnd.NextDouble() * 1300); Canvas.SetTop(dot, 820);
                var tr = new TranslateTransform(); dot.RenderTransform = tr;
                tr.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -900, TimeSpan.FromSeconds(8 + 8 / z * rnd.NextDouble() + 3)) { RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(-rnd.NextDouble() * 10) });
                cv.Children.Add(dot);
            }
            intro.Children.Add(cv);

            // centre stack
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false };
            var logo = new Image { Source = Logo.Image(), Width = 150, Height = 150, Opacity = 0, RenderTransformOrigin = new Point(.5, .5), Effect = new DropShadowEffect { Color = Color.FromRgb(0x7B, 0x8C, 0xFF), BlurRadius = 60, ShadowDepth = 0, Opacity = .7 } };
            var lsc = new ScaleTransform(1.3, 1.3); logo.RenderTransform = lsc;
            var word = new TextBlock { Text = "K E M A S", FontSize = 46, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 26, 0, 0), Opacity = 0, RenderTransformOrigin = new Point(.5, .5) };
            var wsc = new ScaleTransform(1.12, 1); word.RenderTransform = wsc;
            bool store = AppInfo.IsStore;
            var tag = new TextBlock { Text = store ? (ms ? "Selamat datang ke KEMAS" : "Welcome to KEMAS") : (ms ? "Pc awak, Kemas." : "Your PC, Kemas."), FontSize = 18, Foreground = new SolidColorBrush(Color.FromRgb(0xBF, 0xC7, 0xE2)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0), Opacity = 0 };
            var rel = new TextBlock { Text = store ? (ms ? "DIPASANG DARI MICROSOFT STORE  ·  IMBAS, SEMAK, KEMUDIAN BERSIHKAN" : "INSTALLED FROM THE MICROSOFT STORE  ·  SCAN, REVIEW, THEN CLEAN") : (ms ? "PELEPASAN AWAM RASMI  ·  25 OKTOBER 2026" : "OFFICIAL PUBLIC RELEASE  ·  25 OCTOBER 2026"), FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xD3, 0xF5)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 34, 0, 0), Opacity = 0 };
            stack.Children.Add(logo); stack.Children.Add(word); stack.Children.Add(tag); stack.Children.Add(rel);
            intro.Children.Add(stack);

            // light sweep
            var sweep = new Rectangle { Height = 2, Width = 520, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Opacity = 0, IsHitTestVisible = false, Effect = new BlurEffect { Radius = 2 } };
            var sb = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            sb.GradientStops.Add(new GradientStop(Color.FromArgb(0, 120, 200, 255), 0)); sb.GradientStops.Add(new GradientStop(Color.FromArgb(255, 170, 225, 255), .5)); sb.GradientStops.Add(new GradientStop(Color.FromArgb(0, 150, 120, 255), 1));
            sweep.Fill = sb; var str = new TranslateTransform(-600, 0); sweep.RenderTransform = str; intro.Children.Add(sweep);

            // letterbox
            var top = new Border { Background = Brushes.Black, Height = 0, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
            var bot = new Border { Background = Brushes.Black, Height = 0, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false };
            intro.Children.Add(top); intro.Children.Add(bot);
            var skip = new TextBlock { Text = ms ? "Klik untuk langkau" : "Click to skip", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x8D, 0x95, 0xB3)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 18, 12), Opacity = 0, IsHitTestVisible = false };
            intro.Children.Add(skip);
            root.Children.Add(intro);

            Func<double, Duration> D = s => new Duration(TimeSpan.FromSeconds(s));
            Action<DependencyObject, DependencyProperty, double, double, double, double> an = (o, p, to, dur, begin, _) =>
            {
                var a = new DoubleAnimation(to, D(dur)) { BeginTime = TimeSpan.FromSeconds(begin), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                ((IAnimatable)o).BeginAnimation(p, a);
            };
            an(top, FrameworkElement.HeightProperty, 64, 2.2, 0, 0); an(bot, FrameworkElement.HeightProperty, 64, 2.2, 0, 0);
            an(glow, UIElement.OpacityProperty, 1, 3.5, 0.6, 0);
            an(skip, UIElement.OpacityProperty, 1, 1, 1.5, 0);
            an(logo, UIElement.OpacityProperty, 1, 1.8, 1.0, 0);
            an(lsc, ScaleTransform.ScaleXProperty, 1, 2.4, 1.0, 0); an(lsc, ScaleTransform.ScaleYProperty, 1, 2.4, 1.0, 0);
            an(word, UIElement.OpacityProperty, 1, 1.6, 2.2, 0); an(wsc, ScaleTransform.ScaleXProperty, 1, 2.6, 2.2, 0);
            an(tag, UIElement.OpacityProperty, 1, 1.4, 3.4, 0);
            an(rel, UIElement.OpacityProperty, 1, 1.4, 4.2, 0);
            an(sweep, UIElement.OpacityProperty, 1, .5, 1.0, 0);
            str.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-600, 1900, D(1.9)) { BeginTime = TimeSpan.FromSeconds(1.0), EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
            sweep.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation { From = 0, To = 1, Duration = D(.5), BeginTime = TimeSpan.FromSeconds(1.0), AutoReverse = true });

            bool closing = false;
            Action close = () =>
            {
                if (closing) return; closing = true;
                var fade = new DoubleAnimation(0, D(.9));
                fade.Completed += (s, e) => { root.Children.Remove(intro); intro = null; };
                intro.BeginAnimation(UIElement.OpacityProperty, fade);
            };
            intro.MouseDown += (s, e) => close();
            KeyEventHandler kh = null;
            kh = (s, e) => { if (intro == null) { W.PreviewKeyDown -= kh; return; } if (e.Key == Key.Escape || e.Key == Key.Space || e.Key == Key.Enter) { e.Handled = true; close(); } };
            W.PreviewKeyDown += kh;
            Delay(store ? 8000 : 7200, close);
        }
    }
}
