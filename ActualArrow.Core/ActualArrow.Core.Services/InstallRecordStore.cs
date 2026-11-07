using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ActualArrow.Core.Abstractions;
using ActualArrow.Core.Models;
using ActualArrow.Core.Services.Install;

namespace ActualArrow.Core.Services;

public sealed class InstallRecordStore : IInstallRecordStore
{
	private const long MaxRecordBytes = 16777216L;

	internal static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		AllowTrailingCommas = true,
		ReadCommentHandling = JsonCommentHandling.Skip
	};

	private readonly ILog _log;

	public InstallRecordStore(ILog log)
	{
		_log = log;
	}

	public string GetPath(string gameDir)
	{
		return Path.Combine(Path.GetFullPath(gameDir), "UserData/ActualArrow/install.json".Replace('/', Path.DirectorySeparatorChar));
	}

	public async Task<InstallRecord?> ReadAsync(string gameDir, CancellationToken ct = default(CancellationToken))
	{
		string path = GetPath(gameDir);
		var (installRecord, ex) = await ReadCoreAsync(path, ct).ConfigureAwait(continueOnCapturedContext: false);
		if (ex != null)
		{
			_log.Warn("Install record unreadable, treated as absent: " + path, ex);
		}
		else if ((object)installRecord != null && installRecord.Schema > 1)
		{
			_log.Warn($"Install record schema {installRecord.Schema} is newer than {1}; reading what is known.");
		}
		return installRecord;
	}

	public async Task WriteAsync(string gameDir, InstallRecord record, CancellationToken ct = default(CancellationToken))
	{
		ct.ThrowIfCancellationRequested();
		string path = GetPath(gameDir);
		if (File.Exists(path))
		{
			Exception item = (await ReadCoreAsync(path, ct).ConfigureAwait(continueOnCapturedContext: false)).Item2;
			if (item != null)
			{
				_log.Error("Install record exists but cannot be read; not overwriting it: " + path, item);
				throw new InstallException(InstallFailure.RecordUnreadable, "Cannot read " + path + ": " + item.Message, item);
			}
		}
		byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(record with
		{
			Schema = 1
		}, JsonOptions);
		await Task.Run(() =>
		{
			FsOps.WriteAllBytesAtomic(path, bytes);
		}, ct).ConfigureAwait(continueOnCapturedContext: false);
		_log.Debug("Install record written: " + path);
	}

	private static async Task<(InstallRecord? Record, Exception? Problem)> ReadCoreAsync(string path, CancellationToken ct)
	{
		if (!File.Exists(path))
		{
			return (Record: null, Problem: null);
		}
		try
		{
			if (new FileInfo(path).Length > 16777216)
			{
				return (Record: null, Problem: new InvalidDataException($"Install record is larger than {16777216L} bytes"));
			}
			(InstallRecord?, Exception?) result;
			await using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
			{
				InstallRecord installRecord = await JsonSerializer.DeserializeAsync<InstallRecord>((Stream)fs, JsonOptions, ct).ConfigureAwait(continueOnCapturedContext: false);
				result = (((object)installRecord == null) ? null : Normalise(installRecord), null);
			}
			return result;
		}
		catch (Exception ex) when ((ex is JsonException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException) ? true : false)
		{
			return (Record: null, Problem: ex);
		}
	}

	private static InstallRecord Normalise(InstallRecord r)
	{
		return r with
		{
			Pack = (((object)r.Pack == null) ? null : r.Pack with
			{
				Manifest = (r.Pack.Manifest ?? PackManifest.Empty),
				Files = (r.Pack.Files ?? Array.Empty<InstalledFile>()).Where((InstalledFile f) => (object)f != null && f.Path != null).ToList(),
				CreatedUserDataFolders = (r.Pack.CreatedUserDataFolders ?? Array.Empty<string>()).Where((string f) => f != null).ToList()
			}),
			Loader = (((object)r.Loader == null) ? null : r.Loader with
			{
				Items = (r.Loader.Items ?? Array.Empty<string>()).Where((string i) => i != null).ToList()
			})
		};
	}
}
