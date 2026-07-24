# CraftSort

Sorts crafting station recipes by item stats — revival of the deprecated SortCraft mod.

Adds sort tab buttons to the left side of the crafting panel. Click a button to sort recipes by that stat (descending). Click again to toggle off.

## Sort Modes

| Button | Sorts by | Relevant items first |
|--------|----------|---------------------|
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

**Food tabs** (HP, Stam, Eitr) appear at cauldrons and food prep tables.
**Combat tabs** (Armor, Block, Phys, etc.) appear at all other stations.
**A→Z** appears everywhere.

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

## Sort mode on open: None/Armor/Block/PhysDmg/ChopDmg/FireDmg/FrostDmg/LightningDmg/PoisonDmg/SpiritDmg/Health/Stamina/Eitr/Name
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
- VNEI
- Jewelcrafting
- Recycle N Reclaim
- AAA_Crafting
- CraftingFilter
- CraftingSearchBar
- MyLittleUI
- SortedMenus
- BetterArchery
- EpicLoot
- PlantEverything

**Known incompatibility:**
- InventorySlots — completely replaces the crafting UI, making CraftSort's buttons invisible. Use one or the other.

## How It Works

CraftSort uses a 3-layer sorting approach for maximum compatibility:

1. **List sort** — sorts the recipe list in `GetAvailableRecipes` Postfix
2. **Weight injection** — sets `Recipe.m_listSortWeight` so vanilla's own sort respects our order
3. **UI re-sort** — Postfix on `UpdateRecipeList` re-sorts `m_availableRecipes` and repositions UI elements (runs last via `Priority.Last` + `HarmonyAfter`)

Craftable items always appear first (vanilla behavior preserved).

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

MIT
