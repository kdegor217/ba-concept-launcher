using ActualArrow.Core.Abstractions;
using ActualArrow.Core.Models;
using Avalonia.Controls;
using Avalonia.Threading;
using ConceptApp.ViewModels;
using ConceptApp.Views;

namespace ConceptApp.Services;

public sealed class AvaloniaUserPrompts : IUserPrompts
{
	private readonly Loc _loc;
	private readonly Func<Window?> _owner;

	public AvaloniaUserPrompts(Loc loc, Func<Window?> owner)
	{
		_loc = loc;
		_owner = owner;
	}

	public Task<bool> ConfirmBackupForeignModsAsync(IReadOnlyList<string> modNames, CancellationToken ct = default)
	{
		var items = modNames.Take(12).ToList();
		var dialog = new ConfirmDialogViewModel(
			_loc.T("Ui.ForeignMods.Title"),
			_loc.T("Ui.ForeignMods.Message"),
			items,
			null,
			null,
			[
				new DialogButton(_loc.T("Ui.Cancel"), "no", DialogButtonKind.Secondary),
				new DialogButton(_loc.T("Ui.ForeignMods.Confirm"), "yes", DialogButtonKind.Primary),
			],
			DialogTone.Warning);
		return ShowAsync(dialog, vm => Equals(vm.Result, "yes"), false, ct);
	}

	public Task<bool> ConfirmReplaceUnrecordedMelonLoaderAsync(string folderPath, CancellationToken ct = default)
	{
		var dialog = new ConfirmDialogViewModel(
			_loc.T("Ui.UnrecordedMl.Title"),
			_loc.T("Ui.UnrecordedMl.Message"),
			[folderPath],
			null,
			null,
			[
				new DialogButton(_loc.T("Ui.Cancel"), "no", DialogButtonKind.Secondary),
				new DialogButton(_loc.T("Ui.UnrecordedMl.Replace"), "yes", DialogButtonKind.Danger),
			],
			DialogTone.Warning);
		return ShowAsync(dialog, vm => Equals(vm.Result, "yes"), false, ct);
	}

	public Task<UninstallOptions?> ConfirmUninstallAsync(UninstallPlan plan, CancellationToken ct = default)
	{
		var items = new List<string>
		{
			_loc.T("Ui.UninstallDialog.PackFiles", plan.PackFileCount),
		};
		if (plan.HasMelonLoader)
			items.Add(_loc.T("Ui.UninstallDialog.MelonLoader"));
		if (plan.Proxies.Count > 0)
			items.Add(_loc.T("Ui.UninstallDialog.Proxies", string.Join(", ", plan.Proxies)));

		var dialog = new ConfirmDialogViewModel(
			_loc.T("Ui.UninstallDialog.Title"),
			_loc.T("Ui.UninstallDialog.Message"),
			items,
			_loc.T("Ui.UninstallDialog.Hint", plan.GameDir),
			_loc.T("Ui.UninstallDialog.RemoveUserData"),
			[
				new DialogButton(_loc.T("Ui.Cancel"), "no", DialogButtonKind.Secondary),
				new DialogButton(_loc.T("Ui.UninstallDialog.Confirm"), "yes", DialogButtonKind.Danger),
			],
			DialogTone.Danger);

		return ShowAsync(
			dialog,
			vm => Equals(vm.Result, "yes") ? new UninstallOptions(vm.IsChecked) : null,
			null,
			ct);
	}

	public Task<HelpDialogChoice> ShowModsDidNotStartAsync(CancellationToken ct = default)
	{
		var dialog = new ConfirmDialogViewModel(
			_loc.T("Ui.Help.Title"),
			_loc.T("Ui.Help.Message"),
			[
				_loc.T("Ui.Help.Antivirus"),
				_loc.T("Ui.Help.SmartAppControl"),
			],
			null,
			null,
			[
				new DialogButton(_loc.T("Ui.Help.OpenLog"), "log", DialogButtonKind.Secondary),
				new DialogButton(_loc.T("Ui.Help.Reinstall"), "reinstall", DialogButtonKind.Primary),
				new DialogButton(_loc.T("Ui.Cancel"), "close", DialogButtonKind.Secondary),
			],
			DialogTone.Warning);

		return ShowAsync(dialog, vm => vm.Result switch
		{
			"log" => HelpDialogChoice.OpenLog,
			"reinstall" => HelpDialogChoice.ReinstallMelonLoader,
			_ => HelpDialogChoice.Close
		}, HelpDialogChoice.Close, ct);
	}

	private Task<T> ShowAsync<T>(ConfirmDialogViewModel vm, Func<ConfirmDialogViewModel, T> answer, T safe, CancellationToken ct)
	{
		if (ct.IsCancellationRequested)
			return Task.FromResult(safe);

		return Dispatcher.UIThread.InvokeAsync(async () =>
		{
			var dialog = new ConfirmDialogWindow { DataContext = vm };
			await using var reg = ct.Register(() => Dispatcher.UIThread.Post(dialog.Close));

			var window = _owner();
			if (window is { IsVisible: true })
			{
				if (window.WindowState == WindowState.Minimized)
				{
					window.WindowState = WindowState.Normal;
					window.Activate();
				}
				await dialog.ShowDialog(window);
			}
			else
			{
				var closed = new TaskCompletionSource();
				dialog.Closed += (_, _) => closed.TrySetResult();
				dialog.Show();
				await closed.Task;
			}

			return ct.IsCancellationRequested || vm.Result is null ? safe : answer(vm);
		});
	}
}
