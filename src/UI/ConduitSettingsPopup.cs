using System;
using System.Collections.Generic;
using System.Linq;
using KingdomFoundation.Api;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KingdomLeylineConduits
{
    public sealed class ConduitSettingsPopup : FoundationPopup
    {
        const float Width = 360, Height = 416;
        ConduitWorld owner;
        LeylineConduit conduit;
        ConduitUi ui;
        RectTransform panel;
        RectTransform settingsBody, pickerBody, iconGrid;
        Button input, output;
        Button previousPage, nextPage;
        TMP_Text description, message, pageLabel;
        Image iconPreview;
        ConduitRuneGraphic runePreview;
        List<(string icon, string name)> iconCatalog;
        string selectedIcon = "";
        int iconPage;
        const int IconsPerPage = 24;
        internal TMP_InputField ChannelField { get; private set; }
        internal TMP_InputField IconSearch { get; private set; }
        internal bool PickerVisible => pickerBody && pickerBody.gameObject.activeSelf;
        internal int IconPage => iconPage;
        ConduitMode selected;
        bool restoreInput;
        int openedFrame;
        internal bool IsOpen => gameObject.activeSelf;

        internal void Initialize(ConduitWorld world, LeylineConduit element, TMP_FontAsset font, Transform parent)
        {
            owner = world;
            conduit = element;
            ui = new ConduitUi(font);
            transform.SetParent(parent, false);
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
            gameObject.AddComponent<GraphicRaycaster>();
            var backdrop = ConduitUi.Rect(root, "DismissBackdrop", 0, 0, 0, 0);
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
            backdrop.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .5f);
            backdrop.gameObject.AddComponent<Button>().onClick.AddListener(ClosePopup);
            panel = ui.Box(root, "LeylineSettingsPanel", 0, 0, Width, Height, "Tooltip").rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero;
            ui.Text(panel, "Leyline Conduit", 20, 14, 280, 32, 23);
            ui.Button(panel, "X", 310, 16, 30, 30, ClosePopup);
            settingsBody = ConduitUi.Rect(panel, "LeylineSettingsBody", 0, 0, Width, Height);
            BuildSettings(settingsBody);
            BuildPicker();
        }

        void BuildSettings(RectTransform target)
        {
            ui.Text(target, "Mode", 20, 54, 320, 22, 14).color = ConduitUi.Muted;
            input = ui.Button(target, "Input", 20, 80, 154, 34, () => SelectMode(ConduitMode.Input));
            input.name = "SelectLeylineInput";
            output = ui.Button(target, "Output", 186, 80, 154, 34, () => SelectMode(ConduitMode.Output));
            output.name = "SelectLeylineOutput";
            description = ui.Text(target, "", 20, 119, 320, 22, 14);
            ui.Text(target, "Leyline Name", 20, 151, 320, 22, 15);
            var box = ui.Box(target, "LeylineNameInput", 20, 178, 320, 36, "Button03");
            var viewport = ConduitUi.Rect(box.transform, "Viewport", 10, 4, 300, 28);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = (TextMeshProUGUI)ui.Text(viewport, "", 0, 0, 300, 28, 17);
            text.overflowMode = TextOverflowModes.Overflow;
            var hint = ui.Text(viewport, "e.g. iron", 0, 0, 300, 28, 17);
            hint.color = ConduitUi.Muted;
            ChannelField = box.gameObject.AddComponent<TMP_InputField>();
            ChannelField.targetGraphic = box;
            ChannelField.textViewport = viewport;
            ChannelField.textComponent = text;
            ChannelField.placeholder = hint;
            ChannelField.characterLimit = LeylineConduit.ChannelLimit;
            ChannelField.lineType = TMP_InputField.LineType.SingleLine;
            ChannelField.richText = false;
            ChannelField.customCaretColor = true;
            ChannelField.caretColor = ConduitUi.Paper;
            ui.Text(target, "Same name links across tabs.", 20, 218, 320, 22, 12).color = ConduitUi.Muted;
            ui.Text(target, "Icon", 20, 245, 320, 22, 15);
            iconPreview = ui.Icon(target, null, 20, 274, 32);
            iconPreview.name = "LeylineIconPreview";
            runePreview = ConduitUi.Rect(target, "RunePreview", 20, 274, 32, 32).gameObject.AddComponent<ConduitRuneGraphic>();
            runePreview.color = Color.white;
            runePreview.raycastTarget = false;
            ui.Button(target, "Set Icon", 64, 273, 180, 34, ShowIconPicker).name = "SetLeylineIcon";
            ui.Button(target, "Rune", 254, 273, 86, 34, () => SelectIcon("")).name = "ResetLeylineIcon";
            message = ui.Text(target, "", 20, 320, 320, 34, 12);
            message.color = ConduitUi.Red;
            message.textWrappingMode = TextWrappingModes.Normal;
            ui.Button(target, "Apply", 110, 370, 140, 30, Apply).name = "ApplyLeylineSettings";
        }

        void BuildPicker()
        {
            pickerBody = ConduitUi.Rect(panel, "LeylineIconPicker", 0, 0, Width, Height);
            ui.Text(pickerBody, "Choose Icon", 20, 54, 320, 24, 17);
            var box = ui.Box(pickerBody, "SearchLeylineIcons", 20, 84, 320, 32, "Button03");
            var viewport = ConduitUi.Rect(box.transform, "Viewport", 10, 3, 300, 26);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = (TextMeshProUGUI)ui.Text(viewport, "", 0, 0, 300, 26, 16);
            text.overflowMode = TextOverflowModes.Overflow;
            var hint = ui.Text(viewport, "Search game icons\u2026", 0, 0, 300, 26, 16);
            hint.color = ConduitUi.Muted;
            IconSearch = box.gameObject.AddComponent<TMP_InputField>();
            IconSearch.targetGraphic = box;
            IconSearch.textViewport = viewport;
            IconSearch.textComponent = text;
            IconSearch.placeholder = hint;
            IconSearch.characterLimit = 80;
            IconSearch.lineType = TMP_InputField.LineType.SingleLine;
            IconSearch.richText = false;
            IconSearch.customCaretColor = true;
            IconSearch.caretColor = ConduitUi.Paper;
            IconSearch.onValueChanged.AddListener(_ =>
            {
                iconPage = 0;
                RefreshIcons();
            });
            pageLabel = ui.Text(pickerBody, "", 20, 119, 320, 20, 12);
            pageLabel.color = ConduitUi.Muted;
            iconGrid = ConduitUi.Rect(pickerBody, "LeylineIconGrid", 20, 142, 318, 210);
            previousPage = ui.Button(pickerBody, "Prev", 20, 370, 80, 30, () =>
            {
                iconPage--;
                RefreshIcons();
            });
            previousPage.name = "PreviousLeylineIcons";
            ui.Button(pickerBody, "Back", 140, 370, 80, 30, HideIconPicker).name = "BackFromLeylineIcons";
            nextPage = ui.Button(pickerBody, "Next", 260, 370, 80, 30, () =>
            {
                iconPage++;
                RefreshIcons();
            });
            nextPage.name = "NextLeylineIcons";
            pickerBody.gameObject.SetActive(false);
        }

        internal void ShowIconPicker()
        {
            iconCatalog = ConduitIcons.Catalog();
            iconPage = 0;
            IconSearch.SetTextWithoutNotify("");
            settingsBody.gameObject.SetActive(false);
            pickerBody.gameObject.SetActive(true);
            RefreshIcons();
        }

        void HideIconPicker()
        {
            IconSearch.DeactivateInputField();
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(null);
            pickerBody.gameObject.SetActive(false);
            settingsBody.gameObject.SetActive(true);
        }

        internal void SelectIcon(string id)
        {
            selectedIcon = id;
            RefreshIconPreview();
            if (PickerVisible)
                HideIconPicker();
        }

        void RefreshIconPreview()
        {
            iconPreview.sprite = ConduitIcons.Find(selectedIcon);
            iconPreview.enabled = iconPreview.sprite;
            runePreview.enabled = !iconPreview.sprite;
        }

        void RefreshIcons()
        {
            if (iconCatalog == null)
                return;
            // Destroy is deferred by Unity; disable old buttons immediately so
            // search/page changes never leave invisible clickable choices.
            foreach (Transform child in iconGrid)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            string query = (IconSearch.text ?? "").Trim();
            var choices = iconCatalog.Where(option =>
                (option.name ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || option.icon.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            int pages = Math.Max(1, (choices.Count + IconsPerPage - 1) / IconsPerPage);
            iconPage = Mathf.Clamp(iconPage, 0, pages - 1);
            pageLabel.text = choices.Count == 0
                ? "No matching icons."
                : "Page " + (iconPage + 1) + " / " + pages + " \u00b7 " + choices.Count + " icons";
            previousPage.interactable = iconPage > 0;
            nextPage.interactable = iconPage + 1 < pages;
            int index = 0;
            foreach (var choice in choices.Skip(iconPage * IconsPerPage).Take(IconsPerPage))
            {
                string id = choice.icon;
                var button = ui.Button(iconGrid, "", (index % 6) * 54, (index / 6) * 54, 48, 48, () => SelectIcon(id));
                button.name = "LeylineIconChoice_" + id;
                ui.Icon(button.transform, ConduitIcons.Find(id), 8, 8, 32);
                ui.Select(button, selectedIcon == id);
                button.gameObject.AddComponent<FoundationTooltipSource>().SetTooltip(choice.name ?? id, "Use this conduit icon.");
                index++;
            }
        }

        void SelectMode(ConduitMode mode)
        {
            selected = mode;
            ui.Select(input, mode == ConduitMode.Input);
            ui.Select(output, mode == ConduitMode.Output);
            description.text = (mode == ConduitMode.Input ? "Machine \u2192 leyline" : "Leyline \u2192 machine")
                + " \u00b7 " + ConduitCosts.TransferMana + " Mana/item";
            description.color = mode == ConduitMode.Input ? ConduitUi.Green : ConduitUi.Blue;
            message.text = mode != conduit.Mode && conduit.Port.ConnectedPipe != null
                ? "Disconnect the pipe before changing mode."
                : "";
        }

        internal void Open()
        {
            if (IsOpen)
            {
                ClosePopup();
                return;
            }
            SelectMode(conduit.Mode);
            ChannelField.SetTextWithoutNotify(conduit.Channel);
            message.text = "";
            selectedIcon = conduit.SelectedIconId;
            RefreshIconPreview();
            HideIconPicker();
            conduit.Editing = true;
            Show(true);
            transform.SetAsLastSibling();
            openedFrame = Time.frameCount;
            restoreInput = FoundationApi.UiAccess.IsGameInputEnabled;
            FoundationApi.UiAccess.SetGameInputEnabled(false);
        }

        internal void Apply()
        {
            if (!conduit.Configure(selected, ChannelField.text, selectedIcon, out var reason))
            {
                message.text = reason;
                return;
            }
            owner.Refresh();
            ClosePopup();
        }

        void Update()
        {
            if (!owner || !owner.isActiveAndEnabled || !FoundationApi.UiAccess.IsWorkspaceVisible)
            {
                ClosePopup();
                return;
            }
            if (Time.frameCount != openedFrame && ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)))
            {
                if (PickerVisible)
                    HideIconPicker();
                else
                    ClosePopup();
                return;
            }
            var parent = transform.parent as RectTransform;
            if (parent)
                panel.localScale = Vector3.one * Mathf.Max(.1f, Mathf.Min(1, (parent.rect.width - 24) / Width, (parent.rect.height - 24) / Height));
        }

        public override void PopupClosed()
        {
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject
                && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
                EventSystem.current.SetSelectedGameObject(null);
            base.PopupClosed();
        }

        void OnDisable()
        {
            if (conduit != null)
                conduit.Editing = false;
            base.OnPointerExit(null);
            if (restoreInput && FoundationApi.UiAccess.Root && !FoundationApi.UiAccess.IsMenuVisible)
                FoundationApi.UiAccess.SetGameInputEnabled(true);
            restoreInput = false;
        }
    }
}

