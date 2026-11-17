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
        const float Width = 368, Height = 446;
        const int IconsPerPage = 24;

        ConduitWorld owner;
        LeylineConduit conduit;
        ConduitUi ui;
        RectTransform panel;
        RectTransform settingsBody, pickerBody, iconGrid, costRow;
        Button input, output;
        Button previousPage, nextPage;
        TMP_Text directionText, costTailText, message, pageLabel;
        Image manaIcon, iconPreview;
        ConduitRuneGraphic runePreview;
        List<(string icon, string name)> iconCatalog;
        readonly List<string> presetOptions = new List<string>();
        string selectedIcon = "";
        int iconPage;
        ConduitMode selected;
        bool restoreInput;
        int openedFrame;

        internal TMP_Dropdown PresetDropdown { get; private set; }
        internal TMP_InputField PostfixField { get; private set; }
        internal TMP_InputField ChannelField { get; private set; }
        internal TMP_InputField IconSearch { get; private set; }
        internal bool PickerVisible => pickerBody && pickerBody.gameObject.activeSelf;
        internal int IconPage => iconPage;
        internal bool IsOpen => gameObject.activeSelf;

        internal void Initialize(ConduitWorld world, LeylineConduit element, TMP_FontAsset font, Transform parent)
        {
            owner = world;
            conduit = element;
            ui = new ConduitUi(font);
            transform.SetParent(parent, false);
            var root = (RectTransform)transform;
            ConduitUi.Stretch(root);

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
            gameObject.AddComponent<GraphicRaycaster>();

            var backdrop = ConduitUi.Rect(root, "DismissBackdrop", 0, 0, 0, 0);
            ConduitUi.Stretch(backdrop);
            backdrop.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .5f);
            backdrop.gameObject.AddComponent<Button>().onClick.AddListener(ClosePopup);

            panel = ui.Box(root, "LeylineSettingsPanel", 0, 0, Width, Height, "Tooltip").rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero;

            var header = ui.Box(panel, "LeylineHeaderBanner", 0, 0, 236, 38, "DecoTitleRed", 1.5f).rectTransform;
            header.anchorMin = header.anchorMax = new Vector2(.5f, 1f);
            header.pivot = new Vector2(.5f, .5f);
            header.anchoredPosition = new Vector2(0, -2);
            var title = ui.Text(header, "Leyline Conduit", 0, 0, 224, 34, 19);
            title.alignment = TextAlignmentOptions.Center;
            ConduitUi.Stretch(title.rectTransform, 6f);

            var closeButton = ui.Button(panel, "X", Width - 42, 12, 30, 30, ClosePopup, "Button03", 15f);
            closeButton.name = "CloseLeylineSettings";
            ((Image)closeButton.targetGraphic).pixelsPerUnitMultiplier = 2f;

            settingsBody = ConduitUi.Rect(panel, "LeylineSettingsBody", 0, 0, Width, Height);
            BuildSettings(settingsBody);
            BuildPicker();
        }

        void BuildSettings(RectTransform target)
        {
            ui.Text(target, "Mode", 26, 36, 316, 22, 15);
            input = ui.Button(target, "Input", 26, 64, 152, 36, () => SelectMode(ConduitMode.Input), "DecoTitleGreen", 16f);
            input.name = "SelectLeylineInput";
            output = ui.Button(target, "Output", 190, 64, 152, 36, () => SelectMode(ConduitMode.Output), "DecoTitleRed", 16f);
            output.name = "SelectLeylineOutput";

            BuildCostLine(target);

            PresetDropdown = ui.Dropdown(target, "LeylinePresetDropdown", 26, 140, 216, 34);
            PresetDropdown.onValueChanged.AddListener(OnPresetSelected);

            PostfixField = ui.InputField(target, "LeylinePostfixInput", 250, 140, 92, 34, "postfix", 16, 14f);
            PostfixField.onValueChanged.AddListener(OnPostfixChanged);

            ui.Text(target, "Name", 26, 188, 316, 22, 15);
            ChannelField = ui.InputField(target, "LeylineNameInput", 26, 216, 316, 36, "Enter a value...", LeylineConduit.ChannelLimit, 15f);
            ChannelField.onValueChanged.AddListener(OnChannelEdited);
            ui.Text(target, "Same name links across tabs.", 26, 256, 316, 18, 12).color = ConduitUi.Muted;

            ui.Text(target, "Icon", 26, 280, 316, 22, 15);
            iconPreview = ui.Icon(target, null, 34, 312, 32);
            iconPreview.name = "LeylineIconPreview";
            runePreview = ConduitUi.Rect(target, "RunePreview", 34, 312, 32, 32).gameObject.AddComponent<ConduitRuneGraphic>();
            runePreview.color = Color.white;
            runePreview.raycastTarget = false;
            ui.Button(target, "Set Icon", 80, 310, 176, 36, ShowIconPicker, "Button02", 16f).name = "SetLeylineIcon";
            ui.Button(target, "Rune", 264, 310, 78, 36, () => SelectIcon(""), "Button03", 15f).name = "ResetLeylineIcon";

            message = ui.Text(target, "", 26, 354, 316, 32, 12);
            message.color = ConduitUi.Red;
            message.textWrappingMode = TextWrappingModes.Normal;

            ui.Button(target, "Apply", 104, 392, 160, 38, Apply, "Button02", 17f).name = "ApplyLeylineSettings";
        }

        void BuildCostLine(RectTransform target)
        {
            costRow = ConduitUi.Rect(target, "LeylineCostRow", 26, 108, 316, 22);
            var layout = costRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            directionText = ui.Text(costRow, "", 0, 0, 140, 22, 13);
            directionText.overflowMode = TextOverflowModes.Overflow;

            var manaSprite = FoundationApi.UiAccess.CurrencyIcon(nameof(GameCurrency.Mana));
            manaIcon = ui.Icon(costRow, manaSprite, 0, 0, 16);
            manaIcon.name = "LeylineManaCostIcon";
            var iconLayout = manaIcon.gameObject.AddComponent<LayoutElement>();
            iconLayout.minWidth = 16;
            iconLayout.preferredWidth = 16;
            iconLayout.minHeight = 16;
            iconLayout.preferredHeight = 16;
            manaIcon.gameObject.SetActive(manaSprite);

            costTailText = ui.Text(costRow, "", 0, 0, 140, 22, 13);
            costTailText.overflowMode = TextOverflowModes.Overflow;
        }

        void BuildPicker()
        {
            pickerBody = ConduitUi.Rect(panel, "LeylineIconPicker", 0, 0, Width, Height);
            ui.Text(pickerBody, "Choose Icon", 26, 36, 316, 24, 17);
            IconSearch = ui.InputField(pickerBody, "SearchLeylineIcons", 26, 68, 316, 32, "Search game icons\u2026", 80, 15f);
            IconSearch.onValueChanged.AddListener(_ =>
            {
                iconPage = 0;
                RefreshIcons();
            });
            pageLabel = ui.Text(pickerBody, "", 26, 105, 316, 20, 12);
            pageLabel.color = ConduitUi.Muted;
            iconGrid = ConduitUi.Rect(pickerBody, "LeylineIconGrid", 26, 130, 318, 224);
            previousPage = ui.Button(pickerBody, "Prev", 26, 392, 84, 34, () =>
            {
                iconPage--;
                RefreshIcons();
            }, "Button03", 15f);
            previousPage.name = "PreviousLeylineIcons";
            ui.Button(pickerBody, "Back", 142, 392, 84, 34, HideIconPicker, "Button02", 15f).name = "BackFromLeylineIcons";
            nextPage = ui.Button(pickerBody, "Next", 258, 392, 84, 34, () =>
            {
                iconPage++;
                RefreshIcons();
            }, "Button03", 15f);
            nextPage.name = "NextLeylineIcons";
            pickerBody.gameObject.SetActive(false);
        }

        void PopulatePresets(bool allowAutoFill)
        {
            if (PresetDropdown.IsExpanded)
                PresetDropdown.Hide();

            var collected = ConduitChannelSuggestions.Collect(conduit, selected, out int pipeCount);
            presetOptions.Clear();
            presetOptions.Add("Custom");
            presetOptions.AddRange(collected);

            PresetDropdown.ClearOptions();
            PresetDropdown.AddOptions(presetOptions);
            if (PresetDropdown.template)
                PresetDropdown.template.sizeDelta = new Vector2(PresetDropdown.template.sizeDelta.x, Mathf.Min(210f, Mathf.Max(80f, presetOptions.Count * 34f + 12f)));

            string current = (ChannelField.text ?? "").Trim();
            if (current.Length == 0)
            {
                PostfixField.SetTextWithoutNotify("");
                if (allowAutoFill && selected == ConduitMode.Input && pipeCount > 0)
                {
                    PresetDropdown.SetValueWithoutNotify(1);
                    ChannelField.SetTextWithoutNotify(presetOptions[1]);
                }
                else
                {
                    PresetDropdown.SetValueWithoutNotify(0);
                    ChannelField.SetTextWithoutNotify("");
                }
                PresetDropdown.RefreshShownValue();
                return;
            }

            MatchPresetToExistingChannel(current);
            PresetDropdown.RefreshShownValue();
        }

        void MatchPresetToExistingChannel(string current)
        {
            for (int i = 1; i < presetOptions.Count; i++)
            {
                if (string.Equals(current, presetOptions[i], StringComparison.OrdinalIgnoreCase))
                {
                    PresetDropdown.SetValueWithoutNotify(i);
                    PostfixField.SetTextWithoutNotify("");
                    return;
                }
            }

            if (selected == ConduitMode.Input)
            {
                int bestIndex = 0;
                int bestLength = 0;
                for (int i = 1; i < presetOptions.Count; i++)
                {
                    string opt = presetOptions[i];
                    if (opt.Length > bestLength
                        && current.StartsWith(opt + " ", StringComparison.OrdinalIgnoreCase))
                    {
                        bestIndex = i;
                        bestLength = opt.Length;
                    }
                }

                if (bestIndex > 0)
                {
                    PresetDropdown.SetValueWithoutNotify(bestIndex);
                    PostfixField.SetTextWithoutNotify(current.Substring(bestLength + 1).Trim());
                    return;
                }
            }

            PresetDropdown.SetValueWithoutNotify(0);
            PostfixField.SetTextWithoutNotify("");
        }

        void OnPresetSelected(int index)
        {
            if (index <= 0 || index >= presetOptions.Count)
                return;
            ApplyPresetToName(presetOptions[index]);
        }

        void OnPostfixChanged(string _)
        {
            if (selected != ConduitMode.Input)
                return;
            int index = PresetDropdown.value;
            if (index <= 0 || index >= presetOptions.Count)
                return;
            ApplyPresetToName(presetOptions[index]);
        }

        void OnChannelEdited(string value)
        {
            int index = PresetDropdown.value;
            if (index <= 0 || index >= presetOptions.Count)
                return;
            string expected = selected == ConduitMode.Input
                ? ConduitChannelSuggestions.Combine(presetOptions[index], PostfixField.text)
                : presetOptions[index];
            if (!string.Equals((value ?? "").Trim(), expected, StringComparison.OrdinalIgnoreCase))
            {
                PresetDropdown.SetValueWithoutNotify(0);
                PresetDropdown.RefreshShownValue();
            }
        }

        void ApplyPresetToName(string baseName)
        {
            string combined = selected == ConduitMode.Input
                ? ConduitChannelSuggestions.Combine(baseName, PostfixField.text)
                : baseName;
            ChannelField.SetTextWithoutNotify(combined);
        }

        internal void ShowIconPicker()
        {
            if (PresetDropdown && PresetDropdown.IsExpanded)
                PresetDropdown.Hide();
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

        void SelectMode(ConduitMode mode, bool allowAutoFill = false)
        {
            selected = mode;
            ui.StyleModeButton(input, true, mode == ConduitMode.Input);
            ui.StyleModeButton(output, false, mode == ConduitMode.Output);

            PostfixField.interactable = mode == ConduitMode.Input;
            if (mode == ConduitMode.Output)
                PostfixField.SetTextWithoutNotify("");

            Color lineColor = mode == ConduitMode.Input ? ConduitUi.Green : ConduitUi.Blue;
            int manaCost = ConduitCosts.TransferMana;
            directionText.text = (mode == ConduitMode.Input ? "Machine \u2192 " : "Leyline \u2192 Machine \u00b7 ") + manaCost;
            directionText.color = lineColor;
            costTailText.text = "Mana per item";
            costTailText.color = lineColor;
            if (costRow)
                LayoutRebuilder.ForceRebuildLayoutImmediate(costRow);

            PopulatePresets(allowAutoFill);
            message.text = "";
        }

        internal void Open()
        {
            if (IsOpen)
            {
                ClosePopup();
                return;
            }
            ChannelField.SetTextWithoutNotify(conduit.Channel ?? "");
            PostfixField.SetTextWithoutNotify("");
            SelectMode(conduit.Mode, allowAutoFill: true);
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
                if (PresetDropdown && PresetDropdown.IsExpanded)
                    PresetDropdown.Hide();
                else if (PickerVisible)
                    HideIconPicker();
                else
                    ClosePopup();
                return;
            }
            var parent = transform.parent as RectTransform;
            if (parent)
                panel.localScale = Vector3.one * Mathf.Max(.1f, Mathf.Min(1, (parent.rect.width - 24) / Width, (parent.rect.height - 24) / (Height + 24)));
        }

        public override void PopupClosed()
        {
            if (PresetDropdown && PresetDropdown.IsExpanded)
                PresetDropdown.Hide();
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject
                && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
                EventSystem.current.SetSelectedGameObject(null);
            base.PopupClosed();
        }

        void OnDisable()
        {
            if (PresetDropdown && PresetDropdown.IsExpanded)
                PresetDropdown.Hide();
            if (conduit != null)
                conduit.Editing = false;
            base.OnPointerExit(null);
            if (restoreInput && FoundationApi.UiAccess.Root && !FoundationApi.UiAccess.IsMenuVisible)
                FoundationApi.UiAccess.SetGameInputEnabled(true);
            restoreInput = false;
        }
    }
}
