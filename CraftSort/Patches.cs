using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace CraftSort
{
    [HarmonyPatch(typeof(InventoryGui), "UpdateRecipeList", new[] { typeof(List<Recipe>) })]
    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter(new[] {
        "com.maxsch.valheim.vnei",
        "org.bepinex.plugins.jewelcrafting",
        "Azumatt.Recycle_N_Reclaim",
        "Azumatt.AzuAntiArthriticCrafting",
        "com.sighsorry1029.InventorySlots",
        "shudnal.MyLittleUI",
        "goldenrevolver.SortedMenus",
        "aedenthorn.CraftingFilter",
        "com.MoistGravy.CraftingSearchBar"
    })]
    class Patch_UpdateRecipeList
    {
        private static FieldInfo? _availableRecipesField;
        private static PropertyInfo? _recipeProp;
        private static FieldInfo? _recipeField;
        private static object[] _reorderCache = System.Array.Empty<object>();

        [HarmonyPrepare]
        static bool Prepare()
        {
            var method = AccessTools.Method(typeof(InventoryGui), "UpdateRecipeList", new[] { typeof(List<Recipe>) });
            if (method == null)
            {
                CraftSortPlugin.Log("[CraftSort] WARNING: UpdateRecipeList not found — Transpiler disabled");
                return false;
            }
            return true;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var sortMethod = AccessTools.Method(typeof(Patch_UpdateRecipeList), nameof(SortAvailableRecipes));

            if (sortMethod == null)
            {
                CraftSortPlugin.Log("[CraftSort] WARNING: Transpiler cannot resolve sort method");
                return codes;
            }

            // Find the positioning for-loop at the end of UpdateRecipeList:
            //   ldc.i4.0 → stloc.s → br/br.s → ldarg.0 → ldfld m_availableRecipes
            int insertAt = -1;
            for (int i = 0; i < codes.Count - 4; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_I4_0 &&
                    codes[i + 1].opcode == OpCodes.Stloc_S &&
                    (codes[i + 2].opcode == OpCodes.Br || codes[i + 2].opcode == OpCodes.Br_S) &&
                    codes[i + 3].opcode == OpCodes.Ldarg_0 &&
                    codes[i + 4].opcode == OpCodes.Ldfld &&
                    codes[i + 4].operand is FieldInfo fi && fi.Name == "m_availableRecipes")
                {
                    insertAt = i;
                }
            }

            if (insertAt < 0)
            {
                CraftSortPlugin.Log("[CraftSort] WARNING: positioning loop not found in UpdateRecipeList IL");
                return codes;
            }

            // Transfer labels so switch/br jumps land on our code, not skip over it
            var ldarg = new CodeInstruction(OpCodes.Ldarg_0);
            var call = new CodeInstruction(OpCodes.Call, sortMethod);
            ldarg.labels = codes[insertAt].labels;
            codes[insertAt].labels = new List<Label>();
            codes.Insert(insertAt, call);
            codes.Insert(insertAt, ldarg);

            CraftSortPlugin.Log("[CraftSort] Transpiler injected sort before positioning loop");
            return codes;
        }

        /// <summary>
        /// Called from injected IL right before vanilla positions recipe elements.
        /// Sorts m_availableRecipes in-place so vanilla positions them in our order.
        /// </summary>
        static void SortAvailableRecipes(InventoryGui gui)
        {
            if (!CraftSortPlugin.Enabled) return;
            if (SortLogic.CurrentMode == SortMode.None) return;
            if (gui == null) return;

            var list = GetAvailableRecipesList(gui);
            if (list == null || list.Count < 2) return;

            int count = list.Count;
            SortLogic.EnsureCaches(count);

            if (SortLogic.CurrentMode == SortMode.Name)
                SortByNameOnList(list, count);
            else
                SortByValueOnList(list, count);
        }

        private static IList? GetAvailableRecipesList(InventoryGui gui)
        {
            _availableRecipesField ??= AccessTools.Field(typeof(InventoryGui), "m_availableRecipes");
            return _availableRecipesField?.GetValue(gui) as IList;
        }

        private static Recipe? GetRecipeFromPair(object pair)
        {
            if (_recipeProp == null && _recipeField == null)
            {
                var type = pair.GetType();
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                _recipeProp = type.GetProperty("Recipe", flags);
                if (_recipeProp == null)
                    _recipeField = type.GetField("Recipe", flags);
            }
            if (_recipeProp != null)
                return _recipeProp.GetValue(pair) as Recipe;
            if (_recipeField != null)
                return _recipeField.GetValue(pair) as Recipe;
            return null;
        }

        private static void SortByValueOnList(IList list, int count)
        {
            var valueCache = SortLogic.GetValueCache();
            var indexCache = SortLogic.GetIndexCache();

            for (int i = 0; i < count; i++)
            {
                valueCache[i] = SortLogic.GetSortValue(GetRecipeFromPair(list[i]));
                indexCache[i] = i;
            }

            System.Array.Sort(indexCache, 0, count, SortLogic.ValueComparer);
            ApplyListOrder(list, count, indexCache);
        }

        private static void SortByNameOnList(IList list, int count)
        {
            var nameCache = SortLogic.GetNameCache();
            var indexCache = SortLogic.GetIndexCache();
            var loc = Localization.instance;

            for (int i = 0; i < count; i++)
            {
                var recipe = GetRecipeFromPair(list[i]);
                string? key = recipe?.m_item?.m_itemData?.m_shared?.m_name;
                indexCache[i] = i;
                string name = "";
                if (key != null && loc != null)
                {
                    string locName = loc.Localize(key);
                    if (locName != null) name = locName;
                }
                else if (key != null)
                {
                    name = key;
                }
                nameCache[i] = name;
            }

            System.Array.Sort(indexCache, 0, count, SortLogic.NameComparer);
            ApplyListOrder(list, count, indexCache);
        }

        private static void ApplyListOrder(IList list, int count, int[] indexCache)
        {
            if (_reorderCache.Length < count)
                _reorderCache = new object[count];

            for (int i = 0; i < count; i++)
                _reorderCache[i] = list[indexCache[i]];
            for (int i = 0; i < count; i++)
                list[i] = _reorderCache[i];
        }
    }

    /// <summary>
    /// AAA Crafting compatibility: sorts the FULL cached recipe list before pagination slices it.
    /// Runs AFTER AAA Crafting's Prefix (which populates RecipeListPerfCache.CraftSortedFiltered).
    /// Without this, our Transpiler only sorts the current page (13 items).
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), "UpdateRecipeList", new[] { typeof(List<Recipe>) })]
    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter(new[] { "Azumatt.AzuAntiArthriticCrafting" })]
    class Patch_UpdateRecipeList_AAACrafting
    {
        private static bool _aaaChecked;
        private static FieldInfo? _cacheField;
        private static FieldInfo? _pageField;
        private static MethodInfo? _getPerPageMethod;

        [HarmonyPrepare]
        static bool Prepare()
        {
            return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("Azumatt.AzuAntiArthriticCrafting");
        }

        static void Prefix(ref List<Recipe> recipes)
        {
            if (!CraftSortPlugin.Enabled) return;
            if (SortLogic.CurrentMode == SortMode.None) return;

            if (!_aaaChecked)
            {
                _aaaChecked = true;
                ResolveAAATypes();
            }

            if (_cacheField == null) return;

            var fullList = _cacheField.GetValue(null) as List<Recipe>;
            if (fullList == null || fullList.Count < 2) return;

            SortLogic.SortRecipeList(fullList);

            if (_pageField == null || _getPerPageMethod == null) return;

            int page = (int)_pageField.GetValue(null);
            int perPage = (int)_getPerPageMethod.Invoke(null, null);
            if (perPage < 1) perPage = 13;

            int offset = (page - 1) * perPage;
            int count = System.Math.Min(perPage, fullList.Count - offset);
            if (count <= 0) return;

            recipes = fullList.GetRange(offset, count);
        }

        private static void ResolveAAATypes()
        {
            try
            {
                var cacheType = System.Type.GetType("AzuAntiArthriticCrafting.Patches.RecipeListPerfCache, AzuAntiArthriticCrafting");
                if (cacheType == null)
                {
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        cacheType = asm.GetType("AzuAntiArthriticCrafting.Patches.RecipeListPerfCache");
                        if (cacheType != null) break;
                    }
                }
                if (cacheType != null)
                    _cacheField = cacheType.GetField("CraftSortedFiltered",
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                var paginatorType = System.Type.GetType("AzuAntiArthriticCrafting.Patches.PaginatorPatches, AzuAntiArthriticCrafting");
                if (paginatorType == null)
                {
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        paginatorType = asm.GetType("AzuAntiArthriticCrafting.Patches.PaginatorPatches");
                        if (paginatorType != null) break;
                    }
                }
                if (paginatorType != null)
                    _pageField = paginatorType.GetField("CraftingWindowPage",
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                var utilsType = System.Type.GetType("AzuAntiArthriticCrafting.Utilities.Utilities, AzuAntiArthriticCrafting");
                if (utilsType == null)
                {
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        utilsType = asm.GetType("AzuAntiArthriticCrafting.Utilities.Utilities");
                        if (utilsType != null) break;
                    }
                }
                if (utilsType != null)
                    _getPerPageMethod = utilsType.GetMethod("GetPerPage",
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                CraftSortPlugin.Log($"[CraftSort] AAA Crafting compat: cache={_cacheField != null}, page={_pageField != null}, perPage={_getPerPageMethod != null}");
            }
            catch (System.Exception ex)
            {
                CraftSortPlugin.Log($"[CraftSort] AAA Crafting compat error: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateCraftingPanel")]
    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter(new[] {
        "com.maxsch.valheim.vnei",
        "org.bepinex.plugins.jewelcrafting",
        "Azumatt.Recycle_N_Reclaim",
        "Azumatt.AzuAntiArthriticCrafting",
        "com.sighsorry1029.InventorySlots",
        "shudnal.MyLittleUI",
        "goldenrevolver.SortedMenus",
        "aedenthorn.CraftingFilter",
        "com.MoistGravy.CraftingSearchBar"
    })]
    class Patch_UpdateCraftingPanel
    {
        static void Postfix(InventoryGui __instance)
        {
            if (!CraftSortPlugin.Enabled) return;
            TabUI.EnsureTabsExist(__instance);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    class Patch_InventoryGui_Hide
    {
        static void Postfix()
        {
            TabUI.Reset();
            if (!CraftSortPlugin.RememberLastMode)
            {
                SortLogic.CurrentMode = System.Enum.TryParse<SortMode>(
                    CraftSortPlugin.DefaultSortMode, true, out var mode)
                    ? mode : SortMode.None;
            }
        }
    }
}
