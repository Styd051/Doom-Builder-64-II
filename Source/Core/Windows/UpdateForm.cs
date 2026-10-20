using System;
using System.Windows.Forms;

namespace CodeImp.DoomBuilder.Windows
{
	public partial class UpdateForm : DelayedForm
	{
		public bool IgnoreThisUpdate { get { return ignorethisupdate.Checked; } }
		private readonly UpdateInfo info; // styd: a release of Doom Builder 64 II, not a revision of GZDoom Builder

		internal UpdateForm(UpdateInfo info)
		{
			InitializeComponent();
			this.info = info;
			Setup();
		}

		private void Setup()
		{
			string version = UpdateChecker.VersionText(info.Version);
			this.Text = this.Text.Replace("[ver]", version);
			this.label.Text = label.Text.Replace("[ver]", version);
			this.changelog.SelectedRtf = info.Notes;
		}

		private void UpdateForm_Shown(object sender, EventArgs e)
		{
			this.changelog.Focus();
		}

		private void downloadupdate_Click(object sender, EventArgs e)
		{
			this.DialogResult = DialogResult.OK;

			// styd: the updater runs from a folder of its own
			UpdateChecker.StartUpdater(info);

			this.Close();
		}

		private void cancel_Click(object sender, EventArgs e)
		{
			this.DialogResult = DialogResult.Cancel;
			this.Close();
		}
	}
}
