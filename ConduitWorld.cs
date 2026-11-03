using System.Linq;
using Assets.Behaviour.Flow;
using Assets.Behaviour.UI;
using Behaviour.UI.Tooltip;
using HarmonyLib;
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
        TooltipSource tooltip;
        bool initialized, openWhenReady;
        float refresh;
        internal ConduitSettingsPopup Settings { get; private set; }
        internal LeylineConduit Element => conduit;
        void Start() => Initialize();
        bool Initialize()
        {
            if (initialized) return true;
            var world = GetComponent<WorldFlowElement>();
            if (!(world?.Element is LeylineConduit element)) return false;
            var font = LeylineConduitsPlugin.Field<TMP_Text>(world, "_label")?.font;
            if (!font) return false;
            conduit = element; ui = new ConduitUi(font);
            var body = ConduitUi.Rect(transform, "LeylineDisplay", 0, 0, LeylineConduit.Width, LeylineConduit.Height);
            // Use the existing background for hover info. A transparent overlay
            // above the whole tile would intercept Path Finder's pin clicks.
            var background = LeylineConduitsPlugin.Field<Image>(world, "_background");
            tooltip = background.GetComponent<TooltipSource>() ?? background.gameObject.AddComponent<TooltipSource>();
            tooltip.enabled = true;
            // Native header drag behavior preserves pipes, grid snapping,
            // collision rejection, cancellation and workshop-tab movement.
            var handle = ui.Box(body, "LeylineMoveHandle", 20, 1, 24, 14, "Button03");
            handle.gameObject.AddComponent<WorldFlowLabel>();
            handle.gameObject.AddComponent<TooltipSource>().SetTooltip("Move conduit", "Drag this grip to move. Click to pick up; right-click to cancel.");
            for (int row = 0; row < 2; row++) for (int col = 0; col < 3; col++)
                ui.Fill(handle.transform, "GripRivet", 7 + col * 4, 4 + row * 4, 2, 2, ConduitUi.Gold);
            icon = ui.Icon(body, null, 16, 18, 32); icon.name = "LeylineSelectedIcon";
            rune = ConduitUi.Rect(body, "LeylineRune", 16, 18, 32, 32).gameObject.AddComponent<ConduitRuneGraphic>();
            rune.color = Color.white; rune.raycastTarget = false;
            var gear = ui.Button(body, "", 46, 0, 18, 18, OpenSettings); gear.name = "LeylineSettingsGear";
            var cog = ConduitUi.Rect(gear.transform, "Cog", 3, 3, 12, 12).gameObject.AddComponent<ConduitGearGraphic>();
            cog.color = ConduitUi.Paper; cog.raycastTarget = false;
            gear.gameObject.AddComponent<TooltipSource>().SetTooltip("Leyline settings", "Choose Input or Output and name a leyline.");
            initialized = true; Refresh(); return true;
        }
        void Update()
        {
            if (!Initialize()) return;
            if (openWhenReady) OpenSettings();
            if ((refresh -= Time.unscaledDeltaTime) <= 0) { refresh = .2f; Refresh(); }
        }
        internal void Refresh()
        {
            if (!initialized) return;
            icon.sprite = ConduitIcons.Find(conduit.SelectedIconId); icon.enabled = icon.sprite;
            rune.enabled = !icon.sprite;
            tooltip.SetTooltip("Leyline Conduit · " + conduit.Mode,
                (conduit.Channel.Length == 0 ? "Unbound" : conduit.Channel) + "\n" + conduit.Status + "\n"
                + ConduitCosts.TransferMana + " Mana per item\nTop grip: move. Gear: mode, leyline name and icon.");
        }
        public void OpenSettings()
        {
            if (!GameUI.Instance || !Initialize()) { openWhenReady = true; return; }
            openWhenReady = false;
            if (!Settings)
            {
                var toolbar = AccessTools.Field(typeof(GameUI), "_editHotbar").GetValue(GameUI.Instance) as RectTransform;
                var canvas = toolbar ? toolbar.GetComponentInParent<Canvas>()?.rootCanvas : GetComponentInParent<Canvas>()?.rootCanvas;
                var root = new GameObject("LeylineSettings", typeof(RectTransform)); root.SetActive(false);
                try { Settings = root.AddComponent<ConduitSettingsPopup>(); Settings.Initialize(this, conduit, ui.Font, canvas ? canvas.transform : null); }
                catch { Destroy(root); Settings = null; throw; }
            }
            Settings.Open();
        }
        void OnDisable() { if (Settings && Settings.IsOpen && GameUI.Instance) Settings.ClosePopup(); }
        void OnDestroy()
        {
            if (!Settings) return;
            if (Settings.IsOpen && GameUI.Instance) Settings.ClosePopup();
            Destroy(Settings.gameObject);
        }
    }
}
