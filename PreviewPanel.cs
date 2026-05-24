// ============================================================
// QuickLook.Plugin.DevPowerTool — PreviewPanel.cs
// ============================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.Win32;
using QuickLook.Plugin.DevPowerTool.Helpers;

namespace QuickLook.Plugin.DevPowerTool
{
    public sealed class PreviewPanel : UserControl
    {
        private const long   MaxFileBytes = 2 * 1024 * 1024;
        private const int    MaxLines     = 5_000;

        private const double TrackW    = 38;
        private const double TrackH    = 22;
        private const double ThumbSize = 18;
        private const double ThumbOff  = 2;
        private const double ThumbOn   = TrackW - ThumbSize - 2;

        private static readonly Color TrackColorOff = Color.FromRgb(0x78, 0x78, 0x80);
        private static readonly Color TrackColorOn  = Color.FromRgb(0x34, 0xC7, 0x59);

        private readonly string      _path;
        private readonly DevFileType _fileType;
        private readonly bool        _isDark;
        private readonly Color       _fgColor;
        private readonly Color       _bgColor;

        private TextEditor _editor;
        private Border     _swatchBadge;
        private TextBlock  _swatchLabel;
        private Border     _toggleTrack;
        private Ellipse    _toggleThumb;
        private TextBlock  _toggleLabel;
        private bool       _toggleAnimating;
        private bool       _revealed;

        private List<EnvLine>          _envLines;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public PreviewPanel(string path, DevFileType fileType)
        {
            _path     = path;
            _fileType = fileType;
            _isDark   = DetectDarkTheme();

            _bgColor = _isDark ? Color.FromRgb(0x1E, 0x1E, 0x1E) : Colors.White;
            _fgColor = _isDark ? Color.FromRgb(0xD4, 0xD4, 0xD4) : Color.FromRgb(0x1E, 0x1E, 0x1E);

            BuildUI();

            Loaded   += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void BuildUI()
        {
            Background = new SolidColorBrush(_bgColor);
            _editor    = BuildEditor();

            var root = new Grid();
            root.Children.Add(_editor);

            if (_fileType == DevFileType.EnvFile)
                root.Children.Add(BuildEnvToggleOverlay());
            else
                root.Children.Add(BuildSwatchBadge());

            Content = root;
        }

        private TextEditor BuildEditor()
        {
            var ed = new TextEditor
            {
                IsReadOnly      = true,
                ShowLineNumbers = true,
                FontFamily      = new FontFamily("Cascadia Code, Consolas, Courier New, monospace"),
                FontSize        = 13.5,
                WordWrap        = false,
                Foreground      = new SolidColorBrush(_fgColor),
                Background      = new SolidColorBrush(_bgColor),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                Options = new TextEditorOptions
                {
                    EnableHyperlinks            = false,
                    EnableEmailHyperlinks       = false,
                    ShowBoxForControlCharacters = true,
                    ConvertTabsToSpaces         = false,
                    IndentationSize             = 4,
                    AllowScrollBelowDocument    = false
                },
                LineNumbersForeground = new SolidColorBrush(_isDark
                    ? Color.FromRgb(0x75, 0x75, 0x75)
                    : Color.FromRgb(0xA0, 0xA0, 0xA0))
            };

            ed.SyntaxHighlighting = new PlainTextHighlighting();

            return ed;
        }

        private Border BuildSwatchBadge()
        {
            _swatchLabel = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI, sans-serif"),
                FontSize   = 11,
                Foreground = new SolidColorBrush(_isDark
                    ? Color.FromRgb(0xA6, 0xE3, 0xA1)
                    : Color.FromRgb(0x2E, 0x7D, 0x32))
            };

            _swatchBadge = new Border
            {
                Child               = _swatchLabel,
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

            return _swatchBadge;
        }

        private UIElement BuildEnvToggleOverlay()
        {
            _toggleTrack = new Border
            {
                Width        = TrackW,
                Height       = TrackH,
                CornerRadius = new CornerRadius(TrackH / 2),
                Background   = new SolidColorBrush(TrackColorOff),
                Cursor       = Cursors.Hand
            };

            _toggleThumb = new Ellipse
            {
                Width               = ThumbSize,
                Height              = ThumbSize,
                Fill                = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment   = VerticalAlignment.Center,
                Margin              = new Thickness(ThumbOff, 0, 0, 0),
                Effect              = new DropShadowEffect
                {
                    Color       = Colors.Black,
                    Opacity     = 0.30,
                    BlurRadius  = 4,
                    ShadowDepth = 1
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
                Margin              = new Thickness(0, 4, 0, 0)
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

            _toggleTrack.MouseLeftButtonUp += (_, __) => Toggle();
            stack.MouseLeftButtonUp        += (_, __) => Toggle();

            return new Border
            {
                Child               = stack,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment   = VerticalAlignment.Top,
                Margin              = new Thickness(0, 10, 16, 0),
                Background          = new SolidColorBrush(_isDark
                    ? Color.FromArgb(180, 30, 30, 30)
                    : Color.FromArgb(180, 240, 240, 240)),
                CornerRadius        = new CornerRadius(8),
                Padding             = new Thickness(8, 6, 8, 6)
            };
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await LoadFileAsync(_cts.Token);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        private async Task LoadFileAsync(CancellationToken ct)
        {
            try
            {
                var info = new FileInfo(_path);

                if (info.Length > MaxFileBytes)
                {
                    SetText(string.Format(
                        "[ File too large to preview: {0:N0} KB  —  limit is {1:N0} KB ]\n\n" +
                        "Open the file in a text editor for full viewing.",
                        info.Length / 1024, MaxFileBytes / 1024));
                    return;
                }

                ct.ThrowIfCancellationRequested();

                string raw = await Task.Run(() => ReadFile(_path), ct);

                ct.ThrowIfCancellationRequested();

                if (_fileType == DevFileType.EnvFile)
                {
                    _envLines = EnvMaskingService.Parse(raw);
                    RenderEnvDocument();
                }
                else
                {
                    bool truncated;
                    string text = TruncateLines(raw, MaxLines, out truncated);

                    if (truncated)
                        text += string.Format(
                            "\n\n// ── Preview truncated at {0:N0} lines ──", MaxLines);

                    SetText(text);

                    ct.ThrowIfCancellationRequested();

                    await ScanAndRenderSwatchesAsync(text, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                SetText("Could not load file:\n\n" + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private async Task ScanAndRenderSwatchesAsync(string text, CancellationToken ct)
        {
            var swatches = new List<SwatchInfo>();
            var lines    = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            await Task.Run(() =>
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();

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
                    }
                }
            }, ct);

            ct.ThrowIfCancellationRequested();

            if (swatches.Count == 0) return;

            // Swatches temporarily disabled — renderer re-added after install verified

            _swatchLabel.Text       = string.Format("  {0} colour{1}  ",
                swatches.Count, swatches.Count == 1 ? "" : "s");
            _swatchBadge.Visibility = Visibility.Visible;
        }

        private void RenderEnvDocument()
        {
            if (_envLines == null) return;

            var sb = new StringBuilder(_envLines.Count * 40);

            for (int i = 0; i < _envLines.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(_envLines[i].DisplayText(_revealed));
            }

            SetText(sb.ToString());
        }

        private void Toggle()
        {
            if (_toggleAnimating) return;
            _revealed = !_revealed;
            AnimateToggle(_revealed);
            _toggleLabel.Text = _revealed ? "Hide secrets" : "Show secrets";
            RenderEnvDocument();
        }

        private void AnimateToggle(bool nowOn)
        {
            _toggleAnimating = true;

            var trackBrush = new SolidColorBrush(nowOn ? TrackColorOff : TrackColorOn);
            _toggleTrack.Background = trackBrush;

            trackBrush.BeginAnimation(
                SolidColorBrush.ColorProperty,
                new ColorAnimation
                {
                    To             = nowOn ? TrackColorOn : TrackColorOff,
                    Duration       = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                });

            double fromX = nowOn ? ThumbOff : ThumbOn;
            double toX   = nowOn ? ThumbOn  : ThumbOff;

            _toggleThumb.Margin = new Thickness(fromX, 0, 0, 0);

            var thumbAnim = new ThicknessAnimation
            {
                From           = new Thickness(fromX, 0, 0, 0),
                To             = new Thickness(toX,   0, 0, 0),
                Duration       = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            thumbAnim.Completed += (_, __) =>
            {
                _toggleThumb.Margin = new Thickness(toX, 0, 0, 0);
                _toggleAnimating    = false;
            };

            _toggleThumb.BeginAnimation(MarginProperty, thumbAnim);
        }

        private static string ReadFile(string path)
        {
            using (var reader = new StreamReader(
                path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                return reader.ReadToEnd();
            }
        }

        private static string TruncateLines(string text, int maxLines, out bool truncated)
        {
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            if (lines.Length <= maxLines)
            {
                truncated = false;
                return text;
            }

            truncated = true;
            return string.Join("\n", lines, 0, maxLines);
        }

        private void SetText(string text)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetText(text));
                return;
            }

            _editor.IsReadOnly = false;
            _editor.Text       = text ?? string.Empty;
            _editor.IsReadOnly = true;
        }

        private static bool DetectDarkTheme()
        {
            try
            {
                const string regKey =
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
                using (var key = Registry.CurrentUser.OpenSubKey(regKey))
                {
                    if (key == null) return false;
                    var val = key.GetValue("AppsUseLightTheme");
                    if (val is int i) return i == 0;
                }
            }
            catch { }
            return false;
        }
    }

    // ── PlainTextHighlighting ─────────────────────────────────────────────
    // Minimal IHighlightingDefinition — no custom properties, no DefaultColor.
    // Text colour is set directly on the TextEditor.Foreground DP instead.

    internal sealed class PlainTextHighlighting : IHighlightingDefinition
    {
        public string Name => "PlainText";

        public HighlightingRuleSet MainRuleSet => new HighlightingRuleSet();

        public HighlightingRuleSet GetNamedRuleSet(string name) => null;

        public HighlightingColor GetNamedColor(string name) => null;

        public IEnumerable<HighlightingColor> NamedHighlightingColors
            => new List<HighlightingColor>();

        public IDictionary<string, string> Properties
            => new Dictionary<string, string>();
    }
}