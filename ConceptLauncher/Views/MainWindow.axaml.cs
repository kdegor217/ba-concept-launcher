using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ConceptApp.ViewModels;

namespace ConceptApp.Views;

public partial class MainWindow : Window
{
	private MainViewModel? _vm;
	private bool _closeAllowed;
	private bool _askingClose;

	public MainWindow()
	{
		InitializeComponent();
		DataContextChanged += (_, _) => Attach(DataContext as MainViewModel);
		Closing += OnClosing;
	}

	private void Attach(MainViewModel? vm)
	{
		if (_vm is not null)
			_vm.SettingsRequested -= OnSettingsRequested;
		_vm = vm;
		if (_vm is not null)
			_vm.SettingsRequested += OnSettingsRequested;
	}

	private async void OnSettingsRequested(object? sender, EventArgs e)
	{
		if (_vm is null) return;

		async Task<string?> PickGameDirAsync()
		{
			var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
			{
				Title = _vm.Loc.T("Settings.PickerTitle"),
				AllowMultiple = false
			});
			return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
		}

		async Task UninstallFromSettingsAsync()
		{
			await _vm.UninstallAsync();
		}

		var settings = new SettingsViewModel(
			_vm.Loc,
			_vm.PackName,
			string.IsNullOrWhiteSpace(_vm.GameDir) ? null : _vm.GameDir,
			_vm.PackRepo,
			_vm.CloseOnLaunch,
			_vm.ModInstalled,
			_vm.Layout,
			_vm.Locator,
			PickGameDirAsync,
			UninstallFromSettingsAsync,
			() => _vm.RefreshLocalized());

		var win = new SettingsWindow { DataContext = settings };
		var saved = await win.ShowDialog<bool>(this);
		if (saved)
			await _vm.ApplySettingsAsync(settings);
		_vm.RefreshLocalized();
	}

	private async void OnClosing(object? sender, WindowClosingEventArgs e)
	{
		if (_closeAllowed || _vm is null || !_vm.IsBusy) return;
		e.Cancel = true;
		if (_askingClose) return;
		_askingClose = true;
		try
		{
			var dialog = ConfirmDialogViewModel.CloseWhileBusy(_vm.Loc);
			var win = new ConfirmDialogWindow { DataContext = dialog };
			var result = await win.ShowDialog<object?>(this);
			if (Equals(result, "yes"))
			{
				_vm.CancelCommand.Execute(null);
				_closeAllowed = true;
				Close();
			}
		}
		finally
		{
			_askingClose = false;
		}
	}
}
