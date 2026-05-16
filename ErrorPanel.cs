// ============================================================
// QuickLook.Plugin.DevPowerTool — ErrorPanel.cs
// Fully code-behind fallback panel. No XAML file needed.
// ============================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuickLook.Plugin.DevPowerTool
{
    public class ErrorPanel : UserControl
    {
        public ErrorPanel(string message)
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E));

            var stack = new StackPanel
            {
                Orientation         = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };

            stack.Children.Add(new TextBlock
            {
                Text                = "⚠",
                FontSize            = 40,
                Foreground          = new SolidColorBrush(Color.FromRgb(0xF5, 0xC5, 0x18)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 12)
            });

            stack.Children.Add(new TextBlock
            {
                Text                = "Preview Unavailable",
                FontFamily          = new FontFamily("Segoe UI, sans-serif"),
                FontSize            = 18,
                FontWeight          = FontWeights.SemiBold,
                Foreground          = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 8)
            });

            stack.Children.Add(new TextBlock
            {
                Text                = message,
                FontFamily          = new FontFamily("Cascadia Code, Consolas, monospace"),
                FontSize            = 12,
                Foreground          = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping        = TextWrapping.Wrap,
                MaxWidth            = 500,
                TextAlignment       = TextAlignment.Center
            });

            Content = stack;
        }
    }
}