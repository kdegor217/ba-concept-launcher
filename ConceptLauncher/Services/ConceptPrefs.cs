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

	public ConceptLayout Layout { get; set; } = CompileTimeDefault;
	public string UiLanguage { get; set; } = "ru";
	/// <summary>Stamp so switching between E/G/H release builds resets the default layout.</summary>
	public string BuiltVariant { get; set; } = CompileTimeVariant;

	public static ConceptLayout CompileTimeDefault =>
#if LAYOUT_E
		ConceptLayout.E;
#elif LAYOUT_G
		ConceptLayout.G;
#elif LAYOUT_H
		ConceptLayout.H;
#else
		ConceptLayout.B;
#endif

	public static string CompileTimeVariant =>
#if LAYOUT_E
		"E";
#elif LAYOUT_G
		"G";
#elif LAYOUT_H
		"H";
#else
		"";
#endif

	public static string FilePath =>
		Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			Brand.AppDataFolderName,
			"concept-prefs.json");

	public static ConceptPrefs Load()
	{
		ConceptPrefs prefs;
		try
		{
			if (!File.Exists(FilePath))
				prefs = new ConceptPrefs();
			else
			{
				var json = File.ReadAllText(FilePath);
				prefs = JsonSerializer.Deserialize<ConceptPrefs>(json, JsonOptions) ?? new ConceptPrefs();
			}
		}
		catch
		{
			prefs = new ConceptPrefs();
		}

		// Fixed variant builds always open on their concept when the stamp differs.
		if (!string.IsNullOrEmpty(CompileTimeVariant) &&
		    !string.Equals(prefs.BuiltVariant, CompileTimeVariant, StringComparison.Ordinal))
		{
			prefs.Layout = CompileTimeDefault;
			prefs.BuiltVariant = CompileTimeVariant;
			prefs.Save();
		}

		return prefs;
	}

	public void Save()
	{
		try
		{
			if (!string.IsNullOrEmpty(CompileTimeVariant))
				BuiltVariant = CompileTimeVariant;
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
			File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
		}
		catch
		{
			/* ignore */
		}
	}
}
