// ============================================================
// QuickLook.Plugin.DevPowerTool — PreviewPanel.cs
//
// Uses AvalonEdit TextEditor exactly like QuickLook TextViewer.
// Text colour is forced via a custom IHighlightingDefinition
// that sets the default colour — the correct AvalonEdit way.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
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

        private TextEditor  _editor;
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
            _editor = BuildEditor();
            var grid = new Grid();
            grid.Children.Add(_editor);

            if (_fileType == DevFileType.EnvFile)
                grid.Children.Add(BuildToggleOverlay());
            else
                grid.Children.Add(BuildSwatchOverlay());

            Content = grid;
        }

        // ── AvalonEdit setup — identical to TextViewer ────────────────────

        private TextEditor BuildEditor()
        {
            var ed = new TextEditor
            {
                IsReadOnly                    = true,
                ShowLineNumbers               = true,
                FontFamily                    = new FontFamily("Cascadia Code, Consolas, Courier New, monospace"),
                FontSize                      = 14,
                WordWrap                      = false,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                Options = new TextEditorOptions
                {
                    EnableHyperlinks            = false,
                    EnableEmailHyperlinks       = false,
                    ShowBoxForControlCharacters = true,
                    ConvertTabsToSpaces         = false,
                    IndentationSize             = 4
                }
            };

            ed.Background            = new SolidColorBrush(_bgColor);
            ed.LineNumbersForeground = new SolidColorBrush(
                _isDark ? Color.FromRgb(0x85, 0x85, 0x85) : Color.FromRgb(0xA0, 0xA0, 0xA0));

            // Use a simple custom highlighting that only sets the default text colour.
            // This is how TextViewer controls text colour in AvalonEdit.
            ed.SyntaxHighlighting = new PlainTextHighlighting(_fgColor, _bgColor);

            return ed;
        }

        // ── Swatch overlay ────────────────────────────────────────────────

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
                Margin              = new Thickness(52, 0, 0, 8),
                Visibility          = Visibility.Collapsed
            };

            return _swatchCountOverlay;
        }

        // ── iOS toggle ────────────────────────────────────────────────────

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

            var anim = new ThicknessAnimation
            {
                From           = new Thickness(fromX, 0, 0, 0),
                To             = new Thickness(toX,   0, 0, 0),
                Duration       = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            anim.Completed += (s, e) =>
            {
                _toggleThumb.Margin = new Thickness(toX, 0, 0, 0);
                _toggleAnimating    = false;
            };
            _toggleThumb.BeginAnimation(MarginProperty, anim);
        }

        // ── File loading ──────────────────────────────────────────────────

        private async Task LoadAsync()
        {
            try
            {
                var info = new FileInfo(_path);
                if (info.Length > MaxBytes)
                {
                    SetEditorText(string.Format("[File too large: {0:N0} KB. Limit is {1:N0} KB]",
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

                    _editor.Text = text;
                    await ScanSwatchesAsync(text);
                }
            }
            catch (Exception ex)
            {
                SetEditorText("Could not load file:\n" + ex.Message);
            }
        }

        // ── Colour swatch scanning ────────────────────────────────────────

        private async Task ScanSwatchesAsync(string text)
        {
            var swatches = new List<SwatchInfo>();
            var lines    = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int total    = 0;

            await Task.Run(() =>
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (var token in ColorParser.ParseLine(lines[i]))
                    {
                        if (!token.Color.HasValue) continue;
                        swatches.Add(new SwatchInfo
                        {
                            Line        = i + 1,
                            CharOffset  = token.Index,
                            TokenLength = token.Raw.Length,
                            Color       = token.Color.Value
                        });
                        total++;
                    }
                }
            });

            if (total == 0) return;

            var renderer = new ColorSwatchRenderer(_editor, swatches);
            _editor.TextArea.TextView.BackgroundRenderers.Add(renderer);
            _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);

            _swatchCountText.Text          = string.Format("  {0} colour{1}", total, total == 1 ? "" : "s");
            _swatchCountOverlay.Visibility = Visibility.Visible;
        }

        // ── Env rendering ─────────────────────────────────────────────────

        private void RenderEnv()
        {
            if (_envLines == null) return;
            var sb = new StringBuilder();
            foreach (var line in _envLines)
                sb.AppendLine(line.DisplayText(_revealed));
            _editor.Text = sb.ToString();
        }

        // ── Utilities ─────────────────────────────────────────────────────

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

        private void SetEditorText(string msg)
        {
            _editor.IsReadOnly = false;
            _editor.Text       = msg;
            _editor.IsReadOnly = true;
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

    // ── PlainTextHighlighting ─────────────────────────────────────────────
    // A minimal IHighlightingDefinition that only sets the default text
    // colour. This is the correct way to control text colour in AvalonEdit —
    // the Foreground property on TextEditor is ignored by the renderer.

    internal sealed class PlainTextHighlighting : IHighlightingDefinition
    {
        private readonly HighlightingColor _defaultColor;

        public PlainTextHighlighting(Color foreground, Color background)
        {
            _defaultColor = new HighlightingColor
            {
                Foreground = new SimpleHighlightingBrush(foreground),
                Background = new SimpleHighlightingBrush(background)
            };
        }

        public string Name => "PlainText";

        public HighlightingRuleSet MainRuleSet => new HighlightingRuleSet();

        public HighlightingRuleSet GetNamedRuleSet(string name) => null;

        public HighlightingColor GetNamedColor(string name) => null;

        public IEnumerable<HighlightingColor> NamedHighlightingColors
            => new List<HighlightingColor>();

        public IDictionary<string, string> Properties
            => new System.Collections.Generic.Dictionary<string, string>();

        // This is what actually sets the default text colour in AvalonEdit
        public HighlightingColor DefaultColor => _defaultColor;
    }

    internal sealed class SimpleHighlightingBrush : HighlightingBrush
    {
        private readonly SolidColorBrush _brush;

        public SimpleHighlightingBrush(Color color)
        {
            _brush = new SolidColorBrush(color);
            _brush.Freeze();
        }

        public override Brush GetBrush(ITextRunConstructionContext context) => _brush;

        public override string ToString() => _brush.Color.ToString();
    }
}