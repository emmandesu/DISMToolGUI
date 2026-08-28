using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace DismToolGui
{
    internal static class ThemeCatalog
    {
        private static readonly IReadOnlyList<AppTheme> themes = BuildThemes();

        public static IReadOnlyList<AppTheme> All => themes;
        public static AppTheme Default => GetById("dism-dark");
        public static AppTheme DefaultLight => GetById("dism-light");

        public static AppTheme GetById(string id)
        {
            return themes.FirstOrDefault(theme =>
                       string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase)) ??
                   themes[0];
        }

        private static IReadOnlyList<AppTheme> BuildThemes()
        {
            return new List<AppTheme>
            {
                T("dism-dark", "DISM Dark", "Recommended", true, C(28,28,30), C(28,28,30), C(45,45,48), C(64,64,64), C(0,128,128)),
                T("dism-light", "DISM Light", "Recommended", false, C(255,255,255), C(245,245,245), C(255,255,255), C(220,220,220), C(0,120,160)),
                T("windows-11-dark", "Windows 11 Dark", "Recommended", true, C(32,32,32), C(38,38,38), C(48,48,48), C(56,56,56), C(0,120,212)),
                T("windows-11-light", "Windows 11 Light", "Recommended", false, C(249,249,249), C(243,243,243), C(255,255,255), C(230,230,230), C(0,103,192)),

                T("high-contrast-black", "High Contrast Black", "System & Accessibility", true, C(0,0,0), C(0,0,0), C(20,20,20), C(55,55,55), C(0,255,255), Color.White),
                T("high-contrast-white", "High Contrast White", "System & Accessibility", false, C(255,255,255), C(255,255,255), C(245,245,245), C(220,220,220), C(0,0,180), Color.Black),

                T("sublime-monokai", "Sublime Monokai", "Very Dark", true, C(20,21,18), C(30,31,28), C(50,50,45), C(60,60,52), C(249,38,114)),
                T("chrome-black", "Chrome Black", "Very Dark", true, C(5,5,10), C(15,15,24), C(20,20,30), C(40,40,50), C(0,210,235)),
                T("midnight", "Midnight", "Very Dark", true, C(10,10,30), C(22,22,45), C(30,30,50), C(50,50,80), C(80,150,255)),

                T("light-blue", "Light Blue", "Light", false, C(240,248,255), C(232,241,250), C(220,230,240), C(150,200,230), C(0,100,180)),
                T("solarized-light", "Solarized Light", "Light", false, C(253,246,227), C(246,239,220), C(238,232,213), C(220,210,190), C(38,139,210), C(88,110,117)),
                T("pearl", "Pearl", "Light", false, C(245,245,240), C(238,238,232), C(230,230,225), C(200,200,190), C(70,120,160), C(70,70,65)),

                T("lime", "Lime", "Green", true, C(10,20,10), C(18,35,18), C(30,50,30), C(60,100,60), C(50,205,50)),
                T("forest-green", "Forest Green", "Green", true, C(10,30,10), C(16,45,18), C(20,60,20), C(40,80,40), C(46,180,70)),
                T("dark-emerald", "Dark Emerald", "Green", true, C(0,40,20), C(8,55,28), C(10,70,30), C(20,100,40), C(0,190,100)),

                T("royal-blue", "Royal Blue", "Blue", true, C(10,20,60), C(18,32,78), C(20,40,90), C(35,65,125), C(65,105,225)),
                T("cobalt", "Cobalt", "Blue", true, C(0,25,75), C(5,38,95), C(10,50,110), C(20,75,145), C(50,130,255)),
                T("ocean-deep", "Ocean Deep", "Blue", true, C(5,25,45), C(10,40,65), C(15,55,80), C(25,75,105), C(0,150,200)),

                T("dark-red", "Dark and Red", "Red", true, C(28,10,12), C(42,16,18), C(55,22,25), C(85,30,35), C(215,55,65)),
                T("crimson", "Crimson", "Red", true, C(35,5,15), C(55,10,22), C(70,15,28), C(105,25,40), C(220,20,60)),
                T("ruby", "Ruby", "Red", true, C(35,8,20), C(55,14,30), C(75,20,38), C(105,30,50), C(225,45,85)),

                T("deep-purple", "Deep Purple", "Purple", true, C(25,10,40), C(38,18,58), C(50,25,70), C(75,40,100), C(155,85,220)),
                T("violet-haze", "Violet Haze", "Purple", true, C(30,20,45), C(45,30,62), C(60,40,80), C(85,60,110), C(180,120,230)),
                T("amethyst", "Amethyst", "Purple", true, C(32,18,48), C(48,28,68), C(62,38,88), C(90,55,120), C(170,100,220)),

                T("classic-dark", "Classic Dark", "Gray / Neutral", true, C(24,24,24), C(34,34,34), C(45,45,45), C(64,64,64), C(0,145,170)),
                T("charcoal", "Charcoal", "Gray / Neutral", true, C(30,30,30), C(42,42,42), C(52,52,52), C(72,72,72), C(110,160,190)),
                T("steel", "Steel", "Gray / Neutral", true, C(35,40,45), C(48,54,60), C(58,65,72), C(78,86,95), C(100,160,205)),

                T("gold", "Gold", "Orange / Gold", true, C(35,28,5), C(50,40,10), C(65,52,15), C(95,72,18), C(225,170,0)),
                T("warm-orange", "Warm Orange", "Orange / Gold", true, C(40,20,8), C(58,30,12), C(75,40,18), C(105,55,22), C(235,115,25)),
                T("copper", "Copper", "Orange / Gold", true, C(38,22,14), C(55,34,22), C(72,44,28), C(100,60,35), C(195,105,55)),

                T("fluent-teal", "Fluent Teal", "Teal / Cyan", true, C(8,30,32), C(12,46,48), C(18,58,60), C(28,82,84), C(0,170,170)),
                T("arctic-cyan", "Arctic Cyan", "Teal / Cyan", false, C(235,250,252), C(225,244,247), C(245,255,255), C(190,225,230), C(0,115,135)),
                T("deep-ocean", "Deep Ocean", "Teal / Cyan", true, C(4,24,32), C(8,38,48), C(12,52,62), C(20,72,84), C(0,175,195)),
                T("nordic", "Nordic", "Teal / Cyan", true, C(28,40,48), C(36,52,60), C(46,64,72), C(62,82,90), C(80,170,180)),

                T("neon-magenta", "Neon Magenta", "Pink / Magenta", true, C(30,5,28), C(48,10,45), C(62,18,58), C(88,25,80), C(235,50,205)),
                T("rose-dark", "Rose Dark", "Pink / Magenta", true, C(38,15,24), C(55,24,35), C(72,32,45), C(100,44,60), C(220,90,135)),
                T("raspberry", "Raspberry", "Pink / Magenta", true, C(42,8,24), C(60,14,34), C(78,20,44), C(108,28,58), C(215,55,115)),
                T("cyber-pink", "Cyber Pink", "Pink / Magenta", true, C(18,8,28), C(32,14,46), C(45,22,60), C(68,32,82), C(255,65,180)),

                T("gruvbox-dark", "Gruvbox Dark", "Earth / Brown", true, C(40,40,40), C(50,48,44), C(60,56,50), C(80,73,62), C(215,153,33), C(235,219,178)),
                T("coffee", "Coffee", "Earth / Brown", true, C(34,24,18), C(50,36,26), C(64,46,34), C(88,62,43), C(190,125,70)),
                T("sandstone", "Sandstone", "Earth / Brown", false, C(244,235,218), C(235,224,205), C(250,242,228), C(211,190,160), C(145,85,35), C(60,45,30)),
                T("autumn", "Autumn", "Earth / Brown", true, C(40,25,14), C(58,38,20), C(75,50,28), C(105,67,34), C(210,105,30)),

                T("dracula", "Dracula", "Developer", true, C(40,42,54), C(48,50,64), C(58,60,78), C(68,71,90), C(189,147,249), C(248,248,242)),
                T("one-dark", "One Dark", "Developer", true, C(40,44,52), C(48,52,62), C(55,60,70), C(67,73,85), C(97,175,239), C(220,223,228)),
                T("tokyo-night", "Tokyo Night", "Developer", true, C(26,27,38), C(32,34,48), C(40,42,58), C(55,58,78), C(122,162,247), C(192,202,245)),
                T("nord", "Nord", "Developer", true, C(46,52,64), C(59,66,82), C(67,76,94), C(76,86,106), C(136,192,208), C(236,239,244)),

                T("classic-terminal", "Classic Terminal", "Retro / Terminal", true, C(0,12,0), C(0,22,0), C(0,32,0), C(10,55,10), C(0,200,0), C(140,255,140)),
                T("amber-terminal", "Amber Terminal", "Retro / Terminal", true, C(18,12,0), C(30,20,0), C(42,28,0), C(65,44,5), C(255,176,0), C(255,220,140)),
                T("matrix", "Matrix", "Retro / Terminal", true, C(0,8,0), C(0,18,0), C(0,28,0), C(0,48,0), C(0,255,70), C(150,255,175)),
                T("dos-blue", "DOS Blue", "Retro / Terminal", true, C(0,0,90), C(0,0,110), C(0,0,125), C(20,20,150), C(80,180,255), Color.White)
            };
        }

        private static AppTheme T(
            string id,
            string name,
            string category,
            bool dark,
            Color background,
            Color panel,
            Color input,
            Color button,
            Color accent,
            Color? foreground = null)
        {
            return ThemeFactory.Create(
                id,
                name,
                category,
                dark,
                background,
                panel,
                input,
                button,
                accent,
                foreground);
        }

        private static Color C(int red, int green, int blue)
        {
            return Color.FromArgb(red, green, blue);
        }
    }
}
