// ============================================================
// QuickLook.Plugin.DevPowerTool — ErrorPanel.cs
// Fallback panel shown when the plugin fails to load a file.
// ============================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuickLook.Common.Helpers;

namespace QuickLook.Plugin.DevPowerTool
{
    public class ErrorPanel : UserControl
    {
        public ErrorPanel(string message)
        {
            bool isDark = OSThemeHelper.AppsUseDarkTheme();

            Background = new SolidColorBrush(isDark
                ? Color.FromRgb(0x1E, 0x1E, 0x1E)
                : Colors.White);

            var stack = new StackPanel
            {
                Orientation         = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Margin              = new Thickness(40)
            };

            stack.Children.Add(new TextBlock
            {
                Text                = "⚠",
                FontSize            = 36,
                Foreground          = new SolidColorBrush(Color.FromRgb(0xF5, 0xC5, 0x18)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 12)
            });

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
                Margin              = new Thickness(0, 0, 0, 8)
            });

            stack.Children.Add(new TextBlock
            {
                Text                = message,
                FontFamily          = new FontFamily("Cascadia Code, Consolas, monospace"),
                FontSize            = 12,
                Foreground          = new SolidColorBrush(isDark
                    ? Color.FromRgb(0xF3, 0x8B, 0xA8)
                    : Color.FromRgb(0xCC, 0x00, 0x00)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping        = TextWrapping.Wrap,
                MaxWidth            = 500,
                TextAlignment       = TextAlignment.Center
            });

            Content = stack;
        }
    }
}