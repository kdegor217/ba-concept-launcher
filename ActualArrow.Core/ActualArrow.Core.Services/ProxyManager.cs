using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Abstractions;
using ActualArrow.Core.Models;
using ActualArrow.Core.Services.Install;

namespace ActualArrow.Core.Services;

public sealed class ProxyManager : IProxyManager
{
	public const string StashedSuffix = " (stashed)";

	internal const string StashedBackupFolder = "_stashed";

	private readonly IAppPaths _paths;

	private readonly ILog _log;

	public ProxyManager(IAppPaths paths, ILog log)
	{
		_paths = paths;
		_log = log;
	}

	public ProxyState GetState(string gameDir)
	{
		return new ProxyState((from p in ProxyFiles.Find(gameDir)
			select p.Name).ToList(), (from p in ProxyFiles.Find(_paths.StashDir)
			select p.Name).ToList());
	}

	public Task<IReadOnlyList<string>> StashAllAsync(string gameDir, CancellationToken ct = default(CancellationToken))
	{
		return Task.Run((Func<IReadOnlyList<string>>)(() =>
		{
			List<string> list = new List<string>();
			foreach (var item in ProxyFiles.Find(gameDir))
			{
				var (text, path) = item;
				ct.ThrowIfCancellationRequested();
				string dest = Path.Combine(_paths.StashDir, text);
				Wrap("stash " + text, () =>
				{
					FsOps.MoveFile(path, dest, overwrite: true);
				});
				_log.Info("Proxy stashed: " + text);
				list.Add(text);
			}
			return list;
		}), ct);
	}

	public Task<bool> RestoreMelonProxyAsync(string gameDir, CancellationToken ct = default(CancellationToken))
	{
		return Task.Run(() =>
		{
			string stashed = PackPaths.FindChild(_paths.StashDir, "version.dll", directory: false);
			if (stashed == null)
			{
				return false;
			}
			if (PackPaths.FindChild(gameDir, "version.dll", directory: false) != null)
			{
				_log.Info("Root already has version.dll; stashed copy left in place.");
				return false;
			}
			ct.ThrowIfCancellationRequested();
			Wrap("restore version.dll", () =>
			{
				FsOps.MoveFile(stashed, Path.Combine(gameDir, "version.dll"));
			});
			_log.Info("Proxy restored: version.dll");
			return true;
		}, ct);
	}

	public Task<bool> StashMelonProxyAsync(string gameDir, CancellationToken ct = default(CancellationToken))
	{
		return Task.Run(() =>
		{
			ct.ThrowIfCancellationRequested();
			string inRoot = PackPaths.FindChild(gameDir, "version.dll", directory: false);
			if (inRoot == null)
			{
				return false;
			}
			Wrap("stash version.dll", () =>
			{
				FsOps.MoveFile(inRoot, Path.Combine(_paths.StashDir, "version.dll"), overwrite: true);
			});
			_log.Info("Proxy stashed while idle: version.dll");
			return true;
		}, ct);
	}

	private static void Wrap(string what, Action action)
	{
		try
		{
			action();
		}
		catch (UnauthorizedAccessException ex)
		{
			throw new IOException("Cannot " + what + ": " + ex.Message, ex);
		}
	}
}
