// ============================================================
// QuickLook.Plugin.DevPowerTool — PreviewPanel.cs
// Uses RichTextBox + FlowDocument for guaranteed text visibility.
// Inline colour swatches via InlineUIContainer.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace QuickLook.Plugin.DevPowerTool
{
    public class PreviewPanel : UserControl
    {
        private const long MaxBytes = 2 * 1024 * 1024;
        private const int  MaxLines = 5_000;

        private readonly string      _path;
        private readonly DevFileType _fileType;
        private readonly bool        _isDark;

        private List<EnvLine> _envLines;
        private bool          _revealed = false;

        private RichTextBox _textBox;
        private TextBlock   _swatchCountText;
        private Border      _swatchCountOverlay;
        private Border      _toggleTrack;
        private Ellipse     _toggleThumb;
        private TextBlock   _toggleLabel;
        private bool        _toggleAnimating = false;

        private static readonly Color _trackOff = Color.FromRgb(0x78, 0x78, 0x80);
        private static readonly Color _trackOn  = Color.FromRgb(0x34, 0xC7, 0x59);
        private const double TrackW    = 38;
        private const double TrackH    = 22;
        private const double ThumbSz   = 18;
        private const double ThumbOffX = 2;
        private const double ThumbOnX  = TrackW - ThumbSz - 2;

        // Cached colours
        private Color _fgColor;
        private Color _bgColor;

        public PreviewPanel(string path, DevFileType fileType)
        {
            _path     = path;
            _fileType = fileType;
            _isDark   = IsSystemDarkTheme();
            _bgColor  = _isDark ? Color.FromRgb(0x1E, 0x1E, 0x1E) : Colors.White;
            _fgColor  = _isDark ? Color.FromRgb(0xD4, 0xD4, 0xD4) : Color.FromRgb(0x1E, 0x1E, 0x1E);

            Build();
            Loaded += async (s, e) => await LoadAsync();
        }

        private void Build()
        {
            _textBox = new RichTextBox
            {
                IsReadOnly            = true,
                FontFamily            = new FontFamily("Cascadia Code, Consolas, Courier New, monospace"),
                FontSize              = 14,
                Background            = new SolidColorBrush(_bgColor),
                Foreground            = new SolidColorBrush(_fgColor),
                BorderThickness       = new Thickness(0),
                Padding               = new Thickness(10),
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Document = new FlowDocument
                {
                    FontFamily            = new FontFamily("Cascadia Code, Consolas, Courier New, monospace"),
                    FontSize              = 14,
                    PagePadding           = new Thickness(10),
                    Background            = new SolidColorBrush(_bgColor),
                    Foreground            = new SolidColorBrush(_fgColor),
                    LineHeight            = 20,
                    IsColumnWidthFlexible = false,
                    ColumnWidth           = double.MaxValue
                }
            };

            var grid = new Grid();
            grid.Children.Add(_textBox);

            if (_fileType == DevFileType.EnvFile)
                grid.Children.Add(BuildToggleOverlay());
            else
                grid.Children.Add(BuildSwatchOverlay());

            Content = grid;
        }

        private Border BuildSwatchOverlay()
        {
            _swatchCountText = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI, sans-serif"),
                FontSize   = 11,
                Foreground = new SolidColorBrush(_isDark
                    ? Color.FromRgb(0xA6, 0xE3, 0xA1)
                    : Color.FromRgb(0x2E, 0x7D, 0x32))
            };

            _swatchCountOverlay = new Border
            {
                Child               = _swatchCountText,
                Background          = new SolidColorBrush(_isDark
                    ? Color.FromArgb(210, 20, 45, 20)
                    : Color.FromArgb(210, 220, 245, 220)),
                BorderBrush         = new SolidColorBrush(_isDark
                    ? Color.FromRgb(0x2E, 0x5C, 0x2E)
                    : Color.FromRgb(0x88, 0xCC, 0x88)),
                BorderThickness     = new Thickness(1),
                CornerRadius        = new CornerRadius(4),
                Padding             = new Thickness(8, 3, 8, 3),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment   = VerticalAlignment.Bottom,
                Margin              = new Thickness(10, 0, 0, 8),
                Visibility          = Visibility.Collapsed
            };

            return _swatchCountOverlay;
        }

        private UIElement BuildToggleOverlay()
        {
            _toggleTrack = new Border
            {
                Width        = TrackW,
                Height       = TrackH,
                CornerRadius = new CornerRadius(TrackH / 2),
                Background   = new SolidColorBrush(_trackOff),
                Cursor       = Cursors.Hand
            };

            _toggleThumb = new Ellipse
            {
                Width               = ThumbSz,
                Height              = ThumbSz,
                Fill                = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment   = VerticalAlignment.Center,
                Margin              = new Thickness(ThumbOffX, 0, 0, 0),
                Effect              = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black, Opacity = 0.3, BlurRadius = 4, ShadowDepth = 1
                }
            };

            _toggleLabel = new TextBlock
            {
                Text                = "Show secrets",
                FontFamily          = new FontFamily("Segoe UI, sans-serif"),
                FontSize            = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground          = new SolidColorBrush(_isDark
                    ? Color.FromRgb(0x99, 0x99, 0x99)
                    : Color.FromRgb(0x66, 0x66, 0x66)),
                Margin = new Thickness(0, 4, 0, 0)
            };

            var trackGrid = new Grid { Width = TrackW, Height = TrackH };
            trackGrid.Children.Add(_toggleTrack);
            trackGrid.Children.Add(_toggleThumb);

            var stack = new StackPanel
            {
                Orientation         = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            stack.Children.Add(trackGrid);
            stack.Children.Add(_toggleLabel);

            stack.MouseLeftButtonUp        += (s, e) => ToggleSecrets();
            _toggleTrack.MouseLeftButtonUp += (s, e) => ToggleSecrets();

            return new Border
            {
                Child               = stack,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment   = VerticalAlignment.Top,
                Margin              = new Thickness(0, 10, 16, 0),
                Background          = new SolidColorBrush(_isDark
                    ? Color.FromArgb(180, 30, 30, 30)
                    : Color.FromArgb(180, 240, 240, 240)),
                CornerRadius = new CornerRadius(8),
                Padding      = new Thickness(8, 6, 8, 6)
            };
        }

        private void ToggleSecrets()
        {
            if (_toggleAnimating) return;
            _revealed = !_revealed;
            AnimateToggle(_revealed);
            _toggleLabel.Text = _revealed ? "Hide secrets" : "Show secrets";
            RenderEnv();
        }

        private void AnimateToggle(bool on)
        {
            _toggleAnimating = true;

            var trackBrush = new SolidColorBrush(on ? _trackOff : _trackOn);
            _toggleTrack.Background = trackBrush;
            trackBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation
            {
                To             = on ? _trackOn : _trackOff,
                Duration       = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            });

            double fromX = on ? ThumbOffX : ThumbOnX;
            double toX   = on ? ThumbOnX  : ThumbOffX;
            _toggleThumb.Margin = new Thickness(fromX, 0, 0, 0);

            var thumbAnim = new ThicknessAnimation
            {
                From           = new Thickness(fromX, 0, 0, 0),
                To             = new Thickness(toX,   0, 0, 0),
                Duration       = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            thumbAnim.Completed += (s, e) =>
            {
                _toggleThumb.Margin = new Thickness(toX, 0, 0, 0);
                _toggleAnimating    = false;
            };
            _toggleThumb.BeginAnimation(MarginProperty, thumbAnim);
        }

        private async Task LoadAsync()
        {
            try
            {
                var info = new FileInfo(_path);
                if (info.Length > MaxBytes)
                {
                    SetText(string.Format("[File too large: {0:N0} KB. Limit is {1:N0} KB]",
                        info.Length / 1024, MaxBytes / 1024));
                    return;
                }

                string raw = await Task.Run(() => ReadFile(_path));

                if (_fileType == DevFileType.EnvFile)
                {
                    _envLines = EnvMaskingService.Parse(raw);
                    RenderEnv();
                }
                else
                {
                    bool truncated;
                    string text = Truncate(raw, MaxLines, out truncated);
                    if (truncated)
                        text += string.Format("\n\n// [Preview truncated at {0} lines]", MaxLines);
                    RenderText(text);
                }
            }
            catch (Exception ex)
            {
                SetText("Could not load file:\n" + ex.Message);
            }
        }

        private void RenderText(string text)
        {
            var doc   = _textBox.Document;
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int total = 0;

            doc.Blocks.Clear();

            foreach (var line in lines)
            {
                var para   = new Paragraph { Margin = new Thickness(0), LineHeight = 20 };
                var tokens = ColorParser.ParseLine(line);

                if (tokens.Count == 0)
                {
                    para.Inlines.Add(MakeRun(line, _fgColor));
                }
                else
                {
                    int cursor = 0;
                    foreach (var token in tokens)
                    {
                        if (token.Index > cursor)
                            para.Inlines.Add(MakeRun(line.Substring(cursor, token.Index - cursor), _fgColor));

                        if (token.Color.HasValue)
                        {
                            para.Inlines.Add(MakeSwatch(token.Color.Value));
                            total++;
                        }

                        para.Inlines.Add(MakeRun(token.Raw, _fgColor));
                        cursor = token.Index + token.Raw.Length;
                    }

                    if (cursor < line.Length)
                        para.Inlines.Add(MakeRun(line.Substring(cursor), _fgColor));
                }

                doc.Blocks.Add(para);
            }

            if (total > 0)
            {
                _swatchCountText.Text          = string.Format("  {0} colour{1}", total, total == 1 ? "" : "s");
                _swatchCountOverlay.Visibility = Visibility.Visible;
            }
        }

        private void RenderEnv()
        {
            if (_envLines == null) return;

            var commentColor = _isDark ? Color.FromRgb(0x6C, 0x70, 0x86) : Color.FromRgb(0x90, 0x94, 0xA5);
            var keyColor     = _isDark ? Color.FromRgb(0x89, 0xB4, 0xFA) : Color.FromRgb(0x19, 0x5F, 0xBB);
            var maskedColor  = _isDark ? Color.FromRgb(0x58, 0x5B, 0x70) : Color.FromRgb(0xAA, 0xAA, 0xBB);
            var valueColor   = _isDark ? Color.FromRgb(0xA6, 0xE3, 0xA1) : Color.FromRgb(0x2E, 0x7D, 0x32);

            var doc = _textBox.Document;
            doc.Blocks.Clear();

            foreach (var line in _envLines)
            {
                var para = new Paragraph { Margin = new Thickness(0), LineHeight = 20 };

                if (!line.HasSecret)
                {
                    // Comment, blank, or unrecognised — show as-is
                    para.Inlines.Add(MakeRun(line.Raw, commentColor));
                }
                else if (_revealed)
                {
                    // Revealed: show original raw line coloured by parts
                    // Prefix contains "KEY=" — split at = sign
                    var eqIdx = (line.Prefix ?? "").Length;
                    var raw   = line.Raw ?? "";
                    // Show prefix (key + equals) in key colour, value in value colour
                    if (eqIdx > 0 && eqIdx <= raw.Length)
                    {
                        para.Inlines.Add(new Run(raw.Substring(0, eqIdx))
                            { Foreground = new SolidColorBrush(keyColor), FontWeight = FontWeights.SemiBold });
                        para.Inlines.Add(MakeRun(raw.Substring(eqIdx), valueColor));
                    }
                    else
                    {
                        para.Inlines.Add(MakeRun(raw, _fgColor));
                    }
                }
                else
                {
                    // Masked: show prefix + asterisks
                    string mask = new string('*', Math.Max(8, line.Value == null ? 8 : line.Value.Length));
                    para.Inlines.Add(new Run(line.Prefix ?? "")
                        { Foreground = new SolidColorBrush(keyColor), FontWeight = FontWeights.SemiBold });
                    para.Inlines.Add(MakeRun(mask, maskedColor));
                }

                doc.Blocks.Add(para);
            }
        }

        private static Run MakeRun(string text, Color color)
        {
            return new Run(text) { Foreground = new SolidColorBrush(color) };
        }

        private static InlineUIContainer MakeSwatch(Color color)
        {
            bool dark = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) < 128;
            return new InlineUIContainer(new Border
            {
                Width           = 10,
                Height          = 10,
                Background      = new SolidColorBrush(color),
                BorderBrush     = new SolidColorBrush(dark
                    ? Color.FromArgb(80, 255, 255, 255)
                    : Color.FromArgb(80, 0, 0, 0)),
                BorderThickness = new Thickness(0.8),
                CornerRadius    = new CornerRadius(2),
                Margin          = new Thickness(0, 0, 2, -1)
            });
        }

        private static string ReadFile(string path)
        {
            using (var r = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                return r.ReadToEnd();
        }

        private static string Truncate(string text, int maxLines, out bool truncated)
        {
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            if (lines.Length <= maxLines) { truncated = false; return text; }
            truncated = true;
            return string.Join("\n", lines, 0, maxLines);
        }

        private void SetText(string msg)
        {
            var doc = _textBox.Document;
            doc.Blocks.Clear();
            doc.Blocks.Add(new Paragraph(new Run(msg)
            {
                Foreground = new SolidColorBrush(_isDark
                    ? Color.FromRgb(0xF3, 0x8B, 0xA8)
                    : Color.FromRgb(0xCC, 0x00, 0x00))
            }));
        }

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
            catch { }
            return false;
        }
    }
}