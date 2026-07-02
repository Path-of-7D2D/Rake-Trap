# Contributors

This document is for developers modifying Rake Trap. Player-facing install notes live in [README.md](README.md).

## Repository Layout

- `1A-RakeTrap/` - deployable mod folder shipped in releases.
- `1A-RakeTrap/Config/` - XML appends for the trap block, recipe, and localization.
- `1A-RakeTrap/Resources/raketrap.unity3d` - built Unity asset bundle.
- `1A-RakeTrap/RakeTrap.dll` - built gameplay DLL.
- `src/RakeTrap/` - C# source for the custom block class.
- `UnityProject/` - Unity project used to build the animated rake trap prefab.
- `UnityProject/Assets/RakeTrap/SourceAssets/` - original rake material/texture folders plus `Rake001.fbx`, the modeler-provided source model.

## Tooling

Current local tooling targets:

- 7 Days To Die V3.0.
- Unity `2022.3.62f2`.
- .NET SDK capable of building `net48`.
- Easy Anti-Cheat disabled for in-game testing.

Unity executable used during development:

```powershell
C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe
```

Open the `UnityProject` folder directly in Unity Hub. Do not import the repository root as a Unity project.

## Build Workflow

Run commands from the repository root.

Build the Unity asset bundle:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe' -batchmode -quit -projectPath UnityProject -executeMethod BuildRakeTrapBundle.BuildAll -logFile UnityProject\build-raketrap.log
```

Validate the built bundle:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe' -batchmode -quit -projectPath UnityProject -executeMethod BuildRakeTrapBundle.ValidateBuiltBundle -logFile UnityProject\validate-raketrap.log
```

Build the C# DLL:

```powershell
dotnet build src\RakeTrap\RakeTrap.csproj -v:minimal
```

The C# build copies `RakeTrap.dll` into `1A-RakeTrap`. If the default Steam `Mods` folder exists, it reinstalls the full deployable mod folder to:

```text
C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\Mods\1A-RakeTrap
```

Use `/p:InstallToGame=false` to build without touching the live game folder.

## Implementation Notes

- The block name is `rakeTrap`.
- The custom block class is `RakeTrap.BlockRakeTrap`, configured in XML with the assembly-qualified `Class` value `RakeTrap.BlockRakeTrap, RakeTrap`.
- The trap uses `OnEntityWalking` and `OnEntityCollidedWithBlock` so crossing through the trigger area can spring it even if the entity enters from the handle side.
- Runtime rearm state is a lightweight per-block cooldown keyed by `Vector3i`.
- The visual animation uses `NetPackageAnimateBlock` and an Animator trigger named `Spring`.
- The Unity builder imports `SourceAssets/Rake001.fbx`, then parents both `RakeHead.001` and `RakeHandle` under `Mesh/HandlePivot` so the rake head and handle stay aligned through the spring animation.
- `Mesh/HandlePivot` is positioned at the rake head's ground-contact edge, not the handle, so the tines/head do not dip below the surface as the rake springs up.
- The idle animation holds `Mesh/HandlePivot` at `-90` degrees on X so the upright source model is armed flat on the ground with the tines facing up. The spring animation rotates that pivot to `0` degrees, then returns to `-90` over the 4 second rearm window.
- The block is marked `IsTerrainDecoration=true` with `GndAlign=1` so placing it on terrain preserves the existing voxel density instead of carving a small air hole under the rake.
- The block is marked `StabilityIgnore=true`, and `BlockRakeTrap.OnBlockStartsToFall` is a no-op, because this thin surface prop should not be deleted by block stability/falling physics.
- `BlockRakeTrap.GetCollisionAABB` follows the prefab's negative-Z renderer bounds and block rotation so the visible rake can be focused, damaged, and activated.
- ModelEntity blocks are targeted purely by `Physics.Raycast` against the prefab's own colliders; there is no voxel fallback. The Unity builder bakes a `BoxCollider` onto the prefab root (which `BlockShapeModelEntity` also reads once for the block's custom bounds), and `BlockRakeTrap.OnBlockEntityTransformAfterActivated` tags every collider `T_Block` at runtime — `Voxel.Raycast` only resolves a hit collider back to a block when it carries that tag, and tagging in the Unity project would be fragile because bundles serialize tags by index against the game's tag table.
- `Collide` is `melee,bullet,arrow,rocket` (matching the vanilla bird nest): ray hits are additionally filtered by the block's collide flags per hit mask, while leaving `movement` off keeps the trap walk-through. Without `movement`, the game parents the colliders to its `nocollision` physics layer at spawn, which focus/attack rays still test.
- The trap uses `MaxDamage=100` and vanilla `Path=solid` pathing so zombies can destroy it when it blocks a route without treating it as a preferred target.
- The vanilla block activation system provides a one-second `Pick Up` action and returns one `rakeTrap` item.

## Manual Test Checklist

After building and restarting the game:

- Confirm `Rake Trap` appears in crafting.
- Crafting consumes `1x Forged Iron` and `20x Wood`.
- Place the block on solid ground.
- Spawn or lure a zombie across the metal head.
- The rake snaps upward and returns to armed position after about 4 seconds.
- The target takes light damage.
- Most triggers knock the target down, while occasional triggers only deal the light hit.
- The trap can trigger again after rearming.
- Damage the trap and confirm it is destroyed after 100 block damage.
- Confirm zombies can attack the trap when it blocks their required route but do not prefer it over an open route.
- Interact with the placed trap, choose `Pick Up`, and confirm one `Rake Trap` is returned to inventory.

## Release Workflow

Releases are created with `.github/workflows/release.yml`.

The workflow is manual:

1. Run the `Release` workflow.
2. Enter a `version_tag`, for example `v0.1.0`.
3. The workflow validates the deployable folder, DLL, and resource bundle.
4. It zips `1A-RakeTrap`.
5. It generates changelog notes with `Path-of-7D2D/Changelog-Generator`.
6. It publishes a GitHub release with the zip attached.

Before publishing, ensure the deployable folder is current and committed.

## Git Hygiene

Do not commit local generated state:

- `UnityProject/Library/`
- `UnityProject/Logs/`
- `UnityProject/UserSettings/`
- `src/**/bin/`
- `src/**/obj/`

Do commit intentional deployable outputs when they change:

- `1A-RakeTrap/RakeTrap.dll`
- `1A-RakeTrap/Resources/raketrap.unity3d`
- `1A-RakeTrap/Config/*.xml`
- `1A-RakeTrap/Config/Localization.csv`

Before publishing, run at least:

```powershell
dotnet build src\RakeTrap\RakeTrap.csproj -v:minimal
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe' -batchmode -quit -projectPath UnityProject -executeMethod BuildRakeTrapBundle.ValidateBuiltBundle -logFile UnityProject\validate-raketrap.log
git diff --check
```
