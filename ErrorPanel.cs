// ============================================================
// QuickLook.Plugin.DevPowerTool — ErrorPanel.cs
// Fallback panel shown when the plugin cannot load a file.
// Styled to match the plugin's dark/light theme automatically.
// ============================================================
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace QuickLook.Plugin.DevPowerTool
{
    /// <summary>
    /// A themed error panel used as a safe fallback when file loading fails.
    /// Constructed entirely in code — no XAML required.
    /// </summary>
    public class ErrorPanel : UserControl
    {
        public ErrorPanel(string message)
        {
            bool isDark = IsSystemDarkTheme();

            Background = new SolidColorBrush(isDark
                ? Color.FromRgb(0x1E, 0x1E, 0x1E)
                : Colors.White);

            var container = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Padding             = new Thickness(40, 32, 40, 32),
                CornerRadius        = new CornerRadius(8),
                Background          = new SolidColorBrush(isDark
                    ? Color.FromRgb(0x2D, 0x2D, 0x2D)
                    : Color.FromRgb(0xF8, 0xF8, 0xF8)),
                BorderBrush         = new SolidColorBrush(isDark
                    ? Color.FromRgb(0x44, 0x44, 0x44)
                    : Color.FromRgb(0xE0, 0xE0, 0xE0)),
                BorderThickness     = new Thickness(1)
            };

            var stack = new StackPanel
            {
                Orientation         = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Warning icon
            stack.Children.Add(new TextBlock
            {
                Text                = "⚠",
                FontSize            = 40,
                Foreground          = new SolidColorBrush(Color.FromRgb(0xF5, 0xC5, 0x18)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 14)
            });

            // Title
            stack.Children.Add(new TextBlock
            {
                Text                = "Preview Unavailable",
                FontFamily          = new FontFamily("Segoe UI, sans-serif"),
                FontSize            = 16,
                FontWeight          = FontWeights.SemiBold,
                Foreground          = new SolidColorBrush(isDark
                    ? Color.FromRgb(0xD4, 0xD4, 0xD4)
                    : Color.FromRgb(0x1E, 0x1E, 0x1E)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 10)
            });

            // Separator line
            stack.Children.Add(new Border
            {
                Height          = 1,
                Width           = 300,
                Background      = new SolidColorBrush(isDark
                    ? Color.FromRgb(0x44, 0x44, 0x44)
                    : Color.FromRgb(0xE0, 0xE0, 0xE0)),
                Margin          = new Thickness(0, 0, 0, 10)
            });

            // Error message
            stack.Children.Add(new TextBlock
            {
                Text            = message,
                FontFamily      = new FontFamily("Cascadia Code, Consolas, Courier New, monospace"),
                FontSize        = 12,
                Foreground      = new SolidColorBrush(isDark
                    ? Color.FromRgb(0xF3, 0x8B, 0xA8)
                    : Color.FromRgb(0xCC, 0x00, 0x00)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping    = TextWrapping.Wrap,
                MaxWidth        = 480,
                TextAlignment   = TextAlignment.Center
            });

            container.Child = stack;
            Content         = container;
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static bool IsSystemDarkTheme()
        {
            try
            {
                const string key = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
                using (var reg = Registry.CurrentUser.OpenSubKey(key))
                {
                    if (reg == null) return false;
                    var val = reg.GetValue("AppsUseLightTheme");
                    if (val is int i) return i == 0;
                }
            }
            catch { /* registry unavailable — assume light */ }
            return false;
        }
    }
}
