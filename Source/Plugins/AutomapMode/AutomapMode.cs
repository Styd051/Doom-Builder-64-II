
#region ================== Copyright (c) 2016 Boris Iwanski

/*
 * Copyright (c) 2016 Boris Iwanski https://github.com/biwa/automapmode
 * This program is released under GNU General Public License
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Geometry;

#endregion

namespace CodeImp.DoomBuilder.AutomapMode
{
	[EditMode(DisplayName = "Automap Mode",
			  SwitchAction = "automapmode",	// Action name used to switch to this mode
			  ButtonImage = "automap.png",	// Image resource name for the button
			  ButtonOrder = int.MinValue + 503,	// Position of the button (lower is more to the bottom)
			  ButtonGroup = "000_editing",
			  UseByDefault = true)]

	public class AutomapMode : ClassicMode
	{
		#region ================== Enums

		internal enum ColorPreset
		{
			DOOM,
			HEXEN,
			STRIFE,
			DOOM64, // styd. Only offered for a Doom 64 map
		}

		#endregion

		#region ================== Constants

		private const float LINE_LENGTH_SCALER = 0.001f; //mxd
		private const string DOOM64_HIDE_SPECIAL_FLAG = "33554432"; // styd. "Hide Special In Automap"
		private const string DOOM64_HIDE_SECTOR_FLAG = "512"; // styd. "Hide in Automap"
		private const string DOOM64_SECRET_THING_FLAG = "256"; // styd. "Count Secret On Pickup"
		private const float DOOM64_THING_SIZE = 32f; // styd. How far the points of a thing are from it on the automap of the game

		#endregion

		#region ================== Variables

		private CustomPresentation automappresentation;
		private List<Linedef> validlinedefs;
		private HashSet<Sector> secretsectors; //mxd

		// Highlighted item
		private Linedef highlighted;
		private Sector highlightedsector; // styd. The textured automap of Doom 64 highlights a sector

		//mxd. UI
		private MenusForm menusform;

		//mxd. Colors
		private PixelColor ColorSingleSided;
		private PixelColor ColorSecret;
		private PixelColor ColorFloorDiff;
		private PixelColor ColorCeilDiff;
		private PixelColor ColorMatchingHeight;
		private PixelColor ColorHiddenFlag;
		private PixelColor ColorInvisible;
		private PixelColor ColorBackground;

		// styd. Doom 64: a line with an action, any other line with two sides
		private PixelColor ColorSpecial;
		private PixelColor ColorTwoSided;
		private bool doom64presets;
		private List<Thing> secretthings = new List<Thing>(); // styd. Doom 64: the things that count as a secret when picked up
		
		#endregion

		#region ================== Properties

		public override object HighlightedObject { get { return (Textured ? (object)highlightedsector : highlighted); } } // styd
		
		#endregion

		#region ================== Constructor / Disposer

		//mxd
		public AutomapMode()
		{
			// Create and setup menu
			menusform = new MenusForm();
			menusform.ShowHiddenLines = General.Settings.ReadPluginSetting("automapmode.showhiddenlines", false);
			menusform.ShowSecretSectors = General.Settings.ReadPluginSetting("automapmode.showsecretsectors", false);
			menusform.ShowLocks = General.Settings.ReadPluginSetting("automapmode.showlocks", true);
			menusform.ColorPreset = (ColorPreset)General.Settings.ReadPluginSetting("automapmode.colorpreset", (int)ColorPreset.DOOM);

			// styd. A Doom 64 map has the preset of its game, and remembers its own choice
			doom64presets = ((General.Map != null) && General.Map.DOOM64);
			if(doom64presets)
			{
				menusform.AddDoom64Preset();
				menusform.ColorPreset = (ColorPreset)General.Clamp(General.Settings.ReadPluginSetting("automapmode.doom64colorpreset", (int)ColorPreset.DOOM64), (int)ColorPreset.DOOM, (int)ColorPreset.DOOM64);
				menusform.ShowTextured = General.Settings.ReadPluginSetting("automapmode.doom64textured", false);
			}

			// Handle events
			menusform.OnShowHiddenLinesChanged += delegate
			{
				UpdateValidLinedefs();
				UpdateHighlightedSector(); // styd
				General.Interface.RedrawDisplay();
			};

			menusform.OnShowSecretSectorsChanged += delegate { General.Interface.RedrawDisplay(); };
			menusform.OnShowLocksChanged += delegate { General.Interface.RedrawDisplay(); };
			menusform.OnShowTexturedChanged += delegate { ClearHighlights(); General.Interface.RedrawDisplay(); }; // styd

			menusform.OnColorPresetChanged += delegate
			{
				ApplyColorPreset(menusform.ColorPreset);
				if(doom64presets) { UpdateValidLinedefs(); ClearHighlights(); } // styd. The preset of Doom 64 does not show the same lines, nor the same view
				General.Interface.RedrawDisplay();
			};

			// Apply color preset
			ApplyColorPreset(menusform.ColorPreset);
		}

		#endregion

		#region ================== Methods

		// This highlights a new item
		private void Highlight(Linedef l)
		{
			// styd. The textured automap of Doom 64 draws no line but the highlighted one: the view is drawn again
			if(Textured)
			{
				highlighted = l;
				General.Interface.RedrawDisplay();
				if((highlighted != null) && !highlighted.IsDisposed) General.Interface.ShowLinedefInfo(highlighted);
				else General.Interface.HideInfo();
				return;
			}

			// Update display
			if(renderer.StartPlotter(false))
			{
				// Undraw previous highlight
				if((highlighted != null) && !highlighted.IsDisposed)
				{
					PixelColor c = LinedefIsValid(highlighted) ? DetermineLinedefColor(highlighted) : PixelColor.Transparent;
					renderer.PlotLine(highlighted.Start.Position, highlighted.End.Position, c, LINE_LENGTH_SCALER);
				}

				// Set new highlight
				highlighted = l;

				// Render highlighted item
				if((highlighted != null) && !highlighted.IsDisposed && LinedefIsValid(highlighted))
				{
					renderer.PlotLine(highlighted.Start.Position, highlighted.End.Position, General.Colors.InfoLine, LINE_LENGTH_SCALER);
				}

				// Done
				renderer.Finish();
				renderer.Present();
			}

			// Show highlight info
			if((highlighted != null) && !highlighted.IsDisposed)
				General.Interface.ShowLinedefInfo(highlighted);
			else
				General.Interface.HideInfo();
		}

		// styd. The textured automap of Doom 64 highlights the sector under the mouse (see SectorIsValid)
		private void HighlightSector(Sector s)
		{
			highlightedsector = s;
			General.Interface.RedrawDisplay();
			if((highlightedsector != null) && !highlightedsector.IsDisposed) General.Interface.ShowSectorInfo(highlightedsector);
			else General.Interface.HideInfo();
		}

		// styd. The sectors that the textured automap can highlight, as the lines view its lines: a hidden
		// sector only when the hidden lines (and sectors) are shown
		private bool SectorIsValid(Sector s)
		{
			if((s == null) || s.IsDisposed) return false;
			return !s.IsFlagSet(DOOM64_HIDE_SECTOR_FLAG) || (menusform.ShowHiddenLines ^ General.Interface.CtrlState);
		}

		// styd. Nothing highlighted any more: the textured automap and the lines do not highlight the same
		private void ClearHighlights()
		{
			highlighted = null;
			highlightedsector = null;
			General.Interface.HideInfo();
		}

		// styd. The sector under the mouse is highlighted again: after an undo or a redo, a right click, or when
		// the hidden sectors are shown or not any more
		private void UpdateHighlightedSector()
		{
			if(!Textured) return;
			Sector s = (mouseinside ? General.Map.Map.GetSectorByCoordinates(mousemappos) : null);
			highlightedsector = (SectorIsValid(s) ? s : null);
			if(highlightedsector != null) General.Interface.ShowSectorInfo(highlightedsector);
			else General.Interface.HideInfo();
		}

		//mxd
		internal void UpdateValidLinedefs()
		{
			validlinedefs = new List<Linedef>();
			foreach(Linedef ld in General.Map.Map.Linedefs)
				if(LinedefIsValid(ld)) validlinedefs.Add(ld);
		}

		//mxd
		internal void UpdateSecretSectors()
		{
			secretsectors = new HashSet<Sector>();
			foreach(Sector s in General.Map.Map.Sectors)
				if(SectorIsSecret(s)) secretsectors.Add(s);

			// styd. A Doom 64 thing counts as a secret by its flag (MTF_SECRET of the game)
			secretthings = new List<Thing>();
			if(General.Map.DOOM64)
			{
				foreach(Thing t in General.Map.Map.Things)
					if(t.IsFlagSet(DOOM64_SECRET_THING_FLAG)) secretthings.Add(t);
			}
		}

		// styd. The textured automap of Doom 64 is shown: the floors of the sectors, and no line
		private bool Textured { get { return doom64presets && menusform.ShowTextured && (menusform.ColorPreset == ColorPreset.DOOM64); } }

		// styd. The sectors of the textured automap of Doom 64 (AM_DrawSubsectors of the game): not the
		// ones with "Hide in Automap", not the ones with a sky for a floor. The hidden sectors are shown
		// along with the hidden lines.
		private List<Sector> GetTexturedSectors()
		{
			bool showhidden = (menusform.ShowHiddenLines ^ General.Interface.CtrlState);
			List<Sector> sectors = new List<Sector>(General.Map.Map.Sectors.Count);
			foreach(Sector s in General.Map.Map.Sectors)
			{
				if(General.Map.Data.IsSkyFlat(s.FloorTexture)) continue;
				if(!showhidden && s.IsFlagSet(DOOM64_HIDE_SECTOR_FLAG)) continue;
				sectors.Add(s);
			}
			return sectors;
		}

		// styd. In the textured automap, the lines of the secret sectors are drawn when the secrets are shown
		private bool IsSecretOutline(Linedef ld)
		{
			return menusform.ShowSecretSectors &&
				((ld.Front != null && secretsectors.Contains(ld.Front.Sector)) || (ld.Back != null && secretsectors.Contains(ld.Back.Sector)));
		}

		// styd. The triangle that the game draws for a thing on its automap (AM_DrawThings): three points
		// at 32 units of the thing, at its angle, then 225 and 135 degrees further. The game keeps the
		// angle of a thing in steps of 45 degrees.
		private static Vector2D[] GetThingTriangle(Thing t)
		{
			int[] turns = { 0, 225, 135 };
			Vector2D[] points = new Vector2D[3];
			for(int i = 0; i < 3; i++)
			{
				double angle = ((t.AngleDoom / 45) * 45 + turns[i]) * Math.PI / 180.0;
				points[i] = new Vector2D(t.Position.x + (float)(Math.Cos(angle) * DOOM64_THING_SIZE), t.Position.y + (float)(Math.Sin(angle) * DOOM64_THING_SIZE));
			}
			return points;
		}

		private PixelColor DetermineLinedefColor(Linedef ld)
		{
			//mxd
			if(menusform.ShowLocks)
			{
				PixelColor lockcolor = new PixelColor();
				if(GetLockColor(ld, ref lockcolor)) return lockcolor;
			}
			
			//mxd
			if(menusform.ShowSecretSectors &&
			   (ld.Front != null && secretsectors.Contains(ld.Front.Sector) || ld.Back != null && secretsectors.Contains(ld.Back.Sector)))
				return ColorSecret;

			// styd. The automap of Doom 64 (AM_DrawLine of the game): a line shown as one-sided, then a line
			// with an action that is not hidden, then a line with one side. Nothing depends on the heights.
			if(menusform.ColorPreset == ColorPreset.DOOM64)
			{
				if(ld.IsFlagSet(BuilderPlug.Me.HiddenFlag)) return ColorHiddenFlag;
				if(ld.IsFlagSet(BuilderPlug.Me.SecretFlag)) return ColorSingleSided;
				if(((ld.Action != 0) || (ld.Activate != 0)) && !ld.IsFlagSet(DOOM64_HIDE_SPECIAL_FLAG)) return ColorSpecial;
				if(!ld.IsFlagSet(General.Map.Config.DoubleSidedFlag)) return ColorSingleSided;
				return ColorTwoSided;
			}

			if(ld.IsFlagSet(BuilderPlug.Me.HiddenFlag)) return ColorHiddenFlag;
			if(ld.Back == null || ld.Front == null || ld.IsFlagSet(BuilderPlug.Me.SecretFlag)) return ColorSingleSided;
			if(ld.Front.Sector.FloorHeight != ld.Back.Sector.FloorHeight) return ColorFloorDiff;
			if(ld.Front.Sector.CeilHeight != ld.Back.Sector.CeilHeight) return ColorCeilDiff;

			if(ld.Front.Sector.CeilHeight == ld.Back.Sector.CeilHeight && ld.Front.Sector.FloorHeight == ld.Back.Sector.FloorHeight)
				return ColorMatchingHeight;

			if(menusform.ShowHiddenLines ^ General.Interface.CtrlState) return ColorInvisible; 

			return new PixelColor(255, 255, 255, 255);
		}

		private bool LinedefIsValid(Linedef ld)
		{
			if(menusform.ShowHiddenLines ^ General.Interface.CtrlState) return true;
			if(menusform.ColorPreset == ColorPreset.DOOM64) return !ld.IsFlagSet(BuilderPlug.Me.HiddenFlag); // styd. Every line that is not hidden
			if(ld.IsFlagSet(BuilderPlug.Me.HiddenFlag)) return false;
			if(ld.Back == null || ld.Front == null || ld.IsFlagSet(BuilderPlug.Me.SecretFlag)) return true;
			if(ld.Back != null && ld.Front != null && (ld.Front.Sector.FloorHeight != ld.Back.Sector.FloorHeight || ld.Front.Sector.CeilHeight != ld.Back.Sector.CeilHeight)) return true;

			return false;
		}

		//mxd
		private static bool SectorIsSecret(Sector s)
		{
			// styd. A Doom 64 sector is secret by its flag (MS_SECRET of the game)
			if(General.Map.DOOM64) return s.IsFlagSet("32");

			SectorEffectData data = General.Map.Config.GetSectorEffectData(s.Effect);

			if(General.Map.DOOM)
			{
				// Sector is secret when it's Special is 9 or it has generalized flag 128
				if(data.Effect == 9 || data.GeneralizedBits.Contains(128)) return true;
			}
			else
			{
				//Hexen/UDMF: sector is secret when it has generalized flag 1024
				if(data.GeneralizedBits.Contains(1024)) return true;
			}

			return false;
		}

		//mxd
		private static bool GetLockColor(Linedef l, ref PixelColor lockcolor)
		{
			int locknum = 0;

			// Check locknumber property
			if(General.Map.UDMF)
			{
				locknum = UniFields.GetInteger(l.Fields, "locknumber");
			}

			// Check action
			if(locknum == 0 && l.Action != 0 && General.Map.Data.LockableActions.ContainsKey(l.Action))
			{
				locknum = l.Args[General.Map.Data.LockableActions[l.Action]];
			}

			if(locknum != 0 && General.Map.Data.LockColors.ContainsKey(locknum))
			{
				lockcolor = General.Map.Data.LockColors[locknum];
				return true;
			}

			// No dice
			return false;
		}

		//mxd
		private void ApplyColorPreset(ColorPreset preset)
		{
			switch(preset)
			{
				case ColorPreset.DOOM:
					ColorSingleSided = new PixelColor(255, 252, 0, 0);
					ColorSecret = new PixelColor(255, 255, 0, 255);
					ColorFloorDiff = new PixelColor(255, 188, 120, 72);
					ColorCeilDiff = new PixelColor(255, 252, 252, 0);
					ColorHiddenFlag = new PixelColor(255, 192, 192, 192);
					ColorInvisible = new PixelColor(255, 192, 192, 192);
					ColorMatchingHeight = new PixelColor(255, 108, 108, 108);
					ColorBackground = new PixelColor(255, 0, 0, 0);
					break;

				case ColorPreset.HEXEN:
					ColorSingleSided = new PixelColor(255, 89, 64, 27);
					ColorSecret = new PixelColor(255, 255, 0, 255);
					ColorFloorDiff = new PixelColor(255, 208, 176, 133);
					ColorCeilDiff = new PixelColor(255, 103, 59, 31);
					ColorHiddenFlag = new PixelColor(255, 192, 192, 192);
					ColorInvisible = new PixelColor(255, 108, 108, 108);
					ColorMatchingHeight = new PixelColor(255, 108, 108, 108);
					ColorBackground = new PixelColor(255, 163, 129, 84);
					break;

				case ColorPreset.STRIFE:
					ColorSingleSided = new PixelColor(255, 199, 195, 195);
					ColorSecret = new PixelColor(255, 255, 0, 255);
					ColorFloorDiff = new PixelColor(255, 55, 59, 91);
					ColorCeilDiff = new PixelColor(255, 108, 108, 108);
					ColorHiddenFlag = new PixelColor(255, 0, 87, 130);
					ColorInvisible = new PixelColor(255, 192, 192, 192);
					ColorMatchingHeight = new PixelColor(255, 112, 112, 160);
					ColorBackground = new PixelColor(255, 0, 0, 0);
					break;

				// styd. The colors of the game (am_main.c), with the display brightness of the preferences as
				// the remaster shows its automap. What only the editor shows keeps its color.
				case ColorPreset.DOOM64:
					ColorSingleSided = Doom64Color(164, 0, 0);
					ColorSpecial = Doom64Color(204, 204, 0);
					ColorTwoSided = Doom64Color(138, 92, 48);
					ColorSecret = new PixelColor(255, 255, 0, 255);
					ColorHiddenFlag = new PixelColor(255, 192, 192, 192);
					ColorBackground = new PixelColor(255, 0, 0, 0);
					break;
			}
		}

		// styd. A color of the automap of Doom 64 with the display brightness of the preferences: the
		// remaster draws its automap, then makes the whole picture brighter as it does for the 3D view
		private static PixelColor Doom64Color(int r, int g, int b)
		{
			float brightness = General.Clamp(General.Settings.Doom64DisplayBrightness, 0f, 2f);
			foreach(float part in new float[] { Math.Min(brightness, 1f), brightness - 1f })
			{
				int gray = (int)(part * 255f);
				if(gray <= 0) continue;
				double factor = 1.0 + gray / 255.0;
				r = Math.Min(255, (int)Math.Round(r * factor));
				g = Math.Min(255, (int)Math.Round(g * factor));
				b = Math.Min(255, (int)Math.Round(b * factor));
			}
			return new PixelColor(255, (byte)r, (byte)g, (byte)b);
		}

		#endregion
		
		#region ================== Events

		public override void OnHelp()
		{
			General.ShowHelp("/gzdb/features/classic_modes/mode_automap.html");
		}

		// Cancel mode
		public override void OnCancel()
		{
			base.OnCancel();

			// Return to this mode
			General.Editing.ChangeMode(new AutomapMode());
		}

		// Mode engages
		public override void OnEngage()
		{
			base.OnEngage();
			renderer.DrawMapCenter = false; //mxd

			// Automap presentation without the surfaces
			automappresentation = new CustomPresentation();
			automappresentation.AddLayer(new PresentLayer(RendererLayer.Overlay, BlendingMode.Mask));
			automappresentation.AddLayer(new PresentLayer(RendererLayer.Grid, BlendingMode.Mask));
			automappresentation.AddLayer(new PresentLayer(RendererLayer.Geometry, BlendingMode.Alpha, 1f, true));
			renderer.SetPresentation(automappresentation);

			UpdateValidLinedefs();
			UpdateSecretSectors(); //mxd

			//mxd. Show UI
			menusform.Register();
		}
		
		// Mode disengages
		public override void OnDisengage()
		{
			base.OnDisengage();

			//mxd. Store settings
			General.Settings.WritePluginSetting("automapmode.showhiddenlines", menusform.ShowHiddenLines);
			General.Settings.WritePluginSetting("automapmode.showsecretsectors", menusform.ShowSecretSectors);
			General.Settings.WritePluginSetting("automapmode.showlocks", menusform.ShowLocks);
			if(doom64presets) General.Settings.WritePluginSetting("automapmode.doom64colorpreset", (int)menusform.ColorPreset); // styd
			else General.Settings.WritePluginSetting("automapmode.colorpreset", (int)menusform.ColorPreset);
			if(doom64presets) General.Settings.WritePluginSetting("automapmode.doom64textured", menusform.ShowTextured); // styd

			//mxd. Hide UI
			menusform.Unregister();

			// Hide highlight info
			General.Interface.HideInfo();
		}

		//mxd
		public override void OnUndoEnd()
		{
			UpdateValidLinedefs();
			UpdateSecretSectors();
			UpdateHighlightedSector(); // styd

			base.OnUndoEnd();
		}

		//mxd
		public override void OnRedoEnd()
		{
			UpdateValidLinedefs();
			UpdateSecretSectors();
			UpdateHighlightedSector(); // styd

			base.OnRedoEnd();
		}

		// This redraws the display
		public override void OnRedrawDisplay()
		{
			// styd. The display brightness of the preferences may have changed
			if(menusform.ColorPreset == ColorPreset.DOOM64) ApplyColorPreset(ColorPreset.DOOM64);
			bool textured = Textured;

			renderer.RedrawSurface();
			
			// Render lines
			if(renderer.StartPlotter(true))
			{
				// styd. When the textured automap shows the hidden sectors, their lines are gray as the hidden
				// lines are
				if(textured && (menusform.ShowHiddenLines ^ General.Interface.CtrlState))
				{
					foreach(Sector s in General.Map.Map.Sectors)
					{
						if(!s.IsFlagSet(DOOM64_HIDE_SECTOR_FLAG)) continue;
						foreach(Sidedef sd in s.Sidedefs) renderer.PlotLine(sd.Line.Start.Position, sd.Line.End.Position, ColorHiddenFlag, LINE_LENGTH_SCALER);
					}
				}

				foreach(Linedef ld in General.Map.Map.Linedefs)
				{
					if(textured && !IsSecretOutline(ld)) continue; // styd. The textured automap has no lines
					if(LinedefIsValid(ld))
						renderer.PlotLine(ld.Start.Position, ld.End.Position, DetermineLinedefColor(ld), LINE_LENGTH_SCALER);
				}

				// styd. The things that count as a secret are shown with the secrets, as the game draws a
				// thing on its automap: the outline of a triangle here, a filled one on the textured automap
				if(!textured && menusform.ShowSecretSectors)
				{
					foreach(Thing t in secretthings)
					{
						if(t.IsDisposed) continue;
						Vector2D[] points = GetThingTriangle(t);
						for(int i = 0; i < 3; i++) renderer.PlotLine(points[i], points[(i + 1) % 3], ColorSecret, LINE_LENGTH_SCALER);
					}
				}

				if((highlighted != null) && !highlighted.IsDisposed && LinedefIsValid(highlighted))
				{
					renderer.PlotLine(highlighted.Start.Position, highlighted.End.Position, General.Colors.InfoLine, LINE_LENGTH_SCALER);
				}

				// styd. The lines of the highlighted sector of the textured automap, also when it is hidden
				if(textured && (highlightedsector != null) && !highlightedsector.IsDisposed)
				{
					foreach(Sidedef sd in highlightedsector.Sidedefs)
						renderer.PlotLine(sd.Line.Start.Position, sd.Line.End.Position, General.Colors.InfoLine, LINE_LENGTH_SCALER);
				}

				renderer.Finish();
			}

			//mxd. Render background
			if(renderer.StartOverlay(true))
			{
				RectangleF screenrect = new RectangleF(0, 0, General.Interface.Display.Width, General.Interface.Display.Height);
				renderer.RenderRectangleFilled(screenrect, ColorBackground, false);
				if(textured) renderer.RenderDoom64AutomapFloors(GetTexturedSectors()); // styd
				if(textured && menusform.ShowSecretSectors)
				{
					// styd. Over the floors, in the color of the secrets as it is
					foreach(Thing t in secretthings)
					{
						if(t.IsDisposed) continue;
						Vector2D[] points = GetThingTriangle(t);
						FlatVertex[] triangle = new FlatVertex[3];
						for(int i = 0; i < 3; i++)
						{
							triangle[i].x = points[i].x;
							triangle[i].y = points[i].y;
							triangle[i].z = 1.0f;
							triangle[i].c = ColorSecret.ToInt();
						}
						renderer.RenderHighlight(triangle, ColorSecret.ToInt());
					}
				}
				renderer.Finish();
			}

			renderer.Present();
		}

		protected override void OnSelectEnd()
		{
			// Item highlighted?
			if((highlighted != null) && !highlighted.IsDisposed)
			{
				General.Map.UndoRedo.CreateUndo("Toggle \"Shown as 1-sided on automap\" linedef flag");

				// Toggle flag
				highlighted.SetFlag(BuilderPlug.Me.SecretFlag, !highlighted.IsFlagSet(BuilderPlug.Me.SecretFlag));
				UpdateValidLinedefs();
			}

			base.OnSelectEnd();
		}
		
		protected override void OnEditEnd()
		{
			// styd. In the textured automap of Doom 64, the highlighted sector is hidden or shown again
			// ("Hide in Automap": the game does not draw it there, AM_DrawSubsectors)
			if(Textured && (highlightedsector != null) && !highlightedsector.IsDisposed)
			{
				General.Map.UndoRedo.CreateUndo("Toggle \"Hide in Automap\" sector flag");
				highlightedsector.SetFlag(DOOM64_HIDE_SECTOR_FLAG, !highlightedsector.IsFlagSet(DOOM64_HIDE_SECTOR_FLAG));
				UpdateHighlightedSector(); // Hidden, it is not highlighted any more when the hidden sectors are not shown
				General.Interface.RedrawDisplay();
			}

			// Item highlighted?
			if((highlighted != null) && !highlighted.IsDisposed)
			{
				General.Map.UndoRedo.CreateUndo("Toggle \"Not shown on automap\" linedef flag");

				// Toggle flag
				highlighted.SetFlag(BuilderPlug.Me.HiddenFlag, !highlighted.IsFlagSet(BuilderPlug.Me.HiddenFlag));
				UpdateValidLinedefs();
				General.Interface.RedrawDisplay();
			}

			base.OnEditEnd();
		}
		
		// Mouse moves
		public override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);

			// Not holding any buttons?
			if(e.Button == MouseButtons.None)
			{
				// styd. The textured automap of Doom 64 highlights the sector under the mouse, no line
				if(Textured)
				{
					Sector s = General.Map.Map.GetSectorByCoordinates(mousemappos);
					if(!SectorIsValid(s)) s = null;
					if(s != highlightedsector) HighlightSector(s);
					return;
				}

				// Find the nearest linedef within highlight range
				Linedef l = MapSet.NearestLinedefRange(validlinedefs, mousemappos, BuilderPlug.Me.HighlightRange / renderer.Scale);

				// Highlight if not the same
				if(l != highlighted) Highlight(l);
			}
		}

		// Mouse leaves
		public override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);

			highlightedsector = null; // styd
			// Highlight nothing
			Highlight(null);
		}

		public override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);

			if(e.Control)
			{
				UpdateValidLinedefs();
				UpdateHighlightedSector(); // styd
				General.Interface.RedrawDisplay();
			}
		}

		public override void OnKeyUp(KeyEventArgs e)
		{
			base.OnKeyUp(e);

			if(!e.Control)
			{
				UpdateValidLinedefs();
				UpdateHighlightedSector(); // styd
				General.Interface.RedrawDisplay();
			}
		}

		#endregion
	}
}
