using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CraftSort
{
    [HarmonyPatch(typeof(Player), nameof(Player.GetAvailableRecipes))]
    class Patch_GetAvailableRecipes
    {
        static void Postfix(ref List<Recipe> available)
        {
            if (!CraftSortPlugin.Enabled) return;
            SortLogic.ApplySort(available);
        }
    }

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
        [HarmonyPrepare]
        static bool Prepare()
        {
            var method = AccessTools.Method(typeof(InventoryGui), "UpdateRecipeList", new[] { typeof(List<Recipe>) });
            if (method == null)
            {
                CraftSortPlugin.Log("[CraftSort] WARNING: UpdateRecipeList(List<Recipe>) not found - sort layer 3 disabled");
                return false;
            }
            return true;
        }

        private static FieldInfo? _availableRecipesField;
        private static FieldInfo? _recipeListSpaceField;
        private static PropertyInfo? _recipeProp;
        private static FieldInfo? _recipeField;
        private static PropertyInfo? _interfaceElementProp;
        private static FieldInfo? _interfaceElementField;
        private static bool _interfaceElementTried;
        private static object[] _reorderCache = System.Array.Empty<object>();

        static void Postfix(InventoryGui __instance)
        {
            if (!CraftSortPlugin.Enabled) return;
            if (SortLogic.CurrentMode == SortMode.None) return;

            try
            {
                var list = GetAvailableRecipesList(__instance);
                if (list == null)
                {
                    CraftSortPlugin.Log("[Sort] m_availableRecipes is null");
                    return;
                }
                if (list.Count < 2)
                {
                    CraftSortPlugin.Log($"[Sort] list too small: {list.Count}");
                    return;
                }

                int count = list.Count;
                SortLogic.EnsureCaches(count);

                float space = GetRecipeListSpace(__instance);
                CraftSortPlugin.Log($"[Sort] mode={SortLogic.CurrentMode} count={count} space={space}");

                if (SortLogic.CurrentMode == SortMode.Name)
                {
                    SortByNameOnList(list, count);
                }
                else
                {
                    SortByValueOnList(list, count);
                }

                // Log first 3 elements after sort
                for (int i = 0; i < System.Math.Min(3, count); i++)
                {
                    var r = GetRecipeFromPair(list[i]);
                    string name = r?.m_item?.m_itemData?.m_shared?.m_name ?? "null";
                    CraftSortPlugin.Log($"[Sort] [{i}] = {name}");
                }

                RepositionElements(list, count, space);
                CraftSortPlugin.Log("[Sort] RepositionElements done");
            }
            catch (System.Exception ex)
            {
                CraftSortPlugin.Log($"[Patch_UpdateRecipeList] Error: {ex.InnerException?.Message ?? ex.Message}\n{ex.StackTrace}");
            }
        }

        private static IList? GetAvailableRecipesList(InventoryGui gui)
        {
            if (_availableRecipesField == null)
            {
                _availableRecipesField = AccessTools.Field(typeof(InventoryGui), "m_availableRecipes");
                if (_availableRecipesField == null)
                {
                    CraftSortPlugin.Log("[CraftSort] WARNING: m_availableRecipes field not found - sort layer 3 disabled");
                    return null;
                }
            }
            return _availableRecipesField.GetValue(gui) as IList;
        }

        private static float GetRecipeListSpace(InventoryGui gui)
        {
            _recipeListSpaceField ??= AccessTools.Field(typeof(InventoryGui), "m_recipeListSpace");
            if (_recipeListSpaceField == null) return 30f;
            return (float)_recipeListSpaceField.GetValue(gui);
        }

        private static Recipe? GetRecipeFromPair(object pair)
        {
            if (_recipeProp == null)
            {
                var type = pair.GetType();
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                _recipeProp = type.GetProperty("Recipe", flags);
                if (_recipeProp == null)
                {
                    var field = type.GetField("Recipe", flags);
                    if (field != null)
                    {
                        _recipeProp = null;
                        _recipeField = field;
                    }
                }
            }
            if (_recipeProp != null)
                return _recipeProp.GetValue(pair) as Recipe;
            if (_recipeField != null)
                return _recipeField.GetValue(pair) as Recipe;
            return null;
        }

        private static GameObject? GetInterfaceElement(object pair)
        {
            if (!_interfaceElementTried)
            {
                _interfaceElementTried = true;
                var type = pair.GetType();
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                _interfaceElementProp = type.GetProperty("InterfaceElement", flags);
                if (_interfaceElementProp == null)
                {
                    _interfaceElementField = type.GetField("InterfaceElement", flags);
                }
                if (_interfaceElementProp == null && _interfaceElementField == null)
                {
                    // Fallback: find any GameObject field/property
                    foreach (var f in type.GetFields(flags))
                    {
                        if (f.FieldType == typeof(GameObject))
                        {
                            _interfaceElementField = f;
                            CraftSortPlugin.Log($"[Sort] InterfaceElement found as field: {f.Name}");
                            break;
                        }
                    }
                    if (_interfaceElementField == null)
                    {
                        foreach (var p in type.GetProperties(flags))
                        {
                            if (p.PropertyType == typeof(GameObject))
                            {
                                _interfaceElementProp = p;
                                CraftSortPlugin.Log($"[Sort] InterfaceElement found as property: {p.Name}");
                                break;
                            }
                        }
                    }
                }
                CraftSortPlugin.Log($"[Sort] InterfaceElement: prop={_interfaceElementProp?.Name ?? "null"}, field={_interfaceElementField?.Name ?? "null"}");
            }

            if (_interfaceElementProp != null)
                return _interfaceElementProp.GetValue(pair) as GameObject;
            if (_interfaceElementField != null)
                return _interfaceElementField.GetValue(pair) as GameObject;
            return null;
        }

        private static void SortByValueOnList(IList list, int count)
        {
            var valueCache = SortLogic.GetValueCache();
            var indexCache = SortLogic.GetIndexCache();

            for (int i = 0; i < count; i++)
            {
                var recipe = GetRecipeFromPair(list[i]);
                valueCache[i] = SortLogic.ComputeSortValue(recipe);
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
                    string loc_name = loc.Localize(key);
                    if (loc_name != null) name = loc_name;
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

        private static void RepositionElements(IList list, int count, float space)
        {
            int repositioned = 0;
            int nullElements = 0;
            for (int i = 0; i < count; i++)
            {
                var element = GetInterfaceElement(list[i]);
                if (element != null && element.transform is RectTransform rt)
                {
                    rt.anchoredPosition = new Vector2(0f, i * -space);
                    repositioned++;
                }
                else
                {
                    nullElements++;
                }
            }
            CraftSortPlugin.Log($"[Sort] Reposition: {repositioned} moved, {nullElements} null elements");
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
            SortLogic.RestoreSortWeights();
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
