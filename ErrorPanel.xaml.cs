// ============================================================
// QuickLook.Plugin.DevPowerTool — ErrorPanel.xaml.cs
// ============================================================

using System.Windows.Controls;

namespace QuickLook.Plugin.DevPowerTool
{
    public partial class ErrorPanel : UserControl
    {
        public ErrorPanel(string message)
        {
            InitializeComponent();
            ErrorMessageBlock.Text = message;
        }
    }
}
