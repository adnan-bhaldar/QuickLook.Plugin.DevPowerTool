// ============================================================
// QuickLook.Plugin.DevPowerTool — FileTypeDetector.cs
// Determines which rendering mode to use for a given file path.
// Pure static helper — no I/O, no side effects.
// ============================================================
using System.IO;

namespace QuickLook.Plugin.DevPowerTool
{
    /// <summary>
    /// Distinguishes between .env privacy-mode files and colour-swatch files.
    /// </summary>
    public enum DevFileType
    {
        /// <summary>CSS / SCSS / SASS — show colour swatches.</summary>
        Stylesheet,

        /// <summary>JSON / JS / TS theme or config — show colour swatches.</summary>
        ThemeConfig,

        /// <summary>Tailwind config — show colour swatches with Tailwind label.</summary>
        TailwindConfig,

        /// <summary>.env variants — privacy masking mode.</summary>
        EnvFile,

        /// <summary>Fallback — plain text with colour swatches.</summary>
        PlainText
    }

    /// <summary>
    /// Maps a file path to the appropriate <see cref="DevFileType"/>.
    /// </summary>
    public static class FileTypeDetector
    {
        private static readonly string[] TailwindNames =
        {
            "tailwind.config.js",
            "tailwind.config.ts",
            "tailwind.config.cjs",
            "tailwind.config.mjs"
        };

        private static readonly string[] EnvNames =
        {
            ".env",
            ".env.local",
            ".env.production",
            ".env.development",
            ".env.staging",
            ".env.test"
        };

        /// <summary>
        /// Returns the <see cref="DevFileType"/> for <paramref name="path"/>.
        /// Matching is case-insensitive and based on both name and extension.
        /// </summary>
        public static DevFileType Detect(string path)
        {
            if (string.IsNullOrEmpty(path))
                return DevFileType.PlainText;

            var name = Path.GetFileName(path).ToLowerInvariant();
            var ext  = Path.GetExtension(path).ToLowerInvariant();

            // .env variants — highest priority, matched by full file name
            foreach (var e in EnvNames)
                if (name == e) return DevFileType.EnvFile;

            // Tailwind config — matched by well-known file name
            foreach (var t in TailwindNames)
                if (name == t) return DevFileType.TailwindConfig;

            // Stylesheet extensions
            if (ext == ".css" || ext == ".scss" || ext == ".sass")
                return DevFileType.Stylesheet;

            // Generic theme / config file extensions
            if (ext == ".json" || ext == ".js" || ext == ".ts")
                return DevFileType.ThemeConfig;

            return DevFileType.PlainText;
        }
    }
}
