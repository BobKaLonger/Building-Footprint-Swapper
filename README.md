# Building Upgrade Mover

A SMAPI utility mod for Stardew Valley modders. When a building upgrade results in a larger footprint, the normal upgrade flow places it in the same spot — which may not fit. This mod intercepts the upgrade and immediately enters move mode so the player can reposition the building themselves.

---

## For Players

Install like any other SMAPI mod. This mod does nothing on its own — it only activates for buildings whose mod authors have opted in via the instructions below.

---

## For Modders

### Opting In

Add the following `CustomFields` entry to your upgraded building's data in `Data/Buildings`:

```json
{
  "Action": "EditData",
  "Target": "Data/Buildings",
  "Entries": {
    "YourName.YourMod_PremiumCoop": {
      "CustomFields": {
        "bobkalonger.BFS_util/ForceMove": "true"
      }
    }
  }
}
```

Set this on the **result** of the upgrade (the larger building), not the building being upgraded from.

### How It Works

When a player clicks an upgradeable building in Robin's build menu:

1. The mod checks whether the target blueprint has `bobkalonger.BFS_util/ForceMove` set to `"true"` in its `CustomFields`.
2. If yes, it immediately finishes the upgrade (consuming resources and calling `FinishConstruction`) instead of queueing it for the next day.
3. The carpenter menu then pivots directly into move mode with the newly upgraded building pre-selected, so the player can place it at a valid position for the new footprint.
4. If the player cancels out of move mode, the building remains at its original position in its upgraded state.

### Notes

- Resources and gold are consumed at click time, exactly as the normal upgrade flow would.
- The CustomFields key is read from `Game1.buildingData` (live asset state) at click time, so runtime asset invalidation from Content Patcher config changes is handled gracefully.
- If live data is unavailable at click time (e.g. a config change occurred while the carpenter menu was already open), the mod falls back to the blueprint's cached data and logs a warning. The player will get the behavior that was correct when they opened the menu — not a hard failure.
- This mod does not require a soft dependency. If it isn't installed, the `CustomFields` key is simply ignored by the game.

---

## Known Limitations

- Cancelling out of move mode after the upgrade is completed does not refund resources or revert the upgrade. The building will remain in its upgraded state at its original tile.
- Multiplayer behavior has not been tested. Use with caution in multiplayer environments.

---

## TODO (before 1.0 release)

- [ ] Replace `bobkalonger` in the `CustomFieldKey` and `manifest.json` with actual author name
- [ ] Investigate `FinishConstruction` behavior in multiplayer
- [ ] Consider whether a cancel-and-rollback path is desirable or feasible
