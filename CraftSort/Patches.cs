using HarmonyLib;
using System.Collections.Generic;

namespace CraftSort
{
    [HarmonyPatch(typeof(Player), nameof(Player.GetAvailableRecipes))]
    class Patch_GetAvailableRecipes
    {
        static void Postfix(ref List<Recipe> available)
        {
            if (!CraftSortPlugin.Enabled) return;
            SortLogic.SortRecipes(available);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateCraftingPanel")]
    [HarmonyPriority(Priority.Last)]
    class Patch_UpdateCraftingPanel
    {
        static void Postfix(InventoryGui __instance)
        {
            if (!CraftSortPlugin.Enabled) return;
            TabUI.EnsureTabsExist(__instance);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "Update")]
    [HarmonyPriority(Priority.Last)]
    class Patch_InventoryGui_Update
    {
        static void Postfix(InventoryGui __instance)
        {
            if (!CraftSortPlugin.Enabled) return;
            TabUI.TickPosition();
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    class Patch_InventoryGui_Hide
    {
        static void Postfix()
        {
            TabUI.Reset();
            if (!CraftSortPlugin.RememberLastMode)
                SortLogic.CurrentMode = SortMode.None;
        }
    }
}
