// ============================================================
// QuickLook.Plugin.DevPowerTool — PreviewPanel.cs
//
// Uses AvalonEdit (ICSharpCode.AvalonEdit) exactly as QuickLook's
// built-in TextViewer does. No C# tuples — net462 compatible.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using QuickLook.Common.Helpers;

namespace QuickLook.Plugin.DevPowerTool
{
    // ── Simple struct to avoid ValueTuple (not available in net462) ────────
    internal struct BadgeColors
    {
        public Color Bg;
        public Color Fg;
        public BadgeColors(Color bg, Color fg) { Bg = bg; Fg = fg; }
    }

    public class PreviewPanel : UserControl
    {
        // ── Constants ─────────────────────────────────────────────────────
        private const long MaxBytes = 2 * 1024 * 1024;
        private const int  MaxLines = 5_000;

        // ── State ─────────────────────────────────────────────────────────
        private readonly string      _path;
        private readonly DevFileType _fileType;
        private readonly bool        _isDark;

        private List<EnvLine> _envLines;
        private bool          _revealed = false;

        // ── Controls ──────────────────────────────────────────────────────
        private TextEditor   _editor;
        private TextBlock    _swatchCount;
        private Border       _swatchBadge;
        private ToggleButton _envToggle;

        // ── Constructor ───────────────────────────────────────────────────

        public PreviewPanel(string path, DevFileType fileType)
        {
            _path     = path;
            _fileType = fileType;
            _isDark   = OSThemeHelper.AppsUseDarkTheme();

            Build();
            Loaded += async (s, e) => await LoadAsync();
        }

        // ══════════════════════════════════════════════════════════════════
        // UI CONSTRUCTION
        // ══════════════════════════════════════════════════════════════════

        private void Build()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var toolbar = BuildToolbar();
            Grid.SetRow(toolbar, 0);
            root.Children.Add(toolbar);

            _editor = BuildEditor();
            Grid.SetRow(_editor, 1);
            root.Children.Add(_editor);

            Content = root;
        }

        // ── Toolbar ───────────────────────────────────────────────────────

        private Border BuildToolbar()
        {
            var bg     = _isDark ? Color.FromRgb(0x2D, 0x2D, 0x2D) : Color.FromRgb(0xF0, 0xF0, 0xF0);
            var border = _isDark ? Color.FromRgb(0x45, 0x45, 0x45) : Color.FromRgb(0xCC, 0xCC, 0xCC);

            var toolbar = new Border
            {
                Background      = new SolidColorBrush(bg),
                BorderBrush     = new SolidColorBrush(border),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding         = new Thickness(8, 0, 8, 0)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.Child = grid;

            // ── Left: file-type badge + swatch count ──────────────────────
            var left = new StackPanel
            {
                Orientation       = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            left.Children.Add(new Border { Width = 0 }); // spacer

            var colors = GetBadgeColors();
            var badge  = MakeBadge(GetBadgeText(), colors);
            left.Children.Add(badge);

            _swatchCount = new TextBlock
            {
                FontFamily        = new FontFamily("Segoe UI, sans-serif"),
                FontSize          = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(6, 0, 0, 0),
                Foreground        = new SolidColorBrush(_isDark
                    ? Color.FromRgb(0xA6, 0xE3, 0xA1)
                    : Color.FromRgb(0x2E, 0x7D, 0x32))
            };
            _swatchBadge = new Border
            {
                Padding           = new Thickness(6, 2, 6, 2),
                CornerRadius      = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(6, 0, 0, 0),
                Background        = new SolidColorBrush(_isDark
                    ? Color.FromRgb(0x1E, 0x35, 0x1E)
                    : Color.FromRgb(0xE8, 0xF5, 0xE9)),
                Child             = _swatchCount,
                Visibility        = Visibility.Collapsed
            };
            left.Children.Add(_swatchBadge);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            // ── Right: env toggle (only for .env files) ───────────────────
            if (_fileType == DevFileType.EnvFile)
            {
                _envToggle = BuildToggle();
                _envToggle.Checked   += (s, e) => { _revealed = true;  RenderEnv(); };
                _envToggle.Unchecked += (s, e) => { _revealed = false; RenderEnv(); };

                var right = new StackPanel
                {
                    Orientation       = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };
                right.Children.Add(_envToggle);
                Grid.SetColumn(right, 1);
                grid.Children.Add(right);
            }

            return toolbar;
        }

        /// <summary>
        /// Clean native ToggleButton — no emoji, simple text label that
        /// switches between "Show secrets" and "Hide secrets".
        /// </summary>
        private ToggleButton BuildToggle()
        {
            var label = new TextBlock
            {
                Text              = "Show secrets",
                FontFamily        = new FontFamily("Segoe UI, sans-serif"),
                FontSize          = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground        = new SolidColorBrush(_isDark
                    ? Color.FromRgb(0xCC, 0xCC, 0xCC)
                    : Color.FromRgb(0x22, 0x22, 0x22))
            };

            var toggle = new ToggleButton
            {
                Content           = label,
                IsChecked         = false,
                VerticalAlignment = VerticalAlignment.Center,
                Padding           = new Thickness(10, 3, 10, 3),
                FontSize          = 11,
                Cursor            = System.Windows.Input.Cursors.Hand,
                Background        = new SolidColorBrush(_isDark
                    ? Color.FromRgb(0x3A, 0x3A, 0x3A)
                    : Color.FromRgb(0xE2, 0xE2, 0xE2)),
                BorderBrush       = new SolidColorBrush(_isDark
                    ? Color.FromRgb(0x60, 0x60, 0x60)
                    : Color.FromRgb(0xAA, 0xAA, 0xAA)),
                BorderThickness   = new Thickness(1)
            };

            toggle.Checked   += (s, e) => label.Text = "Hide secrets";
            toggle.Unchecked += (s, e) => label.Text = "Show secrets";

            return toggle;
        }

        private Border MakeBadge(string text, BadgeColors colors)
        {
            return new Border
            {
                Background        = new SolidColorBrush(colors.Bg),
                CornerRadius      = new CornerRadius(4),
                Padding           = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Child             = new TextBlock
                {
                    Text       = text,
                    FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
                    FontSize   = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(colors.Fg)
                }
            };
        }

        // ── AvalonEdit — identical settings to TextViewer ─────────────────

        private TextEditor BuildEditor()
        {
            var ed = new TextEditor
            {
                IsReadOnly                    = true,
                ShowLineNumbers               = true,
                FontFamily                    = new FontFamily("Cascadia Code, Consolas, Courier New, monospace"),
                FontSize                      = 13,
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

            // Exact same colours TextViewer uses
            if (_isDark)
            {
                ed.Background            = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
                ed.Foreground            = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));
                ed.LineNumbersForeground = new SolidColorBrush(Color.FromRgb(0x85, 0x85, 0x85));
            }
            else
            {
                ed.Background            = new SolidColorBrush(Colors.White);
                ed.Foreground            = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
                ed.LineNumbersForeground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0));
            }

            return ed;
        }

        // ══════════════════════════════════════════════════════════════════
        // FILE LOADING
        // ══════════════════════════════════════════════════════════════════

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
                    ApplySyntaxHighlighting();

                    bool truncated;
                    string text = Truncate(raw, MaxLines, out truncated);
                    if (truncated)
                        text += string.Format("\n\n// [Preview truncated at {0} lines]", MaxLines);

                    _editor.Text = text;

                    await ScanAndDrawSwatchesAsync(text);
                }
            }
            catch (Exception ex)
            {
                SetText("Could not load file:\n" + ex.Message);
            }
        }

        // ── Syntax highlighting ───────────────────────────────────────────

        private void ApplySyntaxHighlighting()
        {
            string name = null;
            var ext = Path.GetExtension(_path).ToLowerInvariant();

            switch (_fileType)
            {
                case DevFileType.Stylesheet:
                    name = "CSS";
                    break;
                case DevFileType.TailwindConfig:
                    name = ext == ".ts" ? "TypeScript" : "JavaScript";
                    break;
                case DevFileType.ThemeConfig:
                    if (ext == ".json")    name = "Json";
                    else if (ext == ".ts") name = "TypeScript";
                    else                   name = "JavaScript";
                    break;
            }

            if (name == null) return;

            try
            {
                var def = HighlightingManager.Instance.GetDefinition(name);
                if (def != null) _editor.SyntaxHighlighting = def;
            }
            catch { /* best effort */ }
        }

        // ══════════════════════════════════════════════════════════════════
        // COLOUR SWATCH RENDERING
        // ══════════════════════════════════════════════════════════════════

        private async Task ScanAndDrawSwatchesAsync(string text)
        {
            var swatches = new List<SwatchInfo>();
            var lines    = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int total    = 0;

            bool scanTailwind = _fileType == DevFileType.TailwindConfig
                             || _fileType == DevFileType.ThemeConfig
                             || _fileType == DevFileType.Stylesheet;

            await Task.Run(() =>
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];

                    // CSS colour values
                    foreach (var token in ColorParser.ParseLine(line))
                    {
                        if (!token.Color.HasValue) continue;
                        swatches.Add(new SwatchInfo
                        {
                            Line       = i + 1,
                            CharOffset = token.Index,
                            Color      = token.Color.Value
                        });
                        total++;
                    }

                    // Tailwind utility class names
                    if (scanTailwind)
                    {
                        foreach (var match in TailwindColorMap.FindColors(line))
                        {
                            swatches.Add(new SwatchInfo
                            {
                                Line       = i + 1,
                                CharOffset = match.Index,
                                Color      = match.Color
                            });
                            total++;
                        }
                    }
                }
            });

            if (total == 0) return;

            var renderer = new ColorSwatchRenderer(_editor, swatches);
            _editor.TextArea.TextView.BackgroundRenderers.Add(renderer);
            _editor.TextArea.TextView.InvalidateLayer(ICSharpCode.AvalonEdit.Rendering.KnownLayer.Text);

            _swatchCount.Text       = string.Format("  {0} colour{1}", total, total == 1 ? "" : "s");
            _swatchBadge.Visibility = Visibility.Visible;
        }

        // ══════════════════════════════════════════════════════════════════
        // .ENV RENDERING
        // ══════════════════════════════════════════════════════════════════

        private void RenderEnv()
        {
            if (_envLines == null) return;

            var sb = new StringBuilder();
            foreach (var line in _envLines)
                sb.AppendLine(line.DisplayText(_revealed));

            _editor.Text = sb.ToString();
        }

        // ══════════════════════════════════════════════════════════════════
        // UTILITIES
        // ══════════════════════════════════════════════════════════════════

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
            _editor.IsReadOnly = false;
            _editor.Text       = msg;
            _editor.IsReadOnly = true;
        }

        // ── Badge helpers ─────────────────────────────────────────────────

        private string GetBadgeText()
        {
            switch (_fileType)
            {
                case DevFileType.TailwindConfig: return "TAILWIND";
                case DevFileType.EnvFile:        return ".ENV";
                default:
                    return Path.GetExtension(_path).TrimStart('.').ToUpperInvariant();
            }
        }

        private BadgeColors GetBadgeColors()
        {
            switch (_fileType)
            {
                case DevFileType.Stylesheet:
                    return _isDark
                        ? new BadgeColors(Color.FromRgb(0x1A, 0x3A, 0x5C), Color.FromRgb(0x89, 0xB4, 0xFA))
                        : new BadgeColors(Color.FromRgb(0xBB, 0xDE, 0xFF), Color.FromRgb(0x19, 0x5F, 0xBB));
                case DevFileType.TailwindConfig:
                    return _isDark
                        ? new BadgeColors(Color.FromRgb(0x08, 0x28, 0x22), Color.FromRgb(0x38, 0xBD, 0xF8))
                        : new BadgeColors(Color.FromRgb(0xB2, 0xEB, 0xF2), Color.FromRgb(0x00, 0x6A, 0x7A));
                case DevFileType.EnvFile:
                    return _isDark
                        ? new BadgeColors(Color.FromRgb(0x3A, 0x10, 0x10), Color.FromRgb(0xF3, 0x8B, 0xA8))
                        : new BadgeColors(Color.FromRgb(0xFF, 0xCC, 0xCC), Color.FromRgb(0xAA, 0x00, 0x00));
                default:
                    return _isDark
                        ? new BadgeColors(Color.FromRgb(0x2A, 0x2A, 0x3A), Color.FromRgb(0xCC, 0xCC, 0xDD))
                        : new BadgeColors(Color.FromRgb(0xE4, 0xE4, 0xF0), Color.FromRgb(0x33, 0x33, 0x55));
            }
        }
    }
}