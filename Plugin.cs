using System.Windows;
using QuickLook.Common.Plugin;

namespace QuickLook.Plugin.DevPowerTool
{
    public class Plugin : IViewer
    {
        public int Priority => 0;
        public void Init() { }
        public bool CanHandle(string path) => false;
        public void Prepare(string path, ContextObject context) { }
        public void View(string path, ContextObject context) { }
        public void Cleanup() { }
    }
}