using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ConceptApp.Services;

public sealed class Loc : INotifyPropertyChanged
{
	private string _lang = "ru";

	public static readonly IReadOnlyList<string> Supported =
		["ru", "en", "uk", "de", "pl", "zh"];

	// Each entry: ru, en, uk, de, pl, zh
	private static readonly Dictionary<string, string[]> Map = new()
	{
		["Brand.Sub"] = ["Mod Launcher", "Mod Launcher", "Mod Launcher", "Mod Launcher", "Mod Launcher", "Mod Launcher"],
		["Ui.Modpack"] = ["МОДПАК", "MODPACK", "МОДПАК", "MODPACK", "MODPACK", "模组包"],
		["Ui.Play"] = ["▶  ИГРАТЬ", "▶  PLAY", "▶  ГРАТИ", "▶  SPIELEN", "▶  GRAJ", "▶  开始游戏"],
		["Ui.PlayShort"] = ["Играть", "Play", "Грати", "Spielen", "Graj", "开始"],
		["Ui.Starting"] = ["Запуск…", "Starting…", "Запуск…", "Start…", "Uruchamianie…", "启动中…"],
		["Ui.Vanilla"] = ["Играть без модов", "Play vanilla with no mods", "Грати без модів", "Ohne Mods spielen", "Graj bez modów", "无模组启动"],
		["Ui.VanillaShort"] = ["Ваниль", "Vanilla", "Ваніль", "Vanilla", "Vanilla", "原版"],
		["Ui.Settings"] = ["Настройки", "Settings", "Налаштування", "Einstellungen", "Ustawienia", "设置"],
		["Ui.Cancel"] = ["Отмена", "Cancel", "Скасувати", "Abbrechen", "Anuluj", "取消"],
		["Ui.Ready"] = ["Готов к запуску", "Ready to launch", "Готовий до запуску", "Bereit zum Start", "Gotowy do uruchomienia", "准备启动"],
		["Ui.Branch"] = ["Ветка", "Branch", "Гілка", "Zweig", "Gałąź", "分支"],
		["Ui.UpToDate"] = ["Актуально", "Up to date", "Актуально", "Aktuell", "Aktualne", "已是最新"],
		["Ui.UpdateAvailable"] = ["Есть обновление {0}", "Update available {0}", "Є оновлення {0}", "Update verfügbar {0}", "Dostępna aktualizacja {0}", "有可用更新 {0}"],
		["Ui.Checking"] = ["Проверка…", "Checking…", "Перевірка…", "Prüfen…", "Sprawdzanie…", "检查中…"],
		["Ui.Updating"] = ["Обновление до {0}…", "Updating to {0}…", "Оновлення до {0}…", "Aktualisiere auf {0}…", "Aktualizacja do {0}…", "正在更新到 {0}…"],
		["Ui.Applying"] = ["Установка модов…", "Applying mods…", "Встановлення модів…", "Mods werden angewendet…", "Instalowanie modów…", "正在安装模组…"],
		["Ui.Launching"] = ["Запуск игры…", "Launching…", "Запуск гри…", "Spiel wird gestartet…", "Uruchamianie gry…", "正在启动游戏…"],
		["Ui.Launched"] = ["Запущено", "Launched", "Запущено", "Gestartet", "Uruchomiono", "已启动"],
		["Ui.Cancelled"] = ["Отменено", "Cancelled", "Скасовано", "Abgebrochen", "Anulowano", "已取消"],
		["Ui.LayoutB"] = ["Полный", "Full", "Повний", "Voll", "Pełny", "完整"],
		["Ui.LayoutD"] = ["Минимальный", "Minimal", "Мінімальний", "Minimal", "Minimalny", "精简"],
		["Settings.Layout"] = ["ВАРИАНТ ИНТЕРФЕЙСА", "INTERFACE VARIANT", "ВАРІАНТ ІНТЕРФЕЙСУ", "OBERFLÄCHENVARIANTE", "WARIANT INTERFEJSU", "界面方案"],
		["Status.UpdateThenPlay"] = ["Сначала обновлю пак, потом запущу", "Will update pack, then launch", "Спочатку оновлю пак, потім запущу", "Zuerst Pack aktualisieren, dann starten", "Najpierw zaktualizuję paczkę, potem uruchomię", "先更新模组包再启动"],
		["Settings.Title"] = ["Настройки", "Settings", "Налаштування", "Einstellungen", "Ustawienia", "设置"],
		["Settings.GameDir"] = ["ПАПКА ИГРЫ", "GAME FOLDER", "ПАПКА ГРИ", "SPIELORDNER", "FOLDER GRY", "游戏文件夹"],
		["Settings.Browse"] = ["Обзор…", "Browse…", "Огляд…", "Durchsuchen…", "Przeglądaj…", "浏览…"],
		["Settings.AutoDetect"] = ["Авто", "Auto", "Авто", "Auto", "Auto", "自动"],
		["Settings.GameDirAuto"] = ["Определяется автоматически через Steam", "Detected automatically via Steam", "Визначається автоматично через Steam", "Wird automatisch über Steam erkannt", "Wykrywane automatycznie przez Steam", "通过 Steam 自动检测"],
		["Settings.GameDirManual"] = ["Задан вручную", "Set manually", "Задано вручну", "Manuell gesetzt", "Ustawione ręcznie", "手动设置"],
		["Settings.PathPicked"] = ["Путь выбран", "Path selected", "Шлях вибрано", "Pfad gewählt", "Ścieżka wybrana", "已选择路径"],
		["Settings.Detecting"] = ["Ищем папку игры через Steam…", "Detecting game folder via Steam…", "Шукаємо папку гри через Steam…", "Spielordner wird über Steam gesucht…", "Wykrywanie folderu gry przez Steam…", "正在通过 Steam 检测游戏文件夹…"],
		["Settings.Detected"] = ["Найдено: {0}", "Found: {0}", "Знайдено: {0}", "Gefunden: {0}", "Znaleziono: {0}", "找到：{0}"],
		["Settings.Repo"] = ["РЕПОЗИТОРИЙ ПАКА", "PACK REPOSITORY", "РЕПОЗИТОРІЙ ПАКА", "PACK-REPOSITORY", "REPOZYTORIUM PACZKI", "模组包仓库"],
		["Settings.Language"] = ["ЯЗЫК", "LANGUAGE", "МОВА", "SPRACHE", "JĘZYK", "语言"],
		["Settings.CloseOnLaunch"] = ["Закрывать лаунчер после запуска игры", "Close launcher after game start", "Закривати лаунчер після запуску гри", "Launcher nach Spielstart schließen", "Zamknij launcher po starcie gry", "游戏启动后关闭启动器"],
		["Settings.Save"] = ["Сохранить", "Save", "Зберегти", "Speichern", "Zapisz", "保存"],
		["Settings.Saved"] = ["Сохранено", "Saved", "Збережено", "Gespeichert", "Zapisano", "已保存"],
		["Settings.DangerZone"] = ["ОПАСНАЯ ЗОНА", "DANGER ZONE", "НЕБЕЗПЕЧНА ЗОНА", "GEFAHRENZONE", "STREFA NIEBEZPIECZNA", "危险操作"],
		["Settings.Uninstall"] = ["Удалить мод", "Uninstall mod", "Видалити мод", "Mod deinstallieren", "Odinstaluj moda", "卸载模组"],
		["Settings.UninstallHint"] = [
			"Удалит файлы пака, MelonLoader и version.dll из папки игры",
			"Removes pack files, MelonLoader and version.dll from the game folder",
			"Видалить файли пака, MelonLoader і version.dll з папки гри",
			"Entfernt Pack-Dateien, MelonLoader und version.dll aus dem Spielordner",
			"Usuwa pliki paczki, MelonLoader i version.dll z folderu gry",
			"将从游戏文件夹中删除模组包、MelonLoader 和 version.dll"
		],
		["Settings.Uninstalled"] = ["Мод удалён", "Mod uninstalled", "Мод видалено", "Mod deinstalliert", "Mod odinstalowany", "模组已卸载"],
		["Ui.UninstallDialog.Title"] = ["Удалить мод?", "Uninstall mod?", "Видалити мод?", "Mod deinstallieren?", "Odinstalować moda?", "卸载模组？"],
		["Ui.UninstallDialog.Message"] = [
			"Будут удалены установленные файлы модпака и связанные компоненты.",
			"Installed pack files and related components will be removed.",
			"Будуть видалені встановлені файли модпака та пов’язані компоненти.",
			"Installierte Pack-Dateien und zugehörige Komponenten werden entfernt.",
			"Zainstalowane pliki paczki i powiązane składniki zostaną usunięte.",
			"将删除已安装的模组包文件及相关组件。"
		],
		["Ui.UninstallDialog.PackFiles"] = ["Файлы пака: {0}", "Pack files: {0}", "Файли пака: {0}", "Pack-Dateien: {0}", "Pliki paczki: {0}", "模组包文件：{0}"],
		["Ui.UninstallDialog.MelonLoader"] = ["MelonLoader", "MelonLoader", "MelonLoader", "MelonLoader", "MelonLoader", "MelonLoader"],
		["Ui.UninstallDialog.Proxies"] = ["Прокси: {0}", "Proxies: {0}", "Проксі: {0}", "Proxys: {0}", "Proxy: {0}", "代理：{0}"],
		["Ui.UninstallDialog.Hint"] = ["Пак: {0}", "Pack: {0}", "Пак: {0}", "Pack: {0}", "Paczka: {0}", "模组包：{0}"],
		["Ui.UninstallDialog.RemoveUserData"] = [
			"Также удалить UserData (настройки модов)",
			"Also remove UserData (mod settings)",
			"Також видалити UserData (налаштування модів)",
			"Auch UserData entfernen (Mod-Einstellungen)",
			"Usuń także UserData (ustawienia modów)",
			"同时删除 UserData（模组设置）"
		],
		["Ui.UninstallDialog.Confirm"] = ["Удалить", "Uninstall", "Видалити", "Deinstallieren", "Odinstaluj", "卸载"],
		["Ui.CloseBusy.Title"] = ["Идёт операция", "Operation in progress", "Триває операція", "Vorgang läuft", "Operacja w toku", "操作进行中"],
		["Ui.CloseBusy.Message"] = [
			"Лаунчер сейчас занят. Прервать и закрыть?",
			"The launcher is busy. Abort and close?",
			"Лаунчер зараз зайнятий. Перервати і закрити?",
			"Der Launcher ist beschäftigt. Abbrechen und schließen?",
			"Launcher jest zajęty. Przerwać i zamknąć?",
			"启动器正忙。是否中止并关闭？"
		],
		["Ui.CloseBusy.Wait"] = ["Подождать", "Wait", "Зачекати", "Warten", "Czekaj", "等待"],
		["Ui.CloseBusy.Abort"] = ["Прервать", "Abort", "Перервати", "Abbrechen", "Przerwij", "中止"],
		["Status.Uninstalling"] = ["Удаление…", "Uninstalling…", "Видалення…", "Deinstallation…", "Odinstalowywanie…", "正在卸载…"],
		["Settings.PickerTitle"] = ["Папка Broken Arrow", "Broken Arrow folder", "Папка Broken Arrow", "Broken-Arrow-Ordner", "Folder Broken Arrow", "Broken Arrow 文件夹"],
		["Pack.NotInstalled"] = ["Не установлена", "Not installed", "Не встановлено", "Nicht installiert", "Nie zainstalowano", "未安装"],
		["Ui.ForeignMods.Title"] = ["Чужие моды", "Other mods", "Чужі моди", "Fremde Mods", "Inne mody", "其他模组"],
		["Ui.ForeignMods.Message"] = [
			"В папке Mods есть файлы не из этого пака. Сделать их резервную копию и продолжить?",
			"The Mods folder has files not from this pack. Back them up and continue?",
			"У папці Mods є файли не з цього пака. Зробити їхню резервну копію і продовжити?",
			"Im Mods-Ordner liegen Dateien, die nicht zu diesem Pack gehören. Sichern und fortfahren?",
			"W folderze Mods są pliki spoza tej paczki. Utworzyć kopię zapasową i kontynuować?",
			"Mods 文件夹中有不属于此模组包的文件。是否备份后继续？"
		],
		["Ui.ForeignMods.Confirm"] = ["Продолжить", "Continue", "Продовжити", "Fortfahren", "Kontynuuj", "继续"],
		["Ui.UnrecordedMl.Title"] = ["MelonLoader уже есть", "MelonLoader already present", "MelonLoader уже є", "MelonLoader bereits vorhanden", "MelonLoader już obecny", "已存在 MelonLoader"],
		["Ui.UnrecordedMl.Message"] = [
			"В папке игры уже есть MelonLoader, который ставил не этот лаунчер. Заменить его версией, нужной сборке?",
			"The game folder already has a MelonLoader that this launcher did not install. Replace it?",
			"У папці гри вже є MelonLoader, який ставив не цей лаунчер. Замінити його версією, потрібною збірці?",
			"Im Spielordner ist bereits ein MelonLoader, den dieser Launcher nicht installiert hat. Ersetzen?",
			"W folderze gry jest już MelonLoader, którego nie zainstalował ten launcher. Zastąpić?",
			"游戏文件夹中已有非本启动器安装的 MelonLoader。是否替换为所需版本？"
		],
		["Ui.UnrecordedMl.Replace"] = ["Заменить", "Replace", "Замінити", "Ersetzen", "Zastąp", "替换"],
		["Ui.Help.Title"] = ["Моды не запустились", "Mods did not start", "Моди не запустились", "Mods starten nicht", "Mody się nie uruchomiły", "模组未能启动"],
		["Ui.Help.Message"] = [
			"Игра закрылась, а MelonLoader не оставил лог. Обычно мешает одно из этого:",
			"The game closed and MelonLoader left no log. Usually one of these is in the way:",
			"Гра закрилась, а MelonLoader не залишив лог. Зазвичай заважає одне з цього:",
			"Das Spiel wurde beendet und MelonLoader hat kein Log hinterlassen. Meist stört eines davon:",
			"Gra się zamknęła, a MelonLoader nie zostawił logu. Zwykle przeszkadza jedno z tego:",
			"游戏已关闭，且 MelonLoader 未留下日志。通常是以下原因之一："
		],
		["Ui.Help.Antivirus"] = [
			"антивирус удалил или заблокировал version.dll",
			"antivirus removed or blocked version.dll",
			"антивірус видалив або заблокував version.dll",
			"Antivirus hat version.dll entfernt oder blockiert",
			"antywirus usunął lub zablokował version.dll",
			"杀毒软件删除或拦截了 version.dll"
		],
		["Ui.Help.SmartAppControl"] = [
			"Smart App Control в Windows не даёт загрузить неподписанные dll",
			"Smart App Control blocks unsigned DLLs",
			"Smart App Control у Windows не дає завантажити непідписані dll",
			"Smart App Control blockiert unsignierte DLLs",
			"Smart App Control blokuje niepodpisane DLL",
			"Windows Smart App Control 阻止加载未签名 DLL"
		],
		["Ui.Help.OpenLog"] = ["Открыть лог", "Open log", "Відкрити лог", "Log öffnen", "Otwórz log", "打开日志"],
		["Ui.Help.Reinstall"] = ["Переустановить MelonLoader", "Reinstall MelonLoader", "Перевстановити MelonLoader", "MelonLoader neu installieren", "Zainstaluj ponownie MelonLoader", "重装 MelonLoader"],
		["Status.Ready"] = ["Готов", "Ready", "Готово", "Bereit", "Gotowy", "就绪"],
		["Status.DownloadingMelonLoader"] = ["Скачивание MelonLoader…", "Downloading MelonLoader…", "Завантаження MelonLoader…", "MelonLoader wird heruntergeladen…", "Pobieranie MelonLoader…", "正在下载 MelonLoader…"],
		["Status.InstallingMelonLoader"] = ["Установка MelonLoader…", "Installing MelonLoader…", "Встановлення MelonLoader…", "MelonLoader wird installiert…", "Instalowanie MelonLoader…", "正在安装 MelonLoader…"],
		["Status.DownloadingPack"] = ["Скачивание сборки…", "Downloading pack…", "Завантаження збірки…", "Pack wird heruntergeladen…", "Pobieranie paczki…", "正在下载模组包…"],
		["Status.InstallingPack"] = ["Установка сборки…", "Installing pack…", "Встановлення збірки…", "Pack wird installiert…", "Instalowanie paczki…", "正在安装模组包…"],
		["Status.Launching"] = ["Запуск игры…", "Starting the game…", "Запуск гри…", "Spiel wird gestartet…", "Uruchamianie gry…", "正在启动游戏…"],
		["Status.LaunchedModded"] = ["Игра запущена, ждём ответа модов…", "Game started, waiting for the mods…", "Гру запущено, чекаємо відповіді модів…", "Spiel gestartet, warte auf Mods…", "Gra uruchomiona, czekam na mody…", "游戏已启动，等待模组响应…"],
		["Status.LaunchedVanilla"] = ["Игра запущена без модов", "Game started without mods", "Гру запущено без модів", "Spiel ohne Mods gestartet", "Gra uruchomiona bez modów", "游戏已无模组启动"],
		["Status.WaitingForSteam"] = ["Ждём Steam… {0} с", "Waiting for Steam… {0}s", "Чекаємо Steam… {0} с", "Warte auf Steam… {0}s", "Czekam na Steam… {0}s", "等待 Steam… {0} 秒"],
		["Status.Installed"] = ["Сборка {0} установлена", "Pack {0} installed", "Збірку {0} встановлено", "Pack {0} installiert", "Paczka {0} zainstalowana", "模组包 {0} 已安装"],
		["Status.Uninstalled"] = ["Мод удалён", "Mod uninstalled", "Мод видалено", "Mod deinstalliert", "Mod odinstalowany", "模组已卸载"],
		["Status.Cancelled"] = ["Отменено", "Cancelled", "Скасовано", "Abgebrochen", "Anulowano", "已取消"],
		["Status.CloseGame"] = ["Закройте игру", "Close the game", "Закрийте гру", "Spiel schließen", "Zamknij grę", "请关闭游戏"],
		["Notice.GameNotFound"] = ["Папка игры не найдена", "Game folder not found", "Папку гри не знайдено", "Spielordner nicht gefunden", "Nie znaleziono folderu gry", "未找到游戏文件夹"],
		["Error.GameDirInvalid"] = [
			"Папка игры не найдена или в ней нет BrokenArrow.exe",
			"Game folder not found or it has no BrokenArrow.exe",
			"Папку гри не знайдено або в ній немає BrokenArrow.exe",
			"Spielordner nicht gefunden oder ohne BrokenArrow.exe",
			"Nie znaleziono folderu gry lub brak BrokenArrow.exe",
			"未找到游戏文件夹，或不存在 BrokenArrow.exe"
		],
		["Error.InstallFailed"] = ["Установка не удалась, изменения отменены", "Install failed, changes were rolled back", "Встановлення не вдалося, зміни скасовано", "Installation fehlgeschlagen, Änderungen rückgängig", "Instalacja nieudana, zmiany cofnięte", "安装失败，已回滚更改"],
		["Error.MelonLoaderFailed"] = ["Не удалось установить MelonLoader", "Could not install MelonLoader", "Не вдалося встановити MelonLoader", "MelonLoader konnte nicht installiert werden", "Nie udało się zainstalować MelonLoader", "无法安装 MelonLoader"],
		["Error.LaunchFailed"] = ["Не удалось запустить игру: {0}", "Could not start the game: {0}", "Не вдалося запустити гру: {0}", "Spiel konnte nicht gestartet werden: {0}", "Nie udało się uruchomić gry: {0}", "无法启动游戏：{0}"],
		["Error.SteamNotStarted"] = [
			"Steam не запустился. Откройте Steam, войдите в аккаунт и нажмите «Играть» ещё раз",
			"Steam did not start. Open Steam, sign in and press Play again",
			"Steam не запустився. Відкрийте Steam, увійдіть і натисніть «Грати» ще раз",
			"Steam ist nicht gestartet. Steam öffnen, anmelden und erneut auf Spielen tippen",
			"Steam się nie uruchomił. Otwórz Steam, zaloguj się i naciśnij Grają jeszcze raz",
			"Steam 未能启动。请打开 Steam、登录后再次点击开始"
		],
		["Error.Offline"] = ["Нет сети", "Offline", "Немає мережі", "Offline", "Brak sieci", "离线"],
		["Error.RateLimited"] = ["GitHub ограничил запросы, подождите", "GitHub rate limited the requests, wait a bit", "GitHub обмежив запити, зачекайте", "GitHub hat Anfragen begrenzt, bitte warten", "GitHub ograniczył zapytania, poczekaj", "GitHub 限制了请求，请稍候"],
		["Error.NoPackAsset"] = [
			"У релиза нет файла сборки (.bamod или .zip)",
			"The release has no pack file (.bamod or .zip)",
			"У релізу немає файла збірки (.bamod або .zip)",
			"Das Release hat keine Pack-Datei (.bamod oder .zip)",
			"Wydanie nie ma pliku paczki (.bamod lub .zip)",
			"该版本没有模组包文件（.bamod 或 .zip）"
		],
		["Play.NeedsGameDir"] = ["укажите папку игры в настройках", "set the game folder in Settings", "вкажіть папку гри в налаштуваннях", "Spielordner in den Einstellungen angeben", "ustaw folder gry w Ustawieniach", "请在设置中指定游戏文件夹"],
		["Play.AllReady"] = ["Готово к запуску", "Ready to play", "Готово до запуску", "Bereit zum Spielen", "Gotowe do gry", "可以开始游戏"],
		["Play.WillDownload"] = ["Скачаю сборку ({0})", "Will download the pack ({0})", "Завантажу збірку ({0})", "Pack wird heruntergeladen ({0})", "Pobiorę paczkę ({0})", "将下载模组包（{0}）"],
		["Play.UpdateTo"] = ["Обновлю до {0}", "Will update to {0}", "Оновлю до {0}", "Aktualisiere auf {0}", "Zaktualizuję do {0}", "将更新到 {0}"],
		["Play.Unavailable"] = ["Запуск недоступен", "Play unavailable", "Запуск недоступний", "Start nicht verfügbar", "Uruchomienie niedostępne", "无法启动"],
	};

	private static readonly Dictionary<string, int> LangIndex = new(StringComparer.OrdinalIgnoreCase)
	{
		["ru"] = 0,
		["en"] = 1,
		["uk"] = 2,
		["de"] = 3,
		["pl"] = 4,
		["zh"] = 5,
	};

	public string Lang
	{
		get => _lang;
		set
		{
			var next = Normalize(value);
			if (_lang == next) return;
			_lang = next;
			OnPropertyChanged();
			OnPropertyChanged(nameof(IsRu));
			OnPropertyChanged(nameof(IsEn));
			OnPropertyChanged("Item[]");
		}
	}

	public bool IsRu => _lang == "ru";
	public bool IsEn => _lang == "en";

	public string this[string key] => T(key);

	public event PropertyChangedEventHandler? PropertyChanged;

	public static string Normalize(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return "ru";
		var trimmed = value.Trim();
		return LangIndex.ContainsKey(trimmed) ? trimmed.ToLowerInvariant() : "ru";
	}

	/// <summary>Core Brand.Languages only accepts ru/en — map UI locale for persistence there.</summary>
	public static string ToCoreLanguage(string? uiLang)
	{
		var lang = Normalize(uiLang);
		return lang is "en" or "ru" ? lang : "en";
	}

	public string T(string key, params object[] args)
	{
		if (!Map.TryGetValue(key, out var variants))
			return key;

		var idx = LangIndex.TryGetValue(_lang, out var i) ? i : 0;
		var text = variants[idx];
		if (string.IsNullOrEmpty(text))
			text = variants[1]; // en fallback
		if (string.IsNullOrEmpty(text))
			text = variants[0]; // ru fallback

		return args.Length == 0 ? text : string.Format(text, args);
	}

	public void Toggle() => Lang = IsRu ? "en" : "ru";

	private void OnPropertyChanged([CallerMemberName] string? name = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
