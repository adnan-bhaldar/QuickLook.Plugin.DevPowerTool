// ============================================================
// QuickLook.Plugin.DevPowerTool — Plugin.cs
// Entry point. Implements IViewer and is discovered by QuickLook
// at runtime via reflection.
// ============================================================

using System;
using System.IO;
using System.Windows;
using QuickLook.Common.Plugin;

namespace QuickLook.Plugin.DevPowerTool
{
    /// <summary>
    /// Main plugin class. QuickLook discovers this via the IViewer interface.
    /// Priority 0 = standard; raise to override other text plugins for these
    /// specific extensions.
    /// </summary>
    public class Plugin : IViewer
    {
        // ── Supported file extensions ──────────────────────────────────────
        private static readonly string[] ColorExtensions =
        {
            ".css", ".scss", ".sass"
        };

        private static readonly string[] ColorConfigExtensions =
        {
            ".json", ".js", ".ts"
        };

        private static readonly string[] EnvExtensions =
        {
            ".env"
        };

        // Tailwind config file names (matched by name, not only extension)
        private static readonly string[] TailwindNames =
        {
            "tailwind.config.js",
            "tailwind.config.ts",
            "tailwind.config.cjs",
            "tailwind.config.mjs"
        };

        // .env variant suffixes / full names
        private static readonly string[] EnvNames =
        {
            ".env",
            ".env.local",
            ".env.production",
            ".env.development",
            ".env.staging",
            ".env.test"
        };

        // ── IViewer ────────────────────────────────────────────────────────

        /// <summary>Priority 0 — standard level. Increase if needed.</summary>
        public int Priority => 0;

        /// <summary>One-time init when QuickLook loads the plugin.</summary>
        public void Init() { /* nothing to initialise */ }

        /// <summary>
        /// Returns true when this plugin should handle the given path.
        /// Checks extension AND known config file names.
        /// </summary>
        public bool CanHandle(string path)
        {
            if (Directory.Exists(path))
                return false;

            var name = Path.GetFileName(path).ToLowerInvariant();
            var ext  = Path.GetExtension(path).ToLowerInvariant();

            // .env variants — match by full file name
            foreach (var envName in EnvNames)
                if (name == envName)
                    return true;

            // Colour-aware file types
            foreach (var e in ColorExtensions)
                if (ext == e) return true;

            // Tailwind configs matched by name
            foreach (var twName in TailwindNames)
                if (name == twName) return true;

            // Generic .json / .js / .ts (theme / config files)
            foreach (var e in ColorConfigExtensions)
                if (ext == e) return true;

            return false;
        }

        /// <summary>
        /// Called before the window opens. Set preferred window size here.
        /// </summary>
        public void Prepare(string path, ContextObject context)
        {
            context.PreferredSize = new Size { Width = 900, Height = 650 };
        }

        /// <summary>
        /// Builds the viewer control and assigns it to context.ViewerContent.
        /// QuickLook renders whatever WPF element you place there.
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
                // Fallback: show error gracefully — never crash QuickLook
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
