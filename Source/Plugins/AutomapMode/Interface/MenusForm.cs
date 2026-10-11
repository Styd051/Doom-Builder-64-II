using System;
using System.Windows.Forms;

namespace CodeImp.DoomBuilder.AutomapMode
{
	public partial class MenusForm : UserControl
	{
		public event EventHandler OnShowHiddenLinesChanged;
		public event EventHandler OnShowSecretSectorsChanged;
		public event EventHandler OnShowLocksChanged;
		internal event EventHandler OnColorPresetChanged;
		public event EventHandler OnShowTexturedChanged; // styd

		public bool ShowHiddenLines { get { return showhiddenlines.Checked; } set { showhiddenlines.Checked = value; } }
		public bool ShowSecretSectors { get { return showsecretsectors.Checked; } set { showsecretsectors.Checked = value; } }
		public bool ShowLocks { get { return showlocks.Checked; } set { showlocks.Checked = value; } }
		internal AutomapMode.ColorPreset ColorPreset { get { return (AutomapMode.ColorPreset)colorpreset.SelectedIndex; } set { colorpreset.SelectedIndex = (int)value; } }
		public bool ShowTextured { get { return showtextured.Checked; } set { showtextured.Checked = value; } } // styd
		
		public MenusForm()
		{
			InitializeComponent();
		}

		// styd. The preset of Doom 64 is only offered for a Doom 64 map
		internal void AddDoom64Preset()
		{
			colorpreset.Items.Add("Doom 64");
		}

		public void Register()
		{
			General.Interface.BeginToolbarUpdate(); //mxd
			General.Interface.AddButton(showhiddenlines);
			General.Interface.AddButton(showsecretsectors);
			if(!General.Map.DOOM && !General.Map.DOOM64) General.Interface.AddButton(showlocks);
			General.Interface.AddButton(colorpresetseparator);
			General.Interface.AddButton(colorpresetlabel);
			General.Interface.AddButton(colorpreset);
			if(General.Map.DOOM64) General.Interface.AddButton(showtextured); // styd
			General.Interface.EndToolbarUpdate(); //mxd
		}

		public void Unregister()
		{
			General.Interface.BeginToolbarUpdate(); //mxd
			General.Interface.RemoveButton(showtextured); // styd
			General.Interface.RemoveButton(colorpreset);
			General.Interface.RemoveButton(colorpresetlabel);
			General.Interface.RemoveButton(colorpresetseparator);
			General.Interface.RemoveButton(showlocks);
			General.Interface.RemoveButton(showsecretsectors);
			General.Interface.RemoveButton(showhiddenlines);
			General.Interface.EndToolbarUpdate(); //mxd
		}

		private void showhiddenlines_CheckedChanged(object sender, EventArgs e)
		{
			if(OnShowHiddenLinesChanged != null) OnShowHiddenLinesChanged(showhiddenlines.Checked, EventArgs.Empty);
		}

		private void showsecretsectors_CheckedChanged(object sender, EventArgs e)
		{
			if(OnShowSecretSectorsChanged != null) OnShowSecretSectorsChanged(showsecretsectors.Checked, EventArgs.Empty);
		}

		private void showlocks_CheckedChanged(object sender, EventArgs e)
		{
			if(OnShowLocksChanged != null) OnShowLocksChanged(showlocks.Checked, EventArgs.Empty);
		}

		private void colorpreset_SelectedIndexChanged(object sender, EventArgs e)
		{
			showtextured.Enabled = (colorpreset.SelectedIndex == (int)AutomapMode.ColorPreset.DOOM64); // styd. The textured automap is the one of Doom 64
			if(OnColorPresetChanged != null) OnColorPresetChanged(colorpreset.SelectedIndex, EventArgs.Empty);
		}

		// styd. The textured automap of Doom 64
		private void showtextured_CheckedChanged(object sender, EventArgs e)
		{
			if(OnShowTexturedChanged != null) OnShowTexturedChanged(showtextured.Checked, EventArgs.Empty);
		}
	}
}
