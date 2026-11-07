using Avalonia.Controls;
using ConceptApp.ViewModels;

namespace ConceptApp.Views;

public partial class SettingsWindow : Window
{
    private SettingsViewModel? _vm;

    public SettingsWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as SettingsViewModel);
    }

    private void Attach(SettingsViewModel? vm)
    {
        if (_vm is not null)
            _vm.CloseRequested -= OnCloseRequested;
        _vm = vm;
        if (_vm is not null)
            _vm.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, bool saved) => Close(saved);
}
