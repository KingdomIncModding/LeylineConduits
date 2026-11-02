using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Behaviour.Flow;
using Assets.Behaviour.UI;
using Assets.Source.Flow;
using Assets.Source.Flow.Elements;
using Assets.Source.Flow.Stickers;
using Assets.Source.Items;
using Assets.Source.Player;
using Assets.Source.Util;
using BepInEx;
using HarmonyLib;
using LightJson;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace KingdomLeylineConduits
{
    public sealed partial class LeylineConduitsPlugin
    {
        readonly List<string> checks = new List<string>();
        void Check(bool passed, string name)
        { if (!passed) throw new InvalidOperationException(name); checks.Add(name); }
        static InventoryItem Item(string id = "iron_ingot") => new InventoryItem(id);
        static bool ReceivesPointer(Button button)
        {
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            var screen = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                rect.TransformPoint(rect.rect.center));
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
            return hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform));
        }
        static FlowPipe Connect(FlowOutput output, FlowInput input, FlowDiagram flow = null)
        { var pipe = new FlowPipe(); pipe.Connect(output, input); flow?.Pipes.Add(pipe); return pipe; }
        LeylineConduit Place(FlowDiagram flow, ConduitMode mode, string name = "iron")
        {
            var conduit = new LeylineConduit();
            Check(conduit.Configure(mode, name, out _), "Configure " + mode + " / " + name);
            flow.Elements.Add(conduit); return conduit;
        }
        void ModelChecks()
        {
            var player = new GamePlayer(); GamePlayer.Current = player;
            var a = new FlowDiagram { Label = "Source workshop" }; var b = new FlowDiagram { Label = "Remote workshop" };
            player.Flows.Add(a); player.Flows.Add(b);
            Check(TechNode.Get(LeylineConduit.TechId)?.Previous.Identifier == "enchanter", "Research follows Enchanter");
            Check(!new LeylineConduit().IsPurchaseable(player), "Conduit locked before research");
            player.AddTech("enchanter"); player.AddTech(LeylineConduit.TechId);
            Check(new LeylineConduit().IsPurchaseable(player), "Research unlocks construction");
            Check(FlowElement.Create(LeylineConduit.TypeId) is LeylineConduit && FlowElement.Create("Forge") is Forge, "Native and custom factories resolve");
            var blank = new LeylineConduit();
            Check(blank.Inputs.Count == 1 && blank.Outputs.Count == 0 && !blank.Inputs[0].CanAcceptItem(Item()), "Unbound conduit has one input and refuses arrivals");
            Check(!blank.Configure(ConduitMode.Input, " ", out _) && !blank.Configure(ConduitMode.Input, new string('a', 33), out _)
                && !blank.Configure(ConduitMode.Input, "iron\nore", out _), "Blank, oversized and multiline channel names rejected");
            var inlet = Place(a, ConduitMode.Input, "  Iron  "); var outlet = Place(b, ConduitMode.Output, "iron");
            var wrong = Place(b, ConduitMode.Output, "copper"); var source = new Collector(); var sink = new Stockpile();
            a.Elements.Add(source); b.Elements.Add(sink);
            var incoming = Connect(source.Outputs[0], inlet.Inputs[0], a);
            var outgoing = Connect(outlet.Outputs[0], sink.Inputs[0], b);
            Check(inlet.Channel == "Iron" && inlet.OutputPeers().SequenceEqual(new[] { outlet }), "Names trim whitespace and match case insensitively across tabs");
            Check(inlet.IO.Count() == 1 && outlet.IO.Count() == 1 && inlet.Port.BufferSize == 1 && outlet.Port.BufferSize == 1, "Exactly one native connection and one-item holding slot per conduit");
            var original = Item(); source.Outputs[0].BufferedItems.Add(original); incoming.Update(0);
            Check(inlet.Port.NextItem == original && !source.Outputs[0].HasBuffer, "Native pipe accepts original item into Input conduit");
            inlet.Update(0);
            Check(outlet.Port.NextItem == original && !inlet.Port.HasBuffer && !wrong.Port.HasBuffer, "Leyline moves original object only to a matching Output");
            outgoing.Update(0);
            Check(sink.Inputs[0].NextItem == original && !outlet.Port.HasBuffer, "Native output pipe delivers original item to destination");
            sink.Inputs[0].BufferedItems.Clear();
            Check(!inlet.Configure(ConduitMode.Output, "iron", out _) && inlet.Mode == ConduitMode.Input && incoming.ConnectedInput == inlet.Port,
                "Mode switch refuses connected pipe without corrupting connection");
            var held = Item("iron_sword"); inlet.Port.BufferedItems.Add(held);
            Check(!inlet.Configure(ConduitMode.Input, "other", out _) && inlet.Channel == "Iron", "Buffered items cannot be retuned into another leyline");
            b.DisconnectPipe(outgoing); inlet.Update(0);
            Check(inlet.Port.NextItem == held && !outlet.Port.HasBuffer, "Disconnected output keeps item at Input");
            outgoing = Connect(outlet.Outputs[0], sink.Inputs[0], b);
            var waiting = Item(); outlet.Port.BufferedItems.Add(waiting); inlet.Update(0);
            Check(inlet.Port.NextItem == held && outlet.Port.NextItem == waiting, "Full output applies backpressure without overwrite");
            Check(!inlet.Inputs[0].CanAcceptItem(Item()), "Occupied input blocks additional arrivals");
            sink.Inputs[0].Filter = _ => false; outgoing.Update(0);
            Check(outlet.Port.NextItem == waiting && !sink.Inputs[0].HasBuffer, "Native destination item filter preserves waiting item");
            sink.Inputs[0].Filter = _ => true; sink.Inputs[0].BufferedItems.Add(Item()); outgoing.Update(0);
            Check(outlet.Port.NextItem == waiting, "Full destination queue retains item at output");
            sink.Inputs[0].BufferedItems.Clear(); outgoing.Update(0); sink.Inputs[0].BufferedItems.Clear();
            var second = Place(b, ConduitMode.Output, "IRON"); var sink2 = new Stockpile(); b.Elements.Add(sink2);
            Connect(second.Outputs[0], sink2.Inputs[0], b);
            inlet.Update(0);
            Check(outlet.Port.NextItem == held, "New output joins without disturbing current round robin position");
            outgoing.Update(0); sink.Inputs[0].BufferedItems.Clear();
            var alternate = Item("iron_sword"); inlet.Port.BufferedItems.Add(alternate); inlet.Update(0);
            Check(second.Port.NextItem == alternate && !outlet.Port.HasBuffer, "Round robin advances to next available output");
            var another = Item("iron_helm"); inlet.Port.BufferedItems.Add(another); outlet.Editing = true; inlet.Update(0);
            Check(inlet.Port.NextItem == another, "Editing and full outputs are skipped safely");
            outlet.Editing = false; inlet.Update(0);
            Check(outlet.Port.NextItem == another, "Available output receives next item after blocked peer");
            var sourceCopy = (LeylineConduit)inlet.CreateCopy(); var outputCopy = (LeylineConduit)outlet.CreateCopy();
            Check(sourceCopy.Channel == inlet.Channel && outputCopy.Mode == ConduitMode.Output && outputCopy.IO.Count() == 1
                && !outputCopy.Port.HasBuffer && outputCopy.Port.ConnectedPipe == null, "Copies preserve settings without duplicating buffered inventory or pipes");
            var loaded = FlowDiagram.FromJson(b.ToJson());
            var loadedOut = loaded.Elements.OfType<LeylineConduit>().First(p => p.Channel == "iron");
            Check(loadedOut.Mode == ConduitMode.Output && loadedOut.Outputs.Count == 1 && loadedOut.Inputs.Count == 0
                && loadedOut.Port.NextItem.Type.Identifier == "iron_helm" && loadedOut.Port.ConnectedPipe != null,
                "Native flow save roundtrip restores output role, channel, buffered item and pipe");
            var savedInput = (LeylineConduit)FlowElement.FromJson(inlet.ToJson(), a);
            Check(savedInput.Channel == "Iron" && savedInput.Mode == ConduitMode.Input, "Input channel and mode survive native serialization");
            a.DisconnectPipe(incoming);
            Check(inlet.Configure(ConduitMode.Output, "new rune", out _) && inlet.Inputs.Count == 0 && inlet.Outputs.Count == 1,
                "Disconnected empty conduit switches to exactly one output");
            Check(inlet.Configure(ConduitMode.Input, "new rune", out _) && inlet.Inputs.Count == 1 && inlet.Outputs.Count == 0,
                "Disconnected empty conduit switches back to exactly one input");
            var lonely = Place(a, ConduitMode.Input, "lonely"); var stranded = Item(); lonely.Port.BufferedItems.Add(stranded); lonely.Update(0);
            Check(lonely.Port.NextItem == stranded, "No linked output retains item for future connection");
            var filteredInput = Place(a, ConduitMode.Input, "filtered");
            var rejectedOutput = Place(b, ConduitMode.Output, "filtered"); var acceptedOutput = Place(b, ConduitMode.Output, "filtered");
            var rejectedSink = new Stockpile(); var acceptedSink = new Stockpile();
            rejectedSink.Inputs[0].Filter = _ => false;
            Connect(rejectedOutput.Outputs[0], rejectedSink.Inputs[0]); Connect(acceptedOutput.Outputs[0], acceptedSink.Inputs[0]);
            var filteredItem = Item(); filteredInput.Port.BufferedItems.Add(filteredItem); filteredInput.Update(0);
            Check(!rejectedOutput.Port.HasBuffer && acceptedOutput.Port.NextItem == filteredItem, "Routing skips incompatible destination before committing the item to an Output");
            acceptedOutput.Port.BufferedItems.Clear(); acceptedSink.Inputs[0].BufferedItems.Add(Item());
            filteredInput.Port.BufferedItems.Add(filteredItem); filteredInput.Update(0);
            Check(filteredInput.Port.NextItem == filteredItem && !acceptedOutput.Port.HasBuffer, "Full destination queues prevent remote output admission");
            var iconChoice = ConduitIcons.Catalog().First(option => option.icon == ItemType.Get("iron_ingot").IconName);
            Check(lonely.Configure(ConduitMode.Input, "lonely", iconChoice.icon, out _) && lonely.SelectedIconId == iconChoice.icon
                && ReferenceEquals(lonely.Port.NextItem, stranded), "Icon can change without draining or changing the waiting item");
            var copiedIcon = (LeylineConduit)lonely.CreateCopy();
            var savedIcon = (LeylineConduit)FlowElement.FromJson(lonely.ToJson(), a);
            Check(copiedIcon.SelectedIconId == iconChoice.icon && !copiedIcon.Port.HasBuffer && savedIcon.SelectedIconId == iconChoice.icon
                && savedIcon.Port.NextItem.Type.Identifier == stranded.Type.Identifier, "Native copy and save preserve selected icon without copying inventory");
            Check(!lonely.Configure(ConduitMode.Input, "lonely", "missing_icon", out _) && lonely.SelectedIconId == iconChoice.icon,
                "Unknown icon selection is rejected without changing existing settings");
            Check(lonely.Configure(ConduitMode.Input, "lonely", "", out _) && lonely.SelectedIconId == "", "Rune default can be restored with a waiting item");
            var legacy = lonely.ToJson().AsJsonObject; legacy.Remove("LeylineIcon"); legacy["Inputs"][0].AsJsonObject["Position"] = -48;
            var upgraded = (LeylineConduit)FlowElement.FromJson(legacy, a);
            Check(upgraded.Size == new Vector2Int(64, 64) && upgraded.Port.Position == -32 && upgraded.Channel == "lonely"
                && upgraded.SelectedIconId == "" && upgraded.Port.NextItem.Type.Identifier == stranded.Type.Identifier,
                "Version 0.1 saves shrink safely and retain names and buffered inventory with the default rune");
            legacy["LeylineIcon"] = "missing_icon";
            Check(((LeylineConduit)FlowElement.FromJson(legacy, a)).SelectedIconId == "", "Unavailable saved icon falls back to rune");
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("urakka.kingdominc.ledgermod"))
            {
                var ledger = FlowElement.Create("Ledger"); var ledgerSink = new Stockpile(); b.Elements.Add(ledger); b.Elements.Add(ledgerSink);
                var ledgerInput = Place(a, ConduitMode.Input, "ledger"); var ledgerOutput = Place(b, ConduitMode.Output, "ledger");
                var toLedger = Connect(ledgerOutput.Outputs[0], ledger.Inputs[0]); Connect(ledger.Outputs[0], ledgerSink.Inputs[0]);
                var tracked = Item("iron_sword"); ledgerInput.Port.BufferedItems.Add(tracked); ledgerInput.Update(0); toLedger.Update(0);
                Check(ReferenceEquals(ledgerSink.Inputs[0].NextItem, tracked) && !ledgerOutput.Port.HasBuffer && ledger.IO.All(io => !io.HasBuffer),
                    "Leyline output forwards original item through installed zero-storage Ledger");
            }
        }

        IEnumerator RuntimeCheck()
        {
            yield return new WaitForSecondsRealtime(5);
            string directory = Path.Combine(Paths.GameRootPath, "_Mods", "LeylineConduits", "Verification");
            Directory.CreateDirectory(directory); string error = null;
            try
            {
                if (GameUI.Instance || GamePlayer.Current != null) throw new InvalidOperationException("Diagnostic requires main menu without a loaded kingdom.");
                ModelChecks();
                var player = GamePlayer.CreateNewPlayer(); var flow = player.Flows[0];
                flow.Elements.Clear(); flow.Pipes.Clear(); flow.Label = "Leyline Conduits verification";
                player.AddTech("enchanter"); player.AddTech(LeylineConduit.TechId);
                var inlet = Place(flow, ConduitMode.Input, "iron"); inlet.Position = new Vector2Int(-192, 64);
                var outlet = Place(flow, ConduitMode.Output, "iron"); outlet.Position = new Vector2Int(64, 64);
                flow.Decorations.Add(new FlowDeco { Position = new Vector2Int(-64, 64), Size = new Vector2Int(64, 64),
                    IsParent = false, Sticker = new IconSticker { IconName = ItemType.Get("iron_ingot").IconName } });
                SceneManager.LoadScene("Game");
            }
            catch (Exception ex) { error = ex.ToString(); }
            if (error == null)
            {
                yield return new WaitForSecondsRealtime(4);
                try
                {
                    GameUI.Instance.ToggleTechMenu(false); WorldFlowCamera.Instance.SetZoomLevel(3);
                    var conduits = GamePlayer.Current.Flows[0].Elements.OfType<LeylineConduit>().ToArray();
                    Check(conduits.All(c => c.World && c.World.GetComponent<ConduitWorld>()), "Both modes render in actual game scene");
                    Check(conduits.All(c => c.World.Size == new Vector2(64, 64)), "Conduits occupy one 64 by 64 sticker cell");
                    Check(conduits.All(c => c.World.GetComponentsInChildren<WorldFlowIO>().Length == 1), "Exactly one visible native port per node");
                    Canvas.ForceUpdateCanvases();
                    foreach (var conduit in conduits)
                    {
                        var nodeRect = (RectTransform)conduit.World.transform;
                        var port = (Image)AccessTools.Field(typeof(WorldFlowIO), "_sprite").GetValue(conduit.Port.World);
                        var nodeCenter = nodeRect.TransformPoint(nodeRect.rect.center);
                        var portCenter = port.rectTransform.TransformPoint(port.rectTransform.rect.center);
                        Check(Mathf.Abs(nodeCenter.y - portCenter.y) < .5f, "Native port centered on sticker tile: " + conduit.Mode + " (" + nodeCenter.y + ", " + portCenter.y + ")");
                        var gearButton = conduit.World.GetComponentsInChildren<Button>().Single(button => button.name == "LeylineSettingsGear");
                        Check(ReceivesPointer(gearButton), "Small gear receives pointer at its visual center: " + conduit.Mode);
                        var pin = conduit.World.GetComponentsInChildren<Button>().SingleOrDefault(button => button.name == "PathFinder.Pin");
                        if (pin) Check(ReceivesPointer(pin), "Hover tooltip leaves companion Path Finder pin clickable: " + conduit.Mode);
                    }
                    Check(conduits.All(c => c.World.GetComponentsInChildren<Button>().Count(b => b.enabled && b.name == "LeylineSettingsGear") == 1), "Each node has exactly one working Leyline gear button alongside companion controls");
                    Check(FlowElement.GetBuildableTypes().Count(e => e.ElementType == LeylineConduit.TypeId) == 1, "Build menu contains one unlocked Leyline Conduit");
                    Capture(Path.Combine(directory, "leyline-nodes.png"));
                    ExecuteEvents.Execute(conduits[0].World.GetComponentsInChildren<Button>().Single(b => b.name == "LeylineSettingsGear").gameObject,
                        new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                    var view = conduits[0].World.GetComponent<ConduitWorld>();
                    Check(view.Settings && view.Settings.IsOpen && conduits[0].Editing, "Gear opens settings and suspends network editing");
                    Check(view.Settings.GetComponentsInChildren<TMP_InputField>().Length == 1 && view.Settings.ChannelField.text == "iron", "Settings shows populated editable leyline text box");
                    var popup = view.Settings;
                    popup.GetComponentsInChildren<Button>().Single(button => button.name == "SetLeylineIcon").onClick.Invoke();
                    Check(popup.PickerVisible && popup.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("LeylineIconChoice_")) == 24,
                        "Set Icon opens a bounded grid of game sticker icons");
                    popup.GetComponentsInChildren<Button>().Single(button => button.name == "NextLeylineIcons").onClick.Invoke();
                    Check(popup.IconPage == 1, "Icon grid pages through the native icon catalog");
                    popup.IconSearch.text = "no_such_icon_ever_114514";
                    Check(!popup.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("LeylineIconChoice_")), "Icon search hides every stale choice on no matches");
                    popup.IconSearch.text = ItemType.Get("iron_ingot").IconName;
                    var choice = popup.GetComponentsInChildren<Button>().Single(button => button.name == "LeylineIconChoice_" + ItemType.Get("iron_ingot").IconName);
                    choice.onClick.Invoke();
                    Check(!popup.PickerVisible && conduits[0].SelectedIconId == "", "Selecting an icon returns to settings and waits for Apply");
                    view.Settings.ChannelField.text = "moonstone";
                    view.Settings.GetComponentsInChildren<Button>().Single(b => b.name == "SelectLeylineOutput").onClick.Invoke();
                    view.Settings.Apply();
                    Check(conduits[0].Mode == ConduitMode.Output && conduits[0].Channel == "moonstone" && !view.Settings.IsOpen && !conduits[0].Editing,
                        "Apply changes role and channel and resumes network updates");
                    Check(conduits[0].SelectedIconId == ItemType.Get("iron_ingot").IconName
                        && conduits[0].World.GetComponentsInChildren<Image>().Single(image => image.name == "LeylineSelectedIcon").sprite == ItemType.Get("iron_ingot").GetIcon(),
                        "Applied icon renders on the sticker sized node");
                    conduits[0].World.UpdateIO();
                }
                catch (Exception ex) { error = ex.ToString(); }
            }
            if (error == null)
            {
                yield return new WaitForSecondsRealtime(.4f);
                try
                {
                    var flow = GamePlayer.Current.Flows[0]; var inlet = flow.Elements.OfType<LeylineConduit>().First();
                    Check(inlet.World.GetComponentsInChildren<WorldFlowIO>().Length == 1 && inlet.Port is FlowOutput, "Mode change rebuilds a single visible output port");
                    inlet.Configure(ConduitMode.Input, "iron", out _); inlet.World.GetComponent<ConduitWorld>().OpenSettings();
                    Capture(Path.Combine(directory, "leyline-settings.png"));
                    var popup = inlet.World.GetComponent<ConduitWorld>().Settings;
                    popup.ShowIconPicker();
                    Capture(Path.Combine(directory, "leyline-icon-picker.png"));
                    popup.ClosePopup();
                    inlet.World.GetComponent<ConduitWorld>().Refresh();
                    Capture(Path.Combine(directory, "leyline-nodes.png"));
                    var placed = new LeylineConduit { Position = new Vector2Int(-64, -128) };
                    WorldFlowManager.Instance.CreateNewElement(placed); placed.HandleReopenBuyMenu();
                    Check(placed.World.GetComponent<ConduitWorld>().Settings.IsOpen, "Native placement opens settings immediately before Unity Start");
                    placed.World.GetComponent<ConduitWorld>().Settings.ClosePopup();
                }
                catch (Exception ex) { error = ex.ToString(); }
            }
            var result = new JsonObject { { "passed", error == null }, { "version", Version }, { "gameVersion", Application.version },
                { "unityVersion", Application.unityVersion }, { "savedKingdomLoaded", false }, { "saveWritesBlocked", true }, { "testedAtUtc", DateTime.UtcNow.ToString("O") } };
            var names = new JsonArray(); foreach (string check in checks) names.Add(check); result["checks"] = names;
            if (error != null) result["error"] = error;
            File.WriteAllText(Path.Combine(directory, "runtime-tests.json"), result.ToString(true));
            if (error == null) Logger.LogInfo("LEYLINE_RUNTIME_PASS: " + checks.Count + " checks"); else Logger.LogError("LEYLINE_RUNTIME_FAIL: " + error);
            yield return new WaitForSecondsRealtime(1); Application.Quit();
        }

        static void Capture(string path)
        {
            // Hidden Windows players can suspend presentation and never resume
            // WaitForEndOfFrame. Render cameras explicitly to an offscreen target.
            Canvas.ForceUpdateCanvases();
            var target = RenderTexture.GetTemporary(Screen.width, Screen.height, 24);
            var previous = RenderTexture.active;
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            // Render only this popup as a temporary overlay root. Moving all UI
            // roots would also move native camera/mask objects out of their layers.
            var popup = UnityEngine.Object.FindObjectsByType<ConduitSettingsPopup>(FindObjectsSortMode.None).FirstOrDefault(p => p.IsOpen);
            var popupRoot = popup ? (RectTransform)popup.transform : null;
            var parent = popupRoot ? popupRoot.parent : null;
            var rootCanvas = parent ? parent.GetComponentInParent<Canvas>().rootCanvas : null;
            var popupCanvas = popup ? popup.GetComponent<Canvas>() : null;
            var oldMode = popupCanvas ? popupCanvas.renderMode : RenderMode.ScreenSpaceOverlay;
            var oldCamera = popupCanvas ? popupCanvas.worldCamera : null;
            float oldDistance = popupCanvas ? popupCanvas.planeDistance : 0;
            float oldScaleFactor = popupCanvas ? popupCanvas.scaleFactor : 1;
            var oldAnchorMin = popupRoot ? popupRoot.anchorMin : Vector2.zero;
            var oldAnchorMax = popupRoot ? popupRoot.anchorMax : Vector2.one;
            var oldPivot = popupRoot ? popupRoot.pivot : Vector2.zero;
            var oldSize = popupRoot ? popupRoot.sizeDelta : Vector2.zero;
            var oldPosition = popupRoot ? popupRoot.anchoredPosition : Vector2.zero;
            var oldScale = popupRoot ? popupRoot.localScale : Vector3.one;
            var layers = popup ? popup.GetComponentsInChildren<Transform>(true)
                .Select(t => (transform: t, layer: t.gameObject.layer)).ToArray() : new (Transform transform, int layer)[0];
            var overlayCameraObject = new GameObject("LeylineDiagnosticOverlayCapture");
            var overlayCamera = overlayCameraObject.AddComponent<Camera>();
            overlayCamera.clearFlags = CameraClearFlags.Depth; overlayCamera.orthographic = true;
            overlayCamera.orthographicSize = Screen.height / 2f; overlayCamera.depth = 10000;
            overlayCamera.cullingMask = 1 << 31; overlayCamera.targetTexture = target;
            try
            {
                foreach (var camera in Camera.allCameras.Where(c => c != overlayCamera).OrderBy(c => c.depth))
                {
                    var oldTarget = camera.targetTexture;
                    try { camera.targetTexture = target; camera.Render(); }
                    finally { camera.targetTexture = oldTarget; }
                }
                if (popup)
                {
                    foreach (var entry in layers) entry.transform.gameObject.layer = 31;
                    float scale = rootCanvas.scaleFactor;
                    popupRoot.SetParent(null, false);
                    popupCanvas.renderMode = RenderMode.ScreenSpaceCamera; popupCanvas.worldCamera = overlayCamera;
                    popupCanvas.planeDistance = 10; popupCanvas.scaleFactor = scale;
                    Canvas.ForceUpdateCanvases(); overlayCamera.Render();
                }
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); texture.Apply();
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(texture));
            }
            finally
            {
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
                if (popup)
                {
                    popupCanvas.renderMode = oldMode; popupCanvas.worldCamera = oldCamera;
                    popupCanvas.planeDistance = oldDistance; popupCanvas.scaleFactor = oldScaleFactor;
                    popupRoot.SetParent(parent, false); popupRoot.anchorMin = oldAnchorMin; popupRoot.anchorMax = oldAnchorMax;
                    popupRoot.pivot = oldPivot; popupRoot.sizeDelta = oldSize; popupRoot.anchoredPosition = oldPosition; popupRoot.localScale = oldScale;
                }
                foreach (var entry in layers) if (entry.transform) entry.transform.gameObject.layer = entry.layer;
                UnityEngine.Object.DestroyImmediate(overlayCameraObject);
                UnityEngine.Object.Destroy(texture);
            }
        }

        [HarmonyPatch(typeof(SaveGame), nameof(SaveGame.Store))]
        static class NoSave { static bool Prefix() => !Diagnostic; }
        [HarmonyPatch(typeof(SaveGame), nameof(SaveGame.StoreAutosaveState))]
        static class NoAutosave { static bool Prefix() => !Diagnostic; }
        [HarmonyPatch(typeof(SaveGame), nameof(SaveGame.LoadLatestSave))]
        static class NoLoad { static bool Prefix(ref bool __result) { if (!Diagnostic) return true; __result = false; return false; } }
        [HarmonyPatch(typeof(SteamAchievement), nameof(SteamAchievement.Trigger))]
        static class NoAchievement { static bool Prefix() => !Diagnostic; }
        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.Initialized), MethodType.Getter)]
        static class Offline { static bool Prefix(ref bool __result) { if (!Diagnostic) return true; __result = false; return false; } }
        [HarmonyPatch(typeof(FlowElement), nameof(FlowElement.GetCraftEffectsVolume))]
        static class Silent { static bool Prefix(ref float __result) { if (!Diagnostic) return true; __result = 0; return false; } }
    }
}
