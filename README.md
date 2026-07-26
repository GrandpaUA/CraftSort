# CraftSort

Sorts crafting station recipes by item stats — revival of the deprecated SortCraft mod.

Adds sort tab buttons to the left side of the crafting panel. Click a button to sort recipes by that stat (descending). Click again to toggle off. Hover for tooltip.

## Features

- **15 sort modes** with icon buttons (food stations show 6, combat stations show 12 in a 2×6 grid)
- **New recipe indicator** — blue dot on recipes you haven't viewed yet, with a "New" filter tab
- **Per-character persistence** — viewed recipes tracked per save file
- **AAA Crafting compatible** — global sort across paginated pages
- **Tooltips** on hover for every button

## Sort Modes

| Icon | Sorts by | Relevant items first |
|------|----------|---------------------|
| All | Default order (no sort) | — |
| Armor | `m_armor` | Helmet, Chest, Legs, Shoulder |
| Block | `m_blockPower` | Shield |
| Phys | Blunt + Slash + Pierce | — |
| Fire | Fire damage | — |
| Frost | Frost damage | — |
| Ltng | Lightning damage | — |
| Psn | Poison damage | — |
| Sprt | Spirit damage | — |
| Chop | Chop damage | — |
| HP | Food health | Consumable |
| Stam | Food stamina | Consumable |
| Eitr | Food eitr | Consumable |
| A→Z | Localized name | — |
| New | Unviewed recipes only | — |

**Food tabs** (HP, Stam, Eitr) appear at cauldrons and food prep tables (single column).
**Combat tabs** (Armor, Block, Phys, etc.) appear at all other stations (2 columns × 6 rows).
**A→Z** and **New** appear everywhere.

## New Recipe Indicator

When you discover a new material that unlocks recipes, those recipes get a **blue dot** next to their icon. The dot stays until you click on the recipe to view it. Tracked per character and persisted across game sessions.

The **New** tab filters the list to show only unviewed recipes, with a count badge (e.g. "New (3)").

On first mod install, all currently known recipes are marked as viewed — only recipes discovered *after* installation get the indicator.

## Installation

1. Install [BepInEx 5.4.x](https://docs.bepinex.dev/articles/user_guide/installation/index.html)
2. Drop `CraftSort.dll` into `BepInEx/plugins/`

Or use r2modman/Thunderstore mod manager.

## Configuration

Edit `BepInEx/config/dev.craftsort.cfg`:

```ini
[General]

## Enable or disable the mod
# Setting type: Boolean
# Default value: true
Enabled = true

## Sort mode on open: None/Armor/Block/PhysDmg/ChopDmg/FireDmg/FrostDmg/LightningDmg/PoisonDmg/SpiritDmg/Health/Stamina/Eitr/Name/New
# Setting type: String
# Default value: None
DefaultSortMode = None

## Keep last sort mode between station openings
# Setting type: Boolean
# Default value: false
RememberLastMode = false
```

## Compatibility

Tested with Valheim 0.221.13 (Unity 6 engine).

**Compatible mods** (verified via Harmony patch ordering):
- VNEI, Jewelcrafting, Recycle N Reclaim, AAA_Crafting
- CraftingFilter, CraftingSearchBar, MyLittleUI, SortedMenus
- BetterArchery, EpicLoot, PlantEverything

**Known incompatibility:**
- InventorySlots — completely replaces the crafting UI, hiding CraftSort's buttons.

## How It Works

CraftSort uses a **Transpiler** on `InventoryGui.UpdateRecipeList` that injects a sort call right before vanilla's positioning for-loop. This ensures vanilla positions recipe elements in our sorted order — no manual repositioning needed.

For AAA Crafting compatibility, a separate Prefix sorts the full `RecipeListPerfCache.CraftSortedFiltered` before pagination slices it.

The blue dot indicator uses a Postfix on `UpdateRecipeList` to add/remove dot overlays on recipe icons, and a Postfix on `OnSelectedRecipe` to mark recipes as viewed on click.

## Building from Source

### Requirements
- .NET 8 SDK (for build tooling)
- .NET Framework 4.8 reference assemblies
- Valheim installed via Steam

### Build
```bash
dotnet build CraftSort/CraftSort.csproj -c Debug
```

Output: `CraftSort/bin/Debug/net48/CraftSort.dll`

## License

AGPL-3.0
