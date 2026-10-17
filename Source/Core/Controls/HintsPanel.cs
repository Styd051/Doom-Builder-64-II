using System;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Windows;

namespace CodeImp.DoomBuilder.Controls
{
	internal partial class HintsPanel : UserControl
	{
		public HintsPanel() 
		{
			InitializeComponent();
			hints.Clear();
			hints.ForeColorChanged += delegate { SetTextColor(); }; // styd
		}

		//hints should be in rtf markup!
		internal void SetHints(string hintsText) 
		{
			hints.Clear();
			hints.SelectedRtf = hintsText;
			if(DarkTheme.Active) SetTextColor(); // styd
		}

		// styd. The hints have no color of their own: with the dark theme they take the color of the text
		private void SetTextColor()
		{
			hints.SelectAll();
			hints.SelectionColor = hints.ForeColor;
			hints.Select(0, 0);
		}

		internal void ClearHints() 
		{
			hints.Clear();
		}

		// Fight TextBoxes habit of not releasing the focus by using a carefully placed label
		private void hints_Enter(object sender, EventArgs e) 
		{
			label1.Focus();
		}
	}
}
