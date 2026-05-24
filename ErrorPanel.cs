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
            bool dark = IsDark();

            Background = new SolidColorBrush(dark
                ? Color.FromRgb(0x1E, 0x1E, 0x1E)
                : Colors.White);

            var container = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };

            var stack = new StackPanel
            {
                Orientation         = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Warning icon
            stack.Children.Add(new TextBlock
            {
                Text                = "⚠  Preview Unavailable",
                FontFamily          = new FontFamily("Segoe UI"),
                FontSize            = 16,
                FontWeight          = FontWeights.SemiBold,
                Foreground          = new SolidColorBrush(dark
                    ? Color.FromRgb(0xD4, 0xD4, 0xD4)
                    : Color.FromRgb(0x1E, 0x1E, 0x1E)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 12)
            });

            // Error message
            stack.Children.Add(new TextBlock
            {
                Text            = message,
                FontFamily      = new FontFamily("Consolas, Courier New"),
                FontSize        = 12,
                Foreground      = new SolidColorBrush(dark
                    ? Color.FromRgb(0xF3, 0x8B, 0xA8)
                    : Color.FromRgb(0xCC, 0x00, 0x00)),
                TextWrapping    = TextWrapping.Wrap,
                MaxWidth        = 480,
                TextAlignment   = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            container.Child = stack;
            Content         = container;
        }

        private static bool IsDark()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key?.GetValue("AppsUseLightTheme") is int v)
                        return v == 0;
                }
            }
            catch { /* registry unavailable — assume light */ }
            return false;
        }
    }
}
