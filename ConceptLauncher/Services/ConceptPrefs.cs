using System.Text.Json;
using ActualArrow.Core;
using ConceptApp.ViewModels;

namespace ConceptApp.Services;

public sealed class ConceptPrefs
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
	};

	public ConceptLayout Layout { get; set; } = ConceptLayout.B;
	public string UiLanguage { get; set; } = "ru";

	public static string FilePath =>
		Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			Brand.AppDataFolderName,
			"concept-prefs.json");

	public static ConceptPrefs Load()
	{
		try
		{
			if (!File.Exists(FilePath))
				return new ConceptPrefs();
			var json = File.ReadAllText(FilePath);
			return JsonSerializer.Deserialize<ConceptPrefs>(json, JsonOptions) ?? new ConceptPrefs();
		}
		catch
		{
			return new ConceptPrefs();
		}
	}

	public void Save()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
			File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
		}
		catch
		{
			/* ignore */
		}
	}
}
