namespace LegionLoqControl;

/// <summary>
/// Interaction logic for App.xaml.
/// </summary>
public partial class App : global::System.Windows.Application
{
    public App()
    {
        System.Windows.Media.RenderOptions.ProcessRenderMode =
            System.Windows.Interop.RenderMode.SoftwareOnly;
    }
}