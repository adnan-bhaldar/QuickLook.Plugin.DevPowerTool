using System.IO;

namespace QuickLook.Plugin.DevPowerTool
{
    public enum DevFileType
    {
        Stylesheet,
        ThemeConfig,
        TailwindConfig,
        EnvFile,
        PlainText
    }

    /// <summary>
    /// Maps a file path to the appropriate <see cref="DevFileType"/>.
    /// </summary>
    public static class FileTypeDetector
    {
        private static readonly string[] TailwindNames =
        {
            "tailwind.config.js", "tailwind.config.ts",
            "tailwind.config.cjs", "tailwind.config.mjs"
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
            if (string.IsNullOrEmpty(path)) return DevFileType.PlainText;

            var name = Path.GetFileName(path).ToLowerInvariant();
            var ext  = Path.GetExtension(path).ToLowerInvariant();

            foreach (var e in EnvNames)
                if (name == e) return DevFileType.EnvFile;

            foreach (var t in TailwindNames)
                if (name == t) return DevFileType.TailwindConfig;

            if (ext == ".css" || ext == ".scss" || ext == ".sass")
                return DevFileType.Stylesheet;

            if (ext == ".json" || ext == ".js" || ext == ".ts")
                return DevFileType.ThemeConfig;

            return DevFileType.PlainText;
        }
    }
}