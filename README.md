# Leyline Conduits

Small enchanted ports for Kingdom Inc. Link distant machines by giving their conduits the same leyline name.

[Nexus Mods](https://www.nexusmods.com/kingdominc/mods/9) · [GitHub Releases](https://github.com/urakkaamyx/KingdomInc_LeylineConduits/releases)

## Version 0.3.0 — Portal upgrade

Leyline Conduits is researched directly after **Portal** on the same tech-tree branch. Research costs **4× Portal's research price** in Gold and Mana, including native research discounts. Each conduit costs **2× Portal's construction price** in Gold and Rings.

Transfers cost the same as a native Portal: **12 Mana per item**, reduced to **6**, then **3** by Portal's existing cost-reduction research. Mana is charged once when an Input successfully hands an item to a connected, compatible Output. Insufficient Mana leaves the item waiting; blocked, disconnected or incompatible destinations consume no Mana. The hover tooltip and gear menu show the current rate.

A tiny grip above the rune moves the conduit using native node movement: drag it, or click to pick up and place. Right-click cancels. Pipes follow the node. The pin and gear stay separately clickable. Both ports sit 8 pixels lower to align with the first port of a native machine on the same grid row; existing conduits adopt this position on load.

The cheap testing override has been retired. Old `Testing.GoldOnlyCosts` configuration entries are ignored. The original 0.2.0 balance remains archived in `ConduitCosts.cs`: Gold + Mana, construction multiplier **0.25**, research multiplier **0.5**, and the former Enchanter cost tier.

A custom bronze seal with a blue crystal rune matches across the tech tree, build menu, default conduit tile and settings preview. Player-selected icons are retained. Published downloads are available through the Nexus Mods and GitHub Releases links above.

### Features

- Compact 64 × 64 workspace footprint, matching a one-cell sticker, with one pipe connection per conduit.
- Icon-focused tile with a small gear; hover for mode, leyline name and status.
- Gear menu switches between **Input** (machine → leyline) and **Output** (leyline → machine).
- **Set Icon** opens a searchable, paginated picker using the game's sticker icon catalog. **Rune** restores the default. Click **Apply** to keep the chosen icon.
- A **Leyline Name** text box creates or joins a named channel. Names ignore capitalization and leading/trailing spaces; up to 32 characters.
- Matching conduits link across all workshop tabs in the current kingdom.
- Multiple Outputs share an Input's items in round-robin order, skipping disconnected, occupied or incompatible destinations.
- Each conduit holds one item. Blocked items wait; the network moves the original item object, preserving its equipment/adventurer data.
- Modes, names, selected icons, pipes and waiting items use the game's native save format. Copies retain settings without copying items.
- Research **Portal → Leyline Conduits**, then build from the Utility category.

## Requirements

Windows Mono edition of Kingdom Inc. and BepInEx 5.x installed separately. Development was verified against local **Release v1.0.10**, Unity **6000.3.27f2**, and BepInEx **5.4.23.6**. Game and loader files are not included.

## Installation

Extract the player ZIP into the folder containing `KingdomInc.exe`. The DLL belongs at `BepInEx/plugins/LeylineConduits/LeylineConduits.dll`. A new installation is picked up on the next game launch. Close the game before replacing an existing installed version.

The development installer also accepts `-StageForNextLaunch`: it preserves an already mapped DLL in the project's local backup folder before staging the replacement. This leaves an existing game session running until you choose to restart.

Manual installation is supported. Vortex deployment has not been tested.

## Use

1. Research Portal, then Leyline Conduits, and place two conduits.
2. Open the first gear, choose **Input**, enter a name such as `iron`, and Apply.
3. Connect your producer's output to that conduit's green input.
4. Open the other gear, choose **Output**, enter `iron`, and Apply.
5. Connect that conduit's blue output to your destination.

New conduits open their settings immediately. Unnamed Inputs accept nothing. Opening settings pauses that conduit's network transfers. Close settings to let a waiting Input item leave.

You can change an icon even while an item is waiting or a pipe is connected. Icon choices belong to individual conduits; they do not change the routing channel. Search by an icon's game name, or browse with Prev/Next. Back returns to settings without discarding the draft; closing settings without Apply cancels the draft.

Disconnect a conduit's pipe before switching its mode. Drain waiting items before changing its leyline name or mode. Names create links immediately; there is no separate channel manager.

## Saves, updating and removal

Keep the plugin installed while loading saves containing Leyline Conduits. To uninstall, let waiting items reach their destinations, remove every conduit from every workshop tab, save, close the game, and remove the plugin folder. Removing an occupied node follows the native game's item cleanup rules; drain it first.

Previously purchased Leyline research is retained on existing saves; it is not refunded or revoked. Existing conduits keep their names, roles, icons, pipes and waiting items, and use the new per-item Mana cost after updating.

Existing 0.1.0 conduits automatically use the smaller size and centered connection when loaded, retaining their mode, channel, pipes and waiting items. Saves without an icon, or with an unavailable icon identifier, use the default rune.

This is an initial playable build. Long-running kingdom balance, large networks and custom blueprint import/export have not been validated. Sources that compete for one Output use the game's update order; round robin applies to the Outputs selected by each Input.

## Build and verify

Keep this source directory at `<KingdomInc>/_Mods/LeylineConduits` so references resolve to local game and loader assemblies. A .NET SDK with .NET Standard 2.1 support is required.

```powershell
dotnet build _Mods/LeylineConduits/LeylineConduits.csproj -c Release --nologo
& _Mods/LeylineConduits/verify-isolated.ps1
& _Mods/LeylineConduits/verify-isolated.ps1 -WithExistingPlugins
& _Mods/LeylineConduits/install.ps1
python _Mods/LeylineConduits/Tools/package_nexus.py
```

Diagnostics run in a dedicated local runtime with a disposable kingdom; save writes, latest-save loading, achievements and Steam integration are blocked. The normal running game is left open. Reports and screenshots are written to `Verification/`. Companion verification loads the installed Ledger, Party-Up, Path Finder, Sharable Blueprints and Factory API plugins, with a separate API port.

Source repository: `urakkaamyx/KingdomInc_LeylineConduits`. The build, diagnostic, installation and packaging scripts do not publish externally.

## Credits

Mod code and custom pixel rune by Urakka. The promotional thumbnail was generated with the built-in image generation tool from the user's shared Kingdom .inc thumbnail master. The master remains unchanged. Native UI, fonts, sprites and prefabs are read from the installed game and are not bundled. BepInEx and Harmony remain the work of their respective authors.
