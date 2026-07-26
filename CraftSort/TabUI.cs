using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace CraftSort
{
    public static class TabUI
    {
        private static GameObject? _foodContainer;
        private static GameObject? _combatContainer;
        private static readonly List<(Button btn, Image img, Image border, SortMode mode)> _buttons
            = new List<(Button, Image, Image, SortMode)>();
        private static readonly List<Text> _newTabTexts = new List<Text>();
        private static MethodInfo? _updateCraftingPanel;
        private static readonly object[] _updatePanelArgs = { false };
        private static Sprite? _roundedSprite;
        private static Sprite? _borderSprite;
        private static Font? _cachedFont;
        private static SortMode _lastActiveMode = (SortMode)(-1);
        private static string? _cachedStationKey;
        private static bool _cachedIsFoodStation;

        private static readonly Color NormalBg   = new Color(0.08f, 0.05f, 0.02f, 0.95f);
        private static readonly Color HoverBg    = new Color(0.18f, 0.13f, 0.05f, 0.97f);
        private static readonly Color ActiveBg   = new Color(0.15f, 0.10f, 0.04f, 0.97f);
        private static readonly Color BorderCol  = new Color(1.0f, 0.82f, 0.15f, 1.0f);

        private const float ButtonSize = 50f;
        private const float GridSpacingX = 4f;
        private const float GridSpacingY = 2f;
        private const float ContainerGap = 9f;
        private const float TopOffset = -200f;
        private const int CornerRadius = 4;
        private const int BorderThickness = 5;
        private const float IconSize = 34f;

        public static void EnsureTabsExist(InventoryGui gui)
        {
            if (gui == null || gui.m_crafting == null)
                return;

            if (_foodContainer != null && _combatContainer != null)
            {
                UpdateButtonStates();
                UpdateGroupVisibility();
                UpdateNewCount(NewRecipeTracker.NewCount);
                return;
            }

            CreateTabs(gui);
        }

        private static void CreateTabs(InventoryGui gui)
        {
            var panel = gui.m_crafting;

            _roundedSprite = CreateRoundedSprite(32, 32, CornerRadius);
            _borderSprite = CreateRoundedBorderSprite(32, 32, CornerRadius, BorderThickness);

            _cachedFont = null;
            if (InventoryGui.instance != null)
            {
                var existingText = InventoryGui.instance.GetComponentInChildren<Text>();
                if (existingText != null)
                    _cachedFont = existingText.font;
            }
            _cachedFont ??= Resources.GetBuiltinResource<Font>("Arial.ttf");

            // ── Food container (single column) ──
            _foodContainer = new GameObject("CraftSortFoodTabs");
            _foodContainer.transform.SetParent(panel, false);

            var foodRt = _foodContainer.AddComponent<RectTransform>();
            foodRt.anchorMin = new Vector2(0f, 1f);
            foodRt.anchorMax = new Vector2(0f, 1f);
            foodRt.pivot = new Vector2(1f, 1f);
            foodRt.anchoredPosition = new Vector2(-ContainerGap, TopOffset);
            foodRt.sizeDelta = new Vector2(ButtonSize, 0f);

            var foodVlg = _foodContainer.AddComponent<VerticalLayoutGroup>();
            foodVlg.spacing = GridSpacingY;
            foodVlg.childForceExpandWidth = true;
            foodVlg.childForceExpandHeight = false;
            foodVlg.childControlWidth = true;
            foodVlg.childControlHeight = true;
            foodVlg.childAlignment = TextAnchor.UpperCenter;

            var foodFitter = _foodContainer.AddComponent<ContentSizeFitter>();
            foodFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Combat container (2 columns grid) ──
            _combatContainer = new GameObject("CraftSortCombatTabs");
            _combatContainer.transform.SetParent(panel, false);

            float combatWidth = ButtonSize * 2 + GridSpacingX;
            var combatRt = _combatContainer.AddComponent<RectTransform>();
            combatRt.anchorMin = new Vector2(0f, 1f);
            combatRt.anchorMax = new Vector2(0f, 1f);
            combatRt.pivot = new Vector2(1f, 1f);
            combatRt.anchoredPosition = new Vector2(-ContainerGap, TopOffset);
            combatRt.sizeDelta = new Vector2(combatWidth, 0f);

            var combatGrid = _combatContainer.AddComponent<GridLayoutGroup>();
            combatGrid.cellSize = new Vector2(ButtonSize, ButtonSize);
            combatGrid.spacing = new Vector2(GridSpacingX, GridSpacingY);
            combatGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            combatGrid.constraintCount = 2;
            combatGrid.childAlignment = TextAnchor.UpperLeft;

            var combatFitter = _combatContainer.AddComponent<ContentSizeFitter>();
            combatFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Food buttons ──
            CreateButton("All", SortMode.None, _foodContainer.transform);
            CreateButton("HP", SortMode.Health, _foodContainer.transform);
            CreateButton("Stam", SortMode.Stamina, _foodContainer.transform);
            CreateButton("Eitr", SortMode.Eitr, _foodContainer.transform);
            CreateButton("A\u2192Z", SortMode.Name, _foodContainer.transform);
            CreateButton("New", SortMode.New, _foodContainer.transform);

            // ── Combat buttons (10 + 2 always = 12, 6 per column) ──
            CreateButton("All", SortMode.None, _combatContainer.transform);
            CreateButton("Armor", SortMode.Armor, _combatContainer.transform);
            CreateButton("Block", SortMode.Block, _combatContainer.transform);
            CreateButton("Phys", SortMode.PhysDmg, _combatContainer.transform);
            CreateButton("Fire", SortMode.FireDmg, _combatContainer.transform);
            CreateButton("Frost", SortMode.FrostDmg, _combatContainer.transform);
            CreateButton("Ltng", SortMode.LightningDmg, _combatContainer.transform);
            CreateButton("Psn", SortMode.PoisonDmg, _combatContainer.transform);
            CreateButton("Sprt", SortMode.SpiritDmg, _combatContainer.transform);
            CreateButton("Chop", SortMode.ChopDmg, _combatContainer.transform);
            CreateButton("A\u2192Z", SortMode.Name, _combatContainer.transform);
            CreateButton("New", SortMode.New, _combatContainer.transform);

            UpdateNewCount(NewRecipeTracker.NewCount);
            UpdateGroupVisibility();
            UpdateButtonStates();
        }

        private static void CreateButton(string label, SortMode mode, Transform parent)
        {
            var go = new GameObject($"SortTab_{mode}");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = ButtonSize;
            le.preferredHeight = ButtonSize;

            var img = go.AddComponent<Image>();
            img.sprite = _roundedSprite;
            img.type = Image.Type.Sliced;
            img.color = NormalBg;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = NormalBg;
            colors.highlightedColor = HoverBg;
            colors.pressedColor = ActiveBg;
            colors.selectedColor = NormalBg;
            colors.colorMultiplier = 1f;
            btn.colors = colors;

            var sprite = IconFactory.GetIcon(mode);
            if (sprite != null)
            {
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(go.transform, false);

                var iconRt = iconGo.AddComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.sizeDelta = new Vector2(IconSize, IconSize);
                iconRt.anchoredPosition = Vector2.zero;

                var iconImg = iconGo.AddComponent<Image>();
                iconImg.sprite = sprite;
                iconImg.color = Color.white;
                iconImg.raycastTarget = false;
            }
            else
            {
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(go.transform, false);

                var labelRt = labelGo.AddComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;

                var text = labelGo.AddComponent<Text>();
                text.text = label;
                text.color = Color.white;
                text.fontSize = 10;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.font = _cachedFont ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

                if (mode == SortMode.New)
                    _newTabTexts.Add(text);
            }

            var borderGo = new GameObject("Border");
            borderGo.transform.SetParent(go.transform, false);
            borderGo.transform.SetAsFirstSibling();

            var borderRt = borderGo.AddComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero;
            borderRt.offsetMax = Vector2.zero;

            var borderImg = borderGo.AddComponent<Image>();
            borderImg.sprite = _borderSprite;
            borderImg.type = Image.Type.Sliced;
            borderImg.color = BorderCol;
            borderImg.raycastTarget = false;
            borderGo.SetActive(false);

            var capturedMode = mode;
            btn.onClick.AddListener(() =>
            {
                SortLogic.CurrentMode = SortLogic.CurrentMode == capturedMode
                    ? SortMode.None
                    : capturedMode;
                UpdateButtonStates();
                InvokeUpdateCraftingPanel();
            });

            _buttons.Add((btn, img, borderImg, mode));
        }

        private static void UpdateButtonStates()
        {
            bool fullUpdate = _lastActiveMode == (SortMode)(-1);
            if (!fullUpdate && _lastActiveMode == SortLogic.CurrentMode) return;

            for (int i = 0; i < _buttons.Count; i++)
            {
                var (btn, img, border, mode) = _buttons[i];
                if (btn == null) continue;

                if (fullUpdate || mode == _lastActiveMode || mode == SortLogic.CurrentMode)
                {
                    bool active = mode == SortLogic.CurrentMode;
                    if (border != null)
                        border.gameObject.SetActive(active);
                    if (img != null)
                        img.color = active ? ActiveBg : NormalBg;
                }
            }

            _lastActiveMode = SortLogic.CurrentMode;
        }

        private static void UpdateGroupVisibility()
        {
            var station = Player.m_localPlayer?.GetCurrentCraftingStation();
            string? stationKey = station != null ? station.gameObject.name : null;

            bool isFoodStation;
            if (stationKey == _cachedStationKey)
            {
                isFoodStation = _cachedIsFoodStation;
            }
            else
            {
                isFoodStation = DetectFoodStation(station);
                _cachedStationKey = stationKey;
                _cachedIsFoodStation = isFoodStation;
                CraftSortPlugin.Log($"[CraftSort] Station '{stationKey}' → {(isFoodStation ? "food" : "combat")} UI");
            }

            if (_foodContainer != null) _foodContainer.SetActive(isFoodStation);
            if (_combatContainer != null) _combatContainer.SetActive(!isFoodStation);
        }

        /// <summary>
        /// Three-layer food station detection that works with any mod:
        /// 1. Check recipes currently displayed in the UI (already filtered by vanilla for this station)
        /// 2. Check all ObjectDB recipes that reference this station prefab
        /// 3. Fallback to known vanilla station names
        /// </summary>
        private static bool DetectFoodStation(CraftingStation? station)
        {
            if (station == null) return false;

            // Layer 1: recipes currently shown in the crafting panel
            var gui = InventoryGui.instance;
            if (gui != null)
            {
                var list = Patch_UpdateRecipeList.GetAvailableRecipesList(gui);
                if (list != null && list.Count > 0)
                {
                    int foodCount = 0;
                    int totalCount = 0;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var recipe = Patch_UpdateRecipeList.GetRecipeFromPair(list[i]);
                        var shared = recipe?.m_item?.m_itemData?.m_shared;
                        if (shared == null) continue;
                        totalCount++;
                        if (IsFoodItem(shared)) foodCount++;
                    }
                    if (totalCount > 0)
                        return foodCount > 0;
                }
            }

            // Layer 2: all recipes in ObjectDB that belong to this station
            var db = ObjectDB.instance;
            if (db != null && db.m_recipes != null)
            {
                string stationPrefab = GetPrefabName(station.gameObject);
                int foodCount = 0;
                int totalCount = 0;

                for (int i = 0; i < db.m_recipes.Count; i++)
                {
                    var recipe = db.m_recipes[i];
                    if (recipe == null || recipe.m_craftingStation == null) continue;
                    if (GetPrefabName(recipe.m_craftingStation.gameObject) != stationPrefab) continue;

                    var shared = recipe.m_item?.m_itemData?.m_shared;
                    if (shared == null) continue;

                    totalCount++;
                    if (IsFoodItem(shared)) foodCount++;
                }

                if (totalCount > 0)
                    return foodCount > 0;
            }

            // Layer 3: fallback — known vanilla food station names
            string name = station.gameObject.name;
            return name.IndexOf("cauldron", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("preptable", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsFoodItem(ItemDrop.ItemData.SharedData shared)
        {
            return (int)shared.m_itemType == 2
                && (shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f);
        }

        private static string GetPrefabName(GameObject go)
        {
            string name = go.name;
            const string cloneSuffix = "(Clone)";
            if (name.EndsWith(cloneSuffix))
                return name.Substring(0, name.Length - cloneSuffix.Length);
            return name;
        }

        private static void InvokeUpdateCraftingPanel()
        {
            var gui = InventoryGui.instance;
            if (gui == null) return;

            if (_updateCraftingPanel == null)
            {
                _updateCraftingPanel = typeof(InventoryGui).GetMethod(
                    "UpdateCraftingPanel",
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new Type[] { typeof(bool) },
                    null);

                if (_updateCraftingPanel == null)
                {
                    _updateCraftingPanel = typeof(InventoryGui).GetMethod(
                        "UpdateCraftingPanel",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }

                if (_updateCraftingPanel == null)
                {
                    CraftSortPlugin.Log("[TabUI] ERROR: UpdateCraftingPanel method not found via reflection");
                    return;
                }
            }

            try
            {
                _updateCraftingPanel.Invoke(gui, _updatePanelArgs);
            }
            catch (Exception ex)
            {
                CraftSortPlugin.Log($"[TabUI] InvokeUpdateCraftingPanel error: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public static void UpdateNewCount(int count)
        {
            string text = count > 0 ? $"New ({count})" : "New";
            for (int i = 0; i < _newTabTexts.Count; i++)
            {
                if (_newTabTexts[i] != null)
                    _newTabTexts[i].text = text;
            }
        }

        public static void Reset()
        {
            if (_foodContainer != null)
                UnityEngine.Object.Destroy(_foodContainer);
            if (_combatContainer != null)
                UnityEngine.Object.Destroy(_combatContainer);

            _foodContainer = null;
            _combatContainer = null;
            _buttons.Clear();
            _newTabTexts.Clear();
            _roundedSprite = null;
            _borderSprite = null;
            _lastActiveMode = (SortMode)(-1);
            _cachedStationKey = null;
        }

        private static Sprite CreateRoundedSprite(int width, int height, int radius)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float alpha = RoundedRectAlpha(x, y, width, height, radius);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }

            tex.Apply();
            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static Sprite CreateRoundedBorderSprite(int width, int height, int radius, int thickness)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float outer = RoundedRectAlpha(x, y, width, height, radius);
                    float inner = RoundedRectAlpha(
                        x - thickness, y - thickness,
                        width - thickness * 2, height - thickness * 2,
                        Mathf.Max(1, radius - thickness));
                    float alpha = Mathf.Clamp01(outer - inner);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }

            tex.Apply();
            float b = thickness + 1;
            var border = new Vector4(b, b, b, b);
            return Sprite.Create(tex, new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static float RoundedRectAlpha(int x, int y, int w, int h, int r)
        {
            if (x < 0 || x >= w || y < 0 || y >= h) return 0f;
            int cx = Mathf.Clamp(x, r, w - 1 - r);
            int cy = Mathf.Clamp(y, r, h - 1 - r);
            float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
            if (x >= r && x < w - r) return 1f;
            if (y >= r && y < h - r) return 1f;
            return Mathf.Clamp01(r - dist + 0.5f);
        }
    }
}
