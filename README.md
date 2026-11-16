# Leyline Conduits

**Current release: 0.4.1 — for Kingdom Inc game version 1.0.14.**

Link distant machines with small enchanted conduits. Give an Input and an Output the same leyline name to move items between them, including across workshop tabs.

[Nexus Mods](https://www.nexusmods.com/kingdominc/mods/9) · [GitHub Releases](https://github.com/KingdomIncModding/LeylineConduits/releases)

Requires **Foundation 0.14.5 or later**. Find **Leyline Conduits** under **Tech Tree → Mods**, after researching **Portal**. Existing research and conduits are retained when updating.

## Features

- Compact 64 × 64 tiles, roughly the size of a one-cell sticker.
- One connection per conduit, with a gear to choose **Input** or **Output** and enter a leyline name.
- A tiny top grip lets you drag the conduit or click to pick it up. Right-click cancels placement.
- **Set Icon** opens a searchable picker of game icons. **Rune** restores the default bronze and blue crystal seal.
- Matching names connect conduits across all workshop tabs in the current kingdom.
- Multiple Outputs receive an Input's items in round-robin order, skipping disconnected, occupied or incompatible destinations.
- Each conduit holds one item. Equipment and adventurer details stay with the item.
- Modes, names, icons, pipes and waiting items are saved with the kingdom. Copies retain settings without duplicating inventory.

## Progression and costs

Research Portal first, then purchase **Leyline Conduits** in **Tech Tree → Mods**. Build conduits from the **Utility** category.

Research costs **4× Portal's research price** in Gold and Mana, including research discounts. Construction uses **2× Portal's base construction multiplier** in Gold and Rings.

Transfers cost **12 Mana per item**, reduced to **6**, then **3** by Portal cost-reduction research. Mana is charged once when an Input successfully hands an item to an Output. Insufficient Mana leaves the item waiting. Blocked, disconnected and incompatible destinations consume no Mana. Hover over a conduit or open its gear to see the current rate.

## Requirements and installation

Requires Kingdom Inc. for Windows, **BepInEx 5** and **Foundation 0.14.5+**, installed separately.

1. Close the game.
2. Install the requirements.
3. Extract the Leyline Conduits player ZIP into the folder containing `KingdomInc.exe`.
4. Confirm the plugin files are at `BepInEx/plugins/LeylineConduits/LeylineConduits.dll` and `BepInEx/plugins/LeylineConduits/mod.json`.
5. Launch the game.

When updating, replace the existing plugin files and avoid duplicate copies.

## How to use

1. Place a conduit, open its gear, choose **Input**, enter a name such as `iron`, and click **Apply**.
2. Connect your producer's output to the conduit's green input.
3. Place another conduit, choose **Output**, enter `iron`, and click **Apply**.
4. Connect its blue output to your destination.
5. Choose **Set Icon** in the gear menu to customize either conduit, then **Apply**.

Names ignore capitalization and leading or trailing spaces, with a maximum of 32 characters. Unnamed Inputs accept nothing. Opening settings pauses that conduit's transfers; close settings to resume.

Disconnect the pipe before switching mode. Drain waiting items before changing the leyline name or mode. You can change an icon while a pipe is connected or an item is waiting. Closing settings without Apply cancels your changes.

## Saves and removal

Existing purchased research, names, modes, icons, pipes and waiting items are retained when updating. Missing or unavailable saved icons use the default rune.

Keep Leyline Conduits installed while loading saves containing conduits. Before removing it, let waiting items reach their destinations, remove every conduit from every workshop tab, save and close the game. Then remove `BepInEx/plugins/LeylineConduits`.

## Compatibility

| Class.Method | Hook Type | Impact |
|---|---|---|
| TechNode.GetCost | Foundation hook (before) | Derives Leyline Conduits research cost directly from Portal's current discounted cost. |
| WorldFlowElement.SetElement | Foundation hook (after) | Hides default building chrome, stretches the port container, and attaches the compact tile view. |
| WorldFlowIO.SetIO | Foundation hook (after) | Updates the single conduit port's buffer/empty color sprites. |
| FlowElement.FromJson | Foundation hook (after) | Normalizes legacy saved conduit dimensions and port offset after deserialization. |

## Credits

Created by Urakka. The promotional cover uses AI-generated artwork. Game assets are not bundled.

[Source code](https://github.com/KingdomIncModding/LeylineConduits)

