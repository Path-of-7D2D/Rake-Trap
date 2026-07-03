# Rake Trap

![Rake Trap](assets/header.png)

Rake Trap adds a cheap early-game trap to 7 Days To Die V3.0.

## Requirements

- 7 Days To Die V3.0.
- Easy Anti-Cheat disabled. This mod includes a DLL and is marked `SkipWithAntiCheat`.
- For multiplayer, install the mod on the server and on every client that connects.

## Installation

1. Download the latest release zip.
2. Extract the zip.
3. Copy the `1A-RakeTrap` folder into your game `Mods` folder.
4. Restart the game.

Default Steam path:

```text
C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\Mods\1A-RakeTrap
```

## What It Adds

- Craftable `Rake Trap` and `Spiked Rake Trap` blocks.
![Rake Trap](assets/thumbnail.png)
- Recipe: `1x Forged Iron` and `20x Wood`.
- Spiked recipe: `1x Rake Trap`, `4x Nails`, and `2x Forged Iron` at a workbench.
- The trap triggers when an entity steps on it.
- The rake snaps up and then returns to armed position after 4 seconds.
- The normal hit deals light bashing damage, while the spiked trap deals 60 total damage.
- The spiked trap converts 15% of its hit into armor-piercing damage.
- Both traps have a high chance to apply the vanilla knockdown buff.
- A small brush-off chance lets the target take the light hit without being knocked down.
- Both traps have 100 durability and can be destroyed by players or zombies.
- Placed rake traps can be picked up and returned to your inventory.

## Compatibility Notes

This mod adds a new block, recipe, localization, Unity asset bundle, and DLL-backed block class. Mods that heavily replace trap blocks, block stepping/collision behavior, or knockdown buffs may need compatibility testing.

For development notes and build instructions, see [CONTRIBUTORS.md](CONTRIBUTORS.md).
