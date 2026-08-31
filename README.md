<div align="center">

# 🎨 Spectrum

**A fast, keyboard-driven color palette generator for the desktop — built with Avalonia UI.**

Inspired by [Coolors.co](https://coolors.co), reimagined as a native, offline C# desktop app.

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)
![Avalonia](https://img.shields.io/badge/Avalonia%20UI-11-6f2dbd?logo=avalonia&logoColor=white)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-informational)
![License](https://img.shields.io/badge/license-MIT-green)

</div>

<br/>

<!--
  Replace this with a real screenshot or GIF of the app in action.
  A short GIF of pressing Space and watching the palette shuffle is the
  single best piece of marketing this README can have.
  Suggested path: docs/demo.gif
-->
<p align="center">
  <img src="docs/screenshot.png" alt="Spectrum app screenshot" width="850"/>
</p>

<br/>

## ✨ Features

- **Spacebar palette generation** — press `Space` anywhere in the window to regenerate every unlocked swatch, Coolors-style
- **Color harmony engine** — generate from Complementary, Analogous, Triadic, Split-Complementary, Tetradic, or Monochromatic rules, or roll fully random colors
- **Automatic color naming** — every swatch is matched against a curated CSS/X11 + extended color-name database using a redmean-weighted RGB distance (the same approach behind ntc.js)
- **HSL + Temperature color panel** — live gradient-backed sliders for Hue, Saturation, Brightness, and warm/cool Temperature tinting
- **Lock & shuffle** — lock swatches you want to keep, then shuffle the rest
- **Drag-to-reorder** — rearrange swatches directly on the board
- **Undo / redo** — full history across generate, add, remove, clear, and image-extract actions
- **Color blindness simulation** — preview the whole board under Protanopia, Deuteranopia, Tritanopia, or Achromatopsia
- **Extract palette from image** — histogram-based dominant color extraction from any image file
- **Export** — JSON, CSS custom properties, SCSS variables, Tailwind config, or plain text, with one-click clipboard copy
- **Light / dark theme toggle**
- **Studio-style monochrome UI** driven entirely by a design-token stylesheet (`Styles/Theme.axaml`)

## 🗺️ Roadmap

- [ ] Alpha channel support in the color panel
- [ ] Palette save/load to disk (file-picker plumbing already exists, UI wiring pending)
- [ ] WCAG contrast checking surfaced in the UI (contrast-ratio engine already exists)
- [ ] Dedicated color picker control
- [ ] Visual feedback for invalid hex input
- [ ] Accessibility labels on icon-only buttons
- [ ] `CanExecute` guards on more commands
- [ ] Unit tests (harmony + export services) and a CI pipeline

## 🛠️ Tech Stack

- [Avalonia UI](https://avaloniaui.net/) 11 (.NET 9, cross-platform desktop)
- MVVM via [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) source generators (`[ObservableProperty]`, `[RelayCommand]`)
- [Material.Icons.Avalonia](https://github.com/SKProCH/Material.Icons) for iconography

## 🚀 Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Run it

```bash
git clone https://github.com/<your-username>/spectrum.git
cd spectrum
dotnet restore
dotnet run --project Spectrum.csproj
```

Or open `Spectrum.slnx` in your IDE of choice (Visual Studio, Rider, VS Code) and hit run.

## ⌨️ Usage

| Action | How |
|---|---|
| Generate a new palette | Press `Space`, or click **Press Space to Harmonize** |
| Lock a color | Click the lock icon on a swatch |
| Reorder colors | Drag a swatch card |
| Simulate color blindness | **Color Blind** dropdown in the toolbar |
| Extract colors from an image | **Extract from Image** in the toolbar |
| Export a palette | **Export Palette** in the footer, pick a format, copy |

## 🤝 Contributing

This started as a personal project, but issues and PRs are welcome — especially around the roadmap items above.

## 📄 License

[MIT](LICENSE) — replace with your actual license of choice if different.
