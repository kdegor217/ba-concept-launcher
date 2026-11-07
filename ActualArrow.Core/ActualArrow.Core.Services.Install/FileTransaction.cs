using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ActualArrow.Core.Abstractions;

namespace ActualArrow.Core.Services.Install;

internal sealed class FileTransaction
{
	private abstract record Step;

	private sealed record Moved(string From, string To, bool IsDirectory) : Step;

	private sealed record CreatedDirectory(string Path) : Step;

	private readonly BackupFolders _backups;

	private readonly string? _trashParent;

	private readonly ILog _log;

	private readonly Action<string>? _fault;

	private readonly List<Step> _steps = new List<Step>();

	private string? _backupDir;

	private string? _trashDir;

	private int _trashCounter;

	private bool _damaged;

	public string? BackupDir => _backupDir;

	public int RemovedCount { get; private set; }

	public FileTransaction(BackupFolders backups, string? trashParent, ILog log, Action<string>? fault)
	{
		_backups = backups;
		_trashParent = trashParent;
		_log = log;
		_fault = fault;
	}

	public void Checkpoint(string what)
	{
		_fault?.Invoke(what);
	}

	public void Backup(string fullPath, string backupRelative, bool isDirectory)
	{
		if (_backupDir == null)
		{
			_backupDir = _backups.CreateSession();
		}
		string to = PackPaths.ResolveInside(_backupDir, backupRelative);
		Move(fullPath, to, isDirectory, "backup");
		RemovedCount++;
	}

	public void Trash(string fullPath, bool isDirectory)
	{
		if (_trashParent == null)
		{
			throw new InvalidOperationException("This transaction has no trash folder.");
		}
		if (_trashDir == null)
		{
			_trashDir = Path.Combine(_trashParent, "trash-" + Guid.NewGuid().ToString("N").Substring(0, 12));
		}
		string to = Path.Combine(_trashDir, _trashCounter++.ToString(CultureInfo.InvariantCulture), Path.GetFileName(fullPath));
		Move(fullPath, to, isDirectory, "trash");
		RemovedCount++;
	}

	public void Place(string staged, string target, bool isDirectory)
	{
		string directoryName = Path.GetDirectoryName(target);
		if (!string.IsNullOrEmpty(directoryName))
		{
			EnsureDirectory(directoryName);
		}
		Move(staged, target, isDirectory, "place");
	}

	public void EnsureDirectory(string dir)
	{
		Stack<string> stack = new Stack<string>();
		string text = Path.TrimEndingDirectorySeparator(dir);
		while (!string.IsNullOrEmpty(text) && !Directory.Exists(text))
		{
			if (File.Exists(text))
			{
				throw new IOException("A file blocks the folder " + text);
			}
			stack.Push(text);
			text = Path.GetDirectoryName(text);
		}
		string result;
		while (stack.TryPop(out result))
		{
			_fault?.Invoke("mkdir:" + result);
			Directory.CreateDirectory(result);
			_steps.Add(new CreatedDirectory(result));
		}
	}

	private void Move(string from, string to, bool isDirectory, string kind)
	{
		_fault?.Invoke(kind + ":" + from);
		try
		{
			if (isDirectory)
			{
				FsOps.MoveDirectory(from, to);
			}
			else
			{
				FsOps.MoveFile(from, to);
			}
		}
		catch (IncompleteMoveException exception)
		{
			_damaged = true;
			_log.Error("Move left " + from + " incomplete; the full copy is at " + to, exception);
			throw;
		}
		_steps.Add(new Moved(from, to, isDirectory));
	}

	public bool Rollback()
	{
		bool flag = true;
		for (int num = _steps.Count - 1; num >= 0; num--)
		{
			try
			{
				Step step = _steps[num];
				if (!(step is Moved moved))
				{
					if (step is CreatedDirectory createdDirectory && Directory.Exists(createdDirectory.Path) && !Directory.EnumerateFileSystemEntries(createdDirectory.Path).Any())
					{
						Directory.Delete(createdDirectory.Path);
					}
				}
				else if (moved.IsDirectory)
				{
					FsOps.MoveDirectory(moved.To, moved.From);
				}
				else
				{
					Moved moved2 = moved;
					FsOps.MoveFile(moved2.To, moved2.From);
				}
			}
			catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
			{
				flag = false;
				_log.Error($"Rollback step failed: {_steps[num]}", ex);
			}
		}
		if (_damaged)
		{
			flag = false;
			_log.Error("Rollback incomplete: a folder move earlier in this transaction could not be undone.");
		}
		_steps.Clear();
		RemovedCount = 0;
		if (_backupDir != null && FsOps.DeleteIfNoFiles(_backupDir))
		{
			_backupDir = null;
		}
		if (_trashDir != null)
		{
			FsOps.DeleteIfNoFiles(_trashDir);
		}
		if (flag)
		{
			_log.Info("Rolled back.");
		}
		return flag;
	}

	public void Complete()
	{
		_steps.Clear();
		if (_trashDir != null && !FsOps.TryDeleteDirectory(_trashDir))
		{
			_log.Warn("Could not delete trash folder " + _trashDir);
		}
		if (_backupDir != null && FsOps.DeleteIfNoFiles(_backupDir))
		{
			_backupDir = null;
		}
	}
}
