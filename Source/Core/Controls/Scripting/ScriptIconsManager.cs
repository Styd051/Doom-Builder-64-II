using System;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Config;

namespace CodeImp.DoomBuilder.Controls.Scripting
{
	internal class ScriptIconsManager
	{
		private int scripttypeiconsoffset;
		private int scriptgroupiconsoffset;
		private int scriptgroupopeniconsoffset;
		private int doom64iconsoffset; // styd
		private ImageList icons;

		internal int ScriptTypeIconsOffset { get { return scripttypeiconsoffset; } }
		internal int ScriptGroupIconsOffset { get { return scriptgroupiconsoffset; } }
		internal int ScriptGroupOpenIconsOffset { get { return scriptgroupopeniconsoffset; } }
		public ImageList Icons { get { return icons; } }

		public ScriptIconsManager(ImageList icons)
		{
			this.icons = icons;

			int numicons = Enum.GetNames(typeof(ScriptType)).Length;
			numicons--; // styd. All but SKYDEFS, the script type of Doom 64: the list of the window does not have its pictures
			scriptgroupopeniconsoffset = icons.Images.Count - numicons;
			scriptgroupiconsoffset = scriptgroupopeniconsoffset - numicons;
			scripttypeiconsoffset = scriptgroupiconsoffset - numicons;

			// styd. The pictures of SKYDEFS come after all the others: lump, group, open group
			doom64iconsoffset = icons.Images.Count;
			icons.Images.Add(Properties.Resources.ScriptSKYDEFS);
			icons.Images.Add(Properties.Resources.GroupSKYDEFS);
			icons.Images.Add(Properties.Resources.OpenGroupSKYDEFS);
		}

		public int GetResourceIcon(int datalocationtype)
		{
			return datalocationtype;
		}

		public int GetScriptIcon(ScriptType type)
		{
			if(type == ScriptType.SKYDEFS) return doom64iconsoffset; // styd
			int scripttype = (int)type + scripttypeiconsoffset;
			if(scripttype >= scriptgroupiconsoffset) scripttype = scripttypeiconsoffset;
			return scripttype;
		}

		public int GetScriptFolderIcon(ScriptType type, bool opened)
		{
			if(type == ScriptType.SKYDEFS) return doom64iconsoffset + (opened ? 2 : 1); // styd
			int scripttype = (int)type;
			if(scripttype >= scriptgroupiconsoffset - scripttypeiconsoffset)
				scripttype = scripttypeiconsoffset;

			if(opened) return scriptgroupopeniconsoffset + scripttype;
			return scriptgroupiconsoffset + scripttype;
		}
	}
}
