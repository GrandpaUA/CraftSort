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
        Name,
        New
    }

    public static class SortLogic
    {
        public static SortMode CurrentMode = SortMode.None;

        private static float[] _valueCache = System.Array.Empty<float>();
        private static int[] _indexCache = System.Array.Empty<int>();
        private static string[] _nameCache = System.Array.Empty<string>();
        private static Recipe?[] _recipeCache = System.Array.Empty<Recipe?>();

        public static float[] GetValueCache() => _valueCache;
        public static int[] GetIndexCache() => _indexCache;
        public static string[] GetNameCache() => _nameCache;
        public static IComparer<int> ValueComparer => ValueIndexComparer.Instance;
        public static IComparer<int> NameComparer => NameIndexComparer.Instance;

        public static float GetSortValue(Recipe? recipe)
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
                    return s.m_armor + ArmorTypePriority(s);
                case SortMode.Block:
                    return s.m_blockPower + BlockTypePriority(s);
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
                    return s.m_food + FoodTypePriority(s);
                case SortMode.Stamina:
                    return s.m_foodStamina + FoodTypePriority(s);
                case SortMode.Eitr:
                    return s.m_foodEitr + FoodTypePriority(s);
                default:
                    return 0f;
            }
        }

        private const float TypeBonus = 100000f;

        private static float ArmorTypePriority(ItemDrop.ItemData.SharedData s)
        {
            switch ((int)s.m_itemType)
            {
                case 6: case 7: case 11: case 12: case 17: case 18:
                    return TypeBonus;
                default:
                    return 0f;
            }
        }

        private static float BlockTypePriority(ItemDrop.ItemData.SharedData s)
        {
            return (int)s.m_itemType == 5 ? TypeBonus : 0f;
        }

        private static float FoodTypePriority(ItemDrop.ItemData.SharedData s)
        {
            return (int)s.m_itemType == 2 ? TypeBonus : 0f;
        }

        public static void EnsureCaches(int count)
        {
            if (_valueCache.Length < count)
            {
                _valueCache = new float[count];
                _indexCache = new int[count];
                _nameCache = new string[count];
            }
        }

        /// <summary>
        /// Sorts a List&lt;Recipe&gt; in-place by CurrentMode.
        /// Used for AAA Crafting's full cached list (global pagination sort).
        /// </summary>
        public static void SortRecipeList(List<Recipe> recipes)
        {
            if (recipes == null || recipes.Count < 2) return;
            if (CurrentMode == SortMode.None) return;

            int count = recipes.Count;
            EnsureCaches(count);
            if (_recipeCache.Length < count)
                _recipeCache = new Recipe?[count];

            if (CurrentMode == SortMode.Name)
            {
                var nameCache = GetNameCache();
                var indexCache = GetIndexCache();
                var loc = Localization.instance;
                for (int i = 0; i < count; i++)
                {
                    string? key = recipes[i]?.m_item?.m_itemData?.m_shared?.m_name;
                    indexCache[i] = i;
                    nameCache[i] = (key != null && loc != null) ? (loc.Localize(key) ?? "") : (key ?? "");
                }
                System.Array.Sort(indexCache, 0, count, NameComparer);
            }
            else
            {
                var valueCache = GetValueCache();
                var indexCache = GetIndexCache();
                for (int i = 0; i < count; i++)
                {
                    valueCache[i] = GetSortValue(recipes[i]);
                    indexCache[i] = i;
                }
                System.Array.Sort(indexCache, 0, count, ValueComparer);
            }

            var idx = GetIndexCache();
            for (int i = 0; i < count; i++)
                _recipeCache[i] = recipes[idx[i]];
            for (int i = 0; i < count; i++)
                recipes[i] = _recipeCache[i]!;
        }

        private sealed class ValueIndexComparer : IComparer<int>
        {
            public static readonly ValueIndexComparer Instance = new ValueIndexComparer();
            public int Compare(int x, int y)
            {
                int cmp = _valueCache[y].CompareTo(_valueCache[x]);
                return cmp != 0 ? cmp : x.CompareTo(y);
            }
        }

        private sealed class NameIndexComparer : IComparer<int>
        {
            public static readonly NameIndexComparer Instance = new NameIndexComparer();
            public int Compare(int x, int y)
            {
                int cmp = string.Compare(_nameCache[x], _nameCache[y], System.StringComparison.OrdinalIgnoreCase);
                return cmp != 0 ? cmp : x.CompareTo(y);
            }
        }
    }
}
