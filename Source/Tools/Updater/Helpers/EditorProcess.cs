using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace mxd.GZDBUpdater
{
	// styd: the processes of the editor, and the versions of Doom Builder 64 II
	internal static class EditorProcess
	{
		// How long a closing editor may take before the user is asked again (the time does not run while a dialog
		// of the editor is open, such as the question about saving the map)
		internal const int CLOSE_TIMEOUT = 30000;

		[DllImport("user32.dll")]
		private static extern bool IsWindowEnabled(IntPtr hWnd);

		// The editors that run from the given folder
		internal static List<Process> GetProcesses(string name, string folder)
		{
			List<Process> result = new List<Process>();
			string fullfolder = Path.GetFullPath(folder).TrimEnd('\\');
			foreach(Process process in Process.GetProcessesByName(name))
			{
				try
				{
					if(string.Equals(Path.GetDirectoryName(process.MainModule.FileName), fullfolder, StringComparison.OrdinalIgnoreCase))
						result.Add(process);
				}
				catch(Exception) { } // A process of another user, or one that just ended
			}

			return result;
		}

		// Closes the editors as with their close button (each one asks to save its map) and waits for them to end.
		// Returns false when one still runs: its closing was canceled, it took too long, or the wait was canceled.
		internal static bool CloseAndWait(string name, string folder, int timeout, Func<bool> canceled)
		{
			foreach(Process process in GetProcesses(name, folder))
			{
				try { process.CloseMainWindow(); }
				catch(Exception) { }
			}

			int waited = 0;
			while(!canceled())
			{
				List<Process> running = GetProcesses(name, folder);
				if(running.Count == 0) return true;

				// A dialog of the editor is open (its main window is disabled): the user decides, keep waiting
				bool dialogopen = false;
				foreach(Process process in running)
				{
					try
					{
						process.Refresh();
						IntPtr window = process.MainWindowHandle;
						if(window != IntPtr.Zero && !IsWindowEnabled(window)) dialogopen = true;
					}
					catch(Exception) { }
				}

				waited = (dialogopen ? 0 : waited + 100);
				if(waited >= timeout) return false;
				Thread.Sleep(100);
			}

			return false;
		}

		// "V1.2.0", "v1.2" or "1.2.0-beta" give 1.2.0.0; null when there is no version number (as in the editor)
		internal static Version ParseVersion(string text)
		{
			if(string.IsNullOrEmpty(text)) return null;
			text = text.Trim();
			if(text.StartsWith("v", StringComparison.OrdinalIgnoreCase)) text = text.Substring(1);

			int end = 0;
			while(end < text.Length && ((text[end] >= '0' && text[end] <= '9') || text[end] == '.')) end++;

			string[] parts = text.Substring(0, end).Split('.');
			if(parts.Length > 4) return null;

			int[] numbers = new int[4];
			for(int i = 0; i < parts.Length; i++)
			{
				if(!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i])) return null;
			}

			return new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
		}
	}
}
