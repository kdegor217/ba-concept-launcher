using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConceptApp.Services;

namespace ConceptApp.ViewModels;

public enum DialogButtonKind
{
    Secondary,
    Primary,
    Danger
}

public enum DialogTone
{
    Info,
    Warning,
    Danger
}

public sealed record DialogButton(string Text, string Result, DialogButtonKind Kind);

public partial class ConfirmDialogViewModel : ViewModelBase
{
    public string Title { get; }
    public string Message { get; }
    public IReadOnlyList<string> Items { get; }
    public bool HasItems => Items.Count > 0;
    public string? Hint { get; }
    public bool HasHint => !string.IsNullOrEmpty(Hint);
    public string? CheckboxText { get; }
    public bool HasCheckbox => CheckboxText != null;
    public IReadOnlyList<DialogButton> Buttons { get; }
    public DialogTone Tone { get; }
    public bool IsDanger => Tone == DialogTone.Danger;
    public bool IsWarning => Tone == DialogTone.Warning;
    public double DialogWidth { get; init; } = 480;

    [ObservableProperty] private bool _isChecked;

    public string? Result { get; private set; }

    public event EventHandler? Chosen;

    public ConfirmDialogViewModel(
        string title,
        string message,
        IReadOnlyList<string> items,
        string? hint,
        string? checkboxText,
        IReadOnlyList<DialogButton> buttons,
        DialogTone tone)
    {
        Title = title;
        Message = message;
        Items = items;
        Hint = hint;
        CheckboxText = checkboxText;
        Buttons = buttons;
        Tone = tone;
    }

    [RelayCommand]
    private void Choose(DialogButton? button)
    {
        if (button is null) return;
        Result = button.Result;
        Chosen?.Invoke(this, EventArgs.Empty);
    }

    public static ConfirmDialogViewModel Uninstall(Loc loc, string packName, int fileCount = 128)
    {
        return new ConfirmDialogViewModel(
            loc.T("Ui.UninstallDialog.Title"),
            loc.T("Ui.UninstallDialog.Message"),
            [
                loc.T("Ui.UninstallDialog.PackFiles", fileCount),
                loc.T("Ui.UninstallDialog.MelonLoader"),
                loc.T("Ui.UninstallDialog.Proxies", "version.dll"),
            ],
            loc.T("Ui.UninstallDialog.Hint", packName),
            loc.T("Ui.UninstallDialog.RemoveUserData"),
            [
                new DialogButton(loc.T("Ui.Cancel"), "no", DialogButtonKind.Secondary),
                new DialogButton(loc.T("Ui.UninstallDialog.Confirm"), "yes", DialogButtonKind.Danger),
            ],
            DialogTone.Danger);
    }

    public static ConfirmDialogViewModel CloseWhileBusy(Loc loc)
    {
        return new ConfirmDialogViewModel(
            loc.T("Ui.CloseBusy.Title"),
            loc.T("Ui.CloseBusy.Message"),
            Array.Empty<string>(),
            null,
            null,
            [
                new DialogButton(loc.T("Ui.CloseBusy.Wait"), "no", DialogButtonKind.Secondary),
                new DialogButton(loc.T("Ui.CloseBusy.Abort"), "yes", DialogButtonKind.Danger),
            ],
            DialogTone.Warning);
    }
}
