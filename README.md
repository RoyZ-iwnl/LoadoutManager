# Loadout Manager

[中文版](README_CN.md)

A [MelonLoader](https://melonwiki.xyz/) mod for **Gunner HEAT PC** that allows players to customize ammo loadout distribution across ammo racks.

## Features

- **Ammo Rack Editor**: Customize the distribution of each ammo type across different ammo racks
- **Autocannon Feed Support**: Adjust the composition of loaded ammo belts for autocannon weapons
- **Chambered Ammo Selection**: Choose which ammo type is loaded in the chamber
- **Dual Language Support**: English and Chinese UI

## How to Use

1. Enter any vehicle with weapons
2. The loadout editor window will appear automatically
3. Adjust ammo distribution using sliders:
   - Set the count for each ammo type in each rack
   - Use "Fill" button to fill a rack with one ammo type exclusively
   - Use "Clear" button to remove all ammo of that type from the rack
4. Select chambered ammo type at the bottom
5. Click "Apply" to save changes

## Notes for Autocannon Vehicles

- **Rack 0** represents the ready-to-fire ammo on the feed (ammo box)
- For autocannon vehicles, Rack 0 is hidden by default since it's not a standard ammo rack
- You can adjust the loaded belt composition in the "Loaded Ammo" section
- The total loaded rounds are fixed - adjusting one ammo type will rebalance others

## Installation

1. Install [MelonLoader](https://melonwiki.xyz/#/?id=requirements) for GHPC
2. Drop `LoadoutManager.dll` into the `Mods/` folder

## Configuration

After first launch, edit `UserData/MelonPreferences.cfg`:

```
[LoadoutManager]
HideRack0ForAutocannon = true
UIScale = 1.0
Language = 0
```

| Option | Description | Default |
|---|---|---|
| HideRack0ForAutocannon | Hide Rack 0 for autocannon vehicles (Rack 0 is the ready-to-fire ammo on feed) | true |
| UIScale | UI scale multiplier (0.5-2.0) | 1.0 |
| Language | Interface language (0=English, 1=Chinese) | 0 |

## Credits

- Made by RoyZ