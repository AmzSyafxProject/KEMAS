// KEMAS icon set: one stroke family drawn on a 24 x 24 grid (1.7 stroke, round caps and joins) with a soft
// duotone fill in the same colour. Replaces Segoe glyphs wherever a tile or row shows an icon, so KEMAS looks
// like itself on every Windows version. Codes are the old Segoe code points, so callers did not change.
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Kemas
{
    public static class Icons
    {
        // code -> { stroke path, tint path (may be null) }
        static readonly Dictionary<string, string[]> Map = new Dictionary<string, string[]>
        {
            // places
            { "E80F", new[] { "M4 10.8 L12 4 L20 10.8 V19 A2 2 0 0 1 18 21 H6 A2 2 0 0 1 4 19 Z M9.5 21 V15.5 A1 1 0 0 1 10.5 14.5 H13.5 A1 1 0 0 1 14.5 15.5 V21", "M4 10.8 L12 4 L20 10.8 V19 A2 2 0 0 1 18 21 H6 A2 2 0 0 1 4 19 Z" } },
            { "E95E", new[] { "M12 20 C5.2 15.2 3 11.6 3 8.7 C3 6.1 5 4 7.6 4 C9.4 4 11 5 12 6.6 C13 5 14.6 4 16.4 4 C19 4 21 6.1 21 8.7 C21 11.6 18.8 15.2 12 20 Z M6.5 11.5 H9 L10.4 9 L12.8 14 L14.2 11.5 H17.5", "M12 20 C5.2 15.2 3 11.6 3 8.7 C3 6.1 5 4 7.6 4 C9.4 4 11 5 12 6.6 C13 5 14.6 4 16.4 4 C19 4 21 6.1 21 8.7 C21 11.6 18.8 15.2 12 20 Z" } },
            { "EC4A", new[] { "M4.2 17.5 A8.5 8.5 0 1 1 19.8 17.5 M12 14.5 L16 9.5 M12 13.2 A1.3 1.3 0 1 1 11.99 13.2 Z M6.6 8.6 L7.6 9.4 M12 5.6 V6.9 M17.4 8.6 L16.4 9.4", "M4.2 17.5 A8.5 8.5 0 1 1 19.8 17.5 Z" } },
            { "EDA2", new[] { "M3 13 H21 V18 A2 2 0 0 1 19 20 H5 A2 2 0 0 1 3 18 Z M3 13 L5.6 5.4 A2 2 0 0 1 7.5 4 H16.5 A2 2 0 0 1 18.4 5.4 L21 13 M6.6 16.5 H6.7 M11 16.5 H17.5", "M3 13 H21 V18 A2 2 0 0 1 19 20 H5 A2 2 0 0 1 3 18 Z" } },
            { "E74D", new[] { "M4 6.8 H20 M9.5 6.8 V4.8 A1 1 0 0 1 10.5 3.8 H13.5 A1 1 0 0 1 14.5 4.8 V6.8 M6 6.8 L6.9 19.2 A2 2 0 0 0 8.9 21 H15.1 A2 2 0 0 0 17.1 19.2 L18 6.8 M10 11 V16.8 M14 11 V16.8", "M6 6.8 L6.9 19.2 A2 2 0 0 0 8.9 21 H15.1 A2 2 0 0 0 17.1 19.2 L18 6.8 Z" } },
            { "E72E", new[] { "M6.5 10.8 H17.5 A2 2 0 0 1 19.5 12.8 V19 A2 2 0 0 1 17.5 21 H6.5 A2 2 0 0 1 4.5 19 V12.8 A2 2 0 0 1 6.5 10.8 Z M8 10.8 V8 A4 4 0 0 1 16 8 V10.8 M12 14.6 V17.2", "M6.5 10.8 H17.5 A2 2 0 0 1 19.5 12.8 V19 A2 2 0 0 1 17.5 21 H6.5 A2 2 0 0 1 4.5 19 V12.8 A2 2 0 0 1 6.5 10.8 Z" } },
            { "E90F", new[] { "M14.7 4.2 A4.6 4.6 0 0 0 10.4 10.4 L4.3 16.5 A2.1 2.1 0 0 0 7.5 19.7 L13.6 13.6 A4.6 4.6 0 0 0 19.8 9.3 L16.9 12.2 L13.6 11.4 L12.8 8.1 Z", "M14.7 4.2 A4.6 4.6 0 0 0 10.4 10.4 L13.6 13.6 A4.6 4.6 0 0 0 19.8 9.3 L16.9 12.2 L13.6 11.4 L12.8 8.1 Z" } },
            { "E777", new[] { "M4.2 13 A8 8 0 1 0 6.6 6.4 M4 3.8 V7.8 H8 M12 8 V12.3 L14.8 14", "M12 4.5 A8 8 0 1 1 11.99 4.5 Z" } },
            { "E81C", new[] { "M4.2 13 A8 8 0 1 0 6.6 6.4 M4 3.8 V7.8 H8 M12 8 V12.3 L14.8 14", "M12 4.5 A8 8 0 1 1 11.99 4.5 Z" } },
            { "E713", new[] { "M12 5 A7 7 0 1 1 11.99 5 Z M12 9.3 A2.7 2.7 0 1 1 11.99 9.3 Z M12 2.6 V5 M12 19 V21.4 M2.6 12 H5 M19 12 H21.4 M5.4 5.4 L7.1 7.1 M16.9 16.9 L18.6 18.6 M5.4 18.6 L7.1 16.9 M16.9 7.1 L18.6 5.4", "M12 5 A7 7 0 1 1 11.99 5 Z" } },
            { "E790", new[] { "M12 3 A9 9 0 1 0 12 21 C13.3 21 13.9 20.2 13.6 19.1 C13.2 17.6 14.2 16.4 15.7 16.4 H17.6 A3.4 3.4 0 0 0 21 13 C21 7.5 17 3 12 3 Z M7.6 11.6 H7.7 M10 7.6 H10.1 M14.4 7.6 H14.5 M17 11 H17.1", "M12 3 A9 9 0 1 0 12 21 C13.3 21 13.9 20.2 13.6 19.1 C13.2 17.6 14.2 16.4 15.7 16.4 H17.6 A3.4 3.4 0 0 0 21 13 C21 7.5 17 3 12 3 Z" } },
            { "E793", new[] { "M12 3 A9 9 0 1 1 11.99 3 Z M12 3 V21", "M12 3 A9 9 0 0 1 12 21 Z" } },
            { "E7E8", new[] { "M12 3.2 V11 M7.3 6.2 A7.6 7.6 0 1 0 16.7 6.2", "M12 5 A7.6 7.6 0 1 1 11.99 5 Z" } },
            { "E895", new[] { "M4.5 12 A7.5 7.5 0 0 1 17.8 7.3 M19.5 12 A7.5 7.5 0 0 1 6.2 16.7 M18.2 3.6 V7.6 H14.2 M5.8 20.4 V16.4 H9.8", null } },
            { "E72C", new[] { "M19.5 12 A7.5 7.5 0 1 1 17.3 6.7 M19.6 3.8 V8.3 H15.1", null } },
            { "E8B7", new[] { "M3 7 A2 2 0 0 1 5 5 H9.4 L11.4 7 H19 A2 2 0 0 1 21 9 V18 A2 2 0 0 1 19 20 H5 A2 2 0 0 1 3 18 Z M3 10.2 H21", "M3 10.2 H21 V18 A2 2 0 0 1 19 20 H5 A2 2 0 0 1 3 18 Z" } },
            { "E9D9", new[] { "M7.5 6 H16.5 A1.5 1.5 0 0 1 18 7.5 V16.5 A1.5 1.5 0 0 1 16.5 18 H7.5 A1.5 1.5 0 0 1 6 16.5 V7.5 A1.5 1.5 0 0 1 7.5 6 Z M9.8 9.8 H14.2 V14.2 H9.8 Z M9.2 3 V6 M12 3 V6 M14.8 3 V6 M9.2 18 V21 M12 18 V21 M14.8 18 V21 M3 9.2 H6 M3 12 H6 M3 14.8 H6 M18 9.2 H21 M18 12 H21 M18 14.8 H21", "M7.5 6 H16.5 A1.5 1.5 0 0 1 18 7.5 V16.5 A1.5 1.5 0 0 1 16.5 18 H7.5 A1.5 1.5 0 0 1 6 16.5 V7.5 A1.5 1.5 0 0 1 7.5 6 Z" } },
            { "E9D2", new[] { "M8.5 4.5 H7 A2 2 0 0 0 5 6.5 V19 A2 2 0 0 0 7 21 H17 A2 2 0 0 0 19 19 V6.5 A2 2 0 0 0 17 4.5 H15.5 M9.5 3 H14.5 A1 1 0 0 1 15.5 4 V5 A1 1 0 0 1 14.5 6 H9.5 A1 1 0 0 1 8.5 5 V4 A1 1 0 0 1 9.5 3 Z M7.8 13.5 H10 L11.2 11.2 L12.9 15.8 L14.1 13.5 H16.2", "M7 4.5 H17 A2 2 0 0 1 19 6.5 V19 A2 2 0 0 1 17 21 H7 A2 2 0 0 1 5 19 V6.5 A2 2 0 0 1 7 4.5 Z" } },
            { "E7F4", new[] { "M4 4.5 H20 A1.5 1.5 0 0 1 21.5 6 V15.5 A1.5 1.5 0 0 1 20 17 H4 A1.5 1.5 0 0 1 2.5 15.5 V6 A1.5 1.5 0 0 1 4 4.5 Z M8.5 20.5 H15.5 M12 17 V20.5", "M4 4.5 H20 A1.5 1.5 0 0 1 21.5 6 V15.5 A1.5 1.5 0 0 1 20 17 H4 A1.5 1.5 0 0 1 2.5 15.5 V6 A1.5 1.5 0 0 1 4 4.5 Z" } },
            { "E770", new[] { "M4 4.5 H20 A1.5 1.5 0 0 1 21.5 6 V15.5 A1.5 1.5 0 0 1 20 17 H4 A1.5 1.5 0 0 1 2.5 15.5 V6 A1.5 1.5 0 0 1 4 4.5 Z M8.5 20.5 H15.5 M12 17 V20.5", "M4 4.5 H20 A1.5 1.5 0 0 1 21.5 6 V15.5 A1.5 1.5 0 0 1 20 17 H4 A1.5 1.5 0 0 1 2.5 15.5 V6 A1.5 1.5 0 0 1 4 4.5 Z" } },
            { "E71D", new[] { "M5.5 4 H9 A1.5 1.5 0 0 1 10.5 5.5 V9 A1.5 1.5 0 0 1 9 10.5 H5.5 A1.5 1.5 0 0 1 4 9 V5.5 A1.5 1.5 0 0 1 5.5 4 Z M15 4 H18.5 A1.5 1.5 0 0 1 20 5.5 V9 A1.5 1.5 0 0 1 18.5 10.5 H15 A1.5 1.5 0 0 1 13.5 9 V5.5 A1.5 1.5 0 0 1 15 4 Z M5.5 13.5 H9 A1.5 1.5 0 0 1 10.5 15 V18.5 A1.5 1.5 0 0 1 9 20 H5.5 A1.5 1.5 0 0 1 4 18.5 V15 A1.5 1.5 0 0 1 5.5 13.5 Z M16.75 13.5 V20 M13.5 16.75 H20", "M5.5 4 H9 A1.5 1.5 0 0 1 10.5 5.5 V9 A1.5 1.5 0 0 1 9 10.5 H5.5 A1.5 1.5 0 0 1 4 9 V5.5 A1.5 1.5 0 0 1 5.5 4 Z M15 4 H18.5 A1.5 1.5 0 0 1 20 5.5 V9 A1.5 1.5 0 0 1 18.5 10.5 H15 A1.5 1.5 0 0 1 13.5 9 V5.5 A1.5 1.5 0 0 1 15 4 Z M5.5 13.5 H9 A1.5 1.5 0 0 1 10.5 15 V18.5 A1.5 1.5 0 0 1 9 20 H5.5 A1.5 1.5 0 0 1 4 18.5 V15 A1.5 1.5 0 0 1 5.5 13.5 Z" } },
            { "E8C8", new[] { "M9 8 H18 A2 2 0 0 1 20 10 V19 A2 2 0 0 1 18 21 H9 A2 2 0 0 1 7 19 V10 A2 2 0 0 1 9 8 Z M4 15.5 V5.5 A1.5 1.5 0 0 1 5.5 4 H15.5", "M9 8 H18 A2 2 0 0 1 20 10 V19 A2 2 0 0 1 18 21 H9 A2 2 0 0 1 7 19 V10 A2 2 0 0 1 9 8 Z" } },
            { "E8A5", new[] { "M14 3 H7 A2 2 0 0 0 5 5 V19 A2 2 0 0 0 7 21 H17 A2 2 0 0 0 19 19 V8 Z M14 3 V8 H19 M8.6 12.8 H15.4 M8.6 16.4 H12.8", "M14 3 H7 A2 2 0 0 0 5 5 V19 A2 2 0 0 0 7 21 H17 A2 2 0 0 0 19 19 V8 Z" } },
            { "E9F9", new[] { "M4 20 H20 M7 16.5 V12 M11 16.5 V7 M15 16.5 V10 M19 16.5 V5", null } },
            { "E8B9", new[] { "M4.5 4.5 H19.5 A1.5 1.5 0 0 1 21 6 V18 A1.5 1.5 0 0 1 19.5 19.5 H4.5 A1.5 1.5 0 0 1 3 18 V6 A1.5 1.5 0 0 1 4.5 4.5 Z M3 16 L8.5 11 L13 15 L15.8 12.4 L21 17 M15.8 8.4 H15.9", "M4.5 4.5 H19.5 A1.5 1.5 0 0 1 21 6 V18 A1.5 1.5 0 0 1 19.5 19.5 H4.5 A1.5 1.5 0 0 1 3 18 V6 A1.5 1.5 0 0 1 4.5 4.5 Z" } },
            { "E7FC", new[] { "M7.2 7.8 H16.8 A4.2 4.2 0 0 1 21 11.8 L21.4 15.9 A2.6 2.6 0 0 1 17 17.8 L15.4 16.2 H8.6 L7 17.8 A2.6 2.6 0 0 1 2.6 15.9 L3 11.8 A4.2 4.2 0 0 1 7.2 7.8 Z M7.6 10.4 V13.6 M6 12 H9.2 M15.4 11.2 H15.5 M17.4 13.2 H17.5", "M7.2 7.8 H16.8 A4.2 4.2 0 0 1 21 11.8 L21.4 15.9 A2.6 2.6 0 0 1 17 17.8 L15.4 16.2 H8.6 L7 17.8 A2.6 2.6 0 0 1 2.6 15.9 L3 11.8 A4.2 4.2 0 0 1 7.2 7.8 Z" } },
            { "E83F", new[] { "M4.5 7.5 H16.5 A2 2 0 0 1 18.5 9.5 V14.5 A2 2 0 0 1 16.5 16.5 H4.5 A2 2 0 0 1 2.5 14.5 V9.5 A2 2 0 0 1 4.5 7.5 Z M21 10.4 V13.6 M6 10.6 V13.4 M9 10.6 V13.4", "M4.5 7.5 H16.5 A2 2 0 0 1 18.5 9.5 V14.5 A2 2 0 0 1 16.5 16.5 H4.5 A2 2 0 0 1 2.5 14.5 V9.5 A2 2 0 0 1 4.5 7.5 Z" } },
            { "E774", new[] { "M12 3 A9 9 0 1 1 11.99 3 Z M3.2 12 H20.8 M12 3 C9.2 6 9.2 18 12 21 C14.8 18 14.8 6 12 3 Z", "M12 3 A9 9 0 1 1 11.99 3 Z" } },
            { "E968", new[] { "M3.8 9.4 A11.6 11.6 0 0 1 20.2 9.4 M6.9 12.7 A7.2 7.2 0 0 1 17.1 12.7 M9.9 16 A2.9 2.9 0 0 1 14.1 16 M12 19.4 H12.1", null } },
            { "E701", new[] { "M3.8 9.4 A11.6 11.6 0 0 1 20.2 9.4 M6.9 12.7 A7.2 7.2 0 0 1 17.1 12.7 M9.9 16 A2.9 2.9 0 0 1 14.1 16 M12 19.4 H12.1", null } },
            { "EA8F", new[] { "M6 16.4 V11 A6 6 0 0 1 18 11 V16.4 L19.6 18 H4.4 Z M10 21 H14", "M6 16.4 V11 A6 6 0 0 1 18 11 V16.4 L19.6 18 H4.4 Z" } },
            // states and actions
            { "E7BA", new[] { "M10.3 4.5 A2 2 0 0 1 13.7 4.5 L21 17.6 A2 2 0 0 1 19.3 20.5 H4.7 A2 2 0 0 1 3 17.6 Z M12 9.5 V13.6 M12 16.9 H12.1", "M10.3 4.5 A2 2 0 0 1 13.7 4.5 L21 17.6 A2 2 0 0 1 19.3 20.5 H4.7 A2 2 0 0 1 3 17.6 Z" } },
            { "E73E", new[] { "M5 12.6 L9.8 17.4 L19 7.2", null } },
            { "E783", new[] { "M12 3 A9 9 0 1 1 11.99 3 Z M9 9 L15 15 M15 9 L9 15", "M12 3 A9 9 0 1 1 11.99 3 Z" } },
            { "E946", new[] { "M12 3 A9 9 0 1 1 11.99 3 Z M12 11 V16.4 M12 7.6 H12.1", "M12 3 A9 9 0 1 1 11.99 3 Z" } },
            { "E897", new[] { "M12 3 A9 9 0 1 1 11.99 3 Z M9.6 9.4 A2.5 2.5 0 1 1 13.2 11.6 C12.4 12 12 12.6 12 13.4 M12 16.6 H12.1", "M12 3 A9 9 0 1 1 11.99 3 Z" } },
            { "E945", new[] { "M13.2 2.6 L5 13.4 H11.6 L10.6 21.4 L19 10.4 H12.4 Z", "M13.2 2.6 L5 13.4 H11.6 L10.6 21.4 L19 10.4 H12.4 Z" } },
            { "E721", new[] { "M10.6 4 A6.6 6.6 0 1 1 10.59 4 Z M15.6 15.6 L20.4 20.4", "M10.6 4 A6.6 6.6 0 1 1 10.59 4 Z" } },
            { "E7A7", new[] { "M9 14.2 L4.2 9.4 L9 4.6 M4.4 9.4 H14.4 A5.4 5.4 0 0 1 14.4 20.2 H11", null } },
            { "E896", new[] { "M12 3.6 V15 M7.2 10.4 L12 15.2 L16.8 10.4 M4.6 19.6 H19.4", null } },
            { "E916", new[] { "M12 5.6 A8 8 0 1 1 11.99 5.6 Z M12 9.4 V13.6 L14.6 15.2 M9.6 2.6 H14.4", "M12 5.6 A8 8 0 1 1 11.99 5.6 Z" } },
            { "E71A", new[] { "M8 7 H16 A1 1 0 0 1 17 8 V16 A1 1 0 0 1 16 17 H8 A1 1 0 0 1 7 16 V8 A1 1 0 0 1 8 7 Z", "M8 7 H16 A1 1 0 0 1 17 8 V16 A1 1 0 0 1 16 17 H8 A1 1 0 0 1 7 16 V8 A1 1 0 0 1 8 7 Z" } },
            { "E711", new[] { "M6.4 6.4 L17.6 17.6 M17.6 6.4 L6.4 17.6", null } },
            { "E710", new[] { "M12 5 V19 M5 12 H19", null } },
            { "E72A", new[] { "M5 12 H19 M13.6 6.6 L19 12 L13.6 17.4", null } },
            { "E9F5", new[] { "M7.5 6 H16.5 A1.5 1.5 0 0 1 18 7.5 V16.5 A1.5 1.5 0 0 1 16.5 18 H7.5 A1.5 1.5 0 0 1 6 16.5 V7.5 A1.5 1.5 0 0 1 7.5 6 Z M9.8 9.8 H14.2 V14.2 H9.8 Z M9.2 3 V6 M14.8 3 V6 M9.2 18 V21 M14.8 18 V21 M3 9.2 H6 M3 14.8 H6 M18 9.2 H21 M18 14.8 H21", "M7.5 6 H16.5 A1.5 1.5 0 0 1 18 7.5 V16.5 A1.5 1.5 0 0 1 16.5 18 H7.5 A1.5 1.5 0 0 1 6 16.5 V7.5 A1.5 1.5 0 0 1 7.5 6 Z" } },
            { "E964", new[] { "M7.5 6 H16.5 A1.5 1.5 0 0 1 18 7.5 V16.5 A1.5 1.5 0 0 1 16.5 18 H7.5 A1.5 1.5 0 0 1 6 16.5 V7.5 A1.5 1.5 0 0 1 7.5 6 Z M9.8 9.8 H14.2 V14.2 H9.8 Z M9.2 3 V6 M14.8 3 V6 M9.2 18 V21 M14.8 18 V21 M3 9.2 H6 M3 14.8 H6 M18 9.2 H21 M18 14.8 H21", "M7.5 6 H16.5 A1.5 1.5 0 0 1 18 7.5 V16.5 A1.5 1.5 0 0 1 16.5 18 H7.5 A1.5 1.5 0 0 1 6 16.5 V7.5 A1.5 1.5 0 0 1 7.5 6 Z" } },
        };

        /// SVG/WPF path data for the contact sheet and tests.
        public static IEnumerable<KeyValuePair<string, string[]>> All { get { return Map; } }

        static readonly Dictionary<string, Geometry> geo = new Dictionary<string, Geometry>();
        static Geometry G(string d)
        {
            Geometry g;
            if (!geo.TryGetValue(d, out g)) { g = Geometry.Parse(d); g.Freeze(); geo[d] = g; }
            return g;
        }

        public static bool Has(string code) { return code != null && Map.ContainsKey(code.ToUpperInvariant()); }

        /// A vector icon whose colour follows <paramref name="colorSource"/>'s Foreground (so code that recolours the old
        /// glyph TextBlock keeps working). Returns null when the code has no KEMAS icon.
        public static FrameworkElement Make(string code, double size, FrameworkElement colorSource)
        {
            string[] d;
            if (code == null || !Map.TryGetValue(code.ToUpperInvariant(), out d)) return null;
            var cv = new Canvas { Width = 24, Height = 24 };
            var bind = new Binding("Foreground") { Source = colorSource };
            if (d[1] != null)
            {
                var tint = new Path { Data = G(d[1]), Opacity = 0.2 };
                tint.SetBinding(Shape.FillProperty, bind);
                cv.Children.Add(tint);
            }
            var line = new Path { Data = G(d[0]), StrokeThickness = 1.7, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
            line.SetBinding(Shape.StrokeProperty, bind);
            cv.Children.Add(line);
            return new Viewbox { Width = size, Height = size, Child = cv, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        }

        /// Swaps a Segoe glyph TextBlock for the KEMAS icon in place (same parent, same slot). The TextBlock stays,
        /// hidden, as the colour source. Does nothing when there is no KEMAS icon for the glyph.
        public static void Upgrade(TextBlock glyph, string code, double size)
        {
            var ic = Make(code, size, glyph);
            if (ic == null) return;
            var parent = glyph.Parent as Panel;
            if (parent == null) return;
            glyph.Visibility = Visibility.Hidden;
            ic.Margin = glyph.Margin;
            Grid.SetColumn(ic, Grid.GetColumn(glyph)); Grid.SetRow(ic, Grid.GetRow(glyph));
            parent.Children.Insert(parent.Children.IndexOf(glyph) + 1, ic);
        }
    }
}
