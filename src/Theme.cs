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
                r["BgBrush"] = B("#111522"); r["SideBrush"] = B("#151A29"); r["CardBrush"] = B("#191F30"); r["LineBrush"] = B("#30394D");
                r["TextBrush"] = B("#F4F6FC"); r["MutedBrush"] = B("#A4AEC2"); r["HoverBrush"] = B("#222B40"); r["SelectBrush"] = B("#26324D"); r["NavSelBrush"] = B("#222B40");
                r["GhostBrush"] = B("#222B40"); r["TrackBrush"] = B("#30394D"); r["BoxBrush"] = B("#4A5570"); r["InputBrush"] = B("#222B40");
                r["ThumbBrush"] = B("#3A4560"); r["AltRowBrush"] = B("#1C2335"); r["DialogBrush"] = B("#191F30"); r["OverlayBrush"] = B("#D9111522");
                r["IconTile"] = B("#22315A"); r["HeroText"] = B("#F4F6FC"); r["HeroMuted"] = B("#A4AEC2");
                r["HeroBrush"] = B("#191F30"); r["RingGlow"] = B("#00000000");
                r["AuroraA"] = B("#00000000"); r["AuroraB"] = B("#00000000"); r["AuroraC"] = B("#00000000");
            }
            else
            {
                r["BgBrush"] = B("#F5F6FA"); r["SideBrush"] = B("#FBFBFD"); r["CardBrush"] = B("#FFFFFF"); r["LineBrush"] = B("#E8EBF2");
                r["TextBrush"] = B("#172033"); r["MutedBrush"] = B("#788195"); r["HoverBrush"] = B("#F0F2F7"); r["SelectBrush"] = B("#EAF0FE"); r["NavSelBrush"] = B("#FFFFFF");
                r["GhostBrush"] = B("#F0F2F7"); r["TrackBrush"] = B("#E8EBF2"); r["BoxBrush"] = B("#C9CFDC"); r["InputBrush"] = B("#FFFFFF");
                r["ThumbBrush"] = B("#D3D8E3"); r["AltRowBrush"] = B("#F7F8FB"); r["DialogBrush"] = B("#FFFFFF"); r["OverlayBrush"] = B("#99172033");
                r["IconTile"] = B("#EAF0FE"); r["HeroText"] = B("#172033"); r["HeroMuted"] = B("#788195");
                r["HeroBrush"] = B("#FFFFFF"); r["RingGlow"] = B("#00000000");
                r["AuroraA"] = B("#00000000"); r["AuroraB"] = B("#00000000"); r["AuroraC"] = B("#00000000");
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
