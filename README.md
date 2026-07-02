# Rake Trap

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

- A craftable `Rake Trap` block.
- Recipe: `1x Forged Iron` and `20x Wood`.
- The trap triggers when an entity steps on it.
- The rake snaps up and then returns to armed position after 4 seconds.
- The hit deals light bashing damage and has a high chance to apply the vanilla knockdown buff.
- A small brush-off chance lets the target take the light hit without being knocked down.
- The trap has 100 durability and can be destroyed by players or zombies.
- Placed rake traps can be picked up and returned to your inventory.

## Compatibility Notes

This mod adds a new block, recipe, localization, Unity asset bundle, and DLL-backed block class. Mods that heavily replace trap blocks, block stepping/collision behavior, or knockdown buffs may need compatibility testing.

For development notes and build instructions, see [CONTRIBUTORS.md](CONTRIBUTORS.md).
