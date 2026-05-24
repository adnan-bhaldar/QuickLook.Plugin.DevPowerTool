using System;
using System.IO;
using System.Windows;
using QuickLook.Common.Plugin;

namespace QuickLook.Plugin.DevPowerTool
{
    public class Plugin : IViewer
    {
        private static readonly string[] ColorExtensions = { ".css", ".scss", ".sass" };
        private static readonly string[] ThemeExtensions = { ".json", ".js", ".ts" };
        private static readonly string[] TailwindNames   =
        {
            "tailwind.config.js", "tailwind.config.ts",
            "tailwind.config.cjs", "tailwind.config.mjs"
        };
        private static readonly string[] EnvNames =
        {
            ".env", ".env.local", ".env.production",
            ".env.development", ".env.staging", ".env.test"
        };

        public int Priority => 0;

        public void Init() { }

        public bool CanHandle(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (Directory.Exists(path))     return false;

            var name = Path.GetFileName(path).ToLowerInvariant();
            var ext  = Path.GetExtension(path).ToLowerInvariant();

            foreach (var e in EnvNames)
                if (name == e) return true;

            foreach (var t in TailwindNames)
                if (name == t) return true;

            foreach (var e in ColorExtensions)
                if (ext == e) return true;

            foreach (var e in ThemeExtensions)
                if (ext == e) return true;

            return false;
        }

        public void Prepare(string path, ContextObject context)
        {
            context.PreferredSize = new Size(920, 660);
        }

        public void View(string path, ContextObject context)
        {
            try
            {
                var panel = new PreviewPanel(path, FileTypeDetector.Detect(path));
                context.ViewerContent = panel;
                context.Title         = Path.GetFileName(path);
                context.IsBusy        = false;
            }
            catch (Exception ex)
            {
                var error = new ErrorPanel(ex.Message);
                context.ViewerContent = error;
                context.Title         = Path.GetFileName(path);
                context.IsBusy        = false;
            }
        }

        public void Cleanup() { }
    }
}