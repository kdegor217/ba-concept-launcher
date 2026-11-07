# Concept: BAMod visual → ActualArrow Avalonia

Превью: открой `concept-preview.html` в браузере.

## Идея

Оставить **логику и биндинги ActualArrow**, вернуть **композицию и палитру BAMod**:

| Было (ActualArrow) | Стало (concept) |
|---|---|
| 980×440, wide `header.png` hero | **480×640**, brand row + pack card |
| Background `#050709` | `#1D1F23` |
| Accent `#EE9035` | `#E2A23A` |
| OnAccent `#050709` | `#1B1407` |
| Radius 4 | **8** |
| Sora/Montserrat headings | **Inter** (Sora fallback) |
| HazardBar prominent | thin 4px progress |

## Файлы для разработчиков

Скопировать/смержить в репозиторий ActualArrowLauncher:

```
Concept/App.axaml                  → App.axaml (resources/styles)
Concept/Theme/Palette.axaml        → Theme/Palette.axaml
Concept/Theme/Buttons.axaml        → Theme/Buttons.axaml
Concept/Theme/Controls.axaml       → Theme/Controls.axaml
Concept/Views/MainWindow.axaml     → Views/MainWindow.axaml
```

Имена `x:Name` сохранены (`PlayModded`, `PackPicker`, `Banner`, …), чтобы code-behind `InitializeComponent` продолжил работать.

## Замечания по интеграции

1. **Шрифты Inter** — взять из старого лаунчера (`Fonts.Inter-*.ttf`) в `Assets/Fonts/` и обновить `FontFamily` URI, либо оставить Sora как fallback (уже прописан).
2. **`Progress`**: в концепте это `ProgressBar`. Сейчас в коде тип `HazardBar`. Либо оставить HazardBar и стилизовать тонко, либо заменить контрол в `MainWindow.cs`.
3. **`Header` / `HeaderImage`** спрятаны (`IsVisible=False`), чтобы не ломать `UpdateBannerCap()` / `FitToScreen()`.
4. **Settings** в концепте в flyout у нижней кнопки (как BAMod ghost Settings + меню More).
5. Wide `header.png` можно использовать позже в Settings/About, не в первом viewport.

## Что сказать команде одной фразой

> Верните компактный BAMod-layout (brand + pack card + big PLAY) и графитовую палитру Inter/`#E2A23A` в Avalonia-токены `ActualArrow.*`, убрав wide hero banner из главного окна.
