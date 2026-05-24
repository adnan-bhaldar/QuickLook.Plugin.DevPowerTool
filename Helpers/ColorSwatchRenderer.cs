// ============================================================
// QuickLook.Plugin.DevPowerTool — Helpers/ColorSwatchRenderer.cs
// ColorSwatchRenderer temporarily removed to fix silent
// ReflectionTypeLoadException during QuickLook plugin install.
// IBackgroundRenderer will be re-added once install is verified.
// ============================================================
using System.Windows.Media;

namespace QuickLook.Plugin.DevPowerTool.Helpers
{
    public sealed class SwatchInfo
    {
        public int   Line        { get; set; }
        public int   CharOffset  { get; set; }
        public int   TokenLength { get; set; }
        public Color Color       { get; set; }
    }
}