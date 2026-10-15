# Analyze dock — what changed and why

## What we did this round
- Build is green again: `dotnet build --configuration Release` passes with 0 warnings and 0 errors.
- Restored the working Analyze workspace UI from the repo by reverting the over-edited `Views/Workspaces/AnalyzeWorkspace.axaml`.
- The Analyze ViewModel still got a small, safe upgrade:
  - `ReferencePath` is now exposed so the dock can show a breadcrumb-style path like `Studio › Palette › BaseName`.
  - `UsageNote` is now exposed so the identity card can carry a short usage-first line.

## Why Analyze is the anchor
The plan is to make Spectrum feel like a **color reference app**, not just a palette editor. Analyze is the natural place for that because it already has:
- identity first: swatch, name, hex, character tags
- a conversion table: common and advanced color spaces
- contrast: preview, ratio, WCAG role verdicts, APCA
- color-vision collisions
- OKLCH ramp and tint/shade/tone generators

## What the Analyze dock should read like
- a short breadcrumb at top: workspace › palette › current color
- an identity card with name, hex, character tags, and a short usage note
- a conversion table grouped into common vs advanced, with row-level copy
- contrast section with sample text, ratio, three WCAG role rows, and APCA
- color-vision section with the worst collision pairs first
- an OKLCH ramp strip users can click to add colors

## Interaction story
- hover on the palette board
- select a swatch
- the Analyze dock updates to the selected color’s reference record
- the user gets one coherent “this color” view instead of a raw readout

## Remaining limits
- `Assets/Fragment-Mono-Regular.ttf` is still a placeholder; the mono token currently falls back to system monospace in `App.axaml`.
- `IconStyles.axaml` exists but is minimal and currently does not style MaterialIcon classes.
- The Analyze workspace UI file was reverted to the previously working version; the richer Analyze dock edits were removed because they created an invalid `Child` property assignment on `Border`.

## Files of interest
- `ViewModels/Workspaces/AnalyzeWorkspaceViewModel.cs`
- `Styles/Theme.axaml`
- `App.axaml`
- `Views/Workspaces/AnalyzeWorkspace.axaml`
- `IconStyles.axaml`
- `Assets/Fragment-Mono-Regular.ttf`
- `Assets/Cabazon W01 Regular.ttf`
