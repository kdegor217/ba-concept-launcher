using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Abstractions;
using ActualArrow.Core.Models;
using ActualArrow.Core.Services.Basics;

namespace ActualArrow.Core.Services;

public sealed class SettingsStore : ISettingsStore
{
	public const string BadSuffix = ".bad";

	private static readonly Regex RepoRegex = new Regex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})/[A-Za-z0-9._-]{1,100}$", RegexOptions.CultureInvariant);

	private static readonly JsonSerializerOptions ReadOptions = CreateOptions(writeIndented: false);

	private static readonly JsonSerializerOptions WriteOptions = CreateOptions(writeIndented: true);

	private readonly IAppPaths _paths;

	private readonly ILog _log;

	private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

	private volatile LauncherSettings _current = new LauncherSettings();

	private volatile bool _loaded;

	public LauncherSettings Current => _current;

	public event EventHandler<LauncherSettings>? Changed;

	public SettingsStore(IAppPaths paths, ILog log)
	{
		_paths = paths ?? throw new ArgumentNullException("paths");
		_log = log ?? throw new ArgumentNullException("log");
	}

	public async Task<LauncherSettings> LoadAsync(CancellationToken ct = default(CancellationToken))
	{
		await _gate.WaitAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			return await LoadCoreAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			_gate.Release();
		}
	}

	public async Task<LauncherSettings> UpdateAsync(Func<LauncherSettings, LauncherSettings> change, CancellationToken ct = default(CancellationToken))
	{
		ArgumentNullException.ThrowIfNull(change, "change");
		await _gate.WaitAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
		LauncherSettings launcherSettings;
		try
		{
			if (!_loaded)
			{
				await LoadCoreAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
			}
			LauncherSettings next = change(_current) ?? throw new InvalidOperationException("The settings change returned null.");
			next = Sanitize(next, (string m) =>
			{
				_log.Warn("settings: " + m);
			});
			string text = JsonSerializer.Serialize(next, WriteOptions);
			await AtomicFile.WriteAllTextAsync(_paths.SettingsFile, text + "\n", ct).ConfigureAwait(continueOnCapturedContext: false);
			_current = next;
			launcherSettings = next;
		}
		finally
		{
			_gate.Release();
		}
		Changed?.Invoke(this, launcherSettings);
		return launcherSettings;
	}

	private async Task<LauncherSettings> LoadCoreAsync(CancellationToken ct)
	{
		string file = _paths.SettingsFile;
		string text = null;
		try
		{
			if (File.Exists(file))
			{
				text = await File.ReadAllTextAsync(file, ct).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			_log.Warn("settings: cannot read " + file + ", using defaults", ex);
			text = null;
		}
		LauncherSettings launcherSettings;
		if (text == null)
		{
			launcherSettings = new LauncherSettings();
		}
		else if (string.IsNullOrWhiteSpace(text))
		{
			_log.Warn("settings: " + file + " is empty, using defaults");
			launcherSettings = new LauncherSettings();
		}
		else
		{
			string value = null;
			LauncherSettings launcherSettings2;
			try
			{
				launcherSettings2 = JsonSerializer.Deserialize<LauncherSettings>(text, ReadOptions);
				if ((object)launcherSettings2 == null)
				{
					value = "it holds no object";
				}
			}
			catch (Exception ex2) when ((ex2 is JsonException || ex2 is NotSupportedException || ex2 is InvalidOperationException) ? true : false)
			{
				launcherSettings2 = null;
				value = ex2.Message;
			}
			if ((object)launcherSettings2 == null)
			{
				_log.Warn($"settings: {file} is corrupt ({value}); moving it to {Path.GetFileName(file)}{".bad"}, using defaults");
				QuarantineBadFile(file);
				launcherSettings = new LauncherSettings();
			}
			else
			{
				launcherSettings = Sanitize(launcherSettings2, (string m) =>
				{
					_log.Warn("settings: " + m);
				});
			}
		}
		_current = launcherSettings;
		_loaded = true;
		return launcherSettings;
	}

	private void QuarantineBadFile(string file)
	{
		try
		{
			File.Move(file, file + ".bad", overwrite: true);
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
			_log.Warn("settings: could not move the bad file aside", ex);
		}
	}

	internal static LauncherSettings Sanitize(LauncherSettings s, Action<string>? warn)
	{
		string text = s.Language?.Trim().ToLowerInvariant();
		if (text == null || !Brand.Languages.Contains(text))
		{
			if (s.Language != null)
			{
				warn?.Invoke($"unknown language '{s.Language}', using '{"ru"}'");
			}
			text = "ru";
		}
		string text2 = Blank(s.PackRepo)?.Trim();
		if (text2 != null && !RepoRegex.IsMatch(text2))
		{
			warn?.Invoke("ignoring invalid PackRepo '" + text2 + "'");
			text2 = null;
		}
		return s with
		{
			Language = text,
			GameDir = Blank(s.GameDir)?.Trim(),
			PackRepo = text2,
			LastEtag = Blank(s.LastEtag),
			LastReleaseJson = Blank(s.LastReleaseJson),
			LastReleaseRepo = Blank(s.LastReleaseRepo)
		};
	}

	private static string? Blank(string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			return value;
		}
		return null;
	}

	private static JsonSerializerOptions CreateOptions(bool writeIndented)
	{
		DefaultJsonTypeInfoResolver defaultJsonTypeInfoResolver = new DefaultJsonTypeInfoResolver();
		defaultJsonTypeInfoResolver.Modifiers.Add((JsonTypeInfo info) =>
		{
			if (info.Kind == JsonTypeInfoKind.Object)
			{
				for (int num = info.Properties.Count - 1; num >= 0; num--)
				{
					if (info.Properties[num].Set == null)
					{
						info.Properties.RemoveAt(num);
					}
				}
			}
		});
		return new JsonSerializerOptions
		{
			TypeInfoResolver = defaultJsonTypeInfoResolver,
			WriteIndented = writeIndented,
			PropertyNameCaseInsensitive = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
		};
	}
}
