using System.Collections.Generic;

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

        private static readonly List<(Recipe recipe, int originalWeight)> _weightBackup
            = new List<(Recipe, int)>();

        public static float GetSortValue(Recipe recipe)
        {
            if (recipe == null) return 0f;
            var item = recipe.m_item;
            if (item == null) return 0f;
            var data = item.m_itemData;
            if (data == null) return 0f;
            var s = data.m_shared;
            if (s == null) return 0f;

            switch (CurrentMode)
            {
                case SortMode.Armor:
                    return s.m_armor + ItemTypePriority(s, 6, 7, 11, 12, 17, 18);
                case SortMode.Block:
                    return s.m_blockPower + ItemTypePriority(s, 5);
                case SortMode.PhysDmg:
                    return s.m_damages.GetTotalPhysicalDamage();
                case SortMode.ChopDmg:
                    return s.m_damages.m_chop;
                case SortMode.FireDmg:
                    return s.m_damages.m_fire;
                case SortMode.FrostDmg:
                    return s.m_damages.m_frost;
                case SortMode.LightningDmg:
                    return s.m_damages.m_lightning;
                case SortMode.PoisonDmg:
                    return s.m_damages.m_poison;
                case SortMode.SpiritDmg:
                    return s.m_damages.m_spirit;
                case SortMode.Health:
                    return s.m_food + ItemTypePriority(s, 2);
                case SortMode.Stamina:
                    return s.m_foodStamina + ItemTypePriority(s, 2);
                case SortMode.Eitr:
                    return s.m_foodEitr + ItemTypePriority(s, 2);
                default:
                    return 0f;
            }
        }

        private static float ItemTypePriority(ItemDrop.ItemData.SharedData s, params int[] types)
        {
            int t = (int)s.m_itemType;
            for (int i = 0; i < types.Length; i++)
            {
                if (t == types[i]) return 100000f;
            }
            return 0f;
        }

        public static void ApplySortWeights(List<Recipe> recipes)
        {
            _weightBackup.Clear();

            if (CurrentMode == SortMode.None || recipes == null)
                return;

            if (CurrentMode == SortMode.Name)
            {
                for (int i = 0; i < recipes.Count; i++)
                {
                    var r = recipes[i];
                    if (r == null) continue;
                    _weightBackup.Add((r, r.m_listSortWeight));
                    r.m_listSortWeight = 0;
                }
            }
            else
            {
                for (int i = 0; i < recipes.Count; i++)
                {
                    var r = recipes[i];
                    if (r == null) continue;
                    _weightBackup.Add((r, r.m_listSortWeight));

                    float val = GetSortValue(r);
                    int weight = -(int)(val * 100f);
                    if (weight < -999999) weight = -999999;
                    if (weight > 999999) weight = 999999;
                    r.m_listSortWeight = weight;
                }
            }
        }

        public static void RestoreSortWeights()
        {
            for (int i = 0; i < _weightBackup.Count; i++)
            {
                var (recipe, original) = _weightBackup[i];
                if (recipe != null)
                    recipe.m_listSortWeight = original;
            }
            _weightBackup.Clear();
        }
    }
}
