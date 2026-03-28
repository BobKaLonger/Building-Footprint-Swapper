# Building Footprint Swapper

A SMAPI utility mod for Stardew Valley modders. When a building upgrade results in a larger footprint, this mod intercepts the upgrade and immediately enters move mode so the player can reposition the building themselves. This mod will block off the new footprint on the map, so no objects can be placed on the tile that the new building will occupy. Additionally, current game behavior when setting values for UpgradeSignTile and UpgradeSignHeight in a Content Pack does not actually change the position of the sign, and it just plain ol' won't show up for a modded building getting an upgrade. This mod reads UpgradeSignHeight and UpgradeSignTile directly from the Content Pack, allowing modders to set the position of the upgrade sign per existing documentation on the Stardew Modding Wiki.

---

## For Players

Install like any other SMAPI mod. This mod does nothing on its own — it only activates for buildings whose mod authors set up the Content Pack to trigger it.

---

## For Modders

Add the following `CustomFields` entry to your upgraded building's data in `Data/Buildings`:

```json
{
  "Action": "EditData",
  "Target": "Data/Buildings",
  "Entries": {
    "YourName.YourMod_YourBuilding": {
      "CustomFields": {
        "bobkalonger.BFS_util/ForceMove": "true"
      }
    }
  }
}
```

For the upgrade sign position, follow the Stardew Modding Wiki documentation:

```json
      "UpgradeSignTile": {
        "X": 0,
        "Y": 4
      },
      "UpgradeSignHeight": "48",
```

To further clarify, the X value will center the sign on that tile. If you want the sign centered on two tiles, you can use 0.5 values. The Y value needs to be pretty big to actually draw on TOP of the building texture, and 4 is usually good enough (though this places it way at the bottom or off the building).Use UpgradeSignHeight to move it back up, and remember that 16px is one tile, so in the example above, it would move back up 3 tiles. You are able to shift the sign by 1 pixel, allowing for some fine positioning. If you set the Y value for UpgradeSignTile too small, the sign will draw behind the building, and you can only see it when the player triggers "FadeWhenBehind" and the building goes see-through. Regardless, now you can play around with the values in your Content Pack and just test out what happens. Have fun!

### How It Works

---

When a player clicks an upgradeable building in Robin's build menu:

1. The mod checks whether the target blueprint has `bobkalonger.BFS_util/ForceMove` set to `"true"` in its `CustomFields`.
2. If yes, it reads the values for BuildCost and BuildMaterials, stores those values and defers charging the player until the move is complete.
3. The carpenter menu then immediately triggers move mode, allowing the player to reposition the building before the upgrade starts. The new, larger footprint is shown during the move.
4. When the player places the building (second click) they pay the gold and materials stored, and the old building will shift (temporarily during the upgrade) to the upper left corner of the NEW footprint. The rest of the upgrade sequence continues as in the base game.
5. During the upgrade, the player is locked out from placing objects within the new footprint to protect the future building. NPCs will also path around the new footprint.
5. When the upgrade completes, the player will see the new building fully occupying the new footprint.

If the player cancels out of move mode, the building remains at its original position in its original state. Since the build cost and materials are deferred until the second click, they aren't charged anything.

### Notes

- If live data is unavailable at click time (e.g. a config change occurred while the carpenter menu was already open), the mod falls back to the blueprint's cached data and logs a warning. The player will get the behavior that was correct when they opened the menu — not a hard failure.
- This mod does not require a soft dependency. If it isn't installed, the `CustomFields` key is simply ignored by the game.
- Multiplayer behavior has not been tested. Use with caution in multiplayer environments and PLEASE REPORT BUGS and I will do my best to work them out.