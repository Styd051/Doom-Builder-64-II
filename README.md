# Doom Builder 64 II

A map editor for **Doom 64** (the Doom 64 map format of Doom64 EX and the 2020 remaster), built on
[GZDoom Builder (Bugfix)](https://github.com/UltimateDoomBuilder/UltimateDoomBuilder/tree/last_slimdx) R3112.

It replaces the first Doom Builder 64 II, which was based on Doom Builder 2 (r1302): the Doom 64 code of
Kaiser's Doom Builder 64 and of Doom Builder 64 II was ported into GZDoom Builder (Bugfix), so the editor now has
its drawing tools, visual mode, script editor and plugins. Doom, Boom, Heretic, Hexen and UDMF maps can still be
edited: Doom 64 is one more map format.

**Doom 64 features:**
- Reads and writes the Doom 64 map format: linedef flags, activation types, switch setup, sector flags and the
  five colored lights of a sector with their tags (LIGHTS lump). Vertices keep their decimals.
- Edit windows for the Doom 64 linedef, sector (colors, light tags and index) and thing properties.
- Visual mode with the Doom 64 lighting (floor, ceiling, thing, upper and lower wall colors), switch decals,
  mirrored textures, sprite palettes and Nightmare things; copy and paste of lights, "light only" view.
- 2D views of the floor, ceiling and thing colors.
- Copy/paste and prefabs keep all Doom 64 data; Paste Properties, color gradients over sectors and
  Find & Replace for sector light colors, tags and indices.
- Linedef color presets for Doom 64 (tagged lines, actions, block sound, block monster, automap flags).
- BLAM macro scripts (MACROS lump) in the script editor, and the D64BSP nodebuilder.

**System requirements:**
- 2.4 GHz CPU or faster (multi-core recommended)
- Windows 7, 8 or 10
- Graphics card with Pixel Shader model 2.0 support

**Required software:**
- [Microsoft .Net Framework 4.6.1](https://www.microsoft.com/en-ca/download/details.aspx?id=49981)
- [DirectX 9.0 Runtime](https://www.microsoft.com/en-us/download/details.aspx?id=35&44F86079-8679-400C-BFF2-9CA5F2BCBDFC=1)

**Getting started:**
- Open or create a map with the game configuration "Doom 64: Doom64 EX (Doom 64 format)" and add your
  `DOOM64.WAD` as a resource.
- The settings are stored in `%LOCALAPPDATA%\Doom Builder 64 II\Builder64II.cfg`. They are separate from the
  settings of the first Doom Builder 64 II and of GZDoom Builder, so these editors can be installed side by side.

**Building:**
- Open `Builder.sln` in Visual Studio (the .NET Framework 4.6.1 targeting pack is required) and build the
  Release configuration for x86. The editor is built into the `Build` folder.

**Credits:**
- Doom Builder 64 II is modified and maintained by Styd051.
- Doom Builder 64 was made by Kaiser (villsa).
- GZDoom Builder was made by MaxED; GZDoom Builder (Bugfix) is maintained by ZZYZX.
- Doom Builder 2 was made by Pascal vd Heiden (CodeImp).

Doom Builder 64 II is released under the GNU General Public License (see `Build/GPL.txt`).
