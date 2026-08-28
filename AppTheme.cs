using System;
using System.Drawing;
using System.Windows.Forms;

namespace DismToolGui
{
    internal sealed class AppTheme
    {
        internal AppTheme(
            string id,
            string name,
            string category,
            bool isDark,
            Color background,
            Color panelBackground,
            Color menuBackground,
            Color foreground,
            Color inputBackground,
            Color inputForeground,
            Color buttonBackground,
            Color buttonForeground,
            Color buttonHoverBackground,
            Color accent,
            Color accentHover,
            Color accentForeground,
            Color outputBackground,
            Color outputForeground,
            Color border,
            Color footer,
            Color disabledForeground,
            Color accentDisabledForeground,
            Color selectionBackground,
            Color selectionForeground,
            Color logInfo,
            Color logProcess,
            Color logSuccess,
            Color logWarning,
            Color logError,
            Color logDebug,
            Color logCommand)
        {
            Id = id;
            Name = name;
            Category = category;
            IsDark = isDark;
            Background = background;
            PanelBackground = panelBackground;
            MenuBackground = menuBackground;
            Foreground = foreground;
            InputBackground = inputBackground;
            InputForeground = inputForeground;
            ButtonBackground = buttonBackground;
            ButtonForeground = buttonForeground;
            ButtonHoverBackground = buttonHoverBackground;
            Accent = accent;
            AccentHover = accentHover;
            AccentForeground = accentForeground;
            OutputBackground = outputBackground;
            OutputForeground = outputForeground;
            Border = border;
            Footer = footer;
            DisabledForeground = disabledForeground;
            AccentDisabledForeground = accentDisabledForeground;
            SelectionBackground = selectionBackground;
            SelectionForeground = selectionForeground;
            LogInfo = logInfo;
            LogProcess = logProcess;
            LogSuccess = logSuccess;
            LogWarning = logWarning;
            LogError = logError;
            LogDebug = logDebug;
            LogCommand = logCommand;
        }

        public string Id { get; }
        public string Name { get; }
        public string Category { get; }
        public bool IsDark { get; }
        public Color Background { get; }
        public Color PanelBackground { get; }
        public Color MenuBackground { get; }
        public Color Foreground { get; }
        public Color InputBackground { get; }
        public Color InputForeground { get; }
        public Color ButtonBackground { get; }
        public Color ButtonForeground { get; }
        public Color ButtonHoverBackground { get; }
        public Color Accent { get; }
        public Color AccentHover { get; }
        public Color AccentForeground { get; }
        public Color OutputBackground { get; }
        public Color OutputForeground { get; }
        public Color Border { get; }
        public Color Footer { get; }
        public Color DisabledForeground { get; }
        public Color AccentDisabledForeground { get; }
        public Color SelectionBackground { get; }
        public Color SelectionForeground { get; }
        public Color LogInfo { get; }
        public Color LogProcess { get; }
        public Color LogSuccess { get; }
        public Color LogWarning { get; }
        public Color LogError { get; }
        public Color LogDebug { get; }
        public Color LogCommand { get; }

        public Color GetLogColor(ToolkitLogLevel level)
        {
            switch (level)
            {
                case ToolkitLogLevel.Process:
                    return LogProcess;
                case ToolkitLogLevel.Success:
                    return LogSuccess;
                case ToolkitLogLevel.Warning:
                    return LogWarning;
                case ToolkitLogLevel.Error:
                    return LogError;
                case ToolkitLogLevel.Debug:
                    return LogDebug;
                case ToolkitLogLevel.Command:
                    return LogCommand;
                default:
                    return LogInfo;
            }
        }
    }

    internal static class ThemeFactory
    {
        public static AppTheme Create(
            string id,
            string name,
            string category,
            bool isDark,
            Color background,
            Color panelBackground,
            Color inputBackground,
            Color buttonBackground,
            Color accent,
            Color? requestedForeground = null)
        {
            Color desiredForeground = requestedForeground ??
                (isDark ? Color.FromArgb(235, 235, 235) : Color.FromArgb(25, 25, 25));
            Color foreground = ThemeContrast.EnsureAcross(
                desiredForeground,
                4.5,
                background,
                panelBackground);
            Color menuBackground = panelBackground;
            Color inputForeground = ThemeContrast.Ensure(inputBackground, foreground, 4.5);
            Color buttonForeground = ThemeContrast.Ensure(buttonBackground, foreground, 4.5);
            Color buttonHover = ThemeContrast.Blend(buttonBackground, accent, 0.28);
            Color accentForeground = ThemeContrast.BestAcross(4.5, accent);
            Color accentHover = ThemeContrast.Blend(
                accent,
                accentForeground == Color.White ? Color.Black : Color.White,
                0.12);
            Color outputBackground = isDark
                ? ThemeContrast.Blend(background, Color.Black, 0.42)
                : ThemeContrast.Blend(background, Color.White, 0.62);
            Color outputForeground = ThemeContrast.Ensure(outputBackground, foreground, 4.5);
            Color border = ThemeContrast.Blend(foreground, panelBackground, 0.52);
            Color footer = ThemeContrast.Ensure(background, accent, 4.5);
            Color disabledCandidate = ThemeContrast.Blend(foreground, panelBackground, 0.38);
            Color disabledForeground = ThemeContrast.EnsureAcross(
                disabledCandidate,
                3.0,
                panelBackground,
                inputBackground,
                buttonBackground);
            Color accentDisabledCandidate = ThemeContrast.Blend(
                accentForeground,
                accent,
                0.28);
            Color accentDisabledForeground = ThemeContrast.Ensure(
                accent,
                accentDisabledCandidate,
                3.0);
            Color selectionForeground = ThemeContrast.Ensure(accent, foreground, 4.5);

            Color desiredInfo = foreground;
            Color desiredProcess = isDark ? Color.DeepSkyBlue : Color.RoyalBlue;
            Color desiredSuccess = isDark ? Color.LightGreen : Color.DarkGreen;
            Color desiredWarning = isDark ? Color.Gold : Color.DarkOrange;
            Color desiredError = isDark ? Color.LightSalmon : Color.Firebrick;
            Color desiredDebug = isDark ? Color.Silver : Color.DimGray;
            Color desiredCommand = isDark ? Color.Cyan : Color.DarkCyan;

            return new AppTheme(
                id,
                name,
                category,
                isDark,
                background,
                panelBackground,
                menuBackground,
                foreground,
                inputBackground,
                inputForeground,
                buttonBackground,
                buttonForeground,
                buttonHover,
                accent,
                accentHover,
                accentForeground,
                outputBackground,
                outputForeground,
                border,
                footer,
                disabledForeground,
                accentDisabledForeground,
                accent,
                selectionForeground,
                ThemeContrast.Ensure(outputBackground, desiredInfo, 4.5),
                ThemeContrast.Ensure(outputBackground, desiredProcess, 4.5),
                ThemeContrast.Ensure(outputBackground, desiredSuccess, 4.5),
                ThemeContrast.Ensure(outputBackground, desiredWarning, 4.5),
                ThemeContrast.Ensure(outputBackground, desiredError, 4.5),
                ThemeContrast.Ensure(outputBackground, desiredDebug, 4.5),
                ThemeContrast.Ensure(outputBackground, desiredCommand, 4.5));
        }
    }

    internal static class ThemeContrast
    {
        public static double Ratio(Color first, Color second)
        {
            double lighter = Math.Max(Luminance(first), Luminance(second));
            double darker = Math.Min(Luminance(first), Luminance(second));
            return (lighter + 0.05) / (darker + 0.05);
        }

        public static Color Ensure(Color background, Color desired, double minimumRatio)
        {
            if (Ratio(background, desired) >= minimumRatio)
                return desired;

            Color target = Ratio(background, Color.Black) >= Ratio(background, Color.White)
                ? Color.Black
                : Color.White;
            for (int step = 1; step <= 20; step++)
            {
                Color adjusted = Blend(desired, target, step / 20.0);
                if (Ratio(background, adjusted) >= minimumRatio)
                    return adjusted;
            }

            return target;
        }

        public static Color EnsureAcross(
            Color desired,
            double minimumRatio,
            params Color[] backgrounds)
        {
            bool valid = true;
            foreach (Color background in backgrounds)
                valid &= Ratio(background, desired) >= minimumRatio;
            if (valid)
                return desired;

            return BestAcross(minimumRatio, backgrounds);
        }

        public static Color BestAcross(double minimumRatio, params Color[] backgrounds)
        {
            double blackMinimum = double.MaxValue;
            double whiteMinimum = double.MaxValue;
            foreach (Color background in backgrounds)
            {
                blackMinimum = Math.Min(blackMinimum, Ratio(background, Color.Black));
                whiteMinimum = Math.Min(whiteMinimum, Ratio(background, Color.White));
            }

            Color best = blackMinimum >= whiteMinimum ? Color.Black : Color.White;
            if (Math.Max(blackMinimum, whiteMinimum) >= minimumRatio)
                return best;
            return best;
        }

        public static Color Blend(Color from, Color to, double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            return Color.FromArgb(
                (int)Math.Round(from.R + ((to.R - from.R) * amount)),
                (int)Math.Round(from.G + ((to.G - from.G) * amount)),
                (int)Math.Round(from.B + ((to.B - from.B) * amount)));
        }

        private static double Luminance(Color color)
        {
            return (0.2126 * Linear(color.R)) +
                   (0.7152 * Linear(color.G)) +
                   (0.0722 * Linear(color.B));
        }

        private static double Linear(byte component)
        {
            double value = component / 255.0;
            return value <= 0.03928
                ? value / 12.92
                : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
    }

    internal static class ThemeStyler
    {
        public static void ApplyControlTree(Control parent, AppTheme theme)
        {
            if (parent == null || theme == null)
                return;

            foreach (Control control in parent.Controls)
            {
                if (control is TextBoxBase textBox)
                {
                    textBox.BackColor = theme.InputBackground;
                    textBox.ForeColor = theme.InputForeground;
                }
                else if (control is ComboBox comboBox)
                {
                    comboBox.BackColor = theme.InputBackground;
                    comboBox.ForeColor = theme.InputForeground;
                }
                else if (control is Button button)
                {
                    ApplyButton(button, theme);
                }
                else if (control is DataGridView grid)
                {
                    ApplyGrid(grid, theme);
                }
                else
                {
                    control.BackColor = theme.PanelBackground;
                    control.ForeColor = theme.Foreground;
                }

                if (control.HasChildren)
                    ApplyControlTree(control, theme);
            }
        }

        public static void ApplyButton(Button button, AppTheme theme, bool accent = false)
        {
            button.UseVisualStyleBackColor = false;
            button.BackColor = accent ? theme.Accent : theme.ButtonBackground;
            Color enabledTextColor = accent
                ? theme.AccentForeground
                : theme.ButtonForeground;
            button.ForeColor = button.Enabled ? enabledTextColor : theme.DisabledForeground;
            button.FlatAppearance.BorderColor = theme.Border;
            if (button is ThemedButton themedButton)
            {
                themedButton.EnabledTextColor = enabledTextColor;
                themedButton.DisabledTextColor = theme.DisabledForeground;
            }
        }

        public static void ApplyGrid(DataGridView grid, AppTheme theme)
        {
            grid.BackgroundColor = theme.InputBackground;
            grid.GridColor = theme.Border;
            grid.DefaultCellStyle.BackColor = theme.InputBackground;
            grid.DefaultCellStyle.ForeColor = theme.InputForeground;
            grid.DefaultCellStyle.SelectionBackColor = theme.SelectionBackground;
            grid.DefaultCellStyle.SelectionForeColor = theme.SelectionForeground;
            grid.ColumnHeadersDefaultCellStyle.BackColor = theme.ButtonBackground;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = theme.ButtonForeground;
            grid.EnableHeadersVisualStyles = false;
        }
    }

    /// <summary>
    /// Flat WinForms button that preserves the selected palette's disabled
    /// foreground instead of falling back to the OS color, which can be
    /// unreadable on custom dark backgrounds.
    /// </summary>
    internal sealed class ThemedButton : Button
    {
        public Color EnabledTextColor { get; set; } = SystemColors.ControlText;
        public Color DisabledTextColor { get; set; } = Color.Gray;

        protected override void OnEnabledChanged(EventArgs e)
        {
            ForeColor = Enabled ? EnabledTextColor : DisabledTextColor;
            base.OnEnabledChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Enabled)
            {
                base.OnPaint(e);
                return;
            }

            e.Graphics.Clear(BackColor);
            using (var pen = new Pen(FlatAppearance.BorderColor))
            {
                var borderRectangle = ClientRectangle;
                borderRectangle.Width = Math.Max(0, borderRectangle.Width - 1);
                borderRectangle.Height = Math.Max(0, borderRectangle.Height - 1);
                e.Graphics.DrawRectangle(pen, borderRectangle);
            }

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                ClientRectangle,
                DisabledTextColor,
                BackColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis);
        }
    }
}
