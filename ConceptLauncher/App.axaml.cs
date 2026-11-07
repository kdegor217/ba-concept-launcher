using ActualArrow.Core.Models;
using ActualArrow.Core.Services;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ConceptApp.Services;
using ConceptApp.ViewModels;
using ConceptApp.Views;

namespace ConceptApp;

public partial class App : Application
{
	public Composition? Services { get; private set; }
	public Loc Loc { get; } = new();

	public override void Initialize() => AvaloniaXamlLoader.Load(this);

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			try
			{
				var options = CommandLineOptions.Parse(desktop.Args ?? Array.Empty<string>());
				var prompts = new AvaloniaUserPrompts(Loc, () => desktop.MainWindow);
				Services = Composition.Create(options, prompts);

				var vm = new MainViewModel(Services.Launcher, Services.Settings, Services.Catalog, Services.Locator, Loc);
				var window = new MainWindow { DataContext = vm };
				desktop.MainWindow = window;

				window.Opened += async (_, _) =>
				{
					try
					{
						await vm.InitializeAsync();
					}
					catch (Exception ex)
					{
						Services.Log.Error("Initialize failed", ex);
						vm.Status = Loc.T("Error.LaunchFailed", ex.Message);
					}
				};
				vm.CloseRequested += (_, _) => window.Close();

				desktop.Exit += (_, _) =>
				{
					try
					{
						Services.Launcher.StashLoaderIfIdleAsync().Wait(TimeSpan.FromSeconds(5));
					}
					catch
					{
						/* best effort */
					}

					vm.Dispose();
					Services.Dispose();
				};
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(ex);
				throw;
			}
		}

		base.OnFrameworkInitializationCompleted();
	}
}
