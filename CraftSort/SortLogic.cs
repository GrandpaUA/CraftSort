using System.Collections.Generic;
using System.Linq;

namespace CraftSort
{
    public enum SortMode
    {
        None,
        Armor,
        Block,
        PhysDmg,
        ChopDmg,
        FireDmg,
        FrostDmg,
        LightningDmg,
        PoisonDmg,
        SpiritDmg,
        Health,
        Stamina,
        Eitr,
        Name
    }

    public static class SortLogic
    {
        public static SortMode CurrentMode = SortMode.None;

        public static float GetSortValue(Recipe recipe)
        {
            if (recipe == null) return 0f;
            if (recipe.m_item == null) return 0f;
            if (recipe.m_item.m_itemData == null) return 0f;

            var s = recipe.m_item.m_itemData.m_shared;
            if (s == null) return 0f;

            switch (CurrentMode)
            {
                case SortMode.Armor:        return s.m_armor;
                case SortMode.Block:        return s.m_blockPower;
                case SortMode.PhysDmg:      return s.m_damages.m_blunt + s.m_damages.m_slash + s.m_damages.m_pierce;
                case SortMode.ChopDmg:      return s.m_damages.m_chop;
                case SortMode.FireDmg:      return s.m_damages.m_fire;
                case SortMode.FrostDmg:     return s.m_damages.m_frost;
                case SortMode.LightningDmg: return s.m_damages.m_lightning;
                case SortMode.PoisonDmg:    return s.m_damages.m_poison;
                case SortMode.SpiritDmg:    return s.m_damages.m_spirit;
                case SortMode.Health:       return s.m_food;
                case SortMode.Stamina:      return s.m_foodStamina;
                case SortMode.Eitr:         return s.m_foodEitr;
                default:                    return 0f;
            }
        }

        public static void SortRecipes(List<Recipe> recipes)
        {
            if (CurrentMode == SortMode.None)
                return;

            if (CurrentMode == SortMode.Name)
            {
                var sorted = recipes
                    .OrderBy(r => Localization.instance.Localize(
                        r?.m_item?.m_itemData?.m_shared?.m_name ?? ""))
                    .ToList();

                recipes.Clear();
                recipes.AddRange(sorted);
            }
            else
            {
                var sorted = recipes
                    .OrderByDescending(r => GetSortValue(r))
                    .ToList();

                recipes.Clear();
                recipes.AddRange(sorted);
            }
        }
    }
}
