# BA Concept Launcher / Концепт-лаунчер BA

Русский | English

Пакет для разработчиков: Avalonia UI-концепт (`ConceptLauncher`) + логика `ActualArrow.Core`, без готовых exe и игровых модов.

Developer pack: Avalonia UI concept (`ConceptLauncher`) wired to `ActualArrow.Core` — sources only, no shipping launcher binaries or game mod DLLs.

---

## Что внутри / Contents

| Path | Description |
|------|-------------|
| `ConceptLauncher/` | Avalonia 11 concept app (BAMod-like layout + ActualArrow bindings) |
| `ActualArrow.Core/` | Core library needed to build and run the concept |
| `DESIGN_PACK/` | Visual brief, Concept AXAML notes, HTML previews |
| `.gitignore` | Ignores `bin/`, `obj/`, `.vs/`, secrets |

**Не включено / Excluded:** `ConceptApp.exe` single-file builds, `ActualArrowLauncher.exe`, `BAModLauncher.exe`, Mods DLLs, AppData, large runtime extracts/zips.

---

## Требования / Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows desktop (WinExe + Avalonia Desktop)
- NuGet: Avalonia **11.3.22**, CommunityToolkit.Mvvm **8.4.2**

---

## Сборка / Build

Из корня репозитория / from repo root:

```powershell
dotnet restore ConceptLauncher\ConceptApp.csproj
dotnet build ConceptLauncher\ConceptApp.csproj -c Release
```

Запуск / run:

```powershell
dotnet run --project ConceptLauncher\ConceptApp.csproj -c Release
```

### Single-file publish

```powershell
dotnet publish ConceptLauncher\ConceptApp.csproj -c Release -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\publish
```

Результат: `publish\ConceptApp.exe`.

---

## Ссылки проектов / Project references

`ConceptLauncher/ConceptApp.csproj` ссылается на соседний проект:

```xml
<ProjectReference Include="..\ActualArrow.Core\ActualArrow.Core.csproj" />
```

UI — концепт; бизнес-логика лаунчера идёт через `ActualArrow.Core` (установка паков, Steam/пути, MelonLoader, GitHub releases и т.д.).

The UI is a **concept shell** wired to **ActualArrow.Core**. Keep Core API/contracts stable when iterating on AXAML.

---

## Дизайн / Design notes

См. / see:

- `DESIGN_PACK/VISUAL_BRIEF.md` — сравнение BAMod vs ActualArrow
- `DESIGN_PACK/Concept/README.md` — как мержить concept AXAML в ActualArrow
- `DESIGN_PACK/concepts-preview.html` / `Concept/concept-preview.html` — превью в браузере

Идея концепта: компактная композиция BAMod (brand + pack card + большой PLAY) и графитовая палитра/`#E2A23A` в Avalonia-токенах ActualArrow, без wide hero banner.

---

## Лицензия / Notes

Исходники Core восстановлены/адаптированы для сборки концепта. Используйте только в рамках вашей команды/продукта ActualArrow / Broken Arrow launcher.
