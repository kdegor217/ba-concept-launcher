using System.Collections.ObjectModel;
using ActualArrow.Core.Abstractions;
using ActualArrow.Core.Models;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConceptApp.Services;

namespace ConceptApp.ViewModels;

public sealed class BranchItem
{
	public string Name { get; }
	public string Meta { get; }

	public BranchItem(string name, string meta)
	{
		Name = name;
		Meta = meta;
	}
}

public enum ConceptLayout
{
	B,
	D,
	E,
	G,
	H
}

public partial class MainViewModel : ViewModelBase, IDisposable
{
	private readonly ILauncherService _launcher;
	private readonly ISettingsStore _settings;
	private readonly IPackCatalog _catalog;
	private readonly IGameLocator _locator;
	private readonly ConceptPrefs _prefs;
	private IReadOnlyList<PackCatalogEntry> _catalogEntries = Array.Empty<PackCatalogEntry>();
	private bool _initialized;
	private StatusMessage? _lastStatus;
	private Banner? _lastBanner;

	public Loc Loc { get; }
	public IGameLocator Locator => _locator;

	[ObservableProperty] private ConceptLayout _layout = ConceptLayout.B;
	[ObservableProperty] private string _packName = "ActualArrow";
	[ObservableProperty] private string _installedText = "—";
	[ObservableProperty] private string _latestText = "—";
	[ObservableProperty] private bool _updateAvailable;
	[ObservableProperty] private string _status = "";
	[ObservableProperty] private string _playLabel = "";
	[ObservableProperty] private string _versionText = ActualArrow.Core.Brand.Version;
	[ObservableProperty] private string _progressText = "";
	[ObservableProperty] private double _progress;
	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private Bitmap? _preview;
	[ObservableProperty] private string _selectedBranchName = "release";
	[ObservableProperty] private string _chipText = "";
	[ObservableProperty] private double _windowWidth = 420;
	[ObservableProperty] private double _windowHeight = 520;
	[ObservableProperty] private SizeToContent _windowSizeToContent = SizeToContent.Manual;
	[ObservableProperty] private string _gameDir = "";
	[ObservableProperty] private string _packRepo = ActualArrow.Core.Brand.DefaultPackRepo;
	[ObservableProperty] private bool _closeOnLaunch;
	[ObservableProperty] private bool _modInstalled;
	[ObservableProperty] private bool _hasWizardError;
	[ObservableProperty] private string _wizardErrorTitle = "";
	[ObservableProperty] private string _wizardErrorDetail = "";

	public bool LastUninstallRemoveUserData { get; set; }
	public bool IsLayoutB => Layout == ConceptLayout.B;
	public bool IsLayoutD => Layout == ConceptLayout.D;
	public bool IsLayoutE => Layout == ConceptLayout.E;
	public bool IsLayoutG => Layout == ConceptLayout.G;
	public bool IsLayoutH => Layout == ConceptLayout.H;
	public bool ShowWizardExpand => IsBusy || HasWizardError;
	public bool ShowWizardBusy => IsBusy;
	public bool ShowWizardErrorPanel => !IsBusy && HasWizardError;

	public ObservableCollection<BranchItem> Branches { get; } =
	[
		new("release", "stable"),
	];

	public event EventHandler? SettingsRequested;
	public event EventHandler? CloseRequested;

	public MainViewModel(ILauncherService launcher, ISettingsStore settings, IPackCatalog catalog, IGameLocator locator, Loc loc)
	{
		_launcher = launcher;
		_settings = settings;
		_catalog = catalog;
		_locator = locator;
		Loc = loc;
		_prefs = ConceptPrefs.Load();
		Layout = _prefs.Layout;
		Loc.PropertyChanged += (_, _) => RefreshLocalized();
		_launcher.StateChanged += OnLauncherStateChanged;

		var saved = _settings.Current;
		CloseOnLaunch = saved.CloseOnLaunch;
		if (!string.IsNullOrWhiteSpace(_prefs.UiLanguage))
			Loc.Lang = _prefs.UiLanguage;
		else if (!string.IsNullOrWhiteSpace(saved.Language))
			Loc.Lang = saved.Language;
		if (!string.IsNullOrWhiteSpace(saved.GameDir))
			GameDir = saved.GameDir!;
		if (!string.IsNullOrWhiteSpace(saved.PackRepo))
			PackRepo = saved.PackRepo!;

		try
		{
			Preview = new Bitmap(AssetLoader.Open(new Uri("avares://ConceptApp/Assets/preview.png")));
		}
		catch
		{
			Preview = null;
		}

		ApplyLayoutSize();
		RefreshLocalized();
		ApplyState(_launcher.State);
	}

	public async Task InitializeAsync()
	{
		if (_initialized) return;
		_initialized = true;
		Status = Loc.T("Ui.Checking");
		await _settings.LoadAsync();
		CloseOnLaunch = _settings.Current.CloseOnLaunch;
		if (!string.IsNullOrWhiteSpace(_prefs.UiLanguage))
			Loc.Lang = _prefs.UiLanguage;
		else if (!string.IsNullOrWhiteSpace(_settings.Current.Language))
			Loc.Lang = _settings.Current.Language;
		Layout = _prefs.Layout;
		try
		{
			_catalogEntries = await _catalog.GetAsync();
		}
		catch
		{
			_catalogEntries = Array.Empty<PackCatalogEntry>();
		}
		await _launcher.RefreshAsync();
	}

	private void OnLauncherStateChanged(object? sender, LauncherState state)
		=> Dispatcher.UIThread.Post(() => ApplyState(state));

	private string ResolvePackDisplayName(LauncherState state)
	{
		var repo = state.PackRepo ?? PackRepo;
		var fromCatalog = _catalogEntries.FirstOrDefault(e =>
			string.Equals(e.Repo, repo, StringComparison.OrdinalIgnoreCase))?.Name;
		if (!string.IsNullOrWhiteSpace(fromCatalog))
			return fromCatalog!;

		var manifestName = state.Manifest?.Name;
		if (string.IsNullOrWhiteSpace(manifestName))
			return "ActualArrow";

		if (manifestName.Contains('_', StringComparison.Ordinal) ||
		    manifestName.Contains("Leha", StringComparison.OrdinalIgnoreCase))
			return "ActualArrow";

		return manifestName;
	}

	private void ApplyState(LauncherState state)
	{
		IsBusy = state.IsBusy;
		GameDir = state.Game?.GameDir ?? state.DetectedGameDir ?? GameDir;
		PackRepo = string.IsNullOrWhiteSpace(state.PackRepo) ? PackRepo : state.PackRepo;
		PackName = ResolvePackDisplayName(state);
		InstalledText = string.IsNullOrWhiteSpace(state.InstalledTag) ? "—" : state.InstalledTag!;
		LatestText = string.IsNullOrWhiteSpace(state.LatestTag) ? InstalledText : state.LatestTag!;
		UpdateAvailable = state.PackStatus is PackStatusKind.UpdateAvailable or PackStatusKind.Rollback;
		ModInstalled = state.CanUninstall || !string.IsNullOrWhiteSpace(state.InstalledTag);
		Progress = StatusFormatter.ProgressPercent(state.Progress);
		ProgressText = state.Progress is null ? "" : Loc.T("Status." + state.Progress.Kind, state.Progress.Kind);
		if (ProgressText.StartsWith("Status.", StringComparison.Ordinal))
			ProgressText = state.Progress?.Kind.ToString() ?? "";
		_lastStatus = state.Status;
		_lastBanner = state.Banner;
		Status = StatusFormatter.Format(Loc, state.Status);
		ChipText = UpdateAvailable
			? Loc.T("Ui.UpdateAvailable", LatestText)
			: (ModInstalled ? Loc.T("Ui.UpToDate") : Loc.T("Pack.NotInstalled"));

		UpdateWizardError(state);

		if (!string.IsNullOrWhiteSpace(state.PreviewImagePath) && File.Exists(state.PreviewImagePath))
		{
			try
			{
				Preview = new Bitmap(state.PreviewImagePath);
			}
			catch
			{
				/* keep previous */
			}
		}

		ApplyLayoutSize();
		RefreshLocalized();
		PlayCommand.NotifyCanExecuteChanged();
		VanillaCommand.NotifyCanExecuteChanged();
		CancelCommand.NotifyCanExecuteChanged();
		RetryCommand.NotifyCanExecuteChanged();
	}

	private void UpdateWizardError(LauncherState state)
	{
		var key = state.Banner?.Message.Key ?? state.Status?.Key;
		var isProblem = !state.IsBusy && key is not null &&
			(key.StartsWith("Error.", StringComparison.Ordinal) ||
			 key.Equals("Notice.GameNotFound", StringComparison.Ordinal) ||
			 key.Equals("Play.NeedsGameDir", StringComparison.Ordinal));

		if (!isProblem && !state.IsBusy &&
		    (state.Banner?.Severity is BannerSeverity.Error or BannerSeverity.Warning) &&
		    state.Banner.Message.Key.Equals("Notice.GameNotFound", StringComparison.Ordinal))
			isProblem = true;

		if (!isProblem && !state.IsBusy && string.IsNullOrWhiteSpace(GameDir) && _initialized)
		{
			isProblem = true;
			key = "Notice.GameNotFound";
		}

		HasWizardError = isProblem;
		if (!isProblem)
		{
			WizardErrorTitle = "";
			WizardErrorDetail = "";
			return;
		}

		WizardErrorTitle = StatusFormatter.Format(Loc, state.Banner?.Message ?? state.Status ?? StatusMessage.Of(key ?? "Notice.GameNotFound"));
		WizardErrorDetail = state.Banner?.Detail is null
			? Loc.T("Ui.WizardErrorHint")
			: StatusFormatter.Format(Loc, state.Banner.Detail);
		OnPropertyChanged(nameof(ShowWizardExpand));
		OnPropertyChanged(nameof(ShowWizardBusy));
		OnPropertyChanged(nameof(ShowWizardErrorPanel));
	}

	partial void OnLayoutChanged(ConceptLayout value)
	{
		OnPropertyChanged(nameof(IsLayoutB));
		OnPropertyChanged(nameof(IsLayoutD));
		OnPropertyChanged(nameof(IsLayoutE));
		OnPropertyChanged(nameof(IsLayoutG));
		OnPropertyChanged(nameof(IsLayoutH));
		ApplyLayoutSize();
		RefreshLocalized();
	}

	partial void OnIsBusyChanged(bool value)
	{
		OnPropertyChanged(nameof(ShowWizardExpand));
		OnPropertyChanged(nameof(ShowWizardBusy));
		OnPropertyChanged(nameof(ShowWizardErrorPanel));
		ApplyLayoutSize();
	}

	partial void OnHasWizardErrorChanged(bool value)
	{
		OnPropertyChanged(nameof(ShowWizardExpand));
		OnPropertyChanged(nameof(ShowWizardBusy));
		OnPropertyChanged(nameof(ShowWizardErrorPanel));
		ApplyLayoutSize();
	}

	private void ApplyLayoutSize()
	{
		switch (Layout)
		{
			case ConceptLayout.B:
				WindowSizeToContent = SizeToContent.Manual;
				WindowWidth = 420;
				WindowHeight = 520;
				break;
			case ConceptLayout.E:
				WindowSizeToContent = SizeToContent.Manual;
				WindowWidth = 560;
				WindowHeight = 360;
				break;
			case ConceptLayout.G:
				WindowSizeToContent = SizeToContent.Manual;
				WindowWidth = 480;
				WindowHeight = 560;
				break;
			case ConceptLayout.H:
				WindowSizeToContent = SizeToContent.Height;
				WindowWidth = 720;
				WindowHeight = double.NaN;
				break;
			default: // D
				WindowSizeToContent = SizeToContent.WidthAndHeight;
				WindowWidth = double.NaN;
				WindowHeight = double.NaN;
				break;
		}
	}

	public void RefreshLocalized()
	{
		if (!IsBusy && _launcher.State.Status is null && !HasWizardError)
			Status = Loc.T("Ui.Ready");
		PlayLabel = Layout is ConceptLayout.D or ConceptLayout.H or ConceptLayout.E
			? Loc.T("Ui.PlayShort")
			: Loc.T("Ui.Play");
		if (Layout == ConceptLayout.E)
			PlayLabel = "▶";
		ChipText = UpdateAvailable
			? Loc.T("Ui.UpdateAvailable", LatestText)
			: (ModInstalled ? Loc.T("Ui.UpToDate") : Loc.T("Pack.NotInstalled"));
		OnPropertyChanged(nameof(PackMetaLine));
		OnPropertyChanged(nameof(VanillaLabel));
		OnPropertyChanged(nameof(CancelLabel));
		OnPropertyChanged(nameof(SettingsTip));
		OnPropertyChanged(nameof(BrandSub));
		OnPropertyChanged(nameof(ModpackLabel));
		OnPropertyChanged(nameof(LayoutBLabel));
		OnPropertyChanged(nameof(LayoutDLabel));
		OnPropertyChanged(nameof(LayoutELabel));
		OnPropertyChanged(nameof(LayoutGLabel));
		OnPropertyChanged(nameof(LayoutHLabel));
		OnPropertyChanged(nameof(HudCallsign));
		OnPropertyChanged(nameof(HudSysReady));
		OnPropertyChanged(nameof(VanillaHudLabel));
		OnPropertyChanged(nameof(RetryLabel));
		OnPropertyChanged(nameof(FixFolderLabel));
		if (_lastBanner is not null || _lastStatus is not null)
			UpdateWizardError(_launcher.State);
	}

	public string PackMetaLine => UpdateAvailable
		? $"{InstalledText} → {LatestText} · {SelectedBranchName}"
		: $"{InstalledText} · {SelectedBranchName}";

	public string VanillaLabel => Layout is ConceptLayout.D or ConceptLayout.H
		? Loc.T("Ui.VanillaShort")
		: Loc.T("Ui.Vanilla");
	public string VanillaHudLabel => Loc.T("Ui.VanillaHud");
	public string CancelLabel => Loc.T("Ui.Cancel");
	public string SettingsTip => Loc.T("Ui.Settings");
	public string BrandSub => Loc.T("Brand.Sub");
	public string ModpackLabel => Loc.T("Ui.Modpack");
	public string LayoutBLabel => Loc.T("Ui.LayoutB");
	public string LayoutDLabel => Loc.T("Ui.LayoutD");
	public string LayoutELabel => Loc.T("Ui.LayoutE");
	public string LayoutGLabel => Loc.T("Ui.LayoutG");
	public string LayoutHLabel => Loc.T("Ui.LayoutH");
	public string HudCallsign => Loc.T("Ui.HudCallsign");
	public string HudSysReady => Loc.T("Ui.HudSysReady");
	public string RetryLabel => Loc.T("Ui.Retry");
	public string FixFolderLabel => Loc.T("Ui.FixFolder");

	public async Task ApplySettingsAsync(SettingsViewModel settings)
	{
		GameDir = settings.GameDir;
		PackRepo = settings.RepoText;
		CloseOnLaunch = settings.CloseOnLaunch;
		Layout = settings.SelectedLayout;
		if (settings.SelectedLanguage is not null)
			Loc.Lang = settings.SelectedLanguage.Code;

		_prefs.Layout = Layout;
		_prefs.UiLanguage = Loc.Lang;
		_prefs.Save();

		await _launcher.ApplySettingsAsync(new SettingsChange(
			string.IsNullOrWhiteSpace(settings.GameDir) ? null : settings.GameDir,
			string.IsNullOrWhiteSpace(settings.RepoText) ? null : settings.RepoText,
			Loc.ToCoreLanguage(Loc.Lang),
			settings.CloseOnLaunch));

		if (!settings.ModInstalled)
			MarkUninstalled();

		RefreshLocalized();
	}

	public void MarkUninstalled()
	{
		ModInstalled = false;
		UpdateAvailable = false;
		InstalledText = "—";
		ChipText = Loc.T("Settings.Uninstalled");
		Status = Loc.T("Settings.Uninstalled");
		OnPropertyChanged(nameof(PackMetaLine));
	}

	[RelayCommand]
	private void CyclePack()
	{
		/* catalog switching can be added later; keep UI affordance no-op while busy */
	}

	[RelayCommand]
	private void SelectBranch(string? name)
	{
		if (string.IsNullOrWhiteSpace(name) || IsBusy) return;
		SelectedBranchName = name;
		OnPropertyChanged(nameof(PackMetaLine));
	}

	[RelayCommand(CanExecute = nameof(CanPlay))]
	private async Task PlayAsync()
	{
		var result = await _launcher.PlayModdedAsync();
		if (result.Outcome == OperationOutcome.Succeeded && result.GameStarted && CloseOnLaunch)
			CloseRequested?.Invoke(this, EventArgs.Empty);
	}

	private bool CanPlay() => !IsBusy;

	[RelayCommand(CanExecute = nameof(CanPlay))]
	private async Task VanillaAsync()
	{
		var result = await _launcher.PlayVanillaAsync();
		if (result.Outcome == OperationOutcome.Succeeded && result.GameStarted && CloseOnLaunch)
			CloseRequested?.Invoke(this, EventArgs.Empty);
	}

	[RelayCommand(CanExecute = nameof(CanCancel))]
	private void Cancel() => _launcher.CancelCurrentOperation();

	private bool CanCancel() => IsBusy;

	[RelayCommand]
	private void Settings() => SettingsRequested?.Invoke(this, EventArgs.Empty);

	[RelayCommand(CanExecute = nameof(CanRetry))]
	private async Task RetryAsync()
	{
		Status = Loc.T("Ui.Checking");
		await _launcher.RefreshAsync();
	}

	private bool CanRetry() => !IsBusy;

	public async Task UninstallAsync()
	{
		await _launcher.UninstallAsync();
	}

	public void Dispose()
	{
		_launcher.StateChanged -= OnLauncherStateChanged;
	}
}
