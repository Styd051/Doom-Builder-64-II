namespace CodeImp.DoomBuilder.Windows
{
	partial class SectorEditForm
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

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.Label label1;
            System.Windows.Forms.Label label3;
            System.Windows.Forms.Label label8;
            System.Windows.Forms.GroupBox groupfloorceiling;
            System.Windows.Forms.Label label7;
            System.Windows.Forms.Label label5;
            System.Windows.Forms.Label label6;
            System.Windows.Forms.Label label2;
            System.Windows.Forms.Label label4;
            this.browseeffect = new System.Windows.Forms.Button();
            this.effect = new CodeImp.DoomBuilder.Controls.ActionSelectorControl();
            this.tagSelector = new CodeImp.DoomBuilder.Controls.TagSelector();
            this.heightoffset = new CodeImp.DoomBuilder.Controls.ButtonsNumericTextbox();
            this.brightness = new CodeImp.DoomBuilder.Controls.ButtonsNumericTextbox();
            this.ceilingheight = new CodeImp.DoomBuilder.Controls.ButtonsNumericTextbox();
            this.sectorheightlabel = new System.Windows.Forms.Label();
            this.sectorheight = new System.Windows.Forms.Label();
            this.floortex = new CodeImp.DoomBuilder.Controls.FlatSelectorControl();
            this.floorheight = new CodeImp.DoomBuilder.Controls.ButtonsNumericTextbox();
            this.ceilingtex = new CodeImp.DoomBuilder.Controls.FlatSelectorControl();
            this.cancel = new System.Windows.Forms.Button();
            this.apply = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.tooltip = new System.Windows.Forms.ToolTip(this.components);
            this.groupflags = new System.Windows.Forms.GroupBox();
            this.flags = new CodeImp.DoomBuilder.Controls.CheckboxArrayControl();
            this.grouplights = new System.Windows.Forms.GroupBox();
            this.lightindexlabel = new System.Windows.Forms.Label();
            this.lightcolorlabel = new System.Windows.Forms.Label();
            this.lighthexlabel = new System.Windows.Forms.Label();
            this.lighttaglabel = new System.Windows.Forms.Label();
            this.lightintensitylabel = new System.Windows.Forms.Label();
            this.ceilingcolor = new CodeImp.DoomBuilder.Controls.ColorControlSector();
            this.topcolor = new CodeImp.DoomBuilder.Controls.ColorControlSector();
            this.thingcolor = new CodeImp.DoomBuilder.Controls.ColorControlSector();
            this.lowercolor = new CodeImp.DoomBuilder.Controls.ColorControlSector();
            this.floorcolor = new CodeImp.DoomBuilder.Controls.ColorControlSector();
            label1 = new System.Windows.Forms.Label();
            label3 = new System.Windows.Forms.Label();
            this.groupeffect = new System.Windows.Forms.GroupBox();
            label8 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            groupfloorceiling = new System.Windows.Forms.GroupBox();
            label7 = new System.Windows.Forms.Label();
            label5 = new System.Windows.Forms.Label();
            label6 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            label4 = new System.Windows.Forms.Label();
            groupeffect.SuspendLayout();
            groupfloorceiling.SuspendLayout();
            this.panel1.SuspendLayout();
            this.groupflags.SuspendLayout();
            this.grouplights.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            label1.Location = new System.Drawing.Point(271, 18);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(83, 16);
            label1.TabIndex = 15;
            label1.Text = "Floor";
            label1.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // label3
            // 
            label3.Location = new System.Drawing.Point(363, 18);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(83, 16);
            label3.TabIndex = 14;
            label3.Text = "Ceiling";
            label3.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // groupeffect
            // 
            groupeffect.BackColor = System.Drawing.Color.Transparent;
            groupeffect.Controls.Add(this.browseeffect);
            groupeffect.Controls.Add(this.effect);
            groupeffect.Controls.Add(label8);
            groupeffect.Controls.Add(this.tagSelector);
            groupeffect.Location = new System.Drawing.Point(3, 195);
            groupeffect.Name = "groupeffect";
            groupeffect.Size = new System.Drawing.Size(436, 92);
            groupeffect.TabIndex = 1;
            groupeffect.TabStop = false;
            groupeffect.Text = "Effect and identification";
            // 
            // browseeffect
            // 
            this.browseeffect.Image = global::CodeImp.DoomBuilder.Properties.Resources.List;
            this.browseeffect.Location = new System.Drawing.Point(402, 26);
            this.browseeffect.Name = "browseeffect";
            this.browseeffect.Size = new System.Drawing.Size(28, 25);
            this.browseeffect.TabIndex = 1;
            this.browseeffect.Text = " ";
            this.browseeffect.UseVisualStyleBackColor = true;
            this.browseeffect.Click += new System.EventHandler(this.browseeffect_Click);
            // 
            // effect
            // 
            this.effect.BackColor = System.Drawing.Color.Transparent;
            this.effect.Cursor = System.Windows.Forms.Cursors.Default;
            this.effect.Empty = false;
            this.effect.GeneralizedCategories = null;
            this.effect.GeneralizedOptions = null;
            this.effect.Location = new System.Drawing.Point(61, 28);
            this.effect.Name = "effect";
            this.effect.Size = new System.Drawing.Size(335, 21);
            this.effect.TabIndex = 0;
            this.effect.Value = 402;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new System.Drawing.Point(12, 31);
            label8.Name = "label8";
            label8.Size = new System.Drawing.Size(45, 13);
            label8.TabIndex = 0;
            label8.Text = "Special:";
            // 
            // tagSelector
            // 
            this.tagSelector.Location = new System.Drawing.Point(19, 52);
            this.tagSelector.Name = "tagSelector";
            this.tagSelector.Size = new System.Drawing.Size(414, 34);
            this.tagSelector.TabIndex = 0;
            // 
            // label9
            // 
            label9.Location = new System.Drawing.Point(16, 159);
            label9.Name = "label9";
            label9.Size = new System.Drawing.Size(78, 14);
            label9.TabIndex = 2;
            label9.Text = "Brightness:";
            label9.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // groupfloorceiling
            // 
            groupfloorceiling.BackColor = System.Drawing.Color.Transparent;
            groupfloorceiling.Controls.Add(label7);
            groupfloorceiling.Controls.Add(label5);
            groupfloorceiling.Controls.Add(label6);
            groupfloorceiling.Controls.Add(this.heightoffset);
            groupfloorceiling.Controls.Add(this.brightness);
            groupfloorceiling.Controls.Add(this.ceilingheight);
            groupfloorceiling.Controls.Add(label9);
            groupfloorceiling.Controls.Add(label2);
            groupfloorceiling.Controls.Add(this.sectorheightlabel);
            groupfloorceiling.Controls.Add(label4);
            groupfloorceiling.Controls.Add(this.sectorheight);
            groupfloorceiling.Controls.Add(this.floortex);
            groupfloorceiling.Controls.Add(this.floorheight);
            groupfloorceiling.Controls.Add(this.ceilingtex);
            groupfloorceiling.Location = new System.Drawing.Point(3, 3);
            groupfloorceiling.Name = "groupfloorceiling";
            groupfloorceiling.Size = new System.Drawing.Size(436, 186);
            groupfloorceiling.TabIndex = 0;
            groupfloorceiling.TabStop = false;
            groupfloorceiling.Text = "Floor and ceiling ";
            // 
            // label7
            // 
            label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            label7.ForeColor = System.Drawing.SystemColors.HotTrack;
            label7.Location = new System.Drawing.Point(16, 100);
            label7.Name = "label7";
            label7.Size = new System.Drawing.Size(78, 14);
            label7.TabIndex = 25;
            label7.Text = "Height offset:";
            label7.TextAlign = System.Drawing.ContentAlignment.TopRight;
            this.tooltip.SetToolTip(label7, "Changes floor and ceiling height by given value.\r\nUse \"++\" to raise by sector hei" +
        "ght.\r\nUse \"--\" to lower by sector height.");
            // 
            // label5
            // 
            label5.Location = new System.Drawing.Point(16, 70);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(78, 14);
            label5.TabIndex = 17;
            label5.Text = "Floor height:";
            label5.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label6
            // 
            label6.Location = new System.Drawing.Point(16, 40);
            label6.Name = "label6";
            label6.Size = new System.Drawing.Size(78, 14);
            label6.TabIndex = 19;
            label6.Text = "Ceiling height:";
            label6.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // heightoffset
            // 
            this.heightoffset.AllowDecimal = false;
            this.heightoffset.AllowExpressions = true;
            this.heightoffset.AllowNegative = true;
            this.heightoffset.AllowRelative = true;
            this.heightoffset.ButtonStep = 8;
            this.heightoffset.ButtonStepBig = 16F;
            this.heightoffset.ButtonStepFloat = 1F;
            this.heightoffset.ButtonStepSmall = 1F;
            this.heightoffset.ButtonStepsUseModifierKeys = true;
            this.heightoffset.ButtonStepsWrapAround = false;
            this.heightoffset.Location = new System.Drawing.Point(99, 95);
            this.heightoffset.Name = "heightoffset";
            this.heightoffset.Size = new System.Drawing.Size(88, 24);
            this.heightoffset.StepValues = null;
            this.heightoffset.TabIndex = 26;
            this.heightoffset.WhenTextChanged += new System.EventHandler(this.heightoffset_WhenTextChanged);
            // 
            // brightness
            // 
            this.brightness.AllowDecimal = false;
            this.brightness.AllowExpressions = false;
            this.brightness.AllowNegative = true;
            this.brightness.AllowRelative = true;
            this.brightness.ButtonStep = 8;
            this.brightness.ButtonStepBig = 16F;
            this.brightness.ButtonStepFloat = 1F;
            this.brightness.ButtonStepSmall = 1F;
            this.brightness.ButtonStepsUseModifierKeys = true;
            this.brightness.ButtonStepsWrapAround = false;
            this.brightness.Location = new System.Drawing.Point(99, 154);
            this.brightness.Name = "brightness";
            this.brightness.Size = new System.Drawing.Size(73, 24);
            this.brightness.StepValues = null;
            this.brightness.TabIndex = 24;
            this.brightness.WhenTextChanged += new System.EventHandler(this.brightness_WhenTextChanged);
            // 
            // ceilingheight
            // 
            this.ceilingheight.AllowDecimal = false;
            this.ceilingheight.AllowExpressions = true;
            this.ceilingheight.AllowNegative = true;
            this.ceilingheight.AllowRelative = true;
            this.ceilingheight.ButtonStep = 8;
            this.ceilingheight.ButtonStepBig = 16F;
            this.ceilingheight.ButtonStepFloat = 1F;
            this.ceilingheight.ButtonStepSmall = 1F;
            this.ceilingheight.ButtonStepsUseModifierKeys = true;
            this.ceilingheight.ButtonStepsWrapAround = false;
            this.ceilingheight.Location = new System.Drawing.Point(99, 35);
            this.ceilingheight.Name = "ceilingheight";
            this.ceilingheight.Size = new System.Drawing.Size(88, 24);
            this.ceilingheight.StepValues = null;
            this.ceilingheight.TabIndex = 22;
            this.ceilingheight.WhenTextChanged += new System.EventHandler(this.ceilingheight_TextChanged);
            // 
            // label2
            // 
            label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            label2.Location = new System.Drawing.Point(196, 16);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(114, 16);
            label2.TabIndex = 15;
            label2.Text = "Floor";
            label2.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // sectorheightlabel
            // 
            this.sectorheightlabel.Location = new System.Drawing.Point(16, 130);
            this.sectorheightlabel.Name = "sectorheightlabel";
            this.sectorheightlabel.Size = new System.Drawing.Size(78, 14);
            this.sectorheightlabel.TabIndex = 20;
            this.sectorheightlabel.Text = "Sector height:";
            this.sectorheightlabel.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label4
            // 
            label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            label4.Location = new System.Drawing.Point(316, 16);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(114, 16);
            label4.TabIndex = 14;
            label4.Text = "Ceiling";
            label4.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // sectorheight
            // 
            this.sectorheight.AutoSize = true;
            this.sectorheight.Location = new System.Drawing.Point(100, 130);
            this.sectorheight.Name = "sectorheight";
            this.sectorheight.Size = new System.Drawing.Size(13, 13);
            this.sectorheight.TabIndex = 21;
            this.sectorheight.Text = "0";
            // 
            // floortex
            // 
            this.floortex.Location = new System.Drawing.Point(196, 35);
            this.floortex.MultipleTextures = false;
            this.floortex.Name = "floortex";
            this.floortex.Size = new System.Drawing.Size(114, 138);
            this.floortex.TabIndex = 2;
            this.floortex.TextureName = "";
            this.floortex.OnValueChanged += new System.EventHandler(this.floortex_OnValueChanged);
            // 
            // floorheight
            // 
            this.floorheight.AllowDecimal = false;
            this.floorheight.AllowExpressions = true;
            this.floorheight.AllowNegative = true;
            this.floorheight.AllowRelative = true;
            this.floorheight.ButtonStep = 8;
            this.floorheight.ButtonStepBig = 16F;
            this.floorheight.ButtonStepFloat = 1F;
            this.floorheight.ButtonStepSmall = 1F;
            this.floorheight.ButtonStepsUseModifierKeys = true;
            this.floorheight.ButtonStepsWrapAround = false;
            this.floorheight.Location = new System.Drawing.Point(99, 65);
            this.floorheight.Name = "floorheight";
            this.floorheight.Size = new System.Drawing.Size(88, 24);
            this.floorheight.StepValues = null;
            this.floorheight.TabIndex = 23;
            this.floorheight.WhenTextChanged += new System.EventHandler(this.floorheight_TextChanged);
            // 
            // ceilingtex
            // 
            this.ceilingtex.Location = new System.Drawing.Point(316, 35);
            this.ceilingtex.MultipleTextures = false;
            this.ceilingtex.Name = "ceilingtex";
            this.ceilingtex.Size = new System.Drawing.Size(114, 138);
            this.ceilingtex.TabIndex = 3;
            this.ceilingtex.TextureName = "";
            this.ceilingtex.OnValueChanged += new System.EventHandler(this.ceilingtex_OnValueChanged);
            // 
            // cancel
            // 
            this.cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancel.Location = new System.Drawing.Point(344, 306);
            this.cancel.Name = "cancel";
            this.cancel.Size = new System.Drawing.Size(112, 25);
            this.cancel.TabIndex = 2;
            this.cancel.Text = "Cancel";
            this.cancel.UseVisualStyleBackColor = true;
            this.cancel.Click += new System.EventHandler(this.cancel_Click);
            // 
            // apply
            // 
            this.apply.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.apply.Location = new System.Drawing.Point(226, 306);
            this.apply.Name = "apply";
            this.apply.Size = new System.Drawing.Size(112, 25);
            this.apply.TabIndex = 1;
            this.apply.Text = "OK";
            this.apply.UseVisualStyleBackColor = true;
            this.apply.Click += new System.EventHandler(this.apply_Click);
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.SystemColors.Control;
            this.panel1.Controls.Add(groupfloorceiling);
            this.panel1.Controls.Add(groupeffect);
            this.panel1.Controls.Add(this.groupflags);
            this.panel1.Controls.Add(this.grouplights);
            this.panel1.Location = new System.Drawing.Point(12, 10);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(443, 290);
            this.panel1.TabIndex = 3;
            // 
            // groupflags
            // 
            this.groupflags.Controls.Add(this.flags);
            this.groupflags.Location = new System.Drawing.Point(3, 293);
            this.groupflags.Name = "groupflags";
            this.groupflags.Size = new System.Drawing.Size(436, 136);
            this.groupflags.TabIndex = 2;
            this.groupflags.TabStop = false;
            this.groupflags.Text = "Flags";
            this.groupflags.Visible = false;
            // 
            // flags
            // 
            this.flags.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.flags.AutoScroll = true;
            this.flags.Columns = 3;
            this.flags.Location = new System.Drawing.Point(15, 19);
            this.flags.Name = "flags";
            this.flags.Size = new System.Drawing.Size(415, 112);
            this.flags.TabIndex = 0;
            this.flags.VerticalSpacing = 1;
            // 
            // grouplights
            // 
            this.grouplights.Controls.Add(this.lightindexlabel);
            this.grouplights.Controls.Add(this.lightcolorlabel);
            this.grouplights.Controls.Add(this.lighthexlabel);
            this.grouplights.Controls.Add(this.lighttaglabel);
            this.grouplights.Controls.Add(this.lightintensitylabel);
            this.grouplights.Controls.Add(this.ceilingcolor);
            this.grouplights.Controls.Add(this.topcolor);
            this.grouplights.Controls.Add(this.thingcolor);
            this.grouplights.Controls.Add(this.lowercolor);
            this.grouplights.Controls.Add(this.floorcolor);
            this.grouplights.Location = new System.Drawing.Point(3, 435);
            this.grouplights.Name = "grouplights";
            this.grouplights.Size = new System.Drawing.Size(436, 174);
            this.grouplights.TabIndex = 3;
            this.grouplights.TabStop = false;
            this.grouplights.Text = "Colored lighting";
            this.grouplights.Visible = false;
            // 
            // lightindexlabel
            // 
            this.lightindexlabel.AutoSize = true;
            this.lightindexlabel.Location = new System.Drawing.Point(102, 20);
            this.lightindexlabel.Name = "lightindexlabel";
            this.lightindexlabel.Size = new System.Drawing.Size(33, 13);
            this.lightindexlabel.TabIndex = 0;
            this.lightindexlabel.Text = "Index";
            // 
            // lightcolorlabel
            // 
            this.lightcolorlabel.AutoSize = true;
            this.lightcolorlabel.Location = new System.Drawing.Point(154, 20);
            this.lightcolorlabel.Name = "lightcolorlabel";
            this.lightcolorlabel.Size = new System.Drawing.Size(33, 13);
            this.lightcolorlabel.TabIndex = 1;
            this.lightcolorlabel.Text = "Color";
            // 
            // lighthexlabel
            // 
            this.lighthexlabel.AutoSize = true;
            this.lighthexlabel.Location = new System.Drawing.Point(196, 20);
            this.lighthexlabel.Name = "lighthexlabel";
            this.lighthexlabel.Size = new System.Drawing.Size(33, 13);
            this.lighthexlabel.TabIndex = 2;
            this.lighthexlabel.Text = "Hex";
            // 
            // lighttaglabel
            // 
            this.lighttaglabel.AutoSize = true;
            this.lighttaglabel.Location = new System.Drawing.Point(264, 20);
            this.lighttaglabel.Name = "lighttaglabel";
            this.lighttaglabel.Size = new System.Drawing.Size(33, 13);
            this.lighttaglabel.TabIndex = 3;
            this.lighttaglabel.Text = "Tag";
            // 
            // lightintensitylabel
            // 
            this.lightintensitylabel.AutoSize = true;
            this.lightintensitylabel.Location = new System.Drawing.Point(362, 20);
            this.lightintensitylabel.Name = "lightintensitylabel";
            this.lightintensitylabel.Size = new System.Drawing.Size(33, 13);
            this.lightintensitylabel.TabIndex = 4;
            this.lightintensitylabel.Text = "Intensity";
            // 
            // ceilingcolor
            // 
            this.ceilingcolor.BackColor = System.Drawing.Color.Transparent;
            this.ceilingcolor.Label = "Ceiling:";
            this.ceilingcolor.Location = new System.Drawing.Point(12, 38);
            this.ceilingcolor.Name = "ceilingcolor";
            this.ceilingcolor.Size = new System.Drawing.Size(404, 24);
            this.ceilingcolor.TabIndex = 5;
            // 
            // topcolor
            // 
            this.topcolor.BackColor = System.Drawing.Color.Transparent;
            this.topcolor.Label = "Upper wall:";
            this.topcolor.Location = new System.Drawing.Point(12, 64);
            this.topcolor.Name = "topcolor";
            this.topcolor.Size = new System.Drawing.Size(404, 24);
            this.topcolor.TabIndex = 6;
            // 
            // thingcolor
            // 
            this.thingcolor.BackColor = System.Drawing.Color.Transparent;
            this.thingcolor.Label = "Thing:";
            this.thingcolor.Location = new System.Drawing.Point(12, 90);
            this.thingcolor.Name = "thingcolor";
            this.thingcolor.Size = new System.Drawing.Size(404, 24);
            this.thingcolor.TabIndex = 7;
            // 
            // lowercolor
            // 
            this.lowercolor.BackColor = System.Drawing.Color.Transparent;
            this.lowercolor.Label = "Lower wall:";
            this.lowercolor.Location = new System.Drawing.Point(12, 116);
            this.lowercolor.Name = "lowercolor";
            this.lowercolor.Size = new System.Drawing.Size(404, 24);
            this.lowercolor.TabIndex = 8;
            // 
            // floorcolor
            // 
            this.floorcolor.BackColor = System.Drawing.Color.Transparent;
            this.floorcolor.Label = "Floor:";
            this.floorcolor.Location = new System.Drawing.Point(12, 142);
            this.floorcolor.Name = "floorcolor";
            this.floorcolor.Size = new System.Drawing.Size(404, 24);
            this.floorcolor.TabIndex = 9;
            // 
            // tooltip
            // 
            this.tooltip.AutomaticDelay = 10;
            this.tooltip.AutoPopDelay = 10000;
            this.tooltip.InitialDelay = 10;
            this.tooltip.ReshowDelay = 100;
            // 
            // SectorEditForm
            // 
            this.AcceptButton = this.apply;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.cancel;
            this.ClientSize = new System.Drawing.Size(468, 337);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.cancel);
            this.Controls.Add(this.apply);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SectorEditForm";
            this.Opacity = 0D;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Edit Sector";
            this.HelpRequested += new System.Windows.Forms.HelpEventHandler(this.SectorEditForm_HelpRequested);
            groupeffect.ResumeLayout(false);
            groupeffect.PerformLayout();
            groupfloorceiling.ResumeLayout(false);
            groupfloorceiling.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.groupflags.ResumeLayout(false);
            this.grouplights.ResumeLayout(false);
            this.grouplights.PerformLayout();
            this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Button cancel;
		private System.Windows.Forms.Button apply;
		private CodeImp.DoomBuilder.Controls.FlatSelectorControl floortex;
		private CodeImp.DoomBuilder.Controls.FlatSelectorControl ceilingtex;
		private System.Windows.Forms.Label sectorheight;
		private CodeImp.DoomBuilder.Controls.ActionSelectorControl effect;
		private System.Windows.Forms.Button browseeffect;
		private System.Windows.Forms.Label sectorheightlabel;
		private CodeImp.DoomBuilder.Controls.ButtonsNumericTextbox ceilingheight;
		private CodeImp.DoomBuilder.Controls.ButtonsNumericTextbox floorheight;
		private CodeImp.DoomBuilder.Controls.ButtonsNumericTextbox brightness;
		private CodeImp.DoomBuilder.Controls.TagSelector tagSelector;
		private System.Windows.Forms.Panel panel1;
		private CodeImp.DoomBuilder.Controls.ButtonsNumericTextbox heightoffset;
		private System.Windows.Forms.ToolTip tooltip;
		private System.Windows.Forms.GroupBox groupeffect;
		private System.Windows.Forms.Label label9;
		private System.Windows.Forms.GroupBox groupflags;
		private CodeImp.DoomBuilder.Controls.CheckboxArrayControl flags;
		private System.Windows.Forms.GroupBox grouplights;
		private System.Windows.Forms.Label lightindexlabel;
		private System.Windows.Forms.Label lightcolorlabel;
		private System.Windows.Forms.Label lighthexlabel;
		private System.Windows.Forms.Label lighttaglabel;
		private System.Windows.Forms.Label lightintensitylabel;
		private CodeImp.DoomBuilder.Controls.ColorControlSector ceilingcolor;
		private CodeImp.DoomBuilder.Controls.ColorControlSector topcolor;
		private CodeImp.DoomBuilder.Controls.ColorControlSector thingcolor;
		private CodeImp.DoomBuilder.Controls.ColorControlSector lowercolor;
		private CodeImp.DoomBuilder.Controls.ColorControlSector floorcolor;
	}
}