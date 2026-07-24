using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace CraftSort
{
    public static class TabUI
    {
        private static GameObject? _container;
        private static RectTransform? _containerRect;
        private static readonly List<(Button btn, SortMode mode)> _buttons = new List<(Button, SortMode)>();
        private static MethodInfo? _updateCraftingPanel;
        private static Sprite? _roundedSprite;
        private static Sprite? _borderSprite;
        private static RectTransform? _panelRect;

        private static readonly Color NormalBg   = new Color(0.15f, 0.10f, 0.05f, 0.85f);
        private static readonly Color HoverBg    = new Color(0.30f, 0.22f, 0.08f, 0.95f);
        private static readonly Color ActiveBg   = new Color(0.25f, 0.18f, 0.06f, 0.95f);
        private static readonly Color BorderCol  = new Color(1.0f, 0.82f, 0.15f, 1.0f);

        private const float ButtonHeight = 22f;
        private const int CornerRadius = 4;
        private const int BorderThickness = 3;
        private const float MinContainerWidth = 38f;
        private const float MaxContainerWidth = 80f;
        private const float ContainerLeftPad = 2f;
        private const float ContainerGap = 3f;

        public static void EnsureTabsExist(InventoryGui gui)
        {
            if (gui == null || gui.m_crafting == null)
                return;

            if (_container != null)
            {
                UpdateContainerWidth();
                UpdateButtonStates();
                UpdateGroupVisibility();
                return;
            }

            CreateTabs(gui);
        }

        private static void CreateTabs(InventoryGui gui)
        {
            _panelRect = gui.m_crafting;
            _framesSinceCreate = 0;
            _lastWidth = -1f;

            _roundedSprite = CreateRoundedSprite(32, 32, CornerRadius);
            _borderSprite = CreateRoundedBorderSprite(32, 32, CornerRadius, BorderThickness);

            _container = new GameObject("CraftSortTabContainer");
            _container.transform.SetParent(_panelRect, false);

            _containerRect = _container.AddComponent<RectTransform>();
            _containerRect.anchorMin = new Vector2(0f, 0.12f);
            _containerRect.anchorMax = new Vector2(0f, 0.74f);
            _containerRect.pivot = new Vector2(0f, 1f);
            _containerRect.offsetMin = new Vector2(ContainerLeftPad, 0f);
            _containerRect.offsetMax = new Vector2(ContainerLeftPad + 50f, 0f);

            var vlg = _container.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childAlignment = TextAnchor.UpperLeft;

            var foodTabs = new (SortMode mode, string label)[]
            {
                (SortMode.None, "All"),
                (SortMode.Health, "HP"),
                (SortMode.Stamina, "Stam"),
                (SortMode.Eitr, "Eitr"),
            };

            var combatTabs = new (SortMode mode, string label)[]
            {
                (SortMode.None, "All"),
                (SortMode.Armor, "Armor"),
                (SortMode.Block, "Block"),
                (SortMode.PhysDmg, "Phys"),
                (SortMode.FireDmg, "Fire"),
                (SortMode.FrostDmg, "Frost"),
                (SortMode.LightningDmg, "Ltng"),
                (SortMode.PoisonDmg, "Psn"),
                (SortMode.SpiritDmg, "Sprt"),
                (SortMode.ChopDmg, "Chop"),
            };

            foreach (var (mode, label) in foodTabs)
                CreateButton(label, mode, "_food_");

            foreach (var (mode, label) in combatTabs)
                CreateButton(label, mode, "_combat_");

            CreateButton("A\u2192Z", SortMode.Name, "_always_");

            UpdateGroupVisibility();
            UpdateButtonStates();
        }

        private static float _lastWidth = -1f;
        private static int _framesSinceCreate;

        private static void UpdateContainerWidth()
        {
            if (_containerRect == null || _panelRect == null) return;

            _framesSinceCreate++;
            if (_framesSinceCreate < 3) return;

            float width = DetectAvailableWidth();
            if (Mathf.Approximately(width, _lastWidth)) return;
            _lastWidth = width;

            _containerRect.offsetMax = new Vector2(ContainerLeftPad + width, 0f);
        }

        private static float DetectAvailableWidth()
        {
            if (_panelRect == null) return 40f;

            var panelCorners = new Vector3[4];
            _panelRect.GetWorldCorners(panelCorners);
            float panelWorldWidth = Vector3.Distance(panelCorners[0], panelCorners[3]);
            if (panelWorldWidth < 1f) return 40f;

            Canvas.ForceUpdateCanvases();

            float recipeLeft = FindRecipeListLeftEdge();
            float panelW = _panelRect.rect.width;

            if (recipeLeft <= 0f)
            {
                float pctFallback = panelW * 0.065f;
                float result = Mathf.Clamp(pctFallback, MinContainerWidth, MaxContainerWidth);
                CraftSortPlugin.Log(
                    $"[TabUI] Detection failed. panelW={panelW:F0}, fallback={result:F0}");
                return result;
            }

            float available = recipeLeft - ContainerLeftPad - ContainerGap;
            float clamped = Mathf.Clamp(available, MinContainerWidth, MaxContainerWidth);
            CraftSortPlugin.Log(
                $"[TabUI] Detected recipeLeft={recipeLeft:F0}, panelW={panelW:F0}, width={clamped:F0}");
            return clamped;
        }

        private static float FindRecipeListLeftEdge()
        {
            if (_panelRect == null) return 0f;

            float panelWidth = _panelRect.rect.width;
            if (panelWidth < 1f) return 0f;

            float panelLeftLocal = -panelWidth * _panelRect.pivot.x;

            float bestDistance = float.MaxValue;

            var scrollRects = _panelRect.GetComponentsInChildren<ScrollRect>(true);
            foreach (var sr in scrollRects)
            {
                var srRect = sr.GetComponent<RectTransform>();
                if (srRect == null || srRect == _containerRect) continue;

                float dist = GetLeftEdgeDistanceFromPanelLeft(srRect, panelLeftLocal);
                if (dist > 5f && dist < panelWidth * 0.30f && dist < bestDistance)
                {
                    bestDistance = dist;
                }
            }

            if (bestDistance < float.MaxValue)
                return bestDistance;

            var grids = _panelRect.GetComponentsInChildren<GridLayoutGroup>(true);
            foreach (var grid in grids)
            {
                var gridRect = grid.GetComponent<RectTransform>();
                if (gridRect == null || gridRect == _containerRect) continue;

                float dist = GetLeftEdgeDistanceFromPanelLeft(gridRect, panelLeftLocal);
                if (dist > 5f && dist < panelWidth * 0.30f && dist < bestDistance)
                {
                    bestDistance = dist;
                }
            }

            if (bestDistance < float.MaxValue)
                return bestDistance;

            var hLayouts = _panelRect.GetComponentsInChildren<HorizontalLayoutGroup>(true);
            foreach (var hl in hLayouts)
            {
                var hlRect = hl.GetComponent<RectTransform>();
                if (hlRect == null || hlRect == _containerRect) continue;
                if (hlRect.rect.width < panelWidth * 0.3f) continue;

                float dist = GetLeftEdgeDistanceFromPanelLeft(hlRect, panelLeftLocal);
                if (dist > 5f && dist < panelWidth * 0.30f && dist < bestDistance)
                {
                    bestDistance = dist;
                }
            }

            return bestDistance < float.MaxValue ? bestDistance : 0f;
        }

        private static float GetLeftEdgeDistanceFromPanelLeft(RectTransform child, float panelLeftLocal)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);

            Vector3 localBL = _panelRect!.InverseTransformPoint(corners[0]);
            return localBL.x - panelLeftLocal;
        }

        private static void CreateButton(string label, SortMode mode, string group)
        {
            var go = new GameObject($"SortTab_{mode}_{group}");
            go.transform.SetParent(_container!.transform, false);
            go.AddComponent<RectTransform>();

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = ButtonHeight;
            le.flexibleWidth = 1f;

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
            text.fontSize = 9;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            Font? font = null;
            if (InventoryGui.instance != null)
            {
                var existingText = InventoryGui.instance.GetComponentInChildren<Text>();
                if (existingText != null)
                    font = existingText.font;
            }
            text.font = font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

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
                SortLogic.CurrentMode = capturedMode;
                UpdateButtonStates();
                InvokeUpdateCraftingPanel();
            });

            _buttons.Add((btn, mode));
        }

        private static void UpdateButtonStates()
        {
            foreach (var (btn, mode) in _buttons)
            {
                if (btn == null) continue;

                bool active = mode == SortLogic.CurrentMode;

                var border = btn.transform.Find("Border")?.gameObject;
                if (border != null)
                    border.SetActive(active);
            }
        }

        private static void UpdateGroupVisibility()
        {
            if (_container == null) return;

            var station = Player.m_localPlayer?.GetCurrentCraftingStation();
            bool isCauldron = station != null &&
                station.m_name.IndexOf("cauldron", StringComparison.OrdinalIgnoreCase) >= 0;

            foreach (Transform child in _container.transform)
            {
                string n = child.name;
                if (n.Contains("_food_"))
                    child.gameObject.SetActive(isCauldron);
                if (n.Contains("_combat_"))
                    child.gameObject.SetActive(!isCauldron);
                if (n.Contains("_always_"))
                    child.gameObject.SetActive(true);
            }
        }

        private static void InvokeUpdateCraftingPanel()
        {
            var gui = InventoryGui.instance;
            if (gui == null) return;

            _updateCraftingPanel ??= typeof(InventoryGui).GetMethod(
                "UpdateCraftingPanel",
                BindingFlags.NonPublic | BindingFlags.Instance);

            _updateCraftingPanel?.Invoke(gui, new object[] { false });
        }

        public static void Reset()
        {
            if (_container != null)
                UnityEngine.Object.Destroy(_container);

            _container = null;
            _containerRect = null;
            _panelRect = null;
            _buttons.Clear();
            _roundedSprite = null;
            _borderSprite = null;
            _lastWidth = -1f;
            _framesSinceCreate = 0;
        }

        private static Sprite CreateRoundedSprite(int width, int height, int radius)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float alpha = RoundedRectAlpha(x, y, width, height, radius);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
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
            {
                for (int x = 0; x < width; x++)
                {
                    float outer = RoundedRectAlpha(x, y, width, height, radius);
                    float inner = RoundedRectAlphaInset(x, y, width, height, radius, thickness);
                    float alpha = Mathf.Clamp01(outer - inner);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static float RoundedRectAlpha(int x, int y, int w, int h, int r)
        {
            r = Mathf.Min(r, w / 2, h / 2);
            if (r <= 0) return 1f;

            float dx = 0f, dy = 0f;

            if (x < r) dx = r - x;
            else if (x >= w - r) dx = x - (w - r - 1);

            if (y < r) dy = r - y;
            else if (y >= h - r) dy = y - (h - r - 1);

            if (dx > 0 && dy > 0)
            {
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                return Mathf.Clamp01(r - dist + 0.5f);
            }

            return 1f;
        }

        private static float RoundedRectAlphaInset(int x, int y, int w, int h, int r, int inset)
        {
            int ix = x - inset;
            int iy = y - inset;
            int iw = w - inset * 2;
            int ih = h - inset * 2;
            int ir = Mathf.Max(0, r - inset);

            if (ix < 0 || iy < 0 || ix >= iw || iy >= ih)
                return 0f;

            return RoundedRectAlpha(ix, iy, iw, ih, ir);
        }
    }
}
