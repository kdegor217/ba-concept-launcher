using System.Collections.Generic;
using ActualArrow.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConceptApp.Services;

namespace ConceptApp.ViewModels;

public sealed class LanguageOption
{
	public string Code { get; }
	public string Name { get; }

	public LanguageOption(string code, string name)
	{
		Code = code;
		Name = name;
	}

	public override string ToString() => Name;
}

public sealed class LayoutOption
{
	public ConceptLayout Layout { get; }
	public string Name { get; }

	public LayoutOption(ConceptLayout layout, string name)
	{
		Layout = layout;
		Name = name;
	}

	public override string ToString() => Name;
}

public partial class SettingsViewModel : ViewModelBase
{
	private readonly Loc _loc;
	private readonly IGameLocator? _locator;
	private readonly Func<Task<string?>>? _pickGameDir;
	private readonly Func<Task>? _uninstall;
	private readonly Action? _onSaved;

	[ObservableProperty] private string _gameDir = "";
	[ObservableProperty] private string _gameDirHint = "";
	[ObservableProperty] private string _repoText = ActualArrow.Core.Brand.DefaultPackRepo;
	[ObservableProperty] private LanguageOption? _selectedLanguage;
	[ObservableProperty] private LayoutOption? _selectedLayoutOption;
	[ObservableProperty] private bool _closeOnLaunch;
	[ObservableProperty] private string _statusMessage = "";
	[ObservableProperty] private bool _isUninstalling;
	[ObservableProperty] private bool _modInstalled = true;
	[ObservableProperty] private bool _isDetecting;

	public IReadOnlyList<LanguageOption> Languages { get; }
	public IReadOnlyList<LayoutOption> LayoutOptions { get; private set; }

	public ConceptLayout SelectedLayout => SelectedLayoutOption?.Layout ?? ConceptLayout.B;

	public string Title => _loc.T("Settings.Title");
	public string GameDirLabel => _loc.T("Settings.GameDir");
	public string BrowseLabel => _loc.T("Settings.Browse");
	public string AutoDetectLabel => _loc.T("Settings.AutoDetect");
	public string LanguageLabel => _loc.T("Settings.Language");
	public string LayoutLabel => _loc.T("Settings.Layout");
	public string CloseOnLaunchLabel => _loc.T("Settings.CloseOnLaunch");
	public string CancelLabel => _loc.T("Ui.Cancel");
	public string SaveLabel => _loc.T("Settings.Save");
	public string DangerZoneLabel => _loc.T("Settings.DangerZone");
	public string UninstallLabel => _loc.T("Settings.Uninstall");
	public string UninstallHint => _loc.T("Settings.UninstallHint");

	public event EventHandler<bool>? CloseRequested;
	public bool Saved { get; private set; }

	public SettingsViewModel(
		Loc loc,
		string packName,
		string? gameDir,
		string? repo,
		bool closeOnLaunch,
		bool modInstalled,
		ConceptLayout layout,
		IGameLocator? locator = null,
		Func<Task<string?>>? pickGameDir = null,
		Func<Task>? uninstall = null,
		Action? onSaved = null)
	{
		_loc = loc;
		_locator = locator;
		_pickGameDir = pickGameDir;
		_uninstall = uninstall;
		_onSaved = onSaved;
		Languages =
		[
			new LanguageOption("ru", "Русский"),
			new LanguageOption("en", "English"),
			new LanguageOption("uk", "Українська"),
			new LanguageOption("de", "Deutsch"),
			new LanguageOption("pl", "Polski"),
			new LanguageOption("zh", "中文"),
		];
		SelectedLanguage = Languages.FirstOrDefault(l =>
			string.Equals(l.Code, loc.Lang, StringComparison.OrdinalIgnoreCase)) ?? Languages[0];
		LayoutOptions = BuildLayoutOptions();
		SelectedLayoutOption = LayoutOptions.FirstOrDefault(o => o.Layout == layout) ?? LayoutOptions[0];
		GameDir = gameDir ?? "";
		RepoText = string.IsNullOrWhiteSpace(repo) ? ActualArrow.Core.Brand.DefaultPackRepo : repo;
		CloseOnLaunch = closeOnLaunch;
		ModInstalled = modInstalled;
		GameDirHint = string.IsNullOrWhiteSpace(GameDir)
			? _loc.T("Settings.GameDirAuto")
			: _loc.T("Settings.GameDirManual");
		_ = packName;
	}

	private IReadOnlyList<LayoutOption> BuildLayoutOptions() =>
	[
		new LayoutOption(ConceptLayout.B, _loc.T("Ui.LayoutB")),
		new LayoutOption(ConceptLayout.D, _loc.T("Ui.LayoutD")),
	];

	private void RefreshLayoutOptions()
	{
		var current = SelectedLayout;
		LayoutOptions = BuildLayoutOptions();
		OnPropertyChanged(nameof(LayoutOptions));
		SelectedLayoutOption = LayoutOptions.FirstOrDefault(o => o.Layout == current) ?? LayoutOptions[0];
	}

	partial void OnGameDirChanged(string value)
	{
		GameDirHint = string.IsNullOrWhiteSpace(value)
			? _loc.T("Settings.GameDirAuto")
			: _loc.T("Settings.GameDirManual");
	}

	[RelayCommand]
	private async Task BrowseAsync()
	{
		if (_pickGameDir is null) return;
		var path = await _pickGameDir();
		if (string.IsNullOrWhiteSpace(path)) return;
		GameDir = path;
		StatusMessage = _loc.T("Settings.PathPicked");
	}

	[RelayCommand]
	private async Task AutoDetectAsync()
	{
		if (_locator is null)
		{
			GameDir = "";
			StatusMessage = _loc.T("Settings.GameDirAuto");
			return;
		}

		IsDetecting = true;
		StatusMessage = _loc.T("Settings.Detecting");
		try
		{
			var path = await _locator.DetectAsync();
			if (string.IsNullOrWhiteSpace(path))
			{
				GameDir = "";
				StatusMessage = _loc.T("Notice.GameNotFound");
				return;
			}

			GameDir = path;
			StatusMessage = _loc.T("Settings.Detected", path);
		}
		finally
		{
			IsDetecting = false;
		}
	}

	[RelayCommand]
	private void Cancel()
	{
		Saved = false;
		CloseRequested?.Invoke(this, false);
	}

	[RelayCommand]
	private void Save()
	{
		if (SelectedLanguage is not null)
			_loc.Lang = SelectedLanguage.Code;

		RefreshLayoutOptions();
		Saved = true;
		StatusMessage = _loc.T("Settings.Saved");
		_onSaved?.Invoke();
		CloseRequested?.Invoke(this, true);
	}

	partial void OnModInstalledChanged(bool value) => UninstallCommand.NotifyCanExecuteChanged();
	partial void OnIsUninstallingChanged(bool value) => UninstallCommand.NotifyCanExecuteChanged();

	[RelayCommand(CanExecute = nameof(CanUninstall))]
	private async Task UninstallAsync()
	{
		if (_uninstall is null) return;
		IsUninstalling = true;
		StatusMessage = _loc.T("Status.Uninstalling");
		try
		{
			await _uninstall();
			ModInstalled = false;
			StatusMessage = _loc.T("Settings.Uninstalled");
		}
		finally
		{
			IsUninstalling = false;
		}
	}

	private bool CanUninstall() => ModInstalled && !IsUninstalling && _uninstall is not null;
}
