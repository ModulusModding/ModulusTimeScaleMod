# ModulusTimeScaleMod

A mod for **Modulus** that adds **1x**, **2x**, and **4x** simulation speed buttons to the factory top bar, next to the day/night control. Speed uses the game’s **`GlobalUpdateMultiplier`**.

## Features

- Three speed presets: **1x**, **2x**, **4x**
- Buttons appear on the in-game factory HUD toolbar (beside the day/night control)
- No separate mod config: the live value is whatever **`GlobalUpdateMultiplier`** is for the session (including from the loaded save) until you tap a button
- **Save play time** (total minutes stored on the save and shown in load / save UI) advances with **real wall time**, not scaled game time, so running at 2x or 4x does not inflate the reported hours played

## Installation

1. Requires **ModulusModLoader** installed in `BepInEx\plugins\ModulusModLoader\`.
2. Copy the `ModulusTimeScaleMod` folder to `Documents\My Games\Modulus\mods\`:
   ```
   Documents\My Games\Modulus\mods\
     ModulusTimeScaleMod\
       About\About.xml
       ModulusTimeScaleMod.dll
   ```
3. Launch the game. Load or enter a **factory** level. Use the new speed buttons on the top bar.

## Configuration

There is **no** BepInEx config file for speed. Choose **1x / 2x / 4x** from the toolbar each session; the game’s global multiplier drives what you see.

## Building from Source

1. Copy `ModulusTimeScaleMod.VS.User.props.example` to `ModulusTimeScaleMod.VS.User.props` and set your `SteamLibraryDirectory`.
2. Ensure `ModulusModLoader.dll` is either built in the sibling `ModulusModLoader\` project or deployed to `BepInEx\plugins\ModulusModLoader\`.
3. Open `ModulusTimeScaleMod.sln` and build. The DLL auto-publishes to the mods folder.
