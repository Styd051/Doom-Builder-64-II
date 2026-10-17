
#region ================== Namespaces

using System;
using System.Collections.Generic;
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
	// theme when it is added. Every window of the editor and of its plugins follows the theme, but the
	// script editor (which comes later) and the window of a crash.
	public static class DarkTheme
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
		internal static readonly Color Warning = Color.FromArgb(244, 71, 71);
		internal static readonly Color VisitedLink = Color.FromArgb(197, 134, 192);
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

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string classname, string windowname);

		private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
		private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
		private const int WM_NCACTIVATE = 0x0086;
		private const int WM_REFLECT_NOTIFY = 0x2000 + 0x004E;
		private const int NM_CUSTOMDRAW = -12;
		private const int CDDS_ITEMPREPAINT = 0x00010001;
		private const int CDIS_SELECTED = 0x0001;
		private const int LVM_GETHEADER = 0x1000 + 31;

		// (the notification with which a list asks the colors of an item, as Windows lays it out)
		[StructLayout(LayoutKind.Sequential)]
		private struct NMHDR { public IntPtr hwndFrom; public IntPtr idFrom; public int code; }

		[StructLayout(LayoutKind.Sequential)]
		private struct NMCUSTOMDRAW { public NMHDR hdr; public int dwDrawStage; public IntPtr hdc; public int left, top, right, bottom; public IntPtr dwItemSpec; public int uItemState; public IntPtr lItemlParam; }

		[StructLayout(LayoutKind.Sequential)]
		private struct NMLVCUSTOMDRAW { public NMCUSTOMDRAW nmcd; public int clrText; public int clrTextBk; }

		private static readonly int codeoffset = Marshal.OffsetOf(typeof(NMHDR), "code").ToInt32();
		private static readonly int stageoffset = Marshal.OffsetOf(typeof(NMCUSTOMDRAW), "dwDrawStage").ToInt32();
		private static readonly int stateoffset = Marshal.OffsetOf(typeof(NMCUSTOMDRAW), "uItemState").ToInt32();
		private static readonly int itemoffset = Marshal.OffsetOf(typeof(NMCUSTOMDRAW), "dwItemSpec").ToInt32();
		private static readonly int textoffset = Marshal.OffsetOf(typeof(NMLVCUSTOMDRAW), "clrText").ToInt32();
		private static readonly int textbackoffset = Marshal.OffsetOf(typeof(NMLVCUSTOMDRAW), "clrTextBk").ToInt32();

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
			public bool tabs;
			public bool[] tabstyles;
			public int hottab = -1;
			public IntPtr updown;
			public bool link;
			public Color linkcolor, visitedcolor;
			public bool strip;
			public ToolStripRenderMode rendermode;
			public ToolStripRenderer renderer;
			public string wintheme;
			public ListPainter painter;
			public bool grid;
			public Color gridback, gridcolor;
		}

		// The colors of the cells of a grid
		private class GridColors
		{
			public bool headers;
			public Color cellback, cellfore, cellselback, headerback, headerfore, rowheaderback, rowheaderfore;
		}

		private static ConditionalWeakTable<object, Saved> saved = new ConditionalWeakTable<object, Saved>();
		private static readonly ConditionalWeakTable<object, object> hooked = new ConditionalWeakTable<object, object>();
		private static readonly ConditionalWeakTable<object, object> loadhooked = new ConditionalWeakTable<object, object>();
		private static readonly ConditionalWeakTable<object, GridColors> grids = new ConditionalWeakTable<object, GridColors>();
		private static readonly Dictionary<int, SolidBrush> brushes = new Dictionary<int, SolidBrush>();
		private static readonly Dictionary<int, Pen> pens = new Dictionary<int, Pen>();
		private static ToolStripRenderer renderer;
		private static bool active;
		private static bool changing;

		private static readonly MethodInfo serializeback = typeof(Control).GetMethod("ShouldSerializeBackColor", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		private static readonly MethodInfo serializefore = typeof(Control).GetMethod("ShouldSerializeForeColor", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		private static readonly MethodInfo getstyle = typeof(Control).GetMethod("GetStyle", BindingFlags.Instance | BindingFlags.NonPublic);
		private static readonly MethodInfo setstyle = typeof(Control).GetMethod("SetStyle", BindingFlags.Instance | BindingFlags.NonPublic);
		private static readonly ControlStyles[] tabstyles = { ControlStyles.UserPaint, ControlStyles.AllPaintingInWmPaint, ControlStyles.OptimizedDoubleBuffer, ControlStyles.ResizeRedraw };

		#endregion

		#region ================== Properties

		// True while the theme is dark
		public static bool Active { get { return active; } }

		#endregion

		#region ================== Windows

		// The dark theme on a window, or off
		internal static void Set(Form form, bool dark)
		{
			if(dark) Apply(form); else Revert(form);
			active = dark;
			if(form.IsHandleCreated) SetTitleBar(form, dark); // (otherwise when its handle is made)
			form.Invalidate(true);
		}

		// The dark theme on every open window that follows it, or off (the main window always; the others
		// are listed by Windows Forms once their handle is made, and take the theme when they are loaded)
		internal static void SetAll(bool dark)
		{
			List<Form> forms = new List<Form>();
			if(General.MainWindow != null) forms.Add(General.MainWindow);
			foreach(Form f in Application.OpenForms) if(Follows(f) && !forms.Contains(f)) forms.Add(f);
			foreach(Form f in forms) Set(f, dark);
			active = dark;
		}

		// A window takes the theme of the interface now, and again when it is loaded. The delayed forms do
		// it when they are loaded; another window of a plugin can do it when it is made.
		public static void Follow(Form form)
		{
			if((form == null) || !Follows(form)) return;
			object dummy;
			if(!loadhooked.TryGetValue(form, out dummy))
			{
				loadhooked.Add(form, form);
				form.Load += OnFormLoad;
			}
			FollowNow(form);
		}

		private static void OnFormLoad(object sender, EventArgs e)
		{
			FollowNow((Form)sender);
		}

		private static void FollowNow(Form form)
		{
			Saved s;
			if(saved.TryGetValue(form, out s) != active) Set(form, active);
		}

		// The windows that follow the theme (the script editor comes later; the window of a crash only
		// shows what went wrong)
		private static bool Follows(Form form)
		{
			return !(form is ScriptEditorForm) && !(form is ExceptionDialog);
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

				// Windows draws the title bar again only when it is activated or deactivated (a window that
				// is not shown yet takes the attribute when it is)
				if(!form.Visible) return;
				bool isactive = (Form.ActiveForm == form);
				SendMessage(form.Handle, WM_NCACTIVATE, new IntPtr(isactive ? 0 : 1), IntPtr.Zero);
				SendMessage(form.Handle, WM_NCACTIVATE, new IntPtr(isactive ? 1 : 0), IntPtr.Zero);
			}
			catch(DllNotFoundException) { }
			catch(EntryPointNotFoundException) { }
		}

		#endregion

		#region ================== Controls

		// This gives a control and all that it holds the dark theme
		internal static void Apply(Control c)
		{
			active = true;
			KeepGrids(c);
			ApplyControl(c);
		}

		// This puts back what the dark theme changed in a control and all that it holds
		internal static void Revert(Control c)
		{
			active = false;
			RevertControl(c);
			PutBackGrids(c);
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
			if(active)
			{
				KeepGrids(e.Control);
				ApplyControl(e.Control);
			}
			else
			{
				RevertControl(e.Control);
				PutBackGrids(e.Control);
			}
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

		// What Windows keeps in the window of a control (its theme, the title bar of a window, how a
		// list draws its items) is given again when that window is made again
		private static void OnHandleCreated(object sender, EventArgs e)
		{
			Saved s;
			if(!active || !saved.TryGetValue(sender, out s)) return;
			Control c = (Control)sender;
			if(s.wintheme != null) SetWindowTheme(c.Handle, s.wintheme, null);
			if(s.painter != null) s.painter.Attach((ListView)c);
			Form form = c as Form;
			if(form != null) SetTitleBar(form, true);
		}

		// The color that the theme gives to a background color of the system
		private static Color MapBack(Control c, Color color)
		{
			// (Windows draws a track bar with a transparent background white: it gets the one of what holds it)
			if((c is TrackBar) && (color.A != 255))
			{
				for(Control p = c.Parent; p != null; p = p.Parent) if(p.BackColor.A == 255) return p.BackColor;
				return Back;
			}
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
				case KnownColor.ControlLightLight:
					return Field;
				default:
					return color;
			}
		}

		// The color that the theme gives to a text color of the system
		private static Color MapFore(Color color)
		{
			if(color.IsKnownColor && (color.ToKnownColor() == KnownColor.DarkRed)) return Warning; // (the warnings of the editor)
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
				case KnownColor.InactiveCaptionText:
					return DimText;
				case KnownColor.HotTrack:
					return Link;
				default:
					return color;
			}
		}

		// The color with which a control that draws itself paints what it gives a color of the system:
		// the dark one while the theme is dark, the same otherwise
		internal static Color Map(Color color)
		{
			if(!active) return color;
			Color fore = MapFore(color);
			if(fore != color) return fore;
			if(!color.IsSystemColor) return color;
			if(color.ToKnownColor() == KnownColor.WindowFrame) return DimText;
			return MapBack(null, color);
		}

		// The same as a brush or a pen (a brush or a pen of the system when the color is not changed;
		// the others are kept for later)
		internal static Brush BrushOf(Color color)
		{
			Color c = Map(color);
			if(c.IsSystemColor) return SystemBrushes.FromSystemColor(c);
			SolidBrush brush;
			if(!brushes.TryGetValue(c.ToArgb(), out brush))
			{
				brush = new SolidBrush(c);
				brushes.Add(c.ToArgb(), brush);
			}
			return brush;
		}

		internal static Pen PenOf(Color color)
		{
			Color c = Map(color);
			if(c.IsSystemColor) return SystemPens.FromSystemColor(c);
			Pen pen;
			if(!pens.TryGetValue(c.ToArgb(), out pen))
			{
				pen = new Pen(c);
				pens.Add(c.ToArgb(), pen);
			}
			return pen;
		}

		private static bool IsSet(MethodInfo serialize, Control c)
		{
			if(serialize == null) return true;
			try { return (bool)serialize.Invoke(c, null); }
			catch(Exception) { return true; }
		}

		private static bool GetStyle(Control c, ControlStyles style) { return (bool)getstyle.Invoke(c, new object[] { style }); }
		private static void SetStyle(Control c, ControlStyles style, bool value) { setstyle.Invoke(c, new object[] { style, value }); }

		// (the color of a link that the label did not choose is empty)
		private static Color RawLinkColor(LinkLabel link, string field, Color color)
		{
			FieldInfo f = typeof(LinkLabel).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
			return (f != null ? (Color)f.GetValue(link) : color);
		}

		// This changes a control and keeps what it was
		private static void Change(Control c, Saved s)
		{
			// (first the grids: the color of a grid sets the one of its cells, which are kept apart)
			DataGridView grid = c as DataGridView;
			if(grid != null)
			{
				s.grid = true;
				s.gridback = grid.BackgroundColor;
				s.gridcolor = grid.GridColor;
				grid.BackgroundColor = Field;
				grid.GridColor = Border;
				grid.DefaultCellStyle.BackColor = Field;
				grid.DefaultCellStyle.ForeColor = Text;
				grid.DefaultCellStyle.SelectionBackColor = Checked;
				grid.EnableHeadersVisualStyles = false;
				grid.ColumnHeadersDefaultCellStyle.BackColor = Back;
				grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
				grid.RowHeadersDefaultCellStyle.BackColor = Back;
				grid.RowHeadersDefaultCellStyle.ForeColor = Text;

				// (the rows and cells that the code gives a color of the system are shown with the dark one)
				grid.CellFormatting += OnGridCellFormatting;
			}

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
			// transparent ones laid over a picture: a button transparent because what holds it is
			// transparent is a button as the others)
			ButtonBase button = c as ButtonBase;
			if((button != null) && !(c is CheckBox) && !(c is RadioButton) && ((button.BackColor.A == 255) || !IsSet(serializeback, c)))
			{
				s.button = true;
				s.buttonflat = button.FlatStyle;
				s.buttonvisualback = button.UseVisualStyleBackColor;
				s.buttonborder = button.FlatAppearance.BorderColor;
				button.FlatStyle = FlatStyle.Flat;
				button.UseVisualStyleBackColor = false;
				button.FlatAppearance.BorderColor = Border;

				// (a button with the background of the window, or with the transparent one of what holds
				// it, is a little lighter)
				if((button.BackColor == Back) || (button.BackColor.A != 255))
				{
					if(!s.back) { s.back = true; s.backset = IsSet(serializeback, c); s.backcolor = button.BackColor; }
					button.BackColor = Hover;
				}
			}

			// Combo boxes are flat, with the dark look of Windows for the part that it draws
			ComboBox combo = c as ComboBox;
			if(combo != null)
			{
				s.combo = true;
				s.comboflat = combo.FlatStyle;
				combo.FlatStyle = FlatStyle.Flat;
				s.wintheme = "DarkMode_CFD";
			}

			TabPage page = c as TabPage;
			if((page != null) && page.UseVisualStyleBackColor)
			{
				s.tabpage = true;
				page.UseVisualStyleBackColor = false;
				if(!s.back) { s.back = true; s.backset = IsSet(serializeback, c); s.backcolor = page.BackColor; }
				page.BackColor = Back;
			}

			// The tabs of a window are drawn here (Windows draws them light whatever their colors; the
			// side panels and the script editor draw theirs)
			TabControl tabs = c as TabControl;
			if((tabs != null) && !(tabs is DockersTabsControl) && !(tabs is VSTabControl) && (tabs.Alignment == TabAlignment.Top) && (tabs.Appearance == TabAppearance.Normal) && (getstyle != null) && (setstyle != null))
			{
				s.tabs = true;
				s.tabstyles = new bool[tabstyles.Length];
				for(int i = 0; i < tabstyles.Length; i++)
				{
					s.tabstyles[i] = GetStyle(tabs, tabstyles[i]);
					SetStyle(tabs, tabstyles[i], true);
				}
				tabs.Paint += OnTabsPaint;
				tabs.MouseMove += OnTabsMouseMove;
				tabs.MouseLeave += OnTabsMouseLeave;
			}

			// Links that did not choose their color get a light blue (the blue of Windows cannot be read on
			// a dark background)
			LinkLabel link = c as LinkLabel;
			if(link != null)
			{
				s.link = true;
				s.linkcolor = RawLinkColor(link, "linkColor", link.LinkColor);
				s.visitedcolor = RawLinkColor(link, "visitedLinkColor", link.VisitedLinkColor);
				if(s.linkcolor.IsEmpty || s.linkcolor.IsSystemColor) link.LinkColor = Link;
				if(s.visitedcolor.IsEmpty || s.visitedcolor.IsSystemColor) link.VisitedLinkColor = VisitedLink;
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

			// Lists, trees, text boxes and scroll bars get the dark scroll bars of Windows
			if((c is TreeView) || (c is ListView) || (c is TextBoxBase) || (c is ListBox) || (c is ScrollBar) || (c is DataGridView)) s.wintheme = "DarkMode_Explorer";
			if((s.wintheme != null) && c.IsHandleCreated) SetWindowTheme(c.Handle, s.wintheme, null);

			// A list shows the items to which its code gives a color of the system with the dark one
			ListView list = c as ListView;
			if(list != null)
			{
				s.painter = new ListPainter();
				if(list.IsHandleCreated) s.painter.Attach(list);
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

			ComboBox combo = c as ComboBox;
			if(s.combo && (combo != null)) combo.FlatStyle = s.comboflat;

			ButtonBase button = c as ButtonBase;
			if(s.button && (button != null))
			{
				button.FlatAppearance.BorderColor = s.buttonborder;
				button.FlatStyle = s.buttonflat;
				button.UseVisualStyleBackColor = s.buttonvisualback;
			}

			TabControl tabs = c as TabControl;
			if(s.tabs && (tabs != null))
			{
				tabs.Paint -= OnTabsPaint;
				tabs.MouseMove -= OnTabsMouseMove;
				tabs.MouseLeave -= OnTabsMouseLeave;
				for(int i = 0; i < tabstyles.Length; i++) SetStyle(tabs, tabstyles[i], s.tabstyles[i]);
				if(s.updown != IntPtr.Zero) SetWindowTheme(s.updown, null, null);
				tabs.Invalidate();
			}

			LinkLabel link = c as LinkLabel;
			if(s.link && (link != null))
			{
				link.LinkColor = s.linkcolor;
				link.VisitedLinkColor = s.visitedcolor;
			}

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

			// (a button that took its background from the theme of Windows takes it again, and so does a
			// tab page: setting its color has made it forget that)
			if(s.button && (button != null) && s.buttonvisualback) button.UseVisualStyleBackColor = true;
			TabPage page = c as TabPage;
			if(s.tabpage && (page != null)) page.UseVisualStyleBackColor = true;

			// (the cells of the grids are put back when all the window is)
			DataGridView grid = c as DataGridView;
			if(s.grid && (grid != null))
			{
				grid.CellFormatting -= OnGridCellFormatting;
				grid.BackgroundColor = s.gridback;
				grid.GridColor = s.gridcolor;
			}

			if(s.painter != null) s.painter.Detach();
			if((s.wintheme != null) && c.IsHandleCreated) SetWindowTheme(c.Handle, null, null);
		}

		#endregion

		#region ================== Tabs

		private static void OnTabsPaint(object sender, PaintEventArgs e)
		{
			TabControl tabs = (TabControl)sender;
			Saved s;
			if(!saved.TryGetValue(tabs, out s)) return;
			Graphics g = e.Graphics;

			// Around the tabs: the background of what holds them
			Color around = Back;
			for(Control p = tabs.Parent; p != null; p = p.Parent) if(p.BackColor.A == 255) { around = p.BackColor; break; }
			using(SolidBrush brush = new SolidBrush(around)) g.FillRectangle(brush, tabs.ClientRectangle);

			// The frame of the pages, under the rows of tabs
			int top = 0;
			for(int i = 0; i < tabs.TabCount; i++) top = Math.Max(top, tabs.GetTabRect(i).Bottom);
			Rectangle frame = new Rectangle(0, top, tabs.Width, tabs.Height - top);
			using(SolidBrush brush = new SolidBrush(Back)) g.FillRectangle(brush, frame);
			using(Pen pen = new Pen(Border)) g.DrawRectangle(pen, frame.X, frame.Y, frame.Width - 1, frame.Height - 1);

			// The tabs, the selected one last, a little larger and open on its page
			for(int i = 0; i < tabs.TabCount; i++) if(i != tabs.SelectedIndex) DrawTab(g, tabs, s, i, false);
			if((tabs.SelectedIndex >= 0) && (tabs.SelectedIndex < tabs.TabCount)) DrawTab(g, tabs, s, tabs.SelectedIndex, true);

			// The arrows that scroll the tabs when they do not fit get the dark look of Windows
			if(tabs.IsHandleCreated)
			{
				IntPtr updown = FindWindowEx(tabs.Handle, IntPtr.Zero, "msctls_updown32", null);
				if((updown != IntPtr.Zero) && (updown != s.updown))
				{
					s.updown = updown;
					SetWindowTheme(updown, "DarkMode_Explorer", null);
				}
			}
		}

		private static void DrawTab(Graphics g, TabControl tabs, Saved s, int index, bool selected)
		{
			Rectangle r = tabs.GetTabRect(index);
			if(selected) { r.Inflate(2, 0); r.Y -= 2; r.Height += 3; }
			Color back = (selected ? Back : (index == s.hottab ? Hover : MenuBack));
			using(SolidBrush brush = new SolidBrush(back)) g.FillRectangle(brush, r);
			using(Pen pen = new Pen(Border))
			{
				g.DrawLine(pen, r.Left, r.Bottom - 1, r.Left, r.Top);
				g.DrawLine(pen, r.Left, r.Top, r.Right - 1, r.Top);
				g.DrawLine(pen, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
			}

			// Picture and text
			TabPage page = tabs.TabPages[index];
			Rectangle text = r;
			Image image = null;
			if(tabs.ImageList != null)
			{
				if((page.ImageIndex >= 0) && (page.ImageIndex < tabs.ImageList.Images.Count)) image = tabs.ImageList.Images[page.ImageIndex];
				else if(!string.IsNullOrEmpty(page.ImageKey) && tabs.ImageList.Images.ContainsKey(page.ImageKey)) image = tabs.ImageList.Images[page.ImageKey];
			}
			if(image != null)
			{
				g.DrawImage(image, r.X + 6, r.Y + (r.Height - image.Height) / 2, image.Width, image.Height);
				text = new Rectangle(r.X + 6 + image.Width, r.Y, r.Width - 6 - image.Width, r.Height);
			}
			TextRenderer.DrawText(g, page.Text, tabs.Font, text, (tabs.Enabled ? Text : DimText),
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
		}

		private static void OnTabsMouseMove(object sender, MouseEventArgs e)
		{
			TabControl tabs = (TabControl)sender;
			Saved s;
			if(!saved.TryGetValue(tabs, out s)) return;
			int hot = -1;
			for(int i = 0; i < tabs.TabCount; i++) if(tabs.GetTabRect(i).Contains(e.Location)) hot = i;
			if(hot != s.hottab) { s.hottab = hot; tabs.Invalidate(); }
		}

		private static void OnTabsMouseLeave(object sender, EventArgs e)
		{
			TabControl tabs = (TabControl)sender;
			Saved s;
			if(saved.TryGetValue(tabs, out s) && (s.hottab != -1)) { s.hottab = -1; tabs.Invalidate(); }
		}

		#endregion

		#region ================== Lists and grids

		// A list asks the colors of each item before drawing it, and Windows Forms answers with the colors
		// that the code gave the item. This changes the answer when it is a color of the system to the dark
		// one, without changing the items (some code reads their colors back).
		internal class ListPainter : NativeWindow
		{
			private ListView list;

			internal void Attach(ListView list)
			{
				this.list = list;
				if(this.Handle == IntPtr.Zero) AssignHandle(list.Handle);
				IntPtr header = SendMessage(list.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
				if(header != IntPtr.Zero) SetWindowTheme(header, "DarkMode_ItemsView", null);
			}

			internal void Detach()
			{
				if(this.Handle != IntPtr.Zero) ReleaseHandle();
				if((list != null) && list.IsHandleCreated)
				{
					IntPtr header = SendMessage(list.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
					if(header != IntPtr.Zero) SetWindowTheme(header, null, null);
					list.Invalidate();
				}
			}

			protected override void WndProc(ref Message m)
			{
				base.WndProc(ref m);
				if((m.Msg != WM_REFLECT_NOTIFY) || !active || (m.LParam == IntPtr.Zero)) return;
				if(Marshal.ReadInt32(m.LParam, codeoffset) != NM_CUSTOMDRAW) return;
				if((Marshal.ReadInt32(m.LParam, stageoffset) & CDDS_ITEMPREPAINT) != CDDS_ITEMPREPAINT) return;
				int text = Marshal.ReadInt32(m.LParam, textoffset);
				int back = Marshal.ReadInt32(m.LParam, textbackoffset);
				int newtext = MapColorRef(text, true);
				int newback = MapColorRef(back, false);
				if(newtext != text) Marshal.WriteInt32(m.LParam, textoffset, newtext);
				if(newback != back) Marshal.WriteInt32(m.LParam, textbackoffset, newback);

				// (Windows draws the selected item of a list that shows its selection without the focus in a
				// light gray: it is drawn here in a dark blue. The state given with the notification is not
				// reliable for that: the item tells.)
				if((list == null) || list.Focused || list.HideSelection || list.VirtualMode) return;
				int index = Marshal.ReadIntPtr(m.LParam, itemoffset).ToInt32();
				if((index < 0) || (index >= list.Items.Count) || !list.Items[index].Selected) return;
				int state = Marshal.ReadInt32(m.LParam, stateoffset);
				Marshal.WriteInt32(m.LParam, stateoffset, state & ~CDIS_SELECTED);
				Marshal.WriteInt32(m.LParam, textoffset, ColorTranslator.ToWin32(Text));
				Marshal.WriteInt32(m.LParam, textbackoffset, ColorTranslator.ToWin32(Checked));
			}

			private static int MapColorRef(int colorref, bool text)
			{
				KnownColor[] colors = (text ? new[] { KnownColor.WindowText, KnownColor.ControlText, KnownColor.GrayText, KnownColor.InactiveCaptionText, KnownColor.HotTrack }
											: new[] { KnownColor.Window, KnownColor.Control, KnownColor.InactiveBorder });
				foreach(KnownColor k in colors)
				{
					Color system = Color.FromKnownColor(k);
					if(ColorTranslator.ToWin32(system) == colorref)
						return ColorTranslator.ToWin32(text ? MapFore(system) : (k == KnownColor.Window ? Field : Back));
				}
				return colorref;
			}
		}

		// The cells of a grid take the text color of the grid when it changes, and the grid the one of what
		// holds it: the colors of the cells are kept before anything of a window changes, and put back after
		private static void KeepGrids(Control c)
		{
			DataGridView grid = c as DataGridView;
			GridColors k;
			if((grid != null) && !grids.TryGetValue(grid, out k))
			{
				k = new GridColors();
				k.cellback = grid.DefaultCellStyle.BackColor;
				k.cellfore = grid.DefaultCellStyle.ForeColor;
				k.cellselback = grid.DefaultCellStyle.SelectionBackColor;
				k.headers = grid.EnableHeadersVisualStyles;
				k.headerback = grid.ColumnHeadersDefaultCellStyle.BackColor;
				k.headerfore = grid.ColumnHeadersDefaultCellStyle.ForeColor;
				k.rowheaderback = grid.RowHeadersDefaultCellStyle.BackColor;
				k.rowheaderfore = grid.RowHeadersDefaultCellStyle.ForeColor;
				grids.Add(grid, k);
			}
			foreach(Control child in c.Controls) KeepGrids(child);
		}

		private static void PutBackGrids(Control c)
		{
			DataGridView grid = c as DataGridView;
			GridColors k;
			if((grid != null) && grids.TryGetValue(grid, out k))
			{
				grid.DefaultCellStyle.BackColor = k.cellback;
				grid.DefaultCellStyle.ForeColor = k.cellfore;
				grid.DefaultCellStyle.SelectionBackColor = k.cellselback;
				grid.EnableHeadersVisualStyles = k.headers;
				grid.ColumnHeadersDefaultCellStyle.BackColor = k.headerback;
				grid.ColumnHeadersDefaultCellStyle.ForeColor = k.headerfore;
				grid.RowHeadersDefaultCellStyle.BackColor = k.rowheaderback;
				grid.RowHeadersDefaultCellStyle.ForeColor = k.rowheaderfore;
				grids.Remove(grid);
			}
			foreach(Control child in c.Controls) PutBackGrids(child);
		}

		private static void OnGridCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
		{
			if(!active || (e.CellStyle == null)) return;
			e.CellStyle.ForeColor = MapFore(e.CellStyle.ForeColor);
			if(e.CellStyle.BackColor.IsSystemColor) e.CellStyle.BackColor = MapBack(null, e.CellStyle.BackColor);
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
