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

For stylesheet and config files the plugin parses every line with compiled regular expressions and renders a small inline coloured square swatch immediately before each detected colour token.

**Supported colour formats:**

- `#rgb` · `#rrggbb` · `#rrggbbaa` (hex, case-insensitive)
- `rgb(r, g, b)`
- `rgba(r, g, b, a)`
- `hsl(h, s%, l%)`
- `hsla(h, s%, l%, a)`

The swatch count is shown in the toolbar badge (e.g. *🎨 14 colours*).

Text remains fully **selectable and copyable** — the swatches are purely visual overlays implemented as WPF `InlineUIContainer` elements inside a `FlowDocument`.

---

### Feature 2 — .env Privacy Mode

When a `.env` variant is opened:

- All `KEY=VALUE` assignments are shown as `KEY=********` by default.
- Variable **names** and **comments** always remain visible.
- An **eye (👁) toggle button** in the toolbar reveals / hides all values.
- The original file on disk is **never modified** — all masking is in-memory only.

---

## Project Structure

```
QuickLook.Plugin.DevPowerTool/
├── Plugin.cs                          ← IViewer entry point (QuickLook discovers this)
├── FileTypeDetector.cs                ← Maps file path → DevFileType enum
├── PreviewPanel.xaml                  ← Main WPF UserControl (toolbar + FlowDocumentScrollViewer)
├── PreviewPanel.xaml.cs               ← Code-behind: file loading, rendering, eye toggle
├── ErrorPanel.xaml                    ← Fallback error display
├── ErrorPanel.xaml.cs
├── Helpers/
│   ├── ColorParser.cs                 ← Regex colour token extraction + HSL/Hex/RGB parsing
│   ├── EnvMaskingService.cs           ← .env line parser (key/value/comment categorisation)
│   └── PreviewRenderer.cs             ← Builds WPF FlowDocument with inline swatches
├── Properties/
│   └── AssemblyInfo.cs
├── Scripts/
│   └── pack-zip.ps1                   ← Packages build output → .qlplugin
├── QuickLook.Common/                  ← Git submodule (QuickLook.Common)
├── QuickLook.Plugin.DevPowerTool.csproj
├── QuickLook.Plugin.DevPowerTool.sln
├── QuickLook.Plugin.Metadata.Base.config
├── QuickLook.Plugin.Metadata.config   ← Copied to output; read by QuickLook at runtime
├── .gitmodules
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

> **No NuGet packages are required.** All dependencies come from the `QuickLook.Common` submodule and the .NET Framework 4.6.2 BCL.

---

## Build Instructions

### 1. Clone with submodules

```bash
git clone --recurse-submodules https://github.com/YOUR_USERNAME/QuickLook.Plugin.DevPowerTool.git
cd QuickLook.Plugin.DevPowerTool
```

If you already cloned without submodules:

```bash
git submodule update --init --recursive
```

### 2. Open in Visual Studio

Open `QuickLook.Plugin.DevPowerTool.sln` in Visual Studio 2019 or 2022.

### 3. Build (Release)

Select **Release** configuration from the toolbar, then **Build → Build Solution** (`Ctrl+Shift+B`).

Output: `bin\Release\QuickLook.Plugin.DevPowerTool.dll`

### 4. Package as `.qlplugin`

From the project root, run:

```powershell
powershell -ExecutionPolicy Bypass -File Scripts\pack-zip.ps1
```

This creates `QuickLook.Plugin.DevPowerTool.qlplugin` in the project root.

---

## Installation

1. Make sure **QuickLook** is running (system tray icon visible).
2. In File Explorer, navigate to the project root.
3. Press **Spacebar** on `QuickLook.Plugin.DevPowerTool.qlplugin`.
4. Click **Install** in the QuickLook popup.
5. **Restart QuickLook** (right-click tray icon → Exit, then relaunch).
6. Navigate to a `.css`, `.env`, or other supported file and press **Spacebar**.

### Manual Installation (alternative)

Copy `QuickLook.Plugin.DevPowerTool.dll` and `QuickLook.Plugin.Metadata.config` into:

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
- **`EnvMaskingService`** — Pure parser; converts raw `.env` text into a `List<EnvLine>`. No I/O, no side effects.
- **`PreviewRenderer`** — Converts `IReadOnlyList<string>` lines into a `FlowDocument` with `InlineUIContainer` swatches.
- **`PreviewPanel`** — The WPF `UserControl` assigned to `context.ViewerContent`. Reads the file asynchronously, calls the appropriate renderer, and wires up the eye-toggle button.

---

## License

MIT License. See [LICENSE.txt](LICENSE.txt).
