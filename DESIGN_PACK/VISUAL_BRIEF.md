# Launcher Visual Brief (decompiled)

## Что декомпилировано

| | BAModLauncher.exe (старый) | ActualArrowLauncher.exe (текущий) |
|---|---|---|
| Версия | 1.1.0 | 0.1.1 |
| Runtime | **.NET 8** + Windows Desktop | **.NET 10** |
| UI toolkit | **WinForms** (кастомный owner-draw) | **Avalonia 11.3.22** + FluentTheme |
| Шрифты | Inter (Regular/Medium/SemiBold/Bold/ExtraBold) | Sora + Montserrat (Bold/ExtraBold) + Segoe UI body |
| Иконки | Segoe MDL2 Assets | Path/геометрия + header art |

> Важно для разработчиков: старый лаунчер **не Avalonia**. Его «оптимальный визуал» — это hand-rolled WinForms UI (`Theme`, `RoundButton`, `NavButton`, `ThinProgress`). Новый — Avalonia AXAML (`Theme/Palette.axaml`, `Buttons.axaml`, `Controls.axaml`, `Views/*.axaml`).

## Палитры рядом

### BAMod (оптимальный референс) — `BAModLauncher_UI/Theme.cs`

| Token | RGB | Hex |
|---|---|---|
| Sidebar | 21,22,25 | `#151619` |
| Background | 29,31,35 | `#1D1F23` |
| Panel | 35,37,42 | `#23252A` |
| Surface | 42,45,51 | `#2A2D33` |
| SurfaceHover | 52,55,62 | `#34373E` |
| Border | 58,61,69 | `#3A3D45` |
| Text | 233,234,236 | `#E9EAEC` |
| TextMuted | 150,154,163 | `#969AA3` |
| Accent | 226,162,58 | `#E2A23A` |
| AccentHover | 240,182,86 | `#F0B656` |
| AccentText | 27,20,7 | `#1B1407` |
| Good | 76,194,110 | `#4CC26E` |
| Bad | 229,91,82 | `#E55B52` |

### ActualArrow (текущий) — восстановлено в `ActualArrow_UI/Theme/Palette.axaml.reconstructed`

| Token | Hex |
|---|---|
| Background | `#050709` |
| Panel | `#101216` |
| PanelRaised | `#171A1F` |
| PanelSunken | `#0A0C0F` |
| Border | `#22252B` |
| BorderStrong | `#343841` |
| Text | `#F1F1F1` |
| Muted | `#9AA0A8` |
| Accent | `#EE9035` |
| AccentHover | `#F5A65A` |
| AccentPressed | `#D27A22` |
| OnAccent | `#050709` |
| Danger / Warning / Ok | `#D2493A` / `#E8B84A` / `#6FB35A` |
| Radius | `4` |

## Композиция главного окна

### BAMod `LauncherForm` (460×624, fixed)

1. Header 70px: logo 52×52 + `BROKEN ARROW` (accent, Inter SemiBold 8.5) + `Mod Launcher` (Inter Bold 16)
2. Pack card 150px (`Panel`): preview 118×118 + MODPACK label + name/version/state + setup/channels
3. Primary **PLAY** 58px (Accent, heading 14)
4. Secondary **Play vanilla** 40px
5. Status 40px muted
6. Thin progress (по необходимости)
7. Bottom: Settings ghost button

Плотность, фиксированная ширина, крупный CTA, Inter, мягкий тёплый accent `#E2A23A`, фон не «чёрный», а графитовый.

### ActualArrow `MainWindow` (min ~760×380)

Элементы из NameScope:

- `Header` / `HeaderImage` — широкий banner (`Assets/header.png`, aspect ≈ 5.14)
- `Banner` + status/detail/retry/close
- `ActionBar`: `PackPicker`, `PackName`, chevron, `InstalledVersion`, `Chip`, `More` menu
- `PlayVanilla` / `PlayModded` + `PlaySubtitle`
- `WorkPanel`: `ProgressText`, `HazardBar`, `StatusLine`, `Cancel`
- `VersionLabel`

Шире, banner-first, FluentTheme + кастомные overrides, радиус 4, почти чёрный фон `#050709`, accent более «оранжевый» `#EE9035`, heading через Sora/Montserrat.

## Что взять из BAMod в новый Avalonia-концепт

1. **Графитовый фон** `#1D1F23` / panel `#23252A` вместо почти-чёрного `#050709`
2. **Accent** ближе к `#E2A23A` (золотистее, меньше «чисто оранжевый»)
3. **OnAccent / AccentText** тёмный коричневый `#1B1407`, не `#050709`
4. **Inter** (или близкий UI-гротеск) для body+UI; display можно оставить Sora только для brand/titles
5. **Компактная колонка** ~460–520px с крупным PLAY 56–60px
6. **Карточка модпака** с превью слева (как 118×118), а не только wide header
7. **Radius** 6–8 вместо 4 (у BAMod round buttons заметнее)
8. **Ghost Settings** внизу, без перегруза Fluent-chrome

## Где смотреть код

```
C:\BA\decompiled\
  DESIGN_PACK\                 ← для дизайна / бриф разработчикам
  BAModLauncher\               ← полный C# старого лаунчера
  ActualArrowLauncher\         ← полный C# нового + CompiledAvaloniaXaml
  ActualArrow.Core\            ← бизнес-логика нового
C:\BA\extracted\               ← распакованные DLL из single-file
```

Ключевые файлы визуала:

- Старый: `DESIGN_PACK/BAModLauncher_UI/Theme.cs`, `LauncherForm.cs`, `RoundButton.cs`, `NavButton.cs`
- Новый: `DESIGN_PACK/ActualArrow_UI/Theme/Palette.axaml.reconstructed`, `Views/MainWindow.cs`, `Assets/header.png`
- Скомпилированные AXAML (Buttons/Controls/Views): `ActualArrow_UI/CompiledAvaloniaResources.cs`
