<div align="center">

# 🎨 Spectrum

**An intuitive color palette editor built with Avalonia UI. Create, generate, and export beautiful color palettes for your design projects.**

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/Avalonia%20UI-11.2-6f2dbd?logo=avalonia&logoColor=white)](https://avaloniaui.net/)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-informational)](https://github.com/Megaloduck/Spectrum)
[![License](https://img.shields.io/github/license/Megaloduck/Spectrum?color=green)](LICENSE)
[![Latest Release](https://img.shields.io/github/v/release/Megaloduck/Spectrum?logo=github)](https://github.com/Megaloduck/Spectrum/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/Megaloduck/Spectrum/total?color=blue)](https://github.com/Megaloduck/Spectrum/releases)
[![Stars](https://img.shields.io/github/stars/Megaloduck/Spectrum?style=social)](https://github.com/Megaloduck/Spectrum)
[![Issues](https://img.shields.io/github/issues/Megaloduck/Spectrum)](https://github.com/Megaloduck/Spectrum/issues)

</div>

<br/>

<!--
  Replace this with a real screenshot or GIF of the app in action.
  A short GIF of pressing Space and watching the palette shuffle is the
  single best piece of marketing this README can have.
  Suggested path: docs/demo.gif
-->
<p align="center">
  <img src="docs/screenshot1.png" alt="Spectrum app screenshot" width="49%"/>
  <img src="docs/screenshot2.png" alt="Spectrum app screenshot" width="49%"/>
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

## 🛠️ Tech Stack

- [Avalonia UI](https://avaloniaui.net/) 11 (.NET 9, cross-platform desktop)
- MVVM via [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) source generators (`[ObservableProperty]`, `[RelayCommand]`)
- [Material.Icons.Avalonia](https://github.com/SKProCH/Material.Icons) for iconography

## 🚀 Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Run it

```bash
git clone https://github.com/Megaloduck/Spectrum.git
cd Spectrum
dotnet restore
dotnet run --project Spectrum.csproj
```

Or open `Spectrum.slnx` in your IDE of choice (Visual Studio, Rider, VS Code) and hit run.

## 📦 Download

Get the latest release:

[![Download Latest Release](https://img.shields.io/github/v/release/Megaloduck/Spectrum?style=for-the-badge&logo=github&label=Download%20Latest)](https://github.com/Megaloduck/Spectrum/releases/latest)

### System Requirements
- Windows 10 or later
- No additional dependencies required (self-contained executable)

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

Contributions are welcome! Feel free to open an [issue](https://github.com/Megaloduck/Spectrum/issues) or submit a [pull request](https://github.com/Megaloduck/Spectrum/pulls).

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

<div align="center">
  Made by @Megaloduck with ❤️ using Avalonia UI
</div>
