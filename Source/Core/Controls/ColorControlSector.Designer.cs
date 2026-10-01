namespace CodeImp.DoomBuilder.Controls
{
	partial class ColorControlSector
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if(disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Component Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.label = new System.Windows.Forms.Label();
			this.indexbox = new CodeImp.DoomBuilder.Controls.NumericTextbox();
			this.panel = new System.Windows.Forms.Panel();
			this.hexbox = new System.Windows.Forms.TextBox();
			this.tagbox = new CodeImp.DoomBuilder.Controls.NumericTextbox();
			this.newtag = new System.Windows.Forms.Button();
			this.brighter = new System.Windows.Forms.Button();
			this.darker = new System.Windows.Forms.Button();
			this.dialog = new System.Windows.Forms.ColorDialog();
			this.SuspendLayout();
			//
			// label
			//
			this.label.Location = new System.Drawing.Point(0, 0);
			this.label.Name = "label";
			this.label.Size = new System.Drawing.Size(84, 24);
			this.label.TabIndex = 0;
			this.label.Text = "Color name:";
			this.label.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			//
			// indexbox
			//
			this.indexbox.AllowDecimal = false;
			this.indexbox.AllowExpressions = false;
			this.indexbox.AllowNegative = false;
			this.indexbox.AllowRelative = false;
			this.indexbox.ImeMode = System.Windows.Forms.ImeMode.Off;
			this.indexbox.Location = new System.Drawing.Point(90, 2);
			this.indexbox.Name = "indexbox";
			this.indexbox.Size = new System.Drawing.Size(44, 20);
			this.indexbox.TabIndex = 1;
			//
			// panel
			//
			this.panel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(128)))), ((int)(((byte)(128)))));
			this.panel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel.Cursor = System.Windows.Forms.Cursors.Hand;
			this.panel.Location = new System.Drawing.Point(142, 2);
			this.panel.Name = "panel";
			this.panel.Size = new System.Drawing.Size(34, 20);
			this.panel.TabIndex = 2;
			this.panel.Click += new System.EventHandler(this.panel_Click);
			//
			// hexbox
			//
			this.hexbox.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
			this.hexbox.Location = new System.Drawing.Point(184, 2);
			this.hexbox.MaxLength = 7;
			this.hexbox.Name = "hexbox";
			this.hexbox.Size = new System.Drawing.Size(58, 20);
			this.hexbox.TabIndex = 3;
			this.hexbox.TextChanged += new System.EventHandler(this.hexbox_TextChanged);
			this.hexbox.Leave += new System.EventHandler(this.hexbox_Leave);
			//
			// tagbox
			//
			this.tagbox.AllowDecimal = false;
			this.tagbox.AllowExpressions = false;
			this.tagbox.AllowNegative = false;
			this.tagbox.AllowRelative = true;
			this.tagbox.ImeMode = System.Windows.Forms.ImeMode.Off;
			this.tagbox.Location = new System.Drawing.Point(252, 2);
			this.tagbox.Name = "tagbox";
			this.tagbox.Size = new System.Drawing.Size(48, 20);
			this.tagbox.TabIndex = 4;
			//
			// newtag
			//
			this.newtag.Location = new System.Drawing.Point(303, 1);
			this.newtag.Name = "newtag";
			this.newtag.Size = new System.Drawing.Size(40, 22);
			this.newtag.TabIndex = 5;
			this.newtag.Text = "New";
			this.newtag.UseVisualStyleBackColor = true;
			this.newtag.Click += new System.EventHandler(this.newtag_Click);
			//
			// brighter
			//
			this.brighter.Location = new System.Drawing.Point(352, 1);
			this.brighter.Name = "brighter";
			this.brighter.Size = new System.Drawing.Size(24, 22);
			this.brighter.TabIndex = 6;
			this.brighter.Text = "+";
			this.brighter.UseVisualStyleBackColor = true;
			this.brighter.Click += new System.EventHandler(this.brighter_Click);
			//
			// darker
			//
			this.darker.Location = new System.Drawing.Point(378, 1);
			this.darker.Name = "darker";
			this.darker.Size = new System.Drawing.Size(24, 22);
			this.darker.TabIndex = 7;
			this.darker.Text = "-";
			this.darker.UseVisualStyleBackColor = true;
			this.darker.Click += new System.EventHandler(this.darker_Click);
			//
			// dialog
			//
			this.dialog.FullOpen = true;
			//
			// ColorControlSector
			//
			this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
			this.BackColor = System.Drawing.Color.Transparent;
			this.Controls.Add(this.darker);
			this.Controls.Add(this.brighter);
			this.Controls.Add(this.newtag);
			this.Controls.Add(this.tagbox);
			this.Controls.Add(this.hexbox);
			this.Controls.Add(this.panel);
			this.Controls.Add(this.indexbox);
			this.Controls.Add(this.label);
			this.Name = "ColorControlSector";
			this.Size = new System.Drawing.Size(404, 24);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label label;
		private CodeImp.DoomBuilder.Controls.NumericTextbox indexbox;
		private System.Windows.Forms.Panel panel;
		private System.Windows.Forms.TextBox hexbox;
		private CodeImp.DoomBuilder.Controls.NumericTextbox tagbox;
		private System.Windows.Forms.Button newtag;
		private System.Windows.Forms.Button brighter;
		private System.Windows.Forms.Button darker;
		private System.Windows.Forms.ColorDialog dialog;
	}
}
