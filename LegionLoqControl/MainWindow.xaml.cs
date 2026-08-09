using System.Windows;
using LegionLoqControl.Presentation;
using LegionLoqControl.ViewModels;

namespace LegionLoqControl;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        SourceInitialized += (_, _) => WindowTheme.TryEnableDarkTitleBar(this);
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
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
