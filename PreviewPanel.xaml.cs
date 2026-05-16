// ============================================================
// QuickLook.Plugin.DevPowerTool — PreviewPanel.xaml.cs
// Code-behind for the main preview UserControl.
// Handles:
//   • Reading the file safely (size guard, encoding detection)
//   • Deciding render mode (colour swatches vs .env masking)
//   • Eye-toggle button state
//   • Toolbar label population
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
    public partial class PreviewPanel : UserControl
    {
        // ── Constants ─────────────────────────────────────────────────────
        private const long   MaxFileSizeBytes = 2 * 1024 * 1024; // 2 MB hard cap
        private const int    MaxRenderLines   = 5_000;            // render cap for huge files

        // ── State ─────────────────────────────────────────────────────────
        private readonly string      _path;
        private readonly DevFileType _fileType;

        // .env specific state
        private List<EnvLine> _envLines;
        private bool          _secretsRevealed = false;

        // Colour preview state
        private int _totalColorCount = 0;

        // ── Constructor ───────────────────────────────────────────────────

        public PreviewPanel(string path, DevFileType fileType)
        {
            InitializeComponent();

            _path     = path;
            _fileType = fileType;

            // Wire up loaded event so we can start async work after layout
            Loaded += PreviewPanel_Loaded;
        }

        // ── Event Handlers ────────────────────────────────────────────────

        private async void PreviewPanel_Loaded(object sender, RoutedEventArgs e)
        {
            ConfigureToolbar();
            await LoadAndRenderAsync();
        }

        /// <summary>
        /// Eye button click: toggle secret reveal state and re-render .env view.
        /// </summary>
        private void EyeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _secretsRevealed = !_secretsRevealed;
            UpdateEyeButton();
            RenderEnvDocument();
        }

        // ── Toolbar setup ─────────────────────────────────────────────────

        private void ConfigureToolbar()
        {
            // Set file-type badge label and colour
            switch (_fileType)
            {
                case DevFileType.Stylesheet:
                    FileTypeLabel.Text     = Path.GetExtension(_path).TrimStart('.').ToUpper();
                    FileTypeBadge.Background = new SolidColorBrush(Color.FromRgb(0x21, 0x4B, 0x7A));
                    FileTypeLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA));
                    break;

                case DevFileType.TailwindConfig:
                    FileTypeLabel.Text     = "TAILWIND";
                    FileTypeBadge.Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x38, 0x30));
                    FileTypeLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
                    break;

                case DevFileType.ThemeConfig:
                    FileTypeLabel.Text     = Path.GetExtension(_path).TrimStart('.').ToUpper();
                    FileTypeBadge.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x2A, 0x10));
                    FileTypeLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xC5, 0x18));
                    break;

                case DevFileType.EnvFile:
                    FileTypeLabel.Text     = ".ENV";
                    FileTypeBadge.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x10, 0x10));
                    FileTypeLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8));
                    // Show eye toggle for .env files
                    EyeToggleButton.Visibility = Visibility.Visible;
                    UpdateEyeButton();
                    break;

                default:
                    FileTypeLabel.Text     = "TEXT";
                    FileTypeBadge.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x3A));
                    FileTypeLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xDD));
                    break;
            }
        }

        private void UpdateEyeButton()
        {
            if (_secretsRevealed)
            {
                EyeIcon.Text  = "🙈";
                EyeLabel.Text = "Hide";
                EyeLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8));
            }
            else
            {
                EyeIcon.Text  = "👁";
                EyeLabel.Text = "Reveal";
                EyeLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xCC));
            }
        }

        // ── Async file load + render ──────────────────────────────────────

        private async Task LoadAndRenderAsync()
        {
            try
            {
                var info = new FileInfo(_path);
                if (info.Length > MaxFileSizeBytes)
                {
                    ShowMessage($"File too large to preview ({info.Length / 1024:N0} KB). " +
                                $"Limit is {MaxFileSizeBytes / 1024:N0} KB.");
                    return;
                }

                // Read on background thread to keep UI responsive
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
                        // Parse colours on background thread
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

        // ── Rendering methods ─────────────────────────────────────────────

        /// <summary>
        /// Renders the colour-swatch document for stylesheet / config files.
        /// </summary>
        private void RenderColorDocument(IReadOnlyList<string> lines, bool truncated)
        {
            bool isDark = DetectDarkTheme();
            var  doc    = PreviewRenderer.RenderWithSwatches(lines, isDark);

            if (truncated)
            {
                var notice = new Paragraph(new Run($"[Preview truncated at {MaxRenderLines} lines]")
                {
                    Foreground  = new SolidColorBrush(Color.FromRgb(0xF5, 0xC5, 0x18)),
                    FontStyle   = FontStyles.Italic
                });
                doc.Blocks.Add(notice);
            }

            DocViewer.Document = doc;

            // Update colour count badge
            if (_totalColorCount > 0)
            {
                SwatchCountBadge.Visibility = Visibility.Visible;
                SwatchCountLabel.Text       = $"{_totalColorCount} colour{(_totalColorCount == 1 ? "" : "s")}";
            }
        }

        /// <summary>
        /// Renders the .env masking document; called on initial load and on toggle.
        /// </summary>
        private void RenderEnvDocument()
        {
            if (_envLines == null) return;

            bool isDark = DetectDarkTheme();
            var  doc    = BuildEnvDocument(_envLines, _secretsRevealed, isDark);
            DocViewer.Document = doc;
        }

        /// <summary>
        /// Constructs the FlowDocument for an .env file, masking or revealing
        /// secret values depending on <paramref name="revealed"/>.
        /// </summary>
        private static FlowDocument BuildEnvDocument(
            IReadOnlyList<EnvLine> lines, bool revealed, bool isDark)
        {
            var bgColor = isDark
                ? Color.FromRgb(0x1E, 0x1E, 0x2E)
                : Color.FromRgb(0xFA, 0xFA, 0xFB);

            var fgColor = isDark
                ? Color.FromRgb(0xCD, 0xD6, 0xF4)
                : Color.FromRgb(0x1E, 0x1E, 0x2E);

            var doc = new FlowDocument
            {
                FontFamily            = new FontFamily("Cascadia Code, Consolas, Courier New, monospace"),
                FontSize              = 13,
                LineHeight            = 20,
                PagePadding           = new Thickness(16, 12, 16, 12),
                Background            = new SolidColorBrush(bgColor),
                Foreground            = new SolidColorBrush(fgColor),
                IsColumnWidthFlexible = false,
                ColumnWidth           = double.MaxValue
            };

            foreach (var line in lines)
            {
                var para = new Paragraph { Margin = new Thickness(0), LineHeight = 20 };

                if (!line.IsAssignment)
                {
                    // Comment or blank
                    var commentColor = isDark
                        ? Color.FromRgb(0x6C, 0x70, 0x86)
                        : Color.FromRgb(0x90, 0x94, 0xA5);
                    para.Inlines.Add(new Run(line.Raw) { Foreground = new SolidColorBrush(commentColor) });
                }
                else
                {
                    // KEY in one colour, = in neutral, VALUE in another (or masked)
                    var keyColor   = isDark
                        ? Color.FromRgb(0x89, 0xB4, 0xFA)
                        : Color.FromRgb(0x19, 0x5F, 0xBB);

                    var valueColor = revealed
                        ? (isDark
                            ? Color.FromRgb(0xA6, 0xE3, 0xA1)
                            : Color.FromRgb(0x2E, 0x7D, 0x32))
                        : (isDark
                            ? Color.FromRgb(0x58, 0x5B, 0x70)
                            : Color.FromRgb(0xAA, 0xAA, 0xBB));

                    var eqColor = isDark
                        ? Color.FromRgb(0xCD, 0xD6, 0xF4)
                        : Color.FromRgb(0x50, 0x50, 0x60);

                    string mask  = revealed
                        ? line.Value
                        : new string('*', Math.Max(8, line.Value?.Length ?? 8));

                    para.Inlines.Add(new Run(line.Key) { Foreground = new SolidColorBrush(keyColor), FontWeight = FontWeights.SemiBold });
                    para.Inlines.Add(new Run("=")      { Foreground = new SolidColorBrush(eqColor) });
                    para.Inlines.Add(new Run(mask)     { Foreground = new SolidColorBrush(valueColor) });
                }

                doc.Blocks.Add(para);
            }

            return doc;
        }

        // ── Utility ───────────────────────────────────────────────────────

        /// <summary>
        /// Reads the file with BOM / encoding detection and a UTF-8 fallback.
        /// </summary>
        private static string ReadFile(string path)
        {
            // Let StreamReader auto-detect BOM; fall back to UTF-8 without BOM
            using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Splits text into individual lines, capping at <paramref name="maxLines"/>.
        /// Sets <paramref name="truncated"/> to true when the cap is hit.
        /// </summary>
        private static string[] SplitLines(string text, int maxLines, out bool truncated)
        {
            var all = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            if (all.Length <= maxLines)
            {
                truncated = false;
                return all;
            }
            truncated = true;
            var result = new string[maxLines];
            Array.Copy(all, result, maxLines);
            return result;
        }

        /// <summary>
        /// Heuristic: check if the system uses a dark colour scheme by examining
        /// the default window background luminance.
        /// </summary>
        private static bool DetectDarkTheme()
        {
            try
            {
                var bg  = SystemColors.WindowColor;
                double lum = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B;
                return lum < 128;
            }
            catch
            {
                return false; // safe default
            }
        }

        /// <summary>
        /// Shows a plain error/info message inside the FlowDocumentScrollViewer.
        /// </summary>
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
            DocViewer.Document = doc;
        }
    }
}
