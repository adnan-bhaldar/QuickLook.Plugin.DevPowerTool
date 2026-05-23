// ============================================================
// QuickLook.Plugin.DevPowerTool — Plugin.cs
// Entry point. Implements IViewer; discovered by QuickLook
// at runtime via reflection scanning the plugin DLL.
// ============================================================
using System;
using System.IO;
using System.Windows;
using QuickLook.Common.Plugin;

namespace QuickLook.Plugin.DevPowerTool
{
    /// <summary>
    /// Main plugin class. QuickLook discovers this via the IViewer interface.
    /// Priority 0 = standard level; the file-type checks in CanHandle ensure
    /// we only claim files we know how to render.
    /// </summary>
    public class Plugin : IViewer
    {
        // ── Supported file extensions / names ────────────────────────────

        // Direct extension matches (colour-swatch mode)
        private static readonly string[] ColorExtensions =
        {
            ".css", ".scss", ".sass"
        };

        // Generic theme / config files matched by extension (colour-swatch mode)
        private static readonly string[] ThemeExtensions =
        {
            ".json", ".js", ".ts"
        };

        // Tailwind config matched by exact file name (colour-swatch mode)
        private static readonly string[] TailwindNames =
        {
            "tailwind.config.js",
            "tailwind.config.ts",
            "tailwind.config.cjs",
            "tailwind.config.mjs"
        };

        // .env variants matched by exact file name (privacy-mask mode)
        private static readonly string[] EnvNames =
        {
            ".env",
            ".env.local",
            ".env.production",
            ".env.development",
            ".env.staging",
            ".env.test"
        };

        // ── IViewer ───────────────────────────────────────────────────────

        /// <summary>Priority 0 — standard level.</summary>
        public int Priority => 0;

        /// <summary>One-time initialisation when QuickLook loads the plugin.</summary>
        public void Init() { /* nothing to initialise */ }

        /// <summary>
        /// Returns true when this plugin should handle the given path.
        /// Checks extension AND known config / .env file names.
        /// </summary>
        public bool CanHandle(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (Directory.Exists(path)) return false;

            var name = Path.GetFileName(path).ToLowerInvariant();
            var ext  = Path.GetExtension(path).ToLowerInvariant();

            // .env variants — match by full file name (highest priority)
            foreach (var envName in EnvNames)
                if (name == envName) return true;

            // Tailwind configs matched by name
            foreach (var twName in TailwindNames)
                if (name == twName) return true;

            // Stylesheet extensions
            foreach (var e in ColorExtensions)
                if (ext == e) return true;

            // Generic config / theme file extensions
            foreach (var e in ThemeExtensions)
                if (ext == e) return true;

            return false;
        }

        /// <summary>
        /// Called before the window opens. Set preferred window size here.
        /// </summary>
        public void Prepare(string path, ContextObject context)
        {
            context.PreferredSize = new Size { Width = 920, Height = 660 };
        }

        /// <summary>
        /// Builds the viewer control and assigns it to context.ViewerContent.
        /// QuickLook renders whatever WPF element is placed there.
        /// </summary>
        public void View(string path, ContextObject context)
        {
            try
            {
                var fileType = FileTypeDetector.Detect(path);
                var panel    = new PreviewPanel(path, fileType);

                context.ViewerContent = panel;
                context.Title         = Path.GetFileName(path);
                context.IsBusy        = false;
            }
            catch (Exception ex)
            {
                // Fallback: display a styled error — never crash QuickLook
                var error = new ErrorPanel(ex.Message);
                context.ViewerContent = error;
                context.Title         = Path.GetFileName(path);
                context.IsBusy        = false;
            }
        }

        /// <summary>Dispose / cleanup when QuickLook closes the preview.</summary>
        public void Cleanup() { }
    }
}
