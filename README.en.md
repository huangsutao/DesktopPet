# DesktopPet

[中文](README.md) | **English**

A Windows desktop pet built with **WPF + Spine**. Borderless transparent topmost window, Spine skeletal animation, drag, click interactions, autonomous walking, and sleep.

> **Status:** The main flow mostly works, but there are still rough edges. Expect a learning / hobby project, not a polished product. Feel free to fix issues with AI assistance if you like.

## Screenshots

Desktop pet:

![Main](Screenshots/main.png)

Settings:

![Settings](Screenshots/settings.png)

## Tech stack

- .NET 9 / WPF
- Spine Runtime (C#; must match the export tool major version)
- Rendering: custom `WpfSkeletonRenderer` (WriteableBitmap)
- System tray: NotifyIcon

## Roadmap

| Phase | Scope | Status |
|------|------|--------|
| P0 | Transparent topmost window, drag, tray exit | Done |
| P1 | Load Spine, play idle | Done |
| P2 | State machine (click / drag / sleep) | Done |
| P2/P3 | Autonomous walking (`walkArea` + pauses / random actions) | Done |
| P3 | Multiple skins, scale, click-through, settings window | Done |

No launch-on-startup feature.

## Requirements

- Windows 10/11
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (recommended workload: **.NET desktop development**)
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (can also be installed with VS2022)
- Spine export assets (match Runtime major version, e.g. 4.3.x)

## Quick start

```bash
git clone <repo-url>
cd DesktopPet
dotnet restore
dotnet run --project DesktopPet.csproj
```

Or open `DesktopPet.sln` in Visual Studio 2022, restore NuGet packages, then press F5.

Use the tray menu to switch pets, toggle click-through, and open settings. User settings are stored in `%AppData%/DesktopPet/settings.json`.

### Releases

Prebuilt Windows x64 zip packages (self-contained) are published on GitHub **Releases** when a `v*` tag is pushed. Download, extract, and run `DesktopPet.exe`.

## Asset layout

One folder per pet. **At runtime only `export/` is used:**

```text
Assets/Pets/{petName}/
  ├── export/                 # Copied to build output (loaded by the app)
  │   ├── *.atlas
  │   ├── *.png
  │   └── *-pro.skel / *.json
  ├── images/                 # Editor only, not copied
  └── *.spine                 # Editor only, not copied
```

Set the pet name in settings (default `default`). **Spine Editor export version must match the spine-csharp Runtime version.**

### Animation mapping

`pet-animations.json` in the app root (output directory) controls idle / click / drag / walk / sleep animation candidates. **To add or change assets, edit this file:**

```json
{
  "includeAllNonIdleOnClick": true,
  "defaults": {
    "idle": ["idle"],
    "click": ["jump"],
    "drag": [],
    "walk": ["walk", "run"],
    "sleep": ["sleep", "death", "idle"]
  },
  "pets": [
    { "match": "spineboy", "click": ["jump", "shoot", "portal"], "walk": ["walk", "run"] }
  ]
}
```

- `defaults`: shared candidates for all pets (matched in order against animations that exist on the skeleton)
- `pets[].match`: substring match against skeleton name or pet folder (case-insensitive)
- `includeAllNonIdleOnClick`: when true, click also cycles other non-idle animations on the skeleton
- Restart the app after changes (hot-reload can be added later)

## Project structure

```text
DesktopPet/
├── Assets/Pets/default/          # Spine assets (skel/json + atlas + png)
├── Core/                         # PetState / PetStateMachine / PetConfig / Autonomy*
├── Spine/                        # SpineRuntimeHost / WpfSkeletonRenderer / AnimationController
├── UI/                           # TrayIconService, SettingsWindow
├── Services/                     # SettingsService / ClickThroughService, etc.
├── Resources/                    # Other static resources
└── ThirdParty/SpineCSharp/       # Official spine-csharp sources (option B, in-tree)
```

### Integrating spine-csharp (option B)

Copy the `.cs` files under `src` from official [spine-csharp](https://github.com/EsotericSoftware/spine-runtimes/tree/4.3/spine-csharp) into:

```text
ThirdParty/SpineCSharp/
```

Keep the original folder layout (e.g. `Attachments/`). The SDK-style project compiles them automatically; no separate `ProjectReference` is required.

The WPF project excludes `ColorMono.cs` (MonoGame/XNA) and `ColorUnity.cs` (Unity) in the `.csproj`, and uses `ColorOther.cs` only.

When upgrading the Runtime: replace `ThirdParty/SpineCSharp/` with the matching branch `src`, and keep export assets on the same version.

## Architecture

- **MainWindow**: borderless transparent host; drag, click, autonomous walking
- **PetStateMachine**: Idle / Walk / Sleep / Clicked, etc.
- **AutonomyScheduler**: quiet cadence (pause → walk → pause → act)
- **SpineRuntimeHost**: skeleton load and per-frame Update
- **WpfSkeletonRenderer**: draws Spine meshes to a WPF surface

## Conventions

- Keep Spine Runtime and asset export versions in sync
- Keep rendering and UI decoupled: the window raises events; it does not poke Skeleton internals
- Persist user settings under `%AppData%/DesktopPet/settings.json`
- Treat `ThirdParty/SpineCSharp` as upstream; put app logic under `Spine/`

## Known limitations

- No official Spine control for WPF; a custom render bridge is required
- Windows only (WPF)
- With click-through enabled you cannot click the pet directly; use the tray toggle to turn it off

## License

Original **code and documentation** in this repository are released under the [MIT License](LICENSE) (Copyright © 2026 Sutao).

MIT covers only original parts of this project (for example `Core/`, `UI/`, `Services/`, `Spine/`, `App.*`, `MainWindow.*`, and project configuration). It does **not** cover the third-party items below:

| Component | License notes |
|------|------|
| [`ThirdParty/SpineCSharp`](ThirdParty/SpineCSharp) | [Spine Runtimes License](https://esotericsoftware.com/spine-runtimes-license) (see also [Spine Editor License](https://esotericsoftware.com/spine-editor-license)). Redistribution must keep copyright and license notices; integrating the Runtime into a product usually requires each end user to have a Spine Editor license. |
| [`Assets/Pets`](Assets/Pets) example characters | From Esoteric Software Spine examples. Most samples (e.g. default / alien / stretchyman) allow redistributing images with each folder’s `license.txt`, but **not for any commercial use**. **Hero** (© XDTech) is demo-only and **must not be redistributed or used as a basis for derivative work**. |

When using, distributing, or modifying this repo, follow MIT **and** the third-party terms above. For commercial products, replace example assets with your own and confirm Spine Runtime licensing for your case.
