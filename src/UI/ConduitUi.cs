using System;
using System.Linq;
using KingdomFoundation.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomLeylineConduits
{
    internal sealed class ConduitUi
    {
        internal static readonly Color Paper = new Color(1, .94f, .79f);
        internal static readonly Color Muted = new Color(.72f, .67f, .57f);
        internal static readonly Color Gold = new Color(.94f, .73f, .38f);
        internal static readonly Color Green = new Color(.62f, .85f, .57f);
        internal static readonly Color Red = new Color(.96f, .52f, .44f);
        internal static readonly Color Blue = new Color(.57f, .75f, .91f);
        internal static readonly Color WellColor = new Color(26f / 255f, 18f / 255f, 26f / 255f, 1f);
        internal static readonly Color DropdownHighlight = new Color(.27f, .19f, .23f, 1f);
        internal readonly TMP_FontAsset Font;

        internal ConduitUi(TMP_FontAsset font)
        {
            Font = font;
        }

        internal Sprite Sprite(string name) => FoundationApi.Presentation.FindSprite(name);

        internal static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        internal static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        internal Image Box(Transform parent, string name, float x, float y, float width, float height, string sprite, float pixelsPerUnitMultiplier = 1f)
        {
            var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.sprite = Sprite(sprite);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            image.color = image.sprite ? Color.white : new Color(.2f, .15f, .13f);
            return image;
        }

        internal Image WellBox(Transform parent, string name, float x, float y, float width, float height)
        {
            var backing = Fill(parent, name, x, y, width, height, WellColor);
            backing.raycastTarget = true;
            var borderSprite = Sprite("DefaultBorderDark") ?? Sprite("SimpleBox") ?? Sprite("Button03");
            if (borderSprite)
            {
                var border = Rect(backing.transform, "WellBorder", 0, 0, width, height);
                Stretch(border);
                border.SetAsFirstSibling();
                var borderImage = border.gameObject.AddComponent<Image>();
                borderImage.sprite = borderSprite;
                borderImage.type = Image.Type.Sliced;
                borderImage.color = Color.white;
                borderImage.raycastTarget = false;
            }
            return backing;
        }

        internal Image Fill(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        internal Image Icon(Transform parent, Sprite sprite, float x, float y, float size)
        {
            var image = Rect(parent, "Icon", x, y, size, size).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = sprite;
            return image;
        }

        internal void Rule(Transform parent, float x, float y, float width)
            => Fill(parent, "Rule", x, y, width, 1, new Color(.68f, .51f, .32f, .4f));

        internal void Select(Button button, bool selected)
        {
            ((Image)button.targetGraphic).sprite = Sprite(selected ? "Button02" : "Button03");
            button.GetComponentInChildren<TMP_Text>().color = selected ? Gold : Paper;
        }

        internal void StyleModeButton(Button button, bool isInput, bool selected)
        {
            var image = (Image)button.targetGraphic;
            image.sprite = Sprite(isInput ? "DecoTitleGreen" : "DecoTitleRed") ?? Sprite("Button03");
            image.color = selected
                ? Color.white
                : (isInput ? new Color(.56f, .68f, .58f, .88f) : new Color(.68f, .50f, .52f, .88f));
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label)
                label.color = selected ? Paper : Muted;
        }

        internal TMP_Text Text(Transform parent, string text, float x, float y, float width, float height, float size)
        {
            var label = Rect(parent, text, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Font;
            label.fontSize = size;
            label.color = Paper;
            label.text = text;
            label.raycastTarget = false;
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        internal Button Button(Transform parent, string text, float x, float y, float width, float height, Action action, string spriteName = "Button03", float fontSize = 17f)
        {
            var image = Box(parent, text + "Button", x, y, width, height, spriteName, 1.5f);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.highlightedColor = new Color(1, .94f, .76f);
            colors.pressedColor = new Color(.7f, .65f, .5f);
            colors.disabledColor = new Color(.5f, .5f, .5f, .65f);
            colors.fadeDuration = .08f;
            button.colors = colors;
            var label = Text(image.transform, text, 0, 0, width, height, fontSize);
            label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform, width <= 32f ? 2f : 6f);
            button.onClick.AddListener(() => action());
            return button;
        }

        internal TMP_InputField InputField(Transform parent, string name, float x, float y, float width, float height, string placeholderText, int characterLimit, float fontSize = 15f)
        {
            var box = WellBox(parent, name, x, y, width, height);
            var viewport = Rect(box.transform, "Viewport", 10, 4, width - 20, height - 8);
            Stretch(viewport, 0f);
            viewport.offsetMin = new Vector2(10, 4);
            viewport.offsetMax = new Vector2(-10, -4);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = (TextMeshProUGUI)Text(viewport, "", 0, 0, width - 20, height - 8, fontSize);
            Stretch(text.rectTransform);
            text.overflowMode = TextOverflowModes.Overflow;
            var hint = Text(viewport, placeholderText ?? "", 0, 0, width - 20, height - 8, fontSize);
            Stretch(hint.rectTransform);
            hint.color = Muted;
            var input = box.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = box;
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = hint;
            input.characterLimit = characterLimit;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.richText = false;
            input.customCaretColor = true;
            input.caretColor = Paper;
            input.navigation = new Navigation { mode = Navigation.Mode.None };
            return input;
        }

        internal TMP_Dropdown Dropdown(Transform parent, string name, float x, float y, float width, float height)
        {
            var template = FindDropdownTemplate();
            if (template)
            {
                var obj = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
                obj.name = name;
                var rect = (RectTransform)obj.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(x, -y);
                rect.sizeDelta = new Vector2(width, height);
                var dropdown = obj.GetComponent<TMP_Dropdown>();
                dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
                dropdown.navigation = new Navigation { mode = Navigation.Mode.None };
                StyleClonedDropdown(dropdown, width, height);
                return dropdown;
            }
            return BuildFallbackDropdown(parent, name, x, y, width, height);
        }

        static TMP_Dropdown FindDropdownTemplate()
        {
            var options = FoundationApi.UiAccess.OptionsTemplate;
            if (options)
            {
                var found = options.GetComponentInChildren<TMP_Dropdown>(true);
                if (found)
                    return found;
            }
            var popups = FoundationApi.UiAccess.PopupTemplates;
            if (popups != null)
            {
                foreach (var popup in popups)
                {
                    var found = popup ? popup.GetComponentInChildren<TMP_Dropdown>(true) : null;
                    if (found)
                        return found;
                }
            }
            return Resources.FindObjectsOfTypeAll<TMP_Dropdown>().FirstOrDefault();
        }

        void StyleClonedDropdown(TMP_Dropdown dropdown, float width, float height)
        {
            var bg = dropdown.GetComponent<Image>();
            if (bg)
            {
                bg.sprite = null;
                bg.type = Image.Type.Simple;
                bg.color = WellColor;
                bg.raycastTarget = true;
                var borderSprite = Sprite("DefaultBorderDark") ?? Sprite("SimpleBox") ?? Sprite("Button03");
                if (borderSprite && !dropdown.transform.Find("WellBorder"))
                {
                    var border = Rect(dropdown.transform, "WellBorder", 0, 0, width, height);
                    Stretch(border);
                    border.SetAsFirstSibling();
                    var borderImage = border.gameObject.AddComponent<Image>();
                    borderImage.sprite = borderSprite;
                    borderImage.type = Image.Type.Sliced;
                    borderImage.color = Color.white;
                    borderImage.raycastTarget = false;
                }
            }
            if (dropdown.captionText)
            {
                dropdown.captionText.font = Font;
                dropdown.captionText.color = Paper;
                dropdown.captionText.alignment = TextAlignmentOptions.MidlineLeft;
                dropdown.captionText.textWrappingMode = TextWrappingModes.NoWrap;
                dropdown.captionText.overflowMode = TextOverflowModes.Ellipsis;
                dropdown.captionText.enableAutoSizing = true;
                dropdown.captionText.fontSizeMin = 11f;
                dropdown.captionText.fontSizeMax = 15f;
                dropdown.captionText.raycastTarget = false;
                var captionRect = dropdown.captionText.rectTransform;
                captionRect.anchorMin = Vector2.zero;
                captionRect.anchorMax = Vector2.one;
                captionRect.offsetMin = new Vector2(10f, 2f);
                captionRect.offsetMax = new Vector2(-28f, -2f);
            }
            var arrowTransform = dropdown.transform.Find("Arrow") as RectTransform;
            if (arrowTransform)
            {
                arrowTransform.anchorMin = arrowTransform.anchorMax = new Vector2(1f, .5f);
                arrowTransform.pivot = new Vector2(1f, .5f);
                arrowTransform.anchoredPosition = new Vector2(-8f, 0f);
                arrowTransform.sizeDelta = new Vector2(16f, 16f);
                var arrowImage = arrowTransform.GetComponent<Image>();
                if (arrowImage)
                    arrowImage.color = Color.white;
            }
            if (dropdown.itemText)
            {
                dropdown.itemText.font = Font;
                dropdown.itemText.color = Paper;
                dropdown.itemText.textWrappingMode = TextWrappingModes.NoWrap;
                dropdown.itemText.overflowMode = TextOverflowModes.Ellipsis;
            }
            if (dropdown.template)
            {
                var templateBg = dropdown.template.GetComponent<Image>();
                if (templateBg)
                {
                    templateBg.sprite = Sprite("DefaultBorderDark") ?? templateBg.sprite;
                    templateBg.type = Image.Type.Sliced;
                    templateBg.color = WellColor;
                }
                var toggle = dropdown.itemText ? dropdown.itemText.GetComponentInParent<Toggle>() : null;
                if (toggle && toggle.targetGraphic is Image itemBg)
                    itemBg.color = DropdownHighlight;
            }
        }

        TMP_Dropdown BuildFallbackDropdown(Transform parent, string name, float x, float y, float width, float height)
        {
            var box = WellBox(parent, name, x, y, width, height);
            var caption = Text(box.transform, "", 10, 0, width - 30, height, 15);
            var template = Rect(box.transform, "Template", 0, height, width, 120);
            template.gameObject.SetActive(false);
            var viewport = Rect(template, "Viewport", 0, 0, width, 120);
            Stretch(viewport);
            var content = Rect(viewport, "Content", 0, 0, width, 32);
            var itemRect = Rect(content, "Item", 0, 0, width, 32);
            var toggle = itemRect.gameObject.AddComponent<Toggle>();
            var itemLabel = Text(itemRect, "", 10, 0, width - 20, 32, 14);
            var dropdown = box.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = box;
            dropdown.captionText = caption;
            dropdown.template = template;
            dropdown.itemText = itemLabel;
            toggle.isOn = false;
            return dropdown;
        }
    }
}
