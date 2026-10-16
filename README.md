# Hyland Skills

A skill tree mod for **Schedule I** that lives on your phone: 8 categories, perk trees with prerequisites, combos and a Major Mastery at the end of every tree, active skills with clear timers, plus casino and mobility skills.

The player guide (how points work, controls, every branch) is in [HylandSkills/README.md](HylandSkills/README.md).

> Built for Schedule I **0.4.7 (IL2CPP / main branch)** with MelonLoader 0.7.x and S1API (Forked) 3.2.x.

## Install

Download the zip from the [Releases](../../releases) page and install it with Thunderstore / r2modman, or put `Mods/HylandSkills.dll` in your game's `Mods` folder. It needs MelonLoader and S1API (Forked).

## Build from source

Requirements: the .NET 6 SDK and a Thunderstore (or r2modman) profile for Schedule I with MelonLoader and S1API installed, started at least once so MelonLoader has generated the `Il2CppAssemblies` folder.

```bash
cd HylandSkills
dotnet build -c Release
```

The project reads its references from the Thunderstore profile `Default`. Point it at another profile with `-p:S1Profile="<path to the profile>"`. Add `-p:Deploy=true` to copy the built DLL into that profile's `Mods` folder.

## Repository layout

| Path | What it is |
|---|---|
| `HylandSkills/src/Data` | Skill, branch, combo and synergy definitions |
| `HylandSkills/src/State` | Points, ranks, save data and respec |
| `HylandSkills/src/Effects` | Harmony patches that apply each skill to the game |
| `HylandSkills/src/Actives` | Active skills, slots and timers |
| `HylandSkills/src/UI` | The phone app, HUD cards, procedural sprites, animations and sounds |
| `HylandSkills/Assets/Icons` | Skill icons (Lucide, ISC license) |
| `tools/` | Icon generators and a test launch script |
| `dist/pkg` | Thunderstore package metadata |
| `DESIGN.md`, `MODLOG.md` | Design notes and development log (in Spanish) |

## License

MIT, see [LICENSE](LICENSE). The skill icons are [Lucide](https://lucide.dev) icons under the ISC license ([HylandSkills/Assets/LICENSE-lucide.txt](HylandSkills/Assets/LICENSE-lucide.txt)).
