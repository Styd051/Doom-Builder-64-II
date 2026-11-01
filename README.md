# Doom Builder 64 II

A map editor for **Doom 64** (the Doom 64 map format of Doom64 EX and the 2020 remaster), built on
[GZDoom Builder (Bugfix)](https://github.com/UltimateDoomBuilder/UltimateDoomBuilder/tree/last_slimdx) R3112.

It replaces the first Doom Builder 64, which was based on Doom Builder 2 (r1302): the Doom 64 code of
Kaiser's Doom Builder 64 was ported into GZDoom Builder (Bugfix), so the editor now has
its drawing tools, visual mode, script editor and plugins. Doom, Boom, Heretic, Hexen and UDMF maps can still be
edited: Doom 64 is one more map format.

**Download:**
- Get the newest version on the [releases page](https://github.com/Styd051/Doom-Builder-64-II/releases) and unpack
  the `.zip` file in a folder you can write to, so that the editor can update itself.
- The editor looks for a new version when it starts (option "Check for updates at startup" in Tools > Preferences)
  and with Help > Check for updates. It then downloads and installs the new version itself.

**Doom 64 features:**
- **Map format:** reads and writes the Doom 64 map format: linedef flags, activation types, switch setup, sector
  flags and the five colored lights of a sector with their tags (LIGHTS lump). Vertices keep their decimals.
- **Edit windows:** Doom 64 linedef, sector (colors, light tags and index) and thing properties.
- **Visual mode:**
  - The Doom 64 lighting (floor, ceiling, thing, upper and lower wall colors), switch decals, mirrored textures,
    textures with holes, sprite palettes, translucent things and Nightmare things.
  - The skies of the `SKYDEFS` lump (clouds, pictures, fire) with their fog, and liquid floors with their two layers.
  - "Doom 64 light intensity" and "Doom 64 display brightness" in the preferences: the two brightness settings of
    the remaster. The texture views of the 2D modes use them too.
  - Things with the Spawner flag, which are not there when the level starts, are shown as ghosts. They can also be
    hidden or shown normally, and their fade-in can be previewed when you aim at them or at their Thing Spawn line.
  - Copy and paste of lights, "light only" view.
- **Animations in visual mode**, each with its own button and its line in the View menu: the sky (clouds, lightning,
  fire), liquids and scrolling floors, ceilings and walls, sector light effects, the textures of the `ANIMDEFS` lump,
  and switches when you aim at them.
- **2D modes:** views of the floor, ceiling and thing colors. Linedef color presets for Doom 64 (tagged lines,
  actions, block sound, block monster, automap flags). The Automap mode has the lines and colors of the game
  (preset "Doom 64") and its textured view ("Textured" button), where a right click hides a sector or shows it
  again. The Nodes Viewer mode reads Doom 64 maps.
- **Editing tools:** copy/paste and prefabs keep all Doom 64 data; Paste Properties, color gradients over sectors and
  Find & Replace for sector light colors, tags and indices.
- **Scripts:** BLAM macro scripts (MACROS lump) and the `MAPINFO`, `SKYDEFS` and `ANIMDEFS` lumps of your resources
  in the script editor, with their Doom 64 keywords.
- **Dark theme:** View > Dark Theme, or in the preferences.

**System requirements:**
- 2.4 GHz CPU or faster (multi-core recommended)
- Windows 7, 8 or 10
- Graphics card with Pixel Shader model 2.0 support

**Required software:**
- [Microsoft .Net Framework 4.6.1](https://www.microsoft.com/en-ca/download/details.aspx?id=49981)
- [DirectX 9.0 Runtime](https://www.microsoft.com/en-us/download/details.aspx?id=35&44F86079-8679-400C-BFF2-9CA5F2BCBDFC=1)

**Getting started:**
- Open or create a map with one of the two Doom 64 game configurations and add your `DOOM64.WAD` as a resource:
  - "Doom 64: Doom64 (Doom 64 format)" builds the nodes with D64BSP.
  - "Doom 64: Doom64 Extended (Doom 64 format)" builds them with BSP64 Enhanced and adds what only this nodebuilder
    reads: the linedef flags "No Blockmap" and "Merge Sides", and the sector effect "Blind To Monsters".
- The settings are stored in `%LOCALAPPDATA%\Doom Builder 64 II\Builder64II.cfg`. They are separate from the
  settings of the first Doom Builder 64 and of GZDoom Builder, so these editors can be installed side by side.

**Testing a map in the 2020 remaster:**
- In Tools > Game Configurations, choose your Doom 64 configuration, open the Testing tab and set the Application to
  `DOOM64_x64.exe` in the folder of the game. The parameters are already set.
- Press F9 to test the map. Steam asks you to confirm the custom arguments, then the game starts on your map.
- Your map takes the place of the game map of the same name. The name of the map counts, not the name of the file:
  set it in Edit > Map Options, "Level name". A map named `MAP02` is played as map 2.
- The game turns achievements off while a WAD file is loaded this way.

**Tools:**
- [D64BSP](https://github.com/Styd051/D64BSP): the Doom 64 nodebuilder, made by Kaiser from glBSP.
- [D64BLAM](https://github.com/Styd051/D64BLAM): the compiler of the BLAM macro scripts, made by Kaiser.
- [DOOM64-RE](https://github.com/Erick194/DOOM64-RE): the reverse engineering of the original Doom 64 engine.

**Building:**
- Open `Builder.sln` in Visual Studio (the .NET Framework 4.6.1 targeting pack is required) and build the
  Release configuration for x86. The editor is built into the `Build` folder.

**Making a release:**
- Set the version of the release in `Source/Core/Properties/AssemblyInfo.cs`
  (`[assembly: AssemblyInformationalVersion("1.1.0")]`), then build the Release configuration for x86.
  Keep `AssemblyVersion` in that file: it is the revision of GZDoom Builder (Bugfix), and the plugins must match it.
- Zip the `Build` folder (with `Updater.exe`) and publish it on GitHub as a release whose tag is the same version
  (`V1.1.0`), with the `.zip` file attached. "Check for updates" in the editor compares its version with the tags
  of the releases, then downloads and installs the `.zip` file of the newest one.
- The version and the tag must be the same number, or the editor offers the same update again and again.

**Credits:**
- Doom Builder 64 II is modified and maintained by Styd051.
- Doom Builder 64 was made by Samuel Villarreal (Kaiser).
- GZDoom Builder was made by MaxED; GZDoom Builder (Bugfix) is maintained by ZZYZX.
- Doom Builder 2 was made by Pascal vd Heiden (CodeImp).
- The logo and the background image were made by Hieronymus.
- Erick194 made DOOM64-RE, the reverse engineering of the original Doom 64 engine, which served as the reference for
  the Doom 64 behaviour of Doom Builder 64 II.

Doom Builder 64 II is released under the GNU General Public License (see `Build/GPL.txt`).
