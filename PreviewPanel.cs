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
        private const long   MaxBytes = 2 * 1024 * 1024;
        private const int    MaxLines = 5000;

        private const double TrackW    = 38;
        private const double TrackH    = 22;
        private const double ThumbSize = 18;
        private const double ThumbOff  = 2;
        private const double ThumbOn   = TrackW - ThumbSize - 2;

        private static readonly Color ColOff = Color.FromRgb(0x78, 0x78, 0x80);
        private static readonly Color ColOn  = Color.FromRgb(0x34, 0xC7, 0x59);

        private readonly string      _path;
        private readonly DevFileType _type;
        private readonly bool        _dark;
        private readonly Color       _fg;
        private readonly Color       _bg;

        private TextEditor _ed;
        private Border     _badge;
        private TextBlock  _badgeText;
        private Border     _track;
        private Ellipse    _thumb;
        private TextBlock  _trackLabel;
        private bool       _animating;
        private bool       _revealed;
        private List<EnvLine> _env;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public PreviewPanel(string path, DevFileType type)
        {
            _path = path;
            _type = type;
            _dark = IsDark();
            _bg   = _dark ? Color.FromRgb(0x1E, 0x1E, 0x1E) : Colors.White;
            _fg   = _dark ? Color.FromRgb(0xD4, 0xD4, 0xD4) : Color.FromRgb(0x1E, 0x1E, 0x1E);

            Build();
            Loaded   += (s, e) => { var _ = LoadAsync(_cts.Token); };
            Unloaded += (s, e) => { _cts.Cancel(); _cts.Dispose(); };
        }

        private void BuildUI()
        {
            Background = new SolidColorBrush(_bg);
            _ed = MakeEditor();

            var root = new Grid();
            root.Children.Add(_ed);
            root.Children.Add(_type == DevFileType.EnvFile ? MakeToggle() : MakeBadge());
            Content = root;
        }

        private TextEditor MakeEditor()
        {
            var ed = new TextEditor
            {
                IsReadOnly      = true,
                ShowLineNumbers = true,
                FontFamily      = new FontFamily("Cascadia Code, Consolas, Courier New, monospace"),
                FontSize        = 13.5,
                WordWrap        = false,
                Foreground      = new SolidColorBrush(_fg),
                Background      = new SolidColorBrush(_bg),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                LineNumbersForeground         = new SolidColorBrush(_dark
                    ? Color.FromRgb(0x75, 0x75, 0x75)
                    : Color.FromRgb(0xA0, 0xA0, 0xA0)),
                Options = new TextEditorOptions
                {
                    EnableHyperlinks            = false,
                    EnableEmailHyperlinks       = false,
                    ShowBoxForControlCharacters = true,
                    ConvertTabsToSpaces         = false,
                    IndentationSize             = 4,
                    AllowScrollBelowDocument    = false
                }
            };
            ed.SyntaxHighlighting = new PlainTextHighlighting();
            return ed;
        }

        private UIElement MakeBadge()
        {
            _badgeText = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize   = 11,
                Foreground = new SolidColorBrush(_dark
                    ? Color.FromRgb(0xA6, 0xE3, 0xA1)
                    : Color.FromRgb(0x2E, 0x7D, 0x32))
            };
            _badge = new Border
            {
                Child               = _badgeText,
                Background          = new SolidColorBrush(_dark
                    ? Color.FromArgb(210, 20, 45, 20)
                    : Color.FromArgb(210, 220, 245, 220)),
                BorderBrush         = new SolidColorBrush(_dark
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
            return _badge;
        }

        private UIElement MakeToggle()
        {
            _track = new Border
            {
                Width        = TrackW,
                Height       = TrackH,
                CornerRadius = new CornerRadius(TrackH / 2),
                Background   = new SolidColorBrush(ColOff),
                Cursor       = Cursors.Hand
            };
            _thumb = new Ellipse
            {
                Width               = ThumbSize,
                Height              = ThumbSize,
                Fill                = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment   = VerticalAlignment.Center,
                Margin              = new Thickness(ThumbOff, 0, 0, 0),
                Effect              = new DropShadowEffect
                    { Color = Colors.Black, Opacity = 0.3, BlurRadius = 4, ShadowDepth = 1 }
            };
            _trackLabel = new TextBlock
            {
                Text                = "Show secrets",
                FontFamily          = new FontFamily("Segoe UI"),
                FontSize            = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground          = new SolidColorBrush(_dark
                    ? Color.FromRgb(0x99, 0x99, 0x99)
                    : Color.FromRgb(0x66, 0x66, 0x66)),
                Margin              = new Thickness(0, 4, 0, 0)
            };

            var tg = new Grid { Width = TrackW, Height = TrackH };
            tg.Children.Add(_track);
            tg.Children.Add(_thumb);

            var sp = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(tg);
            sp.Children.Add(_trackLabel);

            _track.MouseLeftButtonUp += (_, __) => Toggle();
            sp.MouseLeftButtonUp     += (_, __) => Toggle();

            return new Border
            {
                Child               = sp,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment   = VerticalAlignment.Top,
                Margin              = new Thickness(0, 10, 16, 0),
                Background          = new SolidColorBrush(_dark
                    ? Color.FromArgb(180, 30, 30, 30)
                    : Color.FromArgb(180, 240, 240, 240)),
                CornerRadius        = new CornerRadius(8),
                Padding             = new Thickness(8, 6, 8, 6)
            };
        }

        private async Task LoadAsync(CancellationToken ct)
        {
            try
            {
                if (new FileInfo(_path).Length > MaxBytes)
                {
                    SetText("[ File too large to preview ]");
                    return;
                }

                ct.ThrowIfCancellationRequested();
                string raw = await Task.Run(() => File.ReadAllText(_path, Encoding.UTF8), ct);
                ct.ThrowIfCancellationRequested();

                if (_type == DevFileType.EnvFile)
                {
                    _env = EnvMaskingService.Parse(raw);
                    RenderEnv();
                }
                else
                {
                    bool truncated;
                    string text = Truncate(raw, MaxLines, out truncated);
                    if (truncated) text += "\n\n// ── Preview truncated ──";
                    SetText(text);
                    ct.ThrowIfCancellationRequested();
                    await ScanSwatchesAsync(text, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { SetText("Error: " + ex.Message); }
        }

        private async Task ScanSwatchesAsync(string text, CancellationToken ct)
        {
            var swatches = new List<SwatchInfo>();
            var lines    = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            await Task.Run(() =>
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    foreach (var t in ColorParser.ParseLine(lines[i]))
                    {
                        if (t.Color.HasValue)
                            swatches.Add(new SwatchInfo
                            {
                                Line = i + 1, CharOffset = t.Index,
                                TokenLength = t.Raw.Length, Color = t.Color.Value
                            });
                    }
                }
            }, ct);

            ct.ThrowIfCancellationRequested();
            if (swatches.Count == 0) return;

            var r = new ColorSwatchRenderer(_ed, swatches);
            _ed.TextArea.TextView.BackgroundRenderers.Add(r);
            _ed.TextArea.TextView.InvalidateLayer(KnownLayer.Background);

            _badgeText.Text    = string.Format("  {0} colour{1}  ", swatches.Count, swatches.Count == 1 ? "" : "s");
            _badge.Visibility  = Visibility.Visible;
        }

        private void RenderEnv()
        {
            if (_env == null) return;
            var sb = new StringBuilder();
            for (int i = 0; i < _env.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(_env[i].DisplayText(_revealed));
            }
            SetText(sb.ToString());
        }

        private void Toggle()
        {
            if (_animating) return;
            _revealed = !_revealed;
            Animate(_revealed);
            _trackLabel.Text = _revealed ? "Hide secrets" : "Show secrets";
            RenderEnv();
        }

        private void Animate(bool on)
        {
            _animating = true;

            var tb = new SolidColorBrush(on ? ColOff : ColOn);
            _track.Background = tb;
            tb.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation
                {
                    To = on ? ColOn : ColOff,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                });

            double fx = on ? ThumbOff : ThumbOn;
            double tx = on ? ThumbOn  : ThumbOff;
            _thumb.Margin = new Thickness(fx, 0, 0, 0);

            var ta = new ThicknessAnimation
            {
                From = new Thickness(fx, 0, 0, 0),
                To   = new Thickness(tx, 0, 0, 0),
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            ta.Completed += (_, __) => { _thumb.Margin = new Thickness(tx, 0, 0, 0); _animating = false; };
            _thumb.BeginAnimation(MarginProperty, ta);
        }

        private void SetText(string text)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => SetText(text)); return; }
            _ed.IsReadOnly = false;
            _ed.Text       = text ?? string.Empty;
            _ed.IsReadOnly = true;
        }

        private static string Truncate(string text, int max, out bool truncated)
        {
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            if (lines.Length <= max) { truncated = false; return text; }
            truncated = true;
            return string.Join("\n", lines, 0, max);
        }

        private static bool IsDark()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (k?.GetValue("AppsUseLightTheme") is int v) return v == 0;
                }
            }
            catch { }
            return false;
        }
    }

    // Minimal IHighlightingDefinition — no custom base classes, no DefaultColor.
    // Text colour set via TextEditor.Foreground instead.
    internal sealed class PlainTextHighlighting : IHighlightingDefinition
    {
        public string Name => "PlainText";
        public HighlightingRuleSet MainRuleSet => new HighlightingRuleSet();
        public HighlightingRuleSet GetNamedRuleSet(string name) => null;
        public HighlightingColor GetNamedColor(string name)     => null;
        public IEnumerable<HighlightingColor> NamedHighlightingColors => new List<HighlightingColor>();
        public IDictionary<string, string> Properties => new Dictionary<string, string>();
    }
}