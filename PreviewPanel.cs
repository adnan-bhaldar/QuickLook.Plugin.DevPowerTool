// ============================================================
// QuickLook.Plugin.DevPowerTool — PreviewPanel.cs
// Fully code-behind WPF UserControl — no XAML file needed.
// This avoids all x:Name / InitializeComponent resolution
// issues that occur with SDK-style net462 + UseWPF projects
// in CI environments.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace QuickLook.Plugin.DevPowerTool
{
    public class PreviewPanel : UserControl
    {
        // ── Constants ─────────────────────────────────────────────────────
        private const long MaxFileSizeBytes = 2 * 1024 * 1024; // 2 MB
        private const int  MaxRenderLines   = 5_000;

        // ── State ─────────────────────────────────────────────────────────
        private readonly string      _path;
        private readonly DevFileType _fileType;

        private List<EnvLine> _envLines;
        private bool          _secretsRevealed = false;
        private int           _totalColorCount = 0;

        // ── UI controls (built in BuildUI, used throughout) ───────────────
        private Border                    _fileTypeBadge;
        private TextBlock                 _fileTypeLabel;
        private Border                    _swatchCountBadge;
        private TextBlock                 _swatchCountLabel;
        private Button                    _eyeToggleButton;
        private TextBlock                 _eyeIcon;
        private TextBlock                 _eyeLabel;
        private FlowDocumentScrollViewer  _docViewer;

        // ── Constructor ───────────────────────────────────────────────────

        public PreviewPanel(string path, DevFileType fileType)
        {
            _path     = path;
            _fileType = fileType;

            BuildUI();
            Loaded += async (s, e) =>
            {
                ConfigureToolbar();
                await LoadAndRenderAsync();
            };
        }

        // ── UI Construction ───────────────────────────────────────────────

        private void BuildUI()
        {
            // Root grid: row 0 = toolbar (40px), row 1 = content
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // ── Toolbar ───────────────────────────────────────────────────
            var toolbarBorder = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2A)),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            Grid.SetRow(toolbarBorder, 0);

            var toolbarGrid = new Grid();
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbarBorder.Child = toolbarGrid;

            // Left: badge stack
            var leftStack = new StackPanel
            {
                Orientation         = Orientation.Horizontal,
                VerticalAlignment   = VerticalAlignment.Center,
                Margin              = new Thickness(12, 0, 0, 0)
            };
            Grid.SetColumn(leftStack, 0);

            _fileTypeLabel = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
                FontSize   = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA))
            };
            _fileTypeBadge = new Border
            {
                CornerRadius      = new CornerRadius(10),
                Padding           = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Background        = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x5C)),
                Child             = _fileTypeLabel
            };
            leftStack.Children.Add(_fileTypeBadge);

            // Swatch count badge (hidden by default)
            _swatchCountLabel = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
                FontSize   = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1))
            };
            var swatchIcon = new TextBlock
            {
                Text              = "🎨 ",
                FontSize          = 10,
                VerticalAlignment = VerticalAlignment.Center
            };
            var swatchInner = new StackPanel { Orientation = Orientation.Horizontal };
            swatchInner.Children.Add(swatchIcon);
            swatchInner.Children.Add(_swatchCountLabel);

            _swatchCountBadge = new Border
            {
                CornerRadius      = new CornerRadius(10),
                Padding           = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(6, 0, 0, 0),
                Background        = new SolidColorBrush(Color.FromRgb(0x2A, 0x3A, 0x2A)),
                Child             = swatchInner,
                Visibility        = Visibility.Collapsed
            };
            leftStack.Children.Add(_swatchCountBadge);
            toolbarGrid.Children.Add(leftStack);

            // Right: eye toggle button
            _eyeIcon = new TextBlock
            {
                Text              = "👁",
                FontSize          = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 5, 0)
            };
            _eyeLabel = new TextBlock
            {
                Text              = "Reveal",
                FontFamily        = new FontFamily("Segoe UI, sans-serif"),
                FontSize          = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground        = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xCC))
            };
            var eyeInner = new StackPanel { Orientation = Orientation.Horizontal };
            eyeInner.Children.Add(_eyeIcon);
            eyeInner.Children.Add(_eyeLabel);

            _eyeToggleButton = new Button
            {
                Content           = eyeInner,
                Background        = Brushes.Transparent,
                BorderBrush       = Brushes.Transparent,
                Foreground        = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xCC)),
                Padding           = new Thickness(8, 4, 8, 4),
                Cursor            = System.Windows.Input.Cursors.Hand,
                Visibility        = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 10, 0)
            };
            _eyeToggleButton.Click += EyeToggleButton_Click;

            var rightStack = new StackPanel
            {
                Orientation       = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            rightStack.Children.Add(_eyeToggleButton);
            Grid.SetColumn(rightStack, 1);
            toolbarGrid.Children.Add(rightStack);

            root.Children.Add(toolbarBorder);

            // ── FlowDocumentScrollViewer ───────────────────────────────────
            _docViewer = new FlowDocumentScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                IsSelectionEnabled            = true,
                IsToolBarVisible              = false,
                Background                    = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E))
            };
            Grid.SetRow(_docViewer, 1);
            root.Children.Add(_docViewer);

            Content = root;
        }

        // ── Toolbar setup ─────────────────────────────────────────────────

        private void ConfigureToolbar()
        {
            switch (_fileType)
            {
                case DevFileType.Stylesheet:
                    _fileTypeLabel.Text       = Path.GetExtension(_path).TrimStart('.').ToUpper();
                    _fileTypeBadge.Background = new SolidColorBrush(Color.FromRgb(0x21, 0x4B, 0x7A));
                    _fileTypeLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA));
                    break;

                case DevFileType.TailwindConfig:
                    _fileTypeLabel.Text       = "TAILWIND";
                    _fileTypeBadge.Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x38, 0x30));
                    _fileTypeLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
                    break;

                case DevFileType.ThemeConfig:
                    _fileTypeLabel.Text       = Path.GetExtension(_path).TrimStart('.').ToUpper();
                    _fileTypeBadge.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x2A, 0x10));
                    _fileTypeLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xC5, 0x18));
                    break;

                case DevFileType.EnvFile:
                    _fileTypeLabel.Text            = ".ENV";
                    _fileTypeBadge.Background      = new SolidColorBrush(Color.FromRgb(0x3A, 0x10, 0x10));
                    _fileTypeLabel.Foreground      = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8));
                    _eyeToggleButton.Visibility    = Visibility.Visible;
                    UpdateEyeButton();
                    break;

                default:
                    _fileTypeLabel.Text       = "TEXT";
                    _fileTypeBadge.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x3A));
                    _fileTypeLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xDD));
                    break;
            }
        }

        private void UpdateEyeButton()
        {
            if (_secretsRevealed)
            {
                _eyeIcon.Text       = "🙈";
                _eyeLabel.Text      = "Hide";
                _eyeLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8));
            }
            else
            {
                _eyeIcon.Text       = "👁";
                _eyeLabel.Text      = "Reveal";
                _eyeLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xCC));
            }
        }

        // ── Event handlers ────────────────────────────────────────────────

        private void EyeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _secretsRevealed = !_secretsRevealed;
            UpdateEyeButton();
            RenderEnvDocument();
        }

        // ── Async load + render ───────────────────────────────────────────

        private async Task LoadAndRenderAsync()
        {
            try
            {
                var info = new FileInfo(_path);
                if (info.Length > MaxFileSizeBytes)
                {
                    ShowMessage($"File too large to preview ({info.Length / 1024:N0} KB). Limit is {MaxFileSizeBytes / 1024:N0} KB.");
                    return;
                }

                string text = await Task.Run(() => ReadFile(_path));

                if (_fileType == DevFileType.EnvFile)
                {
                    _envLines = EnvMaskingService.Parse(text);
                    RenderEnvDocument();
                }
                else
                {
                    await Task.Run(() =>
                    {
                        var rawLines = SplitLines(text, MaxRenderLines, out bool truncated);
                        int total    = 0;
                        foreach (var l in rawLines)
                            total += ColorParser.ParseLine(l).Count;

                        Dispatcher.Invoke(() =>
                        {
                            _totalColorCount = total;
                            RenderColorDocument(rawLines, truncated);
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Could not load file:\n{ex.Message}");
            }
        }

        // ── Rendering ─────────────────────────────────────────────────────

        private void RenderColorDocument(IReadOnlyList<string> lines, bool truncated)
        {
            bool isDark = DetectDarkTheme();
            var  doc    = PreviewRenderer.RenderWithSwatches(lines, isDark);

            if (truncated)
            {
                doc.Blocks.Add(new Paragraph(new Run($"[Preview truncated at {MaxRenderLines} lines]")
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xC5, 0x18)),
                    FontStyle  = FontStyles.Italic
                }));
            }

            _docViewer.Document = doc;

            if (_totalColorCount > 0)
            {
                _swatchCountBadge.Visibility = Visibility.Visible;
                _swatchCountLabel.Text       = $"{_totalColorCount} colour{(_totalColorCount == 1 ? "" : "s")}";
            }
        }

        private void RenderEnvDocument()
        {
            if (_envLines == null) return;
            bool isDark = DetectDarkTheme();
            _docViewer.Document = BuildEnvDocument(_envLines, _secretsRevealed, isDark);
        }

        private static FlowDocument BuildEnvDocument(IReadOnlyList<EnvLine> lines, bool revealed, bool isDark)
        {
            var doc = new FlowDocument
            {
                FontFamily            = new FontFamily("Cascadia Code, Consolas, Courier New, monospace"),
                FontSize              = 13,
                LineHeight            = 20,
                PagePadding           = new Thickness(16, 12, 16, 12),
                Background            = new SolidColorBrush(isDark ? Color.FromRgb(0x1E, 0x1E, 0x2E) : Color.FromRgb(0xFA, 0xFA, 0xFB)),
                Foreground            = new SolidColorBrush(isDark ? Color.FromRgb(0xCD, 0xD6, 0xF4) : Color.FromRgb(0x1E, 0x1E, 0x2E)),
                IsColumnWidthFlexible = false,
                ColumnWidth           = double.MaxValue
            };

            foreach (var line in lines)
            {
                var para = new Paragraph { Margin = new Thickness(0), LineHeight = 20 };

                if (!line.IsAssignment)
                {
                    var commentColor = isDark
                        ? Color.FromRgb(0x6C, 0x70, 0x86)
                        : Color.FromRgb(0x90, 0x94, 0xA5);
                    para.Inlines.Add(new Run(line.Raw) { Foreground = new SolidColorBrush(commentColor) });
                }
                else
                {
                    var keyColor = isDark
                        ? Color.FromRgb(0x89, 0xB4, 0xFA)
                        : Color.FromRgb(0x19, 0x5F, 0xBB);

                    var valueColor = revealed
                        ? (isDark ? Color.FromRgb(0xA6, 0xE3, 0xA1) : Color.FromRgb(0x2E, 0x7D, 0x32))
                        : (isDark ? Color.FromRgb(0x58, 0x5B, 0x70) : Color.FromRgb(0xAA, 0xAA, 0xBB));

                    var eqColor = isDark
                        ? Color.FromRgb(0xCD, 0xD6, 0xF4)
                        : Color.FromRgb(0x50, 0x50, 0x60);

                    string mask = revealed
                        ? line.Value
                        : new string('*', Math.Max(8, line.Value?.Length ?? 8));

                    para.Inlines.Add(new Run(line.Key)  { Foreground = new SolidColorBrush(keyColor),   FontWeight = FontWeights.SemiBold });
                    para.Inlines.Add(new Run("=")        { Foreground = new SolidColorBrush(eqColor) });
                    para.Inlines.Add(new Run(mask)       { Foreground = new SolidColorBrush(valueColor) });
                }

                doc.Blocks.Add(para);
            }

            return doc;
        }

        // ── Utilities ─────────────────────────────────────────────────────

        private static string ReadFile(string path)
        {
            using (var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                return reader.ReadToEnd();
        }

        private static string[] SplitLines(string text, int maxLines, out bool truncated)
        {
            var all = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            if (all.Length <= maxLines) { truncated = false; return all; }
            truncated = true;
            var result = new string[maxLines];
            Array.Copy(all, result, maxLines);
            return result;
        }

        private static bool DetectDarkTheme()
        {
            try
            {
                var bg  = SystemColors.WindowColor;
                double lum = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B;
                return lum < 128;
            }
            catch { return false; }
        }

        private void ShowMessage(string message)
        {
            var doc = new FlowDocument
            {
                FontFamily  = new FontFamily("Segoe UI, sans-serif"),
                FontSize    = 14,
                PagePadding = new Thickness(20)
            };
            doc.Blocks.Add(new Paragraph(new Run(message)
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8))
            }));
            _docViewer.Document = doc;
        }
    }
}