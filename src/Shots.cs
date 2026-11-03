// Developer screenshot pass. Runs only when a file named kemas-shots.txt sits next to KEMAS.exe (never in a
// normal install): opens each page in light and dark, saves shot-<theme>-<page>.png beside the exe, puts the
// user's own theme back and closes. Lets the redesign be checked on a real Windows screen without clicking.
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kemas
{
    public partial class MainWin
    {
        bool RunShotsIfAsked()
        {
            string dir, flag;
            try { dir = AppDomain.CurrentDomain.BaseDirectory; flag = Path.Combine(dir, "kemas-shots.txt"); if (!File.Exists(flag)) return false; }
            catch { return false; }
            var keepTheme = S.Theme; var keepWelcome = S.WelcomeDone;
            var pages = new List<string>();
            try { foreach (var l in File.ReadAllLines(flag)) if (l.Trim().StartsWith("Nav")) pages.Add(l.Trim()); } catch { }
            if (pages.Count == 0) pages.AddRange(new[] { "NavDash", "NavSettings", "NavGame", "NavHubClean", "NavNetwork", "NavAbout" });
            var steps = new List<Action>();
            foreach (var theme in new[] { "light", "dark" })
            {
                var t = theme;
                steps.Add(() => { S.Theme = t; ApplyThemeAndLanguage(); });
                foreach (var p in pages) { var pg = p; steps.Add(() => Go(pg)); steps.Add(() => Shot(dir, t, pg)); }
                // the scan sheet, while working and when done
                steps.Add(() => { Go("NavDash"); StartScan(true); });
                steps.Add(() => Shot(dir, t, "ScanWorking"));
                steps.Add(() => { });
                steps.Add(() => { });
                steps.Add(() => Shot(dir, t, "ScanDone"));
                steps.Add(() => { try { HideOverlay(); } catch { } });
            }
            steps.Add(() =>
            {
                S.Theme = keepTheme; S.WelcomeDone = keepWelcome; ApplyThemeAndLanguage(); QueueSave();
                try { File.Move(flag, flag + ".done"); } catch { }
                QuitRequested = true; W.Close();      // really exit (not to the tray)
            });
            int i = 0;
            Action next = null;
            next = () => { if (i >= steps.Count) return; try { steps[i++](); } catch (Exception ex) { Kemas.Log.Error("shots", ex); } Delay(1800, next); };
            Delay(2500, next);
            return true;
        }

        void Shot(string dir, string theme, string name)
        {
            var root = (FrameworkElement)W.Content;
            var dpi = VisualTreeHelper.GetDpi(W);
            var bmp = new RenderTargetBitmap((int)(root.ActualWidth * dpi.DpiScaleX), (int)(root.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            // paint the window background first: the root grid itself is transparent
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle((Brush)W.FindResource("BgBrush"), null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
                dc.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
            }
            bmp.Render(dv);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp));
            using (var f = File.Create(Path.Combine(dir, "shot-" + theme + "-" + name.Replace("Nav", "") + ".png"))) enc.Save(f);
        }
    }
}
