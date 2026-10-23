// KEMAS - themes (dark / light / system) and the vector logo
using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Kemas
{
    public static class Theme
    {
        public static bool IsDark { get; private set; }

        public static bool SystemPrefersDark()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    if (k != null) return Convert.ToInt32(k.GetValue("AppsUseLightTheme", 1)) == 0;
            }
            catch { }
            return true;
        }

        static SolidColorBrush B(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        static Brush Radial(string inner)
        {
            var c = (Color)ColorConverter.ConvertFromString(inner);
            var b = new RadialGradientBrush(c, Color.FromArgb(0, c.R, c.G, c.B));
            b.Freeze();
            return b;
        }

        static Brush Linear(string a, string b)
        {
            var br = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(a), (Color)ColorConverter.ConvertFromString(b), new Point(0, 0), new Point(1, 1));
            br.Freeze();
            return br;
        }

        public static void Apply(ResourceDictionary r, string mode)
        {
            bool dark = mode == "dark" || (mode == "system" && SystemPrefersDark());
            IsDark = dark;
            if (dark)
            {
                r["BgBrush"] = B("#0B0D14"); r["SideBrush"] = B("#CC0F1119"); r["CardBrush"] = B("#B3151926"); r["LineBrush"] = B("#1FFFFFFF");
                r["TextBrush"] = B("#EEF0F7"); r["MutedBrush"] = B("#9EA6BF"); r["HoverBrush"] = B("#14FFFFFF"); r["SelectBrush"] = B("#1FFFFFFF");
                r["GhostBrush"] = B("#12FFFFFF"); r["TrackBrush"] = B("#1FFFFFFF"); r["BoxBrush"] = B("#3D4566"); r["InputBrush"] = B("#12FFFFFF");
                r["ThumbBrush"] = B("#3A4260"); r["AltRowBrush"] = B("#0AFFFFFF"); r["DialogBrush"] = B("#F2141826"); r["OverlayBrush"] = B("#D90A0C13");
                r["IconTile"] = B("#267B5CFF"); r["HeroText"] = B("#FFFFFF"); r["HeroMuted"] = B("#C3CAE3");
                r["HeroBrush"] = Linear("#E6261D63", "#E60E3350"); r["RingGlow"] = Radial("#4D7B5CFF");
                r["AuroraA"] = Radial("#557B5CFF"); r["AuroraB"] = Radial("#3D2FD4FF"); r["AuroraC"] = Radial("#2EFF5CB8");
            }
            else
            {
                r["BgBrush"] = B("#F4F5FB"); r["SideBrush"] = B("#E6FFFFFF"); r["CardBrush"] = B("#E6FFFFFF"); r["LineBrush"] = B("#1A1B2140");
                r["TextBrush"] = B("#141726"); r["MutedBrush"] = B("#626A85"); r["HoverBrush"] = B("#0F1B2140"); r["SelectBrush"] = B("#1A7B5CFF");
                r["GhostBrush"] = B("#B3FFFFFF"); r["TrackBrush"] = B("#1A1B2140"); r["BoxBrush"] = B("#B5BAD0"); r["InputBrush"] = B("#FFFFFF");
                r["ThumbBrush"] = B("#C3C7D9"); r["AltRowBrush"] = B("#081B2140"); r["DialogBrush"] = B("#FAFFFFFF"); r["OverlayBrush"] = B("#B3E9EBF5");
                r["IconTile"] = B("#1A7B5CFF"); r["HeroText"] = B("#FFFFFF"); r["HeroMuted"] = B("#E3E7FA");
                r["HeroBrush"] = Linear("#6A4DF5", "#1FA9D9"); r["RingGlow"] = Radial("#40FFFFFF");
                r["AuroraA"] = Radial("#407B5CFF"); r["AuroraB"] = Radial("#332FD4FF"); r["AuroraC"] = Radial("#26FF5CB8");
            }
        }
    }

    public static class Logo
    {
        static DrawingImage cached;

        /// The KEMAS mark: gradient squircle, white K with two sweep strokes, sparkle.
        public static DrawingImage Image()
        {
            if (cached != null) return cached;
            var g = new DrawingGroup();
            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x7B, 0x5C, 0xFF), 0));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x5A, 0x7B, 0xFF), 0.55));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x22, 0xD3, 0xF5), 1));
            var rect = new RectangleGeometry(new Rect(16, 16, 480, 480), 124, 124);
            g.Children.Add(new GeometryDrawing(grad, null, rect));
            var gloss = new LinearGradientBrush(Color.FromArgb(0x47, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), new Point(0, 0), new Point(0, 0.5));
            g.Children.Add(new GeometryDrawing(gloss, null, rect));
            g.Children.Add(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(150, 132, 60, 248), 30, 30)));
            var pen1 = new Pen(Brushes.White, 58) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var pen2 = new Pen(new SolidColorBrush(Color.FromArgb(0xD1, 255, 255, 255)), 58) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            g.Children.Add(new GeometryDrawing(null, pen1, Geometry.Parse("M 224,300 C 260,262 298,222 328,188")));
            g.Children.Add(new GeometryDrawing(null, pen2, Geometry.Parse("M 246,288 C 286,316 328,350 362,378")));
            g.Children.Add(new GeometryDrawing(Brushes.White, null, Geometry.Parse("M 382,82 Q 382,128 428,128 Q 382,128 382,174 Q 382,128 336,128 Q 382,128 382,82 Z")));
            g.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromArgb(0xD9, 255, 255, 255)), null, new EllipseGeometry(new Point(434, 190), 10, 10)));
            g.Freeze();
            cached = new DrawingImage(g);
            cached.Freeze();
            return cached;
        }
    }
}
