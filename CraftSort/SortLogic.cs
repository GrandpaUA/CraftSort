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

        private static float[] _valueCache = System.Array.Empty<float>();
        private static int[] _indexCache = System.Array.Empty<int>();
        private static string[] _nameCache = System.Array.Empty<string>();

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

        public static void EnsureCaches(int count)
        {
            if (_valueCache.Length < count)
            {
                _valueCache = new float[count];
                _indexCache = new int[count];
                _nameCache = new string[count];
            }
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
