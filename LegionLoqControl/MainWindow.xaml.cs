using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using LegionLoqControl.Presentation;
using LegionLoqControl.ViewModels;

namespace LegionLoqControl;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        ApplyProductMark();
        DataContext = _viewModel;
        SourceInitialized += (_, _) => WindowTheme.TryEnableDarkTitleBar(this);
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void ApplyProductMark()
    {
        try
        {
            BitmapDecoder decoder = BitmapDecoder.Create(
                new Uri("pack://application:,,,/Assets/logo.ico", UriKind.Absolute),
                BitmapCreateOptions.None,
                BitmapCacheOption.OnLoad);
            BitmapFrame windowFrame = decoder.Frames
                .OrderByDescending(frame => frame.PixelWidth)
                .First();
            BitmapFrame markFrame = decoder.Frames
                .OrderBy(frame => Math.Abs(frame.PixelWidth - 32))
                .First();
            Icon = windowFrame;
            ProductMark.Source = markFrame;
        }
        catch (Exception ex) when (
            ex is IOException
                or NotSupportedException
                or ArgumentException
                or InvalidOperationException
                or FileFormatException)
        {
            // Title-bar and header marks are optional; the exe still uses ApplicationIcon.
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Task dashboardInitialization = _viewModel.InitializeAsync();
        await _viewModel.ProfileWorkspace.InitializeAsync();
        await _viewModel.AutomationWorkspace.InitializeAsync();
        await dashboardInitialization;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.Cancel();
    }
}
