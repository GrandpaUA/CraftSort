# CraftSort — Agent Instructions

## What this mod does

BepInEx 5 mod for Valheim that injects sort tab buttons into crafting stations.
Buttons appear on the left side of the recipe list and reorder recipes by item stats.
Revival of the deprecated SortCraft mod by KGvalheim.
Target game version: Valheim 0.221.13 (Unity 6 engine).

---

## Environment

Project root:   C:\All\Project\vibecode\CraftSort\
Source files:   C:\All\Project\vibecode\CraftSort\CraftSort\
Valheim:        C:\Program Files (x86)\Steam\steamapps\common\Valheim\
BepInEx root:   C:\Users\Admin\AppData\Roaming\r2modmanPlus-local\Valheim\profiles\Gun world\BepInEx\
Deploy DLL to:  ...BepInEx\plugins\CraftSort.dll
BepInEx log:    ...BepInEx\LogOutput.log

---

## Build setup

No .NET SDK is installed. Do this first:

    winget install Microsoft.DotNet.SDK.8

Verify: dotnet --version (must return 8.x or higher)

Then add inside an ItemGroup in CraftSort\CraftSort.csproj:

    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies"
                      Version="1.0.0" PrivateAssets="all" />

Build command:

    cd C:\All\Project\vibecode\CraftSort
    dotnet build CraftSort\CraftSort.csproj -c Debug

Output DLL: CraftSort\bin\Debug\net48\CraftSort.dll
The csproj already has an AfterBuild target that copies the DLL to the plugins folder automatically.

---

## CraftSort.csproj — what is already configured

- TargetFramework: net48
- AssemblyName: CraftSort
- ValheimPath: reads from VALHEIM_INSTALL env variable, falls back to standard Steam path
- BepInExPath: $(ValheimPath)\BepInEx
- All assembly references use Private=false (DLLs are not bundled in output)
- AfterBuild target copies output DLL to the r2modman profile plugins folder
- Do not restructure the csproj — only add the NuGet PackageReference above

Assembly references already declared in csproj:
- $(BepInExPath)\core\BepInEx.dll
- $(BepInExPath)\core\0Harmony.dll
- $(ValheimPath)\valheim_Data\Managed\assembly_valheim.dll
- $(ValheimPath)\valheim_Data\Managed\UnityEngine.dll
- $(ValheimPath)\valheim_Data\Managed\UnityEngine.CoreModule.dll
- $(ValheimPath)\valheim_Data\Managed\UnityEngine.UI.dll

---

## Files to implement

    CraftSort\
    ├── CraftSort.csproj   already written — do not modify structure
    ├── Plugin.cs          BepInEx entry point
    ├── SortLogic.cs       sort enum + value logic
    ├── Patches.cs         Harmony patches
    └── TabUI.cs           Unity UI tab injection

---

## Key Valheim types and fields

Recipe
- m_item: ItemDrop
- m_item.m_itemData: ItemDrop.ItemData
- m_item.m_itemData.m_shared: ItemDrop.ItemData.SharedData

ItemDrop.ItemData.SharedData fields used:
- m_itemType: ItemDrop.ItemData.ItemType (Weapon, Shield, Helmet, Chest, Legs, Shoulder, Consumable, etc.)
- m_armor: float
- m_blockPower: float
- m_food: float        (health restored)
- m_foodStamina: float
- m_foodEitr: float
- m_damages: HitData.DamageTypes
- m_name: string (localization key, use Localization.instance.Localize(m_name) for display)

HitData.DamageTypes fields:
- m_blunt, m_slash, m_pierce, m_chop, m_pickaxe
- m_fire, m_frost, m_lightning, m_poison, m_spirit

Key methods:
- Player.GetAvailableRecipes(ref List<Recipe> recipes) — populates the crafting recipe list
- Player.m_localPlayer.GetCurrentCraftingStation() — returns CraftingStation or null
- CraftingStation.m_name — string name of the station (e.g. "$piece_cauldron")
- InventoryGui.instance — static singleton
- InventoryGui.m_craftingPanel — Transform, parent for tab container
- InventoryGui.UpdateCraftingPanel(bool focusView) — refreshes the crafting UI
- Localization.instance.Localize(string key) — translates m_name to display string

---

## Plugin.cs

Namespace: CraftSort
Class: CraftSortPlugin extends BaseUnityPlugin
Attribute: [BepInPlugin("dev.craftsort", "CraftSort", "1.0.0")]

Fields (private ConfigEntry):
- _enabled:          Config.Bind("General", "Enabled", true, "Enable or disable the mod")
- _defaultSortMode:  Config.Bind("General", "DefaultSortMode", "None", "Sort mode on open: None/Armor/Block/PhysDmg/etc")
- _rememberLastMode: Config.Bind("General", "RememberLastMode", false, "Keep last sort mode between station openings")

Properties (public static):
- Enabled          → Instance._enabled.Value
- DefaultSortMode  → Instance._defaultSortMode.Value
- RememberLastMode → Instance._rememberLastMode.Value

Static: public static CraftSortPlugin Instance { get; private set; }

Awake():
    Instance = this;
    if (!Enabled) return;
    new Harmony("dev.craftsort").PatchAll();
    Logger.LogInfo("CraftSort loaded");

Required usings: BepInEx, BepInEx.Configuration, HarmonyLib

---

## SortLogic.cs

Namespace: CraftSort

Enum SortMode:
    None, Armor, Block, PhysDmg, ChopDmg,
    FireDmg, FrostDmg, LightningDmg, PoisonDmg, SpiritDmg,
    Health, Stamina, Eitr, Name

Static class SortLogic:

    public static SortMode CurrentMode = SortMode.None;

    public static float GetSortValue(Recipe recipe):
        if recipe == null return 0f
        if recipe.m_item == null return 0f
        if recipe.m_item.m_itemData == null return 0f
        var s = recipe.m_item.m_itemData.m_shared
        if s == null return 0f
        switch CurrentMode:
            Armor        → s.m_armor
            Block        → s.m_blockPower
            PhysDmg      → s.m_damages.m_blunt + s.m_damages.m_slash + s.m_damages.m_pierce
            ChopDmg      → s.m_damages.m_chop
            FireDmg      → s.m_damages.m_fire
            FrostDmg     → s.m_damages.m_frost
            LightningDmg → s.m_damages.m_lightning
            PoisonDmg    → s.m_damages.m_poison
            SpiritDmg    → s.m_damages.m_spirit
            Health       → s.m_food
            Stamina      → s.m_foodStamina
            Eitr         → s.m_foodEitr
            _            → 0f

    public static void SortRecipes(List<Recipe> recipes):
        if CurrentMode == SortMode.None:
            return  // preserve game order: craftable items appear first naturally
        if CurrentMode == SortMode.Name:
            sort ascending by:
                Localization.instance.Localize(
                    r?.m_item?.m_itemData?.m_shared?.m_name ?? ""
                )
            null-safe for each recipe
        else:
            stable sort descending by GetSortValue(r)
            recipes with equal values keep original relative order

Required usings: System.Collections.Generic, System.Linq

---

## Patches.cs

Namespace: CraftSort
Required usings: HarmonyLib, System.Collections.Generic

Patch 1 — sort recipe list:
    [HarmonyPatch(typeof(Player), nameof(Player.GetAvailableRecipes))]
    class Patch_GetAvailableRecipes
        static void Postfix(ref List<Recipe> recipes)
            if (!CraftSortPlugin.Enabled) return
            SortLogic.SortRecipes(recipes)

Patch 2 — inject/refresh tab UI:
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateCraftingPanel))]
    class Patch_UpdateCraftingPanel
        static void Postfix(InventoryGui __instance)
            if (!CraftSortPlugin.Enabled) return
            TabUI.EnsureTabsExist(__instance)

Patch 3 — cleanup on close:
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    class Patch_InventoryGui_Hide
        static void Postfix()
            TabUI.Reset()
            if (!CraftSortPlugin.RememberLastMode)
                SortLogic.CurrentMode = SortMode.None

---

## TabUI.cs

Namespace: CraftSort
Required usings: UnityEngine, UnityEngine.UI, System.Collections.Generic

Static fields:
    private static GameObject _container
    private static readonly List<(Button btn, SortMode mode)> _buttons = new()

### Tab groups and labels

Detect station at runtime:
    var station = Player.m_localPlayer?.GetCurrentCraftingStation()
    bool isCauldron = station != null &&
        station.m_name.IndexOf("cauldron", StringComparison.OrdinalIgnoreCase) >= 0

Cauldron group (tag: _food_):
    (None, "All"), (Health, "HP"), (Stamina, "Stam"), (Eitr, "Eitr")

Combat group (tag: _combat_) — all stations including modded ones:
    (None, "All"), (Armor, "Armor"), (Block, "Block"), (PhysDmg, "Phys"),
    (FireDmg, "Fire"), (FrostDmg, "Frost"), (LightningDmg, "Ltng"),
    (PoisonDmg, "Psn"), (SpiritDmg, "Sprt"), (ChopDmg, "Chop")

Always visible (tag: _always_):
    (Name, "A→Z")

No hotkeys — mouse interaction only.

### Container layout

    GameObject name: "CraftSortTabContainer"
    Parent: gui.m_craftingPanel (worldPositionStays: false)

    RectTransform:
        anchorMin: (0, 0.5)
        anchorMax: (0, 0.5)
        pivot: (1, 0.5)
        anchoredPosition: (-5, 0)
        sizeDelta: (78, 500)

    VerticalLayoutGroup:
        spacing: 3
        childForceExpandHeight: false
        childForceExpandWidth: true

### Button structure (per button)

    GameObject name: "SortTab_{mode}_{group}"  (e.g. "SortTab_Armor_combat_")

    Components:
    - RectTransform: sizeDelta (78, 24)
    - Image: background color (see Visual style)
    - Button: targetGraphic = Image above
    - Child GameObject "Label":
        - RectTransform: stretch to fill parent (anchorMin 0,0 anchorMax 1,1 offsetMin/Max 0)
        - Text: label string, white, size 10, Bold, MiddleCenter
        - Font: try to grab from InventoryGui.instance.GetComponentInChildren<Text>()?.font
                fall back to Resources.GetBuiltinResource<Font>("Arial.ttf")
    - Child GameObject "Border":
        - RectTransform: stretch with inset 1px on all sides
        - Image: color (0.8, 0.65, 0.1, 1.0)
        - visible only when this button is the active sort mode

### Visual style — native Valheim look

    Normal background:   Color(0.15f, 0.10f, 0.05f, 0.85f)
    Hover:               Color(0.30f, 0.22f, 0.08f, 0.95f)
    Active background:   Color(0.55f, 0.42f, 0.08f, 1.0f)
    Active border:       Color(0.80f, 0.65f, 0.10f, 1.0f) — Border Image enabled
    Inactive border:     Border Image disabled (SetActive false)

    Button ColorBlock:
        normalColor:      Color(0.15f, 0.10f, 0.05f, 0.85f)
        highlightedColor: Color(0.30f, 0.22f, 0.08f, 0.95f)
        pressedColor:     Color(0.55f, 0.42f, 0.08f, 1.0f)
        selectedColor:    Color(0.55f, 0.42f, 0.08f, 1.0f)
        colorMultiplier:  1

### Methods

EnsureTabsExist(InventoryGui gui):
    if gui == null || gui.m_craftingPanel == null → return
    if _container != null:
        UpdateButtonStates()
        UpdateGroupVisibility()
        return
    CreateTabs(gui)

CreateTabs(InventoryGui gui):
    create container with layout (see above)
    create all food group buttons, then all combat group buttons, then always group
    call UpdateGroupVisibility()
    call UpdateButtonStates()

CreateButton(string label, SortMode mode, string group) → adds to _buttons list:
    build GameObject hierarchy described in Button structure
    var capturedMode = mode
    btn.onClick.AddListener(() => {
        SortLogic.CurrentMode = capturedMode
        UpdateButtonStates()
        InventoryGui.instance?.UpdateCraftingPanel(false)
    })

UpdateButtonStates():
    for each (btn, mode) in _buttons:
        if btn == null continue
        bool active = (mode == SortLogic.CurrentMode)
        btn.GetComponent<Image>().color = active
            ? Color(0.55f, 0.42f, 0.08f, 1.0f)
            : Color(0.15f, 0.10f, 0.05f, 0.85f)
        var border = btn.transform.Find("Border")?.gameObject
        if border != null: border.SetActive(active)

UpdateGroupVisibility():
    compute isCauldron (see Tab groups section)
    foreach Transform child in _container.transform:
        string n = child.name
        if n.Contains("_food_"):   child.gameObject.SetActive(isCauldron)
        if n.Contains("_combat_"): child.gameObject.SetActive(!isCauldron)
        if n.Contains("_always_"): child.gameObject.SetActive(true)

Reset():
    if _container != null: UnityEngine.Object.Destroy(_container)
    _container = null
    _buttons.Clear()

---

## Code conventions

- Every access to Recipe, ItemData, SharedData, Player, InventoryGui must be null-safe
- Use nameof() in HarmonyPatch attributes, never raw strings
- No server-side logic — this is a client-only mod
- After every file is implemented, run a full build before moving to the next file
- If build fails, fix all errors in the current file before proceeding

---

## Implementation order

Step 1: Install .NET 8 SDK — verify dotnet --version returns 8.x
Step 2: Add NuGet PackageReference to csproj — verify dotnet restore succeeds
Step 3: Implement SortLogic.cs — build — fix all errors
Step 4: Implement Plugin.cs — build — fix all errors
Step 5: Implement Patches.cs — build — fix all errors
Step 6: Implement TabUI.cs — build — fix all errors
Step 7: Confirm CraftSort.dll exists in BepInEx\plugins\
Step 8: After game launch, read LogOutput.log and verify success criteria

---

## Success criteria

- dotnet build exits with 0 errors and 0 reference warnings
- File exists: ...BepInEx\plugins\CraftSort.dll
- LogOutput.log line: [Info : BepInEx] Loading [CraftSort 1.0.0]
- LogOutput.log line: CraftSort loaded
- No lines containing [Error near CraftSort or Patch_

---

## Constraints

- BepInEx 5.4.x only — not BepInEx 6 — import Harmony as: using HarmonyLib
- Unity 6 engine (Valheim 0.221.13) — assembly_valheim.dll is from this version
- Private=false on all assembly references — DLLs must not be bundled in output
- Do not restructure CraftSort.csproj — only add the one NuGet PackageReference
- Client-side only — no server synchronization
- After any fix always rebuild and re-check all success criteria before continuing