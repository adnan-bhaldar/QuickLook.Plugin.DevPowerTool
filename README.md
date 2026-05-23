

# QuickLook.Plugin.DevPowerTool

A production-ready **Developer Power-User** preview plugin for [QuickLook (Windows)](https://github.com/QL-Win/QuickLook).

Provides two major features:

| Feature | Files handled |
|---|---|
| 🎨 **Live colour swatches** | `.css`, `.scss`, `.sass`, `tailwind.config.*`, `.json`, `.js`, `.ts` |
| 🔒 **.env privacy masking** | `.env`, `.env.local`, `.env.production`, `.env.development`, `.env.staging`, `.env.test` |

---

## Features

### Feature 1 — Live Colour Swatches

For stylesheet and config files the plugin parses every line with compiled regular expressions and renders a small filled square swatch directly on the editor canvas before each detected colour token.

**Supported colour formats:**

- `#rgb` · `#rrggbb` · `#rrggbbaa` (hex, case-insensitive)
- `rgb(r, g, b)`
- `rgba(r, g, b, a)`
- `hsl(h, s%, l%)`
- `hsla(h, s%, l%, a)`

The swatch count is shown in a floating badge (e.g. *🎨 14 colours*).

Text remains fully **selectable and copyable** — swatches are purely visual overlays drawn in AvalonEdit's Background rendering layer and do not affect the document at all.

---

### Feature 2 — .env Privacy Mode

When a `.env` variant is opened:

- All `KEY=VALUE` assignments are shown as `KEY=********` by default.
- Variable **names** and **comments** always remain visible.
- An **iOS-style toggle** in the top-right corner reveals / hides all values instantly.
- The original file on disk is **never modified** — all masking is in-memory only.

---

## Project Structure

```
QuickLook.Plugin.DevPowerTool/
├── Plugin.cs                          ← IViewer entry point (QuickLook discovers this)
├── FileTypeDetector.cs                ← Maps file path → DevFileType enum
├── PreviewPanel.cs                    ← Main WPF UserControl (AvalonEdit + overlays, code-only)
├── ErrorPanel.cs                      ← Fallback error display (code-only)
├── Helpers/
│   ├── ColorParser.cs                 ← Compiled-regex colour token extraction + HSL/Hex/RGB parsing
│   ├── EnvMaskingService.cs           ← .env line parser (key/value/comment categorisation)
│   └── ColorSwatchRenderer.cs        ← AvalonEdit IBackgroundRenderer for inline swatches
├── Properties/
│   └── AssemblyInfo.cs
├── .github/
│   └── workflows/
│       └── build-and-release.yml      ← CI: build + pre-release on push; stable release on tag
├── QuickLook.Plugin.DevPowerTool.csproj
├── QuickLook.Plugin.DevPowerTool.sln
├── QuickLook.Plugin.Metadata.Base.config
├── QuickLook.Plugin.Metadata.config   ← Copied to output; read by QuickLook at runtime
├── .gitignore
└── README.md
```

---

## Requirements

| Requirement | Version |
|---|---|
| Windows | 10 or later (64-bit) |
| [QuickLook](https://github.com/QL-Win/QuickLook/releases) | 3.x |
| [.NET Framework](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net462) | 4.6.2 |
| [Visual Studio](https://visualstudio.microsoft.com/) | 2019 or 2022 (with **.NET desktop development** workload) |
| MSBuild | Ships with Visual Studio or via [Build Tools](https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022) |

> **No git submodules required.** All dependencies (`AvalonEdit`, `QuickLook.Common`) are restored automatically via NuGet.

---

## Build Instructions

### 1. Clone

```bash
git clone https://github.com/adnan-bhaldar/QuickLook.Plugin.DevPowerTool.git
cd QuickLook.Plugin.DevPowerTool
```

### 2. Restore NuGet packages

The project uses SDK-style `PackageReference` — restore must go through MSBuild:

```powershell
msbuild QuickLook.Plugin.DevPowerTool.sln /t:Restore /p:Configuration=Release /p:Platform="Any CPU"
```

### 3. Build (Release)

**Option A — Visual Studio**

Open `QuickLook.Plugin.DevPowerTool.sln`, select **Release** configuration, press `Ctrl+Shift+B`.

**Option B — MSBuild CLI**

```powershell
msbuild QuickLook.Plugin.DevPowerTool.sln /p:Configuration=Release /p:Platform="Any CPU" /m
```

Output: `bin\Release\QuickLook.Plugin.DevPowerTool.dll`

### 4. Package as `.qlplugin`

Packaging is handled automatically by GitHub Actions — no local script needed.

On every push to `main` a pre-release is created. On every version tag a stable release is created. Both upload `QuickLook.Plugin.DevPowerTool.qlplugin` as a release asset.

To trigger a stable release manually:

```powershell
git tag 2.0.0
git push origin 2.0.0
```

---

## Installation

1. Make sure **QuickLook** is running (system tray icon visible).
2. Download `QuickLook.Plugin.DevPowerTool.qlplugin` from the [Releases](https://github.com/adnan-bhaldar/QuickLook.Plugin.DevPowerTool/releases) page.
3. Press **Spacebar** on the `.qlplugin` file in File Explorer.
4. Click **Install** in the QuickLook popup.
5. **Restart QuickLook** (right-click tray icon → Exit, then relaunch).
6. Navigate to a `.css`, `.env`, or other supported file and press **Spacebar**.

### Manual Installation (alternative)

Copy `QuickLook.Plugin.DevPowerTool.dll`, `QuickLook.Plugin.Metadata.config`, and `ICSharpCode.AvalonEdit.dll` into:

```
%APPDATA%\QuickLook\Plugins\QuickLook.Plugin.DevPowerTool\
```

Then restart QuickLook.

---

## Supported File Extensions

### Colour Swatch Mode

| Extension / Name | Label |
|---|---|
| `.css` | CSS |
| `.scss` | SCSS |
| `.sass` | SASS |
| `tailwind.config.js/ts/cjs/mjs` | TAILWIND |
| `.json` | JSON |
| `.js` | JS |
| `.ts` | TS |

### Privacy Mode (.env)

| File name |
|---|
| `.env` |
| `.env.local` |
| `.env.development` |
| `.env.production` |
| `.env.staging` |
| `.env.test` |

---

## Architecture Notes

- **`Plugin.cs`** — Entry point. Implements `IViewer` from `QuickLook.Common`. QuickLook discovers this class via reflection.
- **`FileTypeDetector`** — Pure static helper; returns a `DevFileType` enum value for any supported path.
- **`ColorParser`** — Stateless; takes a `string` line and returns a `List<ColorToken>` with positions and parsed `Color` values. All five regex patterns are compiled once at class load time.
- **`EnvMaskingService`** — Pure parser; converts raw `.env` text into a `List<EnvLine>`. No I/O, no side effects. `EnvLine.DisplayText(reveal)` handles masking without touching the document.
- **`ColorSwatchRenderer`** — Implements AvalonEdit `IBackgroundRenderer` (`KnownLayer.Background`). Draws rounded filled squares via `DrawingContext` on every render pass. Scroll-offset aware, viewport-culled, all render errors swallowed.
- **`PreviewPanel`** — The WPF `UserControl` assigned to `context.ViewerContent`. Loads the file asynchronously with a `CancellationToken`, calls `ColorParser` per line for swatch files, or `EnvMaskingService` for `.env` files. Contains `PlainTextHighlighting` (correct AvalonEdit pattern for setting default text colour) and the iOS-style animated env toggle.

---

<!-- ## License

MIT License. See [LICENSE.txt](LICENSE.txt). -->