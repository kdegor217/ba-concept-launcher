using System.Net;
using System.Net.Http;
using ActualArrow.Core;
using ActualArrow.Core.Abstractions;
using ActualArrow.Core.Models;
using ActualArrow.Core.Services;

namespace ConceptApp.Services;

public sealed class Composition : IDisposable
{
	private readonly HttpClient _http;
	private readonly HttpClient _downloadHttp;

	public IPackCatalog Catalog { get; }
	public CommandLineOptions Options { get; }
	public TimeProvider Time { get; }
	public IAppPaths Paths { get; }
	public ILog Log { get; }
	public ISettingsStore Settings { get; }
	public IGameLocator Locator { get; }
	public IReleaseFeed Feed { get; }
	public IDownloader Downloader { get; }
	public IInstallRecordStore Records { get; }
	public IProxyManager Proxies { get; }
	public IMelonLoaderInstaller MelonLoader { get; }
	public IPackInstaller Pack { get; }
	public IGameRunner Runner { get; }
	public IUserPrompts Prompts { get; }
	public ILauncherService Launcher { get; }

	private Composition(CommandLineOptions options, IUserPrompts prompts, HttpMessageHandler? handler, TimeProvider time)
	{
		Options = options;
		Time = time;
		_http = CreateHttpClient(handler, followRedirects: true);
		_downloadHttp = handler is null ? CreateHttpClient(null, followRedirects: false) : _http;
		Paths = new AppPaths(options.AppData);
		Log = new FileLog(Paths, time);
		Settings = new SettingsStore(Paths, Log);
		Locator = new GameLocator(Log);
		Feed = new GitHubReleaseFeed(_http, Settings, Log, time);
		Catalog = new PackCatalog(_http, Paths, Log, time);
		Downloader = new HttpDownloader(_downloadHttp, Log);
		Records = new InstallRecordStore(Log);
		Proxies = new ProxyManager(Paths, Log);
		Runner = new GameRunner(Log, options.DryRun, time, Locator.FindSteamRoots);
		MelonLoader = new MelonLoaderInstaller(Paths, Records, Downloader, Proxies, Log, time, null, Runner.IsGameRunning);
		Pack = new PackInstaller(Paths, Records, Proxies, Log, time, Runner.IsGameRunning);
		Prompts = prompts;
		Launcher = new LauncherService(options, Paths, Settings, Locator, Feed, Downloader, Records, Pack, MelonLoader, Proxies, Runner, prompts, Log, time);
	}

	public static Composition Create(CommandLineOptions options, IUserPrompts prompts, HttpMessageHandler? handler = null, TimeProvider? time = null)
		=> new(options, prompts, handler, time ?? TimeProvider.System);

	private static HttpClient CreateHttpClient(HttpMessageHandler? handler, bool followRedirects)
	{
		handler ??= new SocketsHttpHandler
		{
			AllowAutoRedirect = followRedirects,
			MaxAutomaticRedirections = 5,
			AutomaticDecompression = DecompressionMethods.All,
			ConnectTimeout = Brand.GitHub.CheckTimeout,
			UseCookies = false
		};

		var http = new HttpClient(handler, disposeHandler: true)
		{
			BaseAddress = new Uri(Brand.GitHub.ApiBase),
			Timeout = Timeout.InfiniteTimeSpan
		};
		http.DefaultRequestHeaders.UserAgent.ParseAdd(Brand.UserAgent);
		return http;
	}

	public void Dispose()
	{
		if (!ReferenceEquals(_downloadHttp, _http))
			_downloadHttp.Dispose();
		_http.Dispose();
	}
}
