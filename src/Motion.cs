// KEMAS 4.1 motion: one set of timings for the whole app (macOS-like: quick, soft, never bouncy).
//   press  - every button scales to 0.97 while held
//   enter  - cards on a page arrive one after another (40 ms apart, at most 8)
//   hover  - cards lift their border to the accent tint
// Everything is skipped when Reduced motion is on. Nothing here loops, so nothing runs while idle.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Kemas
{
    public partial class MainWin
    {
        public const double MotionFast = 120, MotionBase = 220, MotionSlow = 320;
        static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };

        void SetupMotion()
        {
            EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((s, e) => Press((FrameworkElement)s, true)), true);
            EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler((s, e) => Press((FrameworkElement)s, false)), true);
            EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.MouseLeaveEvent, new MouseEventHandler((s, e) => Press((FrameworkElement)s, false)), true);
        }

        void Press(FrameworkElement b, bool down)
        {
            if (S == null || S.ReducedMotion || b is ToggleButton) return;       // switches, radios and checkboxes animate themselves
            var sc = b.RenderTransform as ScaleTransform;
            if (sc == null)
            {
                if (!down || (b.RenderTransform != null && b.RenderTransform != Transform.Identity)) return;   // never replace a transform someone else uses
                sc = new ScaleTransform(1, 1); b.RenderTransformOrigin = new Point(0.5, 0.5); b.RenderTransform = sc;
            }
            double to = down ? 0.97 : 1;
            if (!down && sc.ScaleX == 1 && !sc.HasAnimatedProperties) return;
            var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(down ? MotionFast : MotionBase)) { EasingFunction = EaseOut };
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, a); sc.BeginAnimation(ScaleTransform.ScaleYProperty, a);
        }

        /// Cards on a newly shown page fade and rise into place one after another.
        void StaggerIn(DependencyObject page)
        {
            if (S.ReducedMotion || page == null) return;
            var cards = new List<FrameworkElement>();
            Collect(page, cards, 0);
            int k = 0;
            foreach (var c in cards.Take(8))
            {
                var tt = c.RenderTransform as TranslateTransform;
                if (tt == null) { if (c.RenderTransform != null && c.RenderTransform != Transform.Identity) continue; tt = new TranslateTransform(); c.RenderTransform = tt; }
                double begin = 40 * k++;
                // key frames from t=0 so a card waiting its turn is already hidden (no flash before it starts)
                c.BeginAnimation(UIElement.OpacityProperty, Delayed(0, 1, begin, MotionBase));
                tt.BeginAnimation(TranslateTransform.YProperty, Delayed(10, 0, begin, MotionSlow));
            }
        }

        static DoubleAnimationUsingKeyFrames Delayed(double from, double to, double waitMs, double ms)
        {
            var a = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
            a.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            if (waitMs > 0) a.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(waitMs))));
            a.KeyFrames.Add(new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(waitMs + ms)), EaseOut));
            return a;
        }

        static readonly string[] CardStyles = { "Card", "SetCard" };
        void Collect(DependencyObject o, List<FrameworkElement> into, int depth)
        {
            if (depth > 7 || into.Count >= 8) return;
            var b = o as Border;
            if (b != null && b.Style != null && CardStyles.Any(n => { var st = W.TryFindResource(n) as Style; return st != null && ReferenceEquals(st, b.Style); }) && b.IsVisible)
            { into.Add(b); return; }
            foreach (var child in LogicalTreeHelper.GetChildren(o).OfType<DependencyObject>()) Collect(child, into, depth + 1);
        }
    }
}
