
#region ================== Namespaces

using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Controls;

#endregion

namespace CodeImp.DoomBuilder.Windows
{
	// styd. The dark theme of the interface, an option of the preferences (off by default). It gives
	// the colors that a control takes from the system a dark gray, gives the toolstrips and their menus
	// a renderer of their own, and keeps what it changed to put it back when the theme is turned off.
	// A color that a control chose itself is kept. A control added later to a dark window gets the
	// theme when it is added.
	internal static class DarkTheme
	{
		#region ================== Colors (a dark gray, as Visual Studio and the dark mode of Windows)

		internal static readonly Color Back = Color.FromArgb(45, 45, 48);
		internal static readonly Color Field = Color.FromArgb(30, 30, 30);
		internal static readonly Color MenuBack = Color.FromArgb(27, 27, 28);
		internal static readonly Color Hover = Color.FromArgb(62, 62, 64);
		internal static readonly Color Border = Color.FromArgb(63, 63, 70);
		internal static readonly Color Text = Color.FromArgb(241, 241, 241);
		internal static readonly Color DimText = Color.FromArgb(153, 153, 153);
		internal static readonly Color Link = Color.FromArgb(86, 156, 214);
		internal static readonly Color Accent = Color.FromArgb(0, 122, 204);
		internal static readonly Color Pressed = Color.FromArgb(0, 84, 153);
		internal static readonly Color Checked = Color.FromArgb(0, 62, 112);

		#endregion

		#region ================== Native

		[DllImport("dwmapi.dll")]
		private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

		[DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
		private static extern int SetWindowTheme(IntPtr hwnd, string appname, string idlist);

		[DllImport("user32.dll")]
		private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

		private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
		private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
		private const int WM_NCACTIVATE = 0x0086;

		#endregion

		#region ================== What was changed

		private class Saved
		{
			public bool back, backset, fore, foreset;
			public Color backcolor, forecolor;
			public bool button, buttonvisualback;
			public FlatStyle buttonflat;
			public Color buttonborder;
			public bool combo;
			public FlatStyle comboflat;
			public bool tabpage;
			public bool strip;
			public ToolStripRenderMode rendermode;
			public ToolStripRenderer renderer;
			public bool wintheme;
			public bool grid, gridheaders;
			public Color gridback, gridcolor, cellback, cellfore, cellselback, headerback, headerfore;
		}

		private static ConditionalWeakTable<object, Saved> saved = new ConditionalWeakTable<object, Saved>();
		private static readonly ConditionalWeakTable<object, object> hooked = new ConditionalWeakTable<object, object>();
		private static ToolStripRenderer renderer;
		private static bool active;
		private static bool changing;

		private static readonly MethodInfo serializeback = typeof(Control).GetMethod("ShouldSerializeBackColor", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		private static readonly MethodInfo serializefore = typeof(Control).GetMethod("ShouldSerializeForeColor", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

		#endregion

		#region ================== Properties

		// True while the theme is dark
		internal static bool Active { get { return active; } }

		#endregion

		#region ================== Methods

		// The dark theme on a window, or off
		internal static void Set(Form form, bool dark)
		{
			if(dark) Apply(form); else Revert(form);
			active = dark;
			SetTitleBar(form, dark);
			form.Invalidate(true);
		}

		// The title bar of a window dark or light (Windows 10 version 2004 and later; earlier versions
		// know the attribute under another number, older ones not at all)
		internal static void SetTitleBar(Form form, bool dark)
		{
			try
			{
				int value = (dark ? 1 : 0);
				if(DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, 4) != 0)
					DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref value, 4);

				// Windows draws the title bar again only when it is activated or deactivated
				bool isactive = (Form.ActiveForm == form);
				SendMessage(form.Handle, WM_NCACTIVATE, new IntPtr(isactive ? 0 : 1), IntPtr.Zero);
				SendMessage(form.Handle, WM_NCACTIVATE, new IntPtr(isactive ? 1 : 0), IntPtr.Zero);
			}
			catch(DllNotFoundException) { }
			catch(EntryPointNotFoundException) { }
		}

		// This gives a control and all that it holds the dark theme
		internal static void Apply(Control c)
		{
			active = true;
			ApplyControl(c);
		}

		// This puts back what the dark theme changed in a control and all that it holds
		internal static void Revert(Control c)
		{
			active = false;
			RevertControl(c);
		}

		private static void ApplyControl(Control c)
		{
			if(c is RenderTargetControl) return;
			Hook(c);

			Saved s;
			if(!saved.TryGetValue(c, out s))
			{
				s = new Saved();
				saved.Add(c, s);
				Change(c, s);
			}

			ToolStrip strip = c as ToolStrip;
			if(strip != null) foreach(ToolStripItem item in strip.Items) ApplyItem(item);
			foreach(Control child in c.Controls) ApplyControl(child);
			if(c.ContextMenuStrip != null) ApplyControl(c.ContextMenuStrip);
		}

		private static void ApplyItem(ToolStripItem item)
		{
			ToolStripDropDownItem dropdown = item as ToolStripDropDownItem;
			if(dropdown != null) ApplyControl(dropdown.DropDown);
			ToolStripControlHost host = item as ToolStripControlHost;
			if(host != null) ApplyControl(host.Control);
		}

		private static void RevertControl(Control c)
		{
			if(c is RenderTargetControl) return;

			ToolStrip strip = c as ToolStrip;
			if(strip != null) foreach(ToolStripItem item in strip.Items) RevertItem(item);
			foreach(Control child in c.Controls) RevertControl(child);
			if(c.ContextMenuStrip != null) RevertControl(c.ContextMenuStrip);

			Saved s;
			if(saved.TryGetValue(c, out s))
			{
				Restore(c, s);
				saved.Remove(c);
			}
		}

		private static void RevertItem(ToolStripItem item)
		{
			ToolStripDropDownItem dropdown = item as ToolStripDropDownItem;
			if(dropdown != null) RevertControl(dropdown.DropDown);
			ToolStripControlHost host = item as ToolStripControlHost;
			if(host != null) RevertControl(host.Control);
		}

		// A control added later, or an item added to a toolstrip, follows the theme of what holds it
		private static void Hook(Control c)
		{
			object dummy;
			if(hooked.TryGetValue(c, out dummy)) return;
			hooked.Add(c, c);

			c.ControlAdded += OnControlAdded;
			c.HandleCreated += OnHandleCreated;
			c.ForeColorChanged += OnForeColorChanged;
			c.BackColorChanged += OnBackColorChanged;
			ToolStrip strip = c as ToolStrip;
			if(strip != null) strip.ItemAdded += OnItemAdded;
			if((c is Label) || ((c is Button) && !(c is CheckBox) && !(c is RadioButton))) c.Paint += OnDisabledPaint;
		}

		private static void OnControlAdded(object sender, ControlEventArgs e)
		{
			Saved s;
			if(!saved.TryGetValue(sender, out s) && active) return; // (what holds it does not have the theme)
			if(active) ApplyControl(e.Control); else RevertControl(e.Control);
		}

		private static void OnItemAdded(object sender, ToolStripItemEventArgs e)
		{
			if(active) ApplyItem(e.Item); else RevertItem(e.Item);
		}

		// A control of a dark window to which its code gives a color of the system gets the dark one
		// instead (the info panels give their labels a color for what they show). That color of the
		// system is the one put back when the theme is turned off.
		private static void OnForeColorChanged(object sender, EventArgs e)
		{
			Control c = (Control)sender;
			Saved s;
			if(!active || changing || !saved.TryGetValue(c, out s) || !IsSet(serializefore, c)) return;
			Color color = c.ForeColor;
			Color mapped = MapFore(color);
			if(mapped == color) return;
			s.fore = true;
			s.foreset = true;
			s.forecolor = color;
			changing = true;
			c.ForeColor = mapped;
			changing = false;
		}

		private static void OnBackColorChanged(object sender, EventArgs e)
		{
			Control c = (Control)sender;
			Saved s;
			if(!active || changing || !saved.TryGetValue(c, out s) || !IsSet(serializeback, c)) return;
			Color color = c.BackColor;
			Color mapped = MapBack(c, color);
			if(mapped == color) return;
			s.back = true;
			s.backset = true;
			s.backcolor = color;
			changing = true;
			c.BackColor = mapped;
			changing = false;
		}

		// Windows Forms draws the text of a label or of a button that is not enabled a little darker than
		// its background: on a dark one it cannot be read. With the dark theme it is drawn again in the
		// gray of a text that is not available (a button only when it has no picture).
		private static void OnDisabledPaint(object sender, PaintEventArgs e)
		{
			Control c = (Control)sender;
			if(!active || c.Enabled || string.IsNullOrEmpty(c.Text)) return;
			Saved s;
			if(!saved.TryGetValue(c, out s)) return;

			Rectangle area = c.ClientRectangle;
			ContentAlignment align;
			TextFormatFlags flags = TextFormatFlags.TextBoxControl | TextFormatFlags.WordBreak;
			Label label = c as Label;
			if(label != null)
			{
				if(label is LinkLabel) return;
				align = label.TextAlign;
				if(!label.UseMnemonic) flags |= TextFormatFlags.NoPrefix;
				if(label.AutoEllipsis) flags |= TextFormatFlags.EndEllipsis;
				if(c.BackColor.A == 255)
				{
					using(SolidBrush back = new SolidBrush(c.BackColor)) e.Graphics.FillRectangle(back, area);
				}
			}
			else
			{
				Button button = (Button)c;
				if((button.Image != null) || (button.BackColor.A != 255)) return;
				align = button.TextAlign;
				using(SolidBrush back = new SolidBrush(button.BackColor)) e.Graphics.FillRectangle(back, area);
				using(Pen border = new Pen(button.FlatAppearance.BorderColor)) e.Graphics.DrawRectangle(border, 0, 0, area.Width - 1, area.Height - 1);
			}

			switch(align)
			{
				case ContentAlignment.TopCenter: flags |= TextFormatFlags.HorizontalCenter; break;
				case ContentAlignment.TopRight: flags |= TextFormatFlags.Right; break;
				case ContentAlignment.MiddleLeft: flags |= TextFormatFlags.VerticalCenter; break;
				case ContentAlignment.MiddleCenter: flags |= TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter; break;
				case ContentAlignment.MiddleRight: flags |= TextFormatFlags.VerticalCenter | TextFormatFlags.Right; break;
				case ContentAlignment.BottomLeft: flags |= TextFormatFlags.Bottom; break;
				case ContentAlignment.BottomCenter: flags |= TextFormatFlags.Bottom | TextFormatFlags.HorizontalCenter; break;
				case ContentAlignment.BottomRight: flags |= TextFormatFlags.Bottom | TextFormatFlags.Right; break;
			}
			Rectangle text = new Rectangle(area.X + c.Padding.Left, area.Y + c.Padding.Top, area.Width - c.Padding.Horizontal, area.Height - c.Padding.Vertical);
			TextRenderer.DrawText(e.Graphics, c.Text, c.Font, text, DimText, flags);
		}

		private static void OnHandleCreated(object sender, EventArgs e)
		{
			Saved s;
			if(active && saved.TryGetValue(sender, out s) && s.wintheme) SetWindowTheme(((Control)sender).Handle, "DarkMode_Explorer", null);
		}

		// The color that the theme gives to a background color of the system
		private static Color MapBack(Control c, Color color)
		{
			if(!color.IsSystemColor) return color;
			switch(color.ToKnownColor())
			{
				case KnownColor.Control:
				case KnownColor.ButtonFace:
				case KnownColor.ControlLight:
				case KnownColor.MenuBar:
					return (c is ToolStripDropDown ? MenuBack : Back);
				case KnownColor.Menu:
					return MenuBack;
				case KnownColor.Window:
				case KnownColor.AppWorkspace:
				case KnownColor.Info:
					return Field;
				default:
					return color;
			}
		}

		// The color that the theme gives to a text color of the system
		private static Color MapFore(Color color)
		{
			if(!color.IsSystemColor) return color;
			switch(color.ToKnownColor())
			{
				case KnownColor.ControlText:
				case KnownColor.WindowText:
				case KnownColor.MenuText:
				case KnownColor.InfoText:
					return Text;
				case KnownColor.GrayText:
				case KnownColor.ControlDark:
					return DimText;
				case KnownColor.HotTrack:
					return Link;
				default:
					return color;
			}
		}

		private static bool IsSet(MethodInfo serialize, Control c)
		{
			if(serialize == null) return true;
			try { return (bool)serialize.Invoke(c, null); }
			catch(Exception) { return true; }
		}

		// This changes a control and keeps what it was
		private static void Change(Control c, Saved s)
		{
			Color back = c.BackColor;
			Color newback = MapBack(c, back);
			if(newback != back)
			{
				s.back = true;
				s.backset = IsSet(serializeback, c);
				s.backcolor = back;
				c.BackColor = newback;
			}

			Color fore = c.ForeColor;
			Color newfore = MapFore(fore);
			if(newfore != fore)
			{
				s.fore = true;
				s.foreset = IsSet(serializefore, c);
				s.forecolor = fore;
				c.ForeColor = newfore;
			}

			// A group box draws its title in its own color only when the color is set on it, otherwise
			// in the color of the theme of Windows, which is dark
			if((c is GroupBox) && !IsSet(serializefore, c))
			{
				s.fore = true;
				s.foreset = false;
				s.forecolor = c.ForeColor;
				c.ForeColor = Text;
			}

			// Buttons are flat, with a border (not the check boxes and radio buttons, nor the
			// transparent ones laid over a picture)
			ButtonBase button = c as ButtonBase;
			if((button != null) && !(c is CheckBox) && !(c is RadioButton) && (button.BackColor.A == 255))
			{
				s.button = true;
				s.buttonflat = button.FlatStyle;
				s.buttonvisualback = button.UseVisualStyleBackColor;
				s.buttonborder = button.FlatAppearance.BorderColor;
				button.FlatStyle = FlatStyle.Flat;
				button.UseVisualStyleBackColor = false;
				button.FlatAppearance.BorderColor = Border;

				// (a button with the background of the window is a little lighter)
				if(button.BackColor == Back)
				{
					if(!s.back) { s.back = true; s.backset = IsSet(serializeback, c); s.backcolor = button.BackColor; }
					button.BackColor = Hover;
				}
			}

			ComboBox combo = c as ComboBox;
			if(combo != null)
			{
				s.combo = true;
				s.comboflat = combo.FlatStyle;
				combo.FlatStyle = FlatStyle.Flat;
			}

			TabPage page = c as TabPage;
			if((page != null) && page.UseVisualStyleBackColor)
			{
				s.tabpage = true;
				page.UseVisualStyleBackColor = false;
				if(!s.back) { s.back = true; s.backset = IsSet(serializeback, c); s.backcolor = page.BackColor; }
				page.BackColor = Back;
			}

			ToolStrip strip = c as ToolStrip;
			if(strip != null)
			{
				if(renderer == null) renderer = new DarkToolStripRenderer();
				s.strip = true;
				s.rendermode = strip.RenderMode;
				s.renderer = strip.Renderer;
				strip.Renderer = renderer;
			}

			DataGridView grid = c as DataGridView;
			if(grid != null)
			{
				s.grid = true;
				s.gridback = grid.BackgroundColor;
				s.gridcolor = grid.GridColor;
				s.cellback = grid.DefaultCellStyle.BackColor;
				s.cellfore = grid.DefaultCellStyle.ForeColor;
				s.cellselback = grid.DefaultCellStyle.SelectionBackColor;
				s.gridheaders = grid.EnableHeadersVisualStyles;
				s.headerback = grid.ColumnHeadersDefaultCellStyle.BackColor;
				s.headerfore = grid.ColumnHeadersDefaultCellStyle.ForeColor;
				grid.BackgroundColor = Field;
				grid.GridColor = Border;
				grid.DefaultCellStyle.BackColor = Field;
				grid.DefaultCellStyle.ForeColor = Text;
				grid.DefaultCellStyle.SelectionBackColor = Checked;
				grid.EnableHeadersVisualStyles = false;
				grid.ColumnHeadersDefaultCellStyle.BackColor = Back;
				grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
			}

			// Lists, trees, text boxes and scroll bars get the dark scroll bars of Windows
			if((c is TreeView) || (c is ListView) || (c is TextBoxBase) || (c is ListBox) || (c is ScrollBar) || (c is DataGridView))
			{
				s.wintheme = true;
				if(c.IsHandleCreated) SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
			}
		}

		// This puts back what was changed
		private static void Restore(Control c, Saved s)
		{
			ToolStrip strip = c as ToolStrip;
			if(s.strip && (strip != null))
			{
				// (back to the renderer of the manager by forgetting the dark one: setting the mode
				// keeps it, and the strip would not take it again)
				if(s.rendermode == ToolStripRenderMode.Custom) strip.Renderer = s.renderer;
				else if(s.rendermode == ToolStripRenderMode.ManagerRenderMode) strip.Renderer = null;
				else strip.RenderMode = s.rendermode;
			}

			DataGridView grid = c as DataGridView;
			if(s.grid && (grid != null))
			{
				grid.BackgroundColor = s.gridback;
				grid.GridColor = s.gridcolor;
				grid.DefaultCellStyle.BackColor = s.cellback;
				grid.DefaultCellStyle.ForeColor = s.cellfore;
				grid.DefaultCellStyle.SelectionBackColor = s.cellselback;
				grid.EnableHeadersVisualStyles = s.gridheaders;
				grid.ColumnHeadersDefaultCellStyle.BackColor = s.headerback;
				grid.ColumnHeadersDefaultCellStyle.ForeColor = s.headerfore;
			}

			ComboBox combo = c as ComboBox;
			if(s.combo && (combo != null)) combo.FlatStyle = s.comboflat;

			ButtonBase button = c as ButtonBase;
			if(s.button && (button != null))
			{
				button.FlatAppearance.BorderColor = s.buttonborder;
				button.FlatStyle = s.buttonflat;
				button.UseVisualStyleBackColor = s.buttonvisualback;
			}

			TabPage page = c as TabPage;
			if(s.tabpage && (page != null)) page.UseVisualStyleBackColor = true;

			if(s.back)
			{
				if(s.backset) c.BackColor = s.backcolor;
				else c.ResetBackColor();
			}
			if(s.fore)
			{
				if(s.foreset) c.ForeColor = s.forecolor;
				else c.ResetForeColor();
			}

			// (a button that took its background from the theme of Windows takes it again)
			if(s.button && (button != null) && s.buttonvisualback) button.UseVisualStyleBackColor = true;

			if(s.wintheme && c.IsHandleCreated) SetWindowTheme(c.Handle, null, null);
		}

		#endregion

		#region ================== Toolstrips

		// The renderer of the toolstrips and of their menus
		private class DarkToolStripRenderer : ToolStripProfessionalRenderer
		{
			public DarkToolStripRenderer() : base(new DarkColorTable())
			{
				this.RoundedEdges = false;
			}

			protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
			{
				if(!e.Item.ForeColor.IsSystemColor && (e.Item.ForeColor != Text)) { base.OnRenderItemText(e); return; }
				e.TextColor = (e.Item.Enabled ? Text : DimText);
				base.OnRenderItemText(e);
			}

			protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
			{
				e.ArrowColor = ((e.Item == null) || e.Item.Enabled ? Text : DimText);
				base.OnRenderArrow(e);
			}
		}

		private class DarkColorTable : ProfessionalColorTable
		{
			public DarkColorTable() { this.UseSystemColors = false; }

			public override Color ToolStripGradientBegin { get { return Back; } }
			public override Color ToolStripGradientMiddle { get { return Back; } }
			public override Color ToolStripGradientEnd { get { return Back; } }
			public override Color ToolStripBorder { get { return Back; } }
			public override Color ToolStripContentPanelGradientBegin { get { return Back; } }
			public override Color ToolStripContentPanelGradientEnd { get { return Back; } }
			public override Color ToolStripPanelGradientBegin { get { return Back; } }
			public override Color ToolStripPanelGradientEnd { get { return Back; } }
			public override Color ToolStripDropDownBackground { get { return MenuBack; } }
			public override Color MenuStripGradientBegin { get { return Back; } }
			public override Color MenuStripGradientEnd { get { return Back; } }
			public override Color StatusStripGradientBegin { get { return Back; } }
			public override Color StatusStripGradientEnd { get { return Back; } }
			public override Color MenuBorder { get { return Border; } }
			public override Color MenuItemBorder { get { return Hover; } }
			public override Color MenuItemSelected { get { return Hover; } }
			public override Color MenuItemSelectedGradientBegin { get { return Hover; } }
			public override Color MenuItemSelectedGradientEnd { get { return Hover; } }
			public override Color MenuItemPressedGradientBegin { get { return MenuBack; } }
			public override Color MenuItemPressedGradientMiddle { get { return MenuBack; } }
			public override Color MenuItemPressedGradientEnd { get { return MenuBack; } }
			public override Color ImageMarginGradientBegin { get { return MenuBack; } }
			public override Color ImageMarginGradientMiddle { get { return MenuBack; } }
			public override Color ImageMarginGradientEnd { get { return MenuBack; } }
			public override Color ImageMarginRevealedGradientBegin { get { return MenuBack; } }
			public override Color ImageMarginRevealedGradientMiddle { get { return MenuBack; } }
			public override Color ImageMarginRevealedGradientEnd { get { return MenuBack; } }
			public override Color ButtonSelectedHighlight { get { return Hover; } }
			public override Color ButtonSelectedHighlightBorder { get { return Accent; } }
			public override Color ButtonSelectedGradientBegin { get { return Hover; } }
			public override Color ButtonSelectedGradientMiddle { get { return Hover; } }
			public override Color ButtonSelectedGradientEnd { get { return Hover; } }
			public override Color ButtonSelectedBorder { get { return Accent; } }
			public override Color ButtonPressedHighlight { get { return Pressed; } }
			public override Color ButtonPressedHighlightBorder { get { return Accent; } }
			public override Color ButtonPressedGradientBegin { get { return Pressed; } }
			public override Color ButtonPressedGradientMiddle { get { return Pressed; } }
			public override Color ButtonPressedGradientEnd { get { return Pressed; } }
			public override Color ButtonPressedBorder { get { return Accent; } }
			public override Color ButtonCheckedHighlight { get { return Checked; } }
			public override Color ButtonCheckedHighlightBorder { get { return Accent; } }
			public override Color ButtonCheckedGradientBegin { get { return Checked; } }
			public override Color ButtonCheckedGradientMiddle { get { return Checked; } }
			public override Color ButtonCheckedGradientEnd { get { return Checked; } }
			public override Color CheckBackground { get { return Checked; } }
			public override Color CheckSelectedBackground { get { return Pressed; } }
			public override Color CheckPressedBackground { get { return Pressed; } }
			public override Color SeparatorDark { get { return Border; } }
			public override Color SeparatorLight { get { return Back; } }
			public override Color GripDark { get { return Border; } }
			public override Color GripLight { get { return Back; } }
			public override Color OverflowButtonGradientBegin { get { return Back; } }
			public override Color OverflowButtonGradientMiddle { get { return Back; } }
			public override Color OverflowButtonGradientEnd { get { return Hover; } }
			public override Color RaftingContainerGradientBegin { get { return Back; } }
			public override Color RaftingContainerGradientEnd { get { return Back; } }
		}

		#endregion
	}
}
