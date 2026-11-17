using KingdomFoundation.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomLeylineConduits
{
    public sealed class ConduitWorld : MonoBehaviour
    {
        LeylineConduit conduit;
        ConduitUi ui;
        Image icon;
        ConduitRuneGraphic rune;
        FoundationTooltipSource tooltip;
        RectTransform handle;
        bool initialized, openWhenReady;
        float refresh;
        internal ConduitSettingsPopup Settings { get; private set; }
        internal LeylineConduit Element => conduit;

        void Start() => Initialize();

        bool Initialize()
        {
            if (initialized)
                return true;
            var world = FoundationApi.WorkspaceAccess.FindBuildingView(gameObject);
            var element = world ? FoundationApi.WorkspaceAccess.FromView(world)?.Behaviour as LeylineConduit : null;
            if (element == null)
                return false;
            var font = FoundationApi.WorkspaceVisuals.LabelText(world)?.font;
            if (!font)
                return false;
            conduit = element;
            ui = new ConduitUi(font);
            var body = ConduitUi.Rect(transform, "LeylineDisplay", 0, 0, LeylineConduit.Width, LeylineConduit.Height);

            // Use the existing background for hover info. A transparent overlay
            // above the whole tile would intercept Path Finder's pin clicks.
            var background = FoundationApi.WorkspaceVisuals.Background(world);
            if (background)
            {
                FoundationApi.UiAccess.RemoveNativeTooltips(background.gameObject);
                tooltip = background.GetComponent<FoundationTooltipSource>() ?? background.gameObject.AddComponent<FoundationTooltipSource>();
                tooltip.enabled = true;
            }

            // Reuse the native label container's WorldFlowLabel grip to preserve
            // pipes, grid snapping, collision rejection, cancellation and tab-swapping.
            var labelText = FoundationApi.WorkspaceVisuals.LabelText(world);
            if (labelText)
            {
                labelText.enabled = false;
                labelText.raycastTarget = false;
            }
            handle = FoundationApi.WorkspaceVisuals.BuildingLabel(world);
            if (handle)
            {
                handle.name = "LeylineMoveHandle";
                FoundationApi.UiAccess.RemoveNativeTooltips(handle.gameObject);
                ConfigureHandle();
                for (int row = 0; row < 2; row++)
                    for (int col = 0; col < 3; col++)
                        ui.Fill(handle, "GripRivet", 7 + col * 4, 4 + row * 4, 2, 2, ConduitUi.Gold);
                handle.gameObject.AddComponent<FoundationTooltipSource>().SetTooltip(
                    "Move conduit", "Drag this grip to move. Click to pick up; right-click to cancel.");
            }

            icon = ui.Icon(body, null, 16, 18, 32);
            icon.name = "LeylineSelectedIcon";
            rune = ConduitUi.Rect(body, "LeylineRune", 16, 18, 32, 32).gameObject.AddComponent<ConduitRuneGraphic>();
            rune.color = Color.white;
            rune.raycastTarget = false;

            var gear = ui.Button(body, "", 46, 0, 18, 18, OpenSettings);
            gear.name = "LeylineSettingsGear";
            var cog = ConduitUi.Rect(gear.transform, "Cog", 3, 3, 12, 12).gameObject.AddComponent<ConduitGearGraphic>();
            cog.color = ConduitUi.Paper;
            cog.raycastTarget = false;
            gear.gameObject.AddComponent<FoundationTooltipSource>().SetTooltip(
                "Leyline settings", "Choose Input or Output and name a leyline.");

            initialized = true;
            Refresh();
            return true;
        }

        void ConfigureHandle()
        {
            if (!handle)
                return;
            handle.anchorMin = handle.anchorMax = handle.pivot = new Vector2(0, 1);
            handle.anchoredPosition = new Vector2(20, -1);
            handle.sizeDelta = new Vector2(24, 14);
            var handleImage = handle.GetComponent<Image>();
            if (handleImage)
            {
                handleImage.sprite = ui.Sprite("Button03");
                handleImage.type = Image.Type.Sliced;
                handleImage.pixelsPerUnitMultiplier = 1.5f;
                handleImage.color = handleImage.sprite ? Color.white : new Color(.2f, .15f, .13f);
                handleImage.raycastTarget = true;
                handleImage.enabled = true;
            }
        }

        void Update()
        {
            if (!Initialize())
                return;
            if (openWhenReady)
                OpenSettings();
            if ((refresh -= Time.unscaledDeltaTime) <= 0)
            {
                refresh = .2f;
                Refresh();
            }
        }

        internal void Refresh()
        {
            if (!initialized)
                return;
            ConfigureHandle();
            icon.sprite = ConduitIcons.Find(conduit.SelectedIconId);
            icon.enabled = icon.sprite;
            rune.enabled = !icon.sprite;
            if (tooltip)
            {
                tooltip.SetTooltip("Leyline Conduit \u00b7 " + conduit.Mode,
                    (conduit.Channel.Length == 0 ? "Unbound" : conduit.Channel) + "\n" + conduit.Status + "\n"
                    + ConduitCosts.TransferMana + " Mana per item");
            }
        }

        public void OpenSettings()
        {
            if (!FoundationApi.UiAccess.Root || !Initialize())
            {
                openWhenReady = true;
                return;
            }
            openWhenReady = false;
            if (!Settings)
            {
                var toolbar = FoundationApi.UiAccess.EditToolbar;
                var canvas = toolbar ? toolbar.GetComponentInParent<Canvas>()?.rootCanvas : GetComponentInParent<Canvas>()?.rootCanvas;
                var root = new GameObject("LeylineSettings", typeof(RectTransform));
                root.SetActive(false);
                try
                {
                    Settings = root.AddComponent<ConduitSettingsPopup>();
                    Settings.Initialize(this, conduit, ui.Font, canvas ? canvas.transform : null);
                }
                catch
                {
                    Destroy(root);
                    Settings = null;
                    throw;
                }
            }
            Settings.Open();
        }

        void OnDisable()
        {
            if (Settings && Settings.IsOpen && FoundationApi.UiAccess.Root)
                Settings.ClosePopup();
        }

        void OnDestroy()
        {
            if (!Settings)
                return;
            if (Settings.IsOpen && FoundationApi.UiAccess.Root)
                Settings.ClosePopup();
            Destroy(Settings.gameObject);
        }
    }
}

