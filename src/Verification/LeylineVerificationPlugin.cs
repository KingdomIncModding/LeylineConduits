using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Behaviour.Flow;
using Assets.Behaviour.UI;
using Assets.Behaviour.UI.TechTree;
using Assets.Source.Flow;
using Assets.Source.Flow.Elements;
using Assets.Source.Items;
using Assets.Source.Items.Type;
using Assets.Source.Player;
using Assets.Source.Util;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using KingdomFoundation.Api;
using LightJson;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KingdomLeylineConduits.Verification
{
    [BepInPlugin("urakka.kingdominc.leylineconduits.verification", "Leyline Conduits Verification", "0.4.2")]
    [BepInDependency(LeylineConduitsPlugin.Id, LeylineConduitsPlugin.Version)]
    public sealed class LeylineVerificationPlugin : BaseUnityPlugin
    {
        static bool Diagnostic => Environment.GetCommandLineArgs().Contains("-leyline-smoke");
        readonly List<string> checks = new List<string>();
        Harmony harmony;

        void Awake()
        {
            if (!Diagnostic)
                return;
            harmony = new Harmony("urakka.kingdominc.leylineconduits.verification");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Application.runInBackground = true;
            StartCoroutine(RunChecks());
        }

        void OnDestroy() => harmony?.UnpatchSelf();

        void Check(bool passed, string name)
        {
            if (!passed)
                throw new InvalidOperationException(name);
            checks.Add(name);
        }

        static InventoryItem Item(string id = "iron_ingot") => new InventoryItem(id);

        static FlowPipe Connect(FlowOutput output, FlowInput input, FlowDiagram flow = null)
        {
            var pipe = new FlowPipe();
            pipe.Connect(output, input);
            flow?.Pipes.Add(pipe);
            return pipe;
        }

        static FlowElement NativeOf(LeylineConduit conduit)
        {
            var prop = typeof(FrameworkWorkspaceNode).GetProperty("Native", BindingFlags.Instance | BindingFlags.NonPublic);
            return (FlowElement)prop.GetValue(conduit);
        }

        static T Field<T>(WorldFlowElement world, string name) where T : class
            => AccessTools.Field(typeof(WorldFlowElement), name).GetValue(world) as T;

        LeylineConduit Place(FlowDiagram flow, ConduitMode mode, string name = "iron")
        {
            var conduit = new LeylineConduit();
            Check(conduit.Configure(mode, name, out _), "Configure " + mode + " / " + name);
            flow.Elements.Add(NativeOf(conduit));
            return conduit;
        }

        static bool ReceivesPointer(Component target)
        {
            var rect = (RectTransform)target.transform;
            var canvas = target.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var screen = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
            return hits.Count > 0 && (hits[0].gameObject == target.gameObject || hits[0].gameObject.transform.IsChildOf(target.transform));
        }

        void TransferCostChecks()
        {
            foreach (int reduction in new[] { 0, 1, 2 })
            {
                var player = new GamePlayer();
                GamePlayer.Current = player;
                if (reduction > 0)
                    player.AddTech("portal_cost_reduce");
                if (reduction > 1)
                    player.AddTech("portal_cost_reduce_2");
                int cost = new Portal().InputCost;
                Check(ConduitCosts.TransferMana == cost && cost == (reduction == 0 ? 12 : reduction == 1 ? 6 : 3),
                    "Per-item Mana matches native Portal with reduction level " + reduction);
                var sourceFlow = new FlowDiagram();
                var targetFlow = new FlowDiagram();
                player.Flows.Add(sourceFlow);
                player.Flows.Add(targetFlow);
                var inlet = Place(sourceFlow, ConduitMode.Input, "paid");
                var outlet = Place(targetFlow, ConduitMode.Output, "paid");
                var nativeInlet = NativeOf(inlet);
                var nativeOutlet = NativeOf(outlet);
                var sink = new Stockpile();
                targetFlow.Elements.Add(sink);
                var pipe = Connect(nativeOutlet.Outputs[0], sink.Inputs[0], targetFlow);
                var original = Item();
                nativeInlet.Inputs[0].BufferedItems.Add(original);
                player.AddCurrency(Currency.Mana, cost - 1, false);
                inlet.Update(0);
                Check(ReferenceEquals(nativeInlet.Inputs[0].NextItem, original) && !nativeOutlet.Outputs[0].HasBuffer
                    && player.GetCurrency(Currency.Mana) == cost - 1 && inlet.Status == "Needs " + cost + " Mana",
                    "Insufficient Mana preserves item and balance at reduction level " + reduction);
                player.AddCurrency(Currency.Mana, 1, false);
                inlet.Update(0);
                Check(!nativeInlet.Inputs[0].HasBuffer && ReferenceEquals(nativeOutlet.Outputs[0].NextItem, original)
                    && player.GetCurrency(Currency.Mana) == 0,
                    "Successful transfer spends exactly one Portal charge at reduction level " + reduction);
                outlet.Update(0);
                pipe.Update(0);
                inlet.Update(0);
                Check(ReferenceEquals(sink.Inputs[0].NextItem, original) && player.GetCurrency(Currency.Mana) == 0,
                    "Output delivery and repeated updates never charge twice at reduction level " + reduction);
                player.AddCurrency(Currency.Mana, 100, false);
                nativeInlet.Inputs[0].BufferedItems.Add(Item());
                inlet.Update(0);
                Check(nativeInlet.Inputs[0].HasBuffer && player.GetCurrency(Currency.Mana) == 100,
                    "Blocked destination costs no Mana at reduction level " + reduction);
                sink.Inputs[0].BufferedItems.Clear();
                sink.Inputs[0].Filter = _ => false;
                inlet.Update(0);
                Check(nativeInlet.Inputs[0].HasBuffer && player.GetCurrency(Currency.Mana) == 100,
                    "Incompatible destination costs no Mana at reduction level " + reduction);
                sink.Inputs[0].Filter = _ => true;
                targetFlow.DisconnectPipe(pipe);
                inlet.Update(0);
                Check(nativeInlet.Inputs[0].HasBuffer && player.GetCurrency(Currency.Mana) == 100,
                    "Disconnected destination costs no Mana at reduction level " + reduction);
                pipe = Connect(nativeOutlet.Outputs[0], sink.Inputs[0], targetFlow);
                inlet.Editing = true;
                inlet.Update(0);
                Check(nativeInlet.Inputs[0].HasBuffer && player.GetCurrency(Currency.Mana) == 100,
                    "Settings pause prevents charging Mana at reduction level " + reduction);
                inlet.Editing = false;
                var loadedNative = FlowElement.FromJson(nativeInlet.ToJson(), sourceFlow);
                sourceFlow.Elements.Remove(nativeInlet);
                sourceFlow.Elements.Add(loadedNative);
                loadedNative.Update(0);
                Check(!loadedNative.Inputs[0].HasBuffer && nativeOutlet.Outputs[0].HasBuffer
                    && player.GetCurrency(Currency.Mana) == 100 - cost,
                    "Saved waiting item pays once when routing resumes at reduction level " + reduction);
            }
        }

        void ModelChecks()
        {
            TransferCostChecks();
            var player = new GamePlayer();
            GamePlayer.Current = player;
            player.AddCurrency(Currency.Mana, 10000, false);
            var a = new FlowDiagram { Label = "Source workshop" };
            var b = new FlowDiagram { Label = "Remote workshop" };
            player.Flows.Add(a);
            player.Flows.Add(b);
            var research = TechNode.Get(LeylineConduit.TechId);
            Check(research?.AllRequirements.Any(n => n.Identifier == "portal") == true,
                "Research retains the native Portal prerequisite on Mods");
            var nativeResearch = TechNode.Get("portal");
            Check(research.GetCost().GetCurrency().SequenceEqual((nativeResearch.GetCost() * 4).GetCurrency())
                && research.GetCost().GetCurrency().Select(c => c.c).SequenceEqual(new[] { Currency.Gold, Currency.Mana }),
                "Upgrade research costs exactly four native Portal research prices in Gold and Mana");
            Check(research.IsLocked && research.AllRequirements.Any(node => node.Identifier == "portal"),
                "Portal is required before upgrade research");
            player.AddTech("portal");
            Check(!research.IsLocked, "Owning Portal makes Leyline research available");
            var sample = FlowElement.Create(LeylineConduit.TypeId);
            Check(sample.Icon == research.Icon && sample.Size == new Vector2Int(64, 64),
                "Research and construction resolve the shared custom pixel seal");
            Check(!sample.IsPurchaseable(player), "Conduit locked before research");
            player.AddTech(LeylineConduit.TechId);
            Check(sample.IsPurchaseable(player), "Research unlocks construction");

            Check(Chainloader.PluginInfos.TryGetValue("urakka.kingdominc.foundation", out var plugin)
                && plugin.Metadata.Version >= new Version(0, 14, 5),
                "Foundation 0.14.5+ dependency loads before Leyline Conduits");
            Check(FoundationApi.TechNodes.TryGet(LeylineConduit.TechId, out var registered)
                && ReferenceEquals(registered, LeylineConduitsPlugin.ResearchRegistration),
                "Legacy research ID is registered through Foundation's public research API");
            Check(FoundationApi.WorkspaceNodes.TryGet(LeylineConduit.TypeId, out var workspace)
                && ReferenceEquals(workspace, LeylineConduitsPlugin.WorkspaceRegistration),
                "Foundation's workspace factory owns the legacy conduit type");

            var blank = new LeylineConduit();
            var nativeBlank = NativeOf(blank);
            Check(blank.Inputs.Count == 1 && blank.Outputs.Count == 0 && !nativeBlank.Inputs[0].CanAcceptItem(Item()),
                "Unbound conduit has one input and refuses arrivals");
            Check(!blank.Configure(ConduitMode.Input, " ", out _) && !blank.Configure(ConduitMode.Input, new string('a', 33), out _)
                && !blank.Configure(ConduitMode.Input, "iron\nore", out _),
                "Blank, oversized and multiline channel names rejected");

            var inlet = Place(a, ConduitMode.Input, "  Iron  ");
            var outlet = Place(b, ConduitMode.Output, "iron");
            var wrong = Place(b, ConduitMode.Output, "copper");
            var nInlet = NativeOf(inlet);
            var nOutlet = NativeOf(outlet);
            var nWrong = NativeOf(wrong);
            var source = new Collector();
            var sink = new Stockpile();
            a.Elements.Add(source);
            b.Elements.Add(sink);
            var incoming = Connect(source.Outputs[0], nInlet.Inputs[0], a);
            var outgoing = Connect(nOutlet.Outputs[0], sink.Inputs[0], b);
            Check(inlet.Channel == "Iron" && inlet.OutputPeers().SequenceEqual(new[] { outlet }),
                "Names trim whitespace and match case insensitively across tabs");
            var original = Item();
            source.Outputs[0].BufferedItems.Add(original);
            incoming.Update(0);
            Check(nInlet.Inputs[0].NextItem == original && !source.Outputs[0].HasBuffer,
                "Native pipe accepts original item into Input conduit");
            inlet.Update(0);
            Check(nOutlet.Outputs[0].NextItem == original && !nInlet.Inputs[0].HasBuffer && !nWrong.Outputs[0].HasBuffer,
                "Leyline moves original object only to a matching Output");
            outgoing.Update(0);
            Check(sink.Inputs[0].NextItem == original && !nOutlet.Outputs[0].HasBuffer,
                "Native output pipe delivers original item to destination");
            sink.Inputs[0].BufferedItems.Clear();

            var held = Item("iron_sword");
            nInlet.Inputs[0].BufferedItems.Add(held);
            Check(inlet.Configure(ConduitMode.Input, "Iron", ConduitIcons.Catalog().First().icon, out _)
                && nInlet.Inputs[0].HasBuffer && nInlet.Inputs[0].BufferedItems[0] == held,
                "Icon change preserves waiting item");
            Check(inlet.Configure(ConduitMode.Input, "other", out _) && inlet.Channel == "other"
                && !nInlet.Inputs[0].HasBuffer,
                "Changing channel destroys waiting item cleanly");
            Check(inlet.Configure(ConduitMode.Input, "Iron", out _) && inlet.Channel == "Iron",
                "Restore Input channel Iron");

            nInlet.Inputs[0].BufferedItems.Add(held);
            Check(inlet.Configure(ConduitMode.Output, "iron", out _) && inlet.Mode == ConduitMode.Output
                && !inlet.Port.HasBuffer && incoming.ConnectedInput.ConnectedPipe == null && source.Outputs[0].ConnectedPipe == null,
                "Mode switch destroys waiting item and disconnects existing pipe cleanly");
            Check(inlet.Configure(ConduitMode.Input, "Iron", out _) && inlet.Mode == ConduitMode.Input,
                "Disconnected conduit switches back to Input");
            incoming = Connect(source.Outputs[0], nInlet.Inputs[0], a);
            nInlet.Inputs[0].BufferedItems.Add(held);

            var second = Place(b, ConduitMode.Output, "IRON");
            var nSecond = NativeOf(second);
            var sink2 = new Stockpile();
            b.Elements.Add(sink2);
            Connect(nSecond.Outputs[0], sink2.Inputs[0], b);
            inlet.Update(0);
            Check(nOutlet.Outputs[0].NextItem == held, "First available output receives held item");
            outgoing.Update(0);
            sink.Inputs[0].BufferedItems.Clear();
            var alternate = Item("iron_sword");
            nInlet.Inputs[0].BufferedItems.Add(alternate);
            inlet.Update(0);
            Check(nSecond.Outputs[0].NextItem == alternate && !nOutlet.Outputs[0].HasBuffer,
                "Round robin advances to next available output");

            var iconChoice = ConduitIcons.Catalog().First(option => option.icon == ItemType.Get("iron_ingot").IconName);
            Check(inlet.Configure(ConduitMode.Input, "Iron", iconChoice.icon, out _) && inlet.SelectedIconId == iconChoice.icon,
                "Icon selection succeeds using Foundation catalog");
            var restored = GamePlayer.FromJson(player.ToJson());
            Check(restored.HasTech(LeylineConduit.TechId), "Full kingdom JSON retains Leyline Conduits research");
            var legacy = nInlet.ToJson().AsJsonObject;
            legacy.Remove("LeylineIcon");
            legacy["Inputs"][0].AsJsonObject["Position"] = -48;
            var upgraded = FlowElement.FromJson(legacy, a);
            Check(upgraded.Size == new Vector2Int(64, 64) && upgraded.Inputs[0].Position == -LeylineConduit.PortOffset,
                "Legacy saves adopt the aligned port layout");
        }

        IEnumerator RunChecks()
        {
            yield return new WaitForSecondsRealtime(4);
            string directory = Path.Combine(Paths.GameRootPath, "_Mods", "LeylineConduits", "_AI", "Scratch");
            Directory.CreateDirectory(directory);
            string error = null;
            try
            {
                if (GameUI.Instance || GamePlayer.Current != null)
                    throw new InvalidOperationException("Diagnostic requires main menu without a loaded kingdom.");
                ModelChecks();
                var player = GamePlayer.CreateNewPlayer();
                var flow = player.Flows[0];
                flow.Elements.Clear();
                flow.Pipes.Clear();
                flow.Label = "Leyline Conduits verification";
                player.AddTech("portal");
                player.AddTech(LeylineConduit.TechId);
                player.AddCurrency(Currency.Mana, 10000, false);
                var inlet = Place(flow, ConduitMode.Input, "iron");
                NativeOf(inlet).Position = new Vector2Int(-192, 64);
                var outlet = Place(flow, ConduitMode.Output, "iron");
                NativeOf(outlet).Position = new Vector2Int(64, 64);
                SceneManager.LoadScene("Game");
            }
            catch (Exception ex) { error = ex.ToString(); }

            if (error == null)
            {
                yield return new WaitForSecondsRealtime(4);
                try
                {
                    GameUI.Instance.ToggleTechMenu(false);
                    WorldFlowCamera.Instance.SetZoomLevel(3);
                    var conduits = FoundationApi.Kingdom.Buildings
                        .Select(b => b.Behaviour as LeylineConduit)
                        .Where(c => c != null)
                        .ToArray();
                    Check(conduits.Length == 2, "Both conduits present in live workspace");
                    Check(conduits.All(c => c.World && c.World.GetComponent<ConduitWorld>()),
                        "Both modes render in actual game scene");
                    Check(conduits.All(c => ((WorldFlowElement)c.World).Size == new Vector2(64, 64)),
                        "Conduits occupy one 64 by 64 sticker cell");
                    Check(conduits.All(c => c.World.GetComponentsInChildren<WorldFlowIO>().Length == 1),
                        "Exactly one visible native port per node");
                    Canvas.ForceUpdateCanvases();

                    foreach (var conduit in conduits)
                    {
                        var world = (WorldFlowElement)conduit.World;
                        var nodeRect = (RectTransform)world.transform;
                        var port = (Image)AccessTools.Field(typeof(WorldFlowIO), "_sprite").GetValue((WorldFlowIO)conduit.Port.World);
                        var nodeCenter = nodeRect.TransformPoint(nodeRect.rect.center);
                        var portCenter = port.rectTransform.TransformPoint(port.rectTransform.rect.center);
                        Check(Mathf.Abs(nodeCenter.y - portCenter.y - 8) < .5f,
                            "Native port lowered eight pixels on sticker tile: " + conduit.Mode);
                        var handle = world.GetComponentsInChildren<WorldFlowLabel>().Single(label => label.name == "LeylineMoveHandle");
                        Check(ReceivesPointer(handle), "Tiny move handle receives pointer: " + conduit.Mode);
                        var startPosition = conduit.Position;
                        handle.OnBeginDrag(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
                        Check(ReferenceEquals(DragDropManager.Instance.Target, handle), "Handle begins native node drag: " + conduit.Mode);
                        var movedPosition = startPosition + new Vector2Int(0, 128);
                        handle.OnDragDropMove(movedPosition, Vector2Int.zero);
                        Check(conduit.Position == movedPosition, "Dragging moves conduit: " + conduit.Mode);
                        DragDropManager.Instance.CancelDragDrop();
                        Check(conduit.Position == startPosition && !DragDropManager.Instance.Busy,
                            "Cancelling handle drag restores original location: " + conduit.Mode);
                        var gearButton = world.GetComponentsInChildren<Button>().Single(button => button.name == "LeylineSettingsGear");
                        Check(ReceivesPointer(gearButton), "Small gear receives pointer: " + conduit.Mode);
                    }

                    var alignmentMachine = new Collector { Position = new Vector2Int(320, conduits[0].Position.y) };
                    WorldFlowManager.Instance.CreateNewElement(alignmentMachine);
                    Canvas.ForceUpdateCanvases();
                    var nativePort = alignmentMachine.Inputs[0].World.transform.position;
                    Check(conduits.All(c => Mathf.Abs(c.Port.World.transform.position.y - nativePort.y) < .5f),
                        "Input and Output conduit ports line up with a native machine on the same grid row");
                    WorldFlowManager.Instance.RemoveElement(alignmentMachine);

                    // Pipe-connected dropdown auto-population + postfix + editable Name field checks.
                    var flow = GamePlayer.Current.Flows[0];
                    var mine = new Mine { Position = new Vector2Int(-384, -64) };
                    mine.SetupResource((ResourceItemType)ItemType.Get("iron_ore"));
                    WorldFlowManager.Instance.CreateNewElement(mine);
                    var pipedConduit = new LeylineConduit();
                    var nativePiped = NativeOf(pipedConduit);
                    nativePiped.Position = new Vector2Int(-192, -64);
                    WorldFlowManager.Instance.CreateNewElement(nativePiped);
                    var minePipe = new FlowPipe();
                    minePipe.Connect(mine.Outputs[0], nativePiped.Inputs[0]);
                    WorldFlowManager.Instance.CreateNewPipe(minePipe);
                    Check(ConduitChannelSuggestions.FormatDisplayName("@iron_mine") == "Iron Mine",
                        "@iron_mine token translates/formats to Iron Mine");
                    var pipedView = nativePiped.World.GetComponent<ConduitWorld>();
                    pipedView.OpenSettings();
                    var pipedPopup = pipedView.Settings;
                    Check(pipedPopup && pipedPopup.IsOpen, "Piped conduit settings popup opens");
                    Check(pipedPopup.PresetDropdown.options.Any(o => o.text == "Iron Mine"),
                        "Dropdown auto-populates Iron Mine from piped @iron_mine");
                    Check(pipedPopup.ChannelField.text == "Iron Mine",
                        "Opening unnamed piped conduit auto-fills Name with Iron Mine");
                    pipedPopup.PostfixField.text = "_1";
                    Check(pipedPopup.ChannelField.text == "Iron Mine _1",
                        "Postfix _1 updates Name to Iron Mine _1");
                    Canvas.ForceUpdateCanvases();
                    Capture(Path.Combine(directory, "leyline-settings-v2.png"), pipedPopup);
                    pipedPopup.ChannelField.text = "Iron Mine _1 Custom";
                    Check(pipedPopup.PresetDropdown.value == 0,
                        "Manual edit of Name switches dropdown back to Custom while keeping edited text");
                    pipedPopup.Apply();
                    Check(pipedConduit.Channel == "Iron Mine _1 Custom",
                        "Apply saves edited channel name");
                    pipedView.OpenSettings();
                    pipedPopup.GetComponentsInChildren<Button>().Single(b => b.name == "SelectLeylineOutput").onClick.Invoke();
                    pipedPopup.Apply();
                    Check(pipedConduit.Mode == ConduitMode.Output && mine.Outputs[0].ConnectedPipe == null
                        && !flow.Pipes.Contains(minePipe),
                        "Switching mode in live workspace disconnects and removes existing pipe");
                    WorldFlowManager.Instance.RemoveElement(nativePiped);
                    WorldFlowManager.Instance.RemoveElement(mine);

                    // Output mode dropdown: only alphabetical active Input channels (no piped buildings or Output-only channels).
                    var extraInputZ = new LeylineConduit();
                    Check(extraInputZ.Configure(ConduitMode.Input, "zeta", out _), "Configure extra Input zeta");
                    var extraInputA = new LeylineConduit();
                    Check(extraInputA.Configure(ConduitMode.Input, "Alpha", out _), "Configure extra Input Alpha");
                    var extraOutputOnly = new LeylineConduit();
                    Check(extraOutputOnly.Configure(ConduitMode.Output, "outputOnlyChannel", out _), "Configure extra Output outputOnlyChannel");
                    var collector = new Collector { Position = new Vector2Int(-192, -192) };
                    var pipedOutput = new LeylineConduit();
                    Check(pipedOutput.Configure(ConduitMode.Output, "alpha", out _), "Configure pipedOutput initial");
                    var nativeExtraZ = NativeOf(extraInputZ); nativeExtraZ.Position = new Vector2Int(-512, -192);
                    var nativeExtraA = NativeOf(extraInputA); nativeExtraA.Position = new Vector2Int(-448, -192);
                    var nativeExtraOut = NativeOf(extraOutputOnly); nativeExtraOut.Position = new Vector2Int(-384, -192);
                    var nativeOut = NativeOf(pipedOutput); nativeOut.Position = new Vector2Int(-320, -192);
                    WorldFlowManager.Instance.CreateNewElement(nativeExtraZ);
                    WorldFlowManager.Instance.CreateNewElement(nativeExtraA);
                    WorldFlowManager.Instance.CreateNewElement(nativeExtraOut);
                    WorldFlowManager.Instance.CreateNewElement(collector);
                    WorldFlowManager.Instance.CreateNewElement(nativeOut);
                    var outPipe = new FlowPipe();
                    outPipe.Connect(nativeOut.Outputs[0], collector.Inputs[0]);
                    WorldFlowManager.Instance.CreateNewPipe(outPipe);
                    var outView = nativeOut.World.GetComponent<ConduitWorld>();
                    outView.OpenSettings();
                    var outPopup = outView.Settings;
                    var outPresetTexts = outPopup.PresetDropdown.options.Select(o => o.text).ToList();
                    Check(outPresetTexts.SequenceEqual(new[] { "Custom", "Alpha", "iron", "zeta" }),
                        "Output dropdown shows only alphabetical active Input channels: " + string.Join(", ", outPresetTexts));
                    Check(!outPopup.PostfixField.interactable,
                        "Postfix field is disabled in Output mode");
                    outView.OpenSettings();
                    WorldFlowManager.Instance.RemoveElement(nativeOut);
                    WorldFlowManager.Instance.RemoveElement(collector);
                    WorldFlowManager.Instance.RemoveElement(nativeExtraOut);
                    WorldFlowManager.Instance.RemoveElement(nativeExtraA);
                    WorldFlowManager.Instance.RemoveElement(nativeExtraZ);

                    ExecuteEvents.Execute(conduits[0].World.GetComponentsInChildren<Button>().Single(b => b.name == "LeylineSettingsGear").gameObject,
                        new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                    var view = conduits[0].World.GetComponent<ConduitWorld>();
                    Check(view.Settings && view.Settings.IsOpen && conduits[0].Editing,
                        "Gear opens settings and suspends network editing");
                    var popup = view.Settings;
                    popup.GetComponentsInChildren<Button>().Single(b => b.name == "SelectLeylineOutput").onClick.Invoke();
                    Canvas.ForceUpdateCanvases();
                    var costRow = popup.transform.Find("LeylineSettingsPanel/LeylineSettingsBody/LeylineCostRow");
                    var dirRect = (RectTransform)costRow.GetChild(0);
                    var iconRect = (RectTransform)costRow.GetChild(1);
                    var tailRect = (RectTransform)costRow.GetChild(2);
                    Check(dirRect.rect.width > 120f && iconRect.anchoredPosition.x > dirRect.anchoredPosition.x + dirRect.rect.width / 2f
                        && tailRect.anchoredPosition.x > iconRect.anchoredPosition.x,
                        "Output cost row lays out direction, Mana icon, and tail without overlap");
                    Capture(Path.Combine(directory, "leyline-settings-output.png"), popup);
                    popup.GetComponentsInChildren<Button>().Single(button => button.name == "SetLeylineIcon").onClick.Invoke();
                    Check(popup.PickerVisible && popup.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("LeylineIconChoice_")),
                        "Set Icon opens searchable icon grid");
                    popup.IconSearch.text = ItemType.Get("iron_ingot").IconName;
                    var choice = popup.GetComponentsInChildren<Button>().Single(button => button.name == "LeylineIconChoice_" + ItemType.Get("iron_ingot").IconName);
                    choice.onClick.Invoke();
                    popup.ChannelField.text = "moonstone";
                    popup.GetComponentsInChildren<Button>().Single(b => b.name == "SelectLeylineOutput").onClick.Invoke();
                    popup.Apply();
                    Check(conduits[0].Mode == ConduitMode.Output && conduits[0].Channel == "moonstone" && !view.Settings.IsOpen && !conduits[0].Editing,
                        "Apply changes role, channel and icon and resumes network updates");
                    Check(conduits[0].World.GetComponentsInChildren<WorldFlowIO>().Length == 1 && conduits[0].Port.IsOutput,
                        "Mode change rebuilds a single visible output port");

                    GameUI.Instance.ToggleTechMenu(true);
                    var tree = TechTreeMenu.Instance;
                    Resources.FindObjectsOfTypeAll<Button>().Single(b => b.name == "ModsTab" && b.gameObject.activeInHierarchy).onClick.Invoke();
                    Resources.FindObjectsOfTypeAll<Button>().Single(b => b.name == "Mod:" + LeylineConduitsPlugin.Id && b.gameObject.activeInHierarchy).onClick.Invoke();
                    Check(tree.Nodes.Count(n => n.Tech.Identifier == LeylineConduit.TechId) == 1,
                        "Mods research shows exactly one Leyline Conduits card");
                    GameUI.Instance.ToggleTechMenu(false);
                }
                catch (Exception ex) { error = ex.ToString(); }
            }

            var result = new JsonObject
            {
                { "passed", error == null },
                { "version", LeylineConduitsPlugin.Version },
                { "gameVersion", Application.version },
                { "unityVersion", Application.unityVersion },
                { "testedAtUtc", DateTime.UtcNow.ToString("O") }
            };
            var names = new JsonArray();
            foreach (string check in checks)
                names.Add(check);
            result["checks"] = names;
            if (error != null)
                result["error"] = error;
            File.WriteAllText(Path.Combine(directory, "runtime-tests.json"), result.ToString(true));
            if (error == null)
                Logger.LogInfo("LEYLINE_RUNTIME_PASS: " + checks.Count + " checks");
            else
                Logger.LogError("LEYLINE_RUNTIME_FAIL: " + error);
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        static void Capture(string path, ConduitSettingsPopup popup)
        {
            Canvas.ForceUpdateCanvases();
            var target = RenderTexture.GetTemporary(Screen.width, Screen.height, 24);
            var previous = RenderTexture.active;
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            var popupRoot = popup ? (RectTransform)popup.transform : null;
            var parent = popupRoot ? popupRoot.parent : null;
            var rootCanvas = parent ? parent.GetComponentInParent<Canvas>()?.rootCanvas : null;
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
            overlayCamera.clearFlags = CameraClearFlags.Depth;
            overlayCamera.orthographic = true;
            overlayCamera.orthographicSize = Screen.height / 2f;
            overlayCamera.depth = 10000;
            overlayCamera.cullingMask = 1 << 31;
            overlayCamera.targetTexture = target;
            try
            {
                foreach (var camera in Camera.allCameras.Where(c => c != overlayCamera).OrderBy(c => c.depth))
                {
                    var oldTarget = camera.targetTexture;
                    try { camera.targetTexture = target; camera.Render(); }
                    finally { camera.targetTexture = oldTarget; }
                }
                if (popup && rootCanvas)
                {
                    foreach (var entry in layers) entry.transform.gameObject.layer = 31;
                    float scale = rootCanvas.scaleFactor;
                    popupRoot.SetParent(null, false);
                    popupCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    popupCanvas.worldCamera = overlayCamera;
                    popupCanvas.planeDistance = 10;
                    popupCanvas.scaleFactor = scale;
                    Canvas.ForceUpdateCanvases();
                    overlayCamera.Render();
                }
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(texture));
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (popup)
                {
                    popupCanvas.renderMode = oldMode;
                    popupCanvas.worldCamera = oldCamera;
                    popupCanvas.planeDistance = oldDistance;
                    popupCanvas.scaleFactor = oldScaleFactor;
                    popupRoot.SetParent(parent, false);
                    popupRoot.anchorMin = oldAnchorMin;
                    popupRoot.anchorMax = oldAnchorMax;
                    popupRoot.pivot = oldPivot;
                    popupRoot.sizeDelta = oldSize;
                    popupRoot.anchoredPosition = oldPosition;
                    popupRoot.localScale = oldScale;
                }
                foreach (var entry in layers)
                    if (entry.transform) entry.transform.gameObject.layer = entry.layer;
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
