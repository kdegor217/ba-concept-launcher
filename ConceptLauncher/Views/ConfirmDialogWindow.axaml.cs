using System;
using Avalonia.Controls;
using Avalonia.Media;
using ConceptApp.ViewModels;

namespace ConceptApp.Views;

public partial class ConfirmDialogWindow : Window
{
    private ConfirmDialogViewModel? _vm;

    public ConfirmDialogWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as ConfirmDialogViewModel);
    }

    private void Attach(ConfirmDialogViewModel? vm)
    {
        if (_vm is not null)
            _vm.Chosen -= OnChosen;

        _vm = vm;
        ButtonsHost.Children.Clear();

        if (_vm is null) return;

        _vm.Chosen += OnChosen;

        if (_vm.IsDanger)
            TitleBlock.Foreground = Brush.Parse("#E55B52");
        else if (_vm.IsWarning)
            TitleBlock.Foreground = Brush.Parse("#E2A23A");

        foreach (var btn in _vm.Buttons)
        {
            var button = new Button
            {
                Content = btn.Text,
                MinWidth = 110,
                MinHeight = 36,
                Padding = new Avalonia.Thickness(14, 6),
                Command = _vm.ChooseCommand,
                CommandParameter = btn,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            };

            switch (btn.Kind)
            {
                case DialogButtonKind.Primary:
                    button.Classes.Add("primary");
                    button.Classes.Add("compact");
                    break;
                case DialogButtonKind.Danger:
                    button.Classes.Add("danger");
                    break;
                default:
                    button.Classes.Add("ghost");
                    break;
            }

            ButtonsHost.Children.Add(button);
        }
    }

    private void OnChosen(object? sender, EventArgs e) => Close(_vm?.Result);
}
