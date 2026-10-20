#region ======================== Namespaces

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using SharpCompress.Archives;
using SharpCompress.Readers;

#endregion

namespace mxd.GZDBUpdater
{
	// styd: the updater of Doom Builder 64 II. The editor starts it from a folder of its own (so that it can replace
	// the Updater.exe of the editor too) with the folder of the editor and the release to install:
	//   -app <folder> -version <version> -url <file of the release> -size <bytes> -page <page of the release>
	// It downloads the file, unpacks it, has the editor closed, installs the files (the replaced files are kept
	// aside, and put back if anything fails) and starts the editor again.
	public partial class MainForm : Form
	{
		#region ======================== Variables

		private const string processToEnd = "Builder";
		private const string appFileName = "Builder.exe";
		private string appFolder = string.Empty;
		private string URL = string.Empty; // The page of the release, to download it manually
		private string packageUrl = string.Empty;
		private long packageSize;
		private Version version;
		private readonly string updateFolder = Path.Combine(Application.StartupPath, "_update") + "\\";
		private static BackgroundWorker worker;
		private static bool appclosing;
		private static bool installing;
		private static bool keepbackup;
		private static MainForm me;
		private IntPtr handle;
		private const string MESSAGEBOX_TITLE = "Doom Builder 64 II Updater";

		#endregion

		#region ======================== Delegates

		private delegate void SetLabelCallback(Label label, string text);
		private delegate void UpdateProgressBarCallback(ByteArgs args, int step, int totalsteps);
		private delegate void CloseDelegate();
		private delegate DialogResult AskDelegate();

		#endregion

		#region ======================== Properties

		public static string ErrorDescription;
		public static bool AppClosing { get { return appclosing; } }
		public static Icon AppIcon { get { return me.Icon; } }

		private string PackageFile { get { return updateFolder + "package" + (packageUrl.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ? ".7z" : ".zip"); } }
		private string StagingFolder { get { return updateFolder + "files\\"; } }
		private string BackupFolder { get { return updateFolder + "backup\\"; } }

		#endregion

		#region ======================== Constructor

		public MainForm()
		{
			if(!ReadArguments(Environment.GetCommandLineArgs()))
			{
				InvokeClose();
			}
			else if(!CheckPremissions(appFolder))
			{
				ErrorDescription = "Update failed: your user account does not have write access to the destination folder \"" + appFolder + "\"\n\nMove the editor to a folder with write access,\nor run the editor as Administrator.";
				InvokeClose();
			}
			else
			{
				me = this;
				InitializeComponent();
			}
		}

		#endregion

		#region ======================== Updater thread

		private void BackgroundWorker(object sender, DoWorkEventArgs e)
		{
			UpdateLabel(label1, "1/6: Checking version...");
			if(!UpdateRequired())
			{
				e.Cancel = true;
				return;
			}
			PreDownload();

			UpdateLabel(label1, "2/6: Downloading Update...");
			Webdata.BytesDownloaded += WebdataOnBytesDownloaded;
			bool downloaded = Webdata.SaveWebFile(packageUrl, packageSize, PackageFile);
			Webdata.BytesDownloaded -= WebdataOnBytesDownloaded;
			if(!downloaded)
			{
				e.Cancel = true;
				return;
			}

			// The package is checked and unpacked before the editor is closed
			UpdateLabel(label1, "3/6: Decompressing package...");
			Thread.Sleep(500);
			if(!Unpack(PackageFile, StagingFolder))
			{
				e.Cancel = true;
				return;
			}

			// Check if the editor is running...
			if(!EditorClosed())
			{
				// Error or user canceled
				e.Cancel = true;
				return;
			}

			UpdateLabel(label1, "5/6: Moving files...");
			Thread.Sleep(500);
			installing = true;
			bool moved = MoveFiles(StagingFolder);
			installing = false;
			if(!moved)
			{
				e.Cancel = true;
				return;
			}

			UpdateLabel(label1, "6/6: Wrapping up...");
			Thread.Sleep(500);
			PostDownload();
		}

		private bool EditorClosed()
		{
			try
			{
				while(EditorProcess.GetProcesses(processToEnd, appFolder).Count > 0)
				{
					// Ask the user how to proceed...
					TaskbarProgress.SetState(handle, TaskbarProgress.TaskbarStates.Paused);
					DialogResult result = (DialogResult)this.Invoke(new AskDelegate(AskToCloseEditor));
					TaskbarProgress.SetState(handle, TaskbarProgress.TaskbarStates.Normal);
					if(result != DialogResult.OK || appclosing) return false;

					// styd: the editor is closed as with its close button: it asks to save the map. It is never killed.
					UpdateLabel(label1, "4/6: Closing the editor...");
					if(EditorProcess.CloseAndWait(processToEnd, appFolder, EditorProcess.CLOSE_TIMEOUT, () => appclosing)) break;
					if(appclosing) return false;

					// Still running: the closing was canceled in the editor. Ask again.
				}
			}
			catch(Exception ex)
			{
				ErrorDescription = "Failed to stop the editor process...\n" + ex.Message;
				return false;
			}

			return true;
		}

		private DialogResult AskToCloseEditor()
		{
			using(UpdateBlockedForm form = new UpdateBlockedForm())
			{
				return form.ShowDialog(this);
			}
		}

		private static void StopBackgroundWorker()
		{
			if(worker != null && !worker.CancellationPending)
			{
				me.UpdateLabel(me.label1, "Stopping Background Thread...");
				worker.CancelAsync();
				while(worker.IsBusy) Application.DoEvents();
			}
		}

		private void WorkerOnRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			InvokeClose();
		}

		#endregion

		#region ======================== Methods

		private void UpdateLabel(Label label, string text)
		{
			if(label.InvokeRequired)
			{
				SetLabelCallback d = UpdateLabel;
				label.Invoke(d, new object[] { label, text });
			}
			else
			{
				label.Text = text;
				label.Refresh();
				Invalidate();
			}
		}

		private void InvokeClose()
		{
			if(this.Disposing || this.IsDisposed) return;
			if(this.InvokeRequired)
			{
				CloseDelegate d = Close;
				this.Invoke(d);
			}
			else
			{
				if(!appclosing && !string.IsNullOrEmpty(ErrorDescription))
				{
					if(!string.IsNullOrEmpty(URL))
					{
						ErrorDescription += Environment.NewLine + Environment.NewLine + "Would you like to download the update manually?";
						TaskbarProgress.SetState(this.Handle, TaskbarProgress.TaskbarStates.Error);
						if(MessageBox.Show(this, ErrorDescription, MESSAGEBOX_TITLE, MessageBoxButtons.YesNo) == DialogResult.Yes)
							Process.Start(URL);
					}
					else
					{
						MessageBox.Show(this, ErrorDescription, MESSAGEBOX_TITLE, MessageBoxButtons.OK);
					}
				}

				WrapUp();
				Close();
			}
		}

		// The editor gives the folder of the editor and the release to install
		private bool ReadArguments(string[] args)
		{
			Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			for(int i = 1; i + 1 < args.Length; i += 2) values[args[i]] = args[i + 1];

			string value;
			if(values.TryGetValue("-app", out value)) appFolder = value.TrimEnd('\\');
			if(values.TryGetValue("-url", out value)) packageUrl = value;
			if(values.TryGetValue("-page", out value)) URL = value;
			if(values.TryGetValue("-size", out value)) long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out packageSize);
			if(values.TryGetValue("-version", out value)) version = EditorProcess.ParseVersion(value);

			// Sanity cheks
			if(string.IsNullOrEmpty(appFolder) || string.IsNullOrEmpty(packageUrl) || version == null)
			{
				URL = string.Empty;
				ErrorDescription = "The updater is started by Doom Builder 64 II (Help -> Check for updates...).";
				return false;
			}

			if(!File.Exists(Path.Combine(appFolder, appFileName)))
			{
				ErrorDescription = "Unable to locate '" + appFileName + "' in \"" + appFolder + "\"...";
				return false;
			}

			return true;
		}

		private bool UpdateRequired()
		{
			// Get local version (styd: the version of Doom Builder 64 II; an older editor only has a revision)
			FileVersionInfo info = FileVersionInfo.GetVersionInfo(Path.Combine(appFolder, appFileName));
			Version localversion = (info.ProductVersion != info.FileVersion ? EditorProcess.ParseVersion(info.ProductVersion) : null);

			if(localversion != null && version <= localversion)
			{
				URL = string.Empty;
				ErrorDescription = "Your version is up to date!";
				return false;
			}

			return true;
		}

		// Unpacks the files of the folder of the package that holds Builder.exe
		private bool Unpack(string file, string unZipTo)
		{
			try
			{
				if(Directory.Exists(unZipTo)) Directory.Delete(unZipTo, true);
				Directory.CreateDirectory(unZipTo);

				if(file.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
				{
					using(IArchive arc = ArchiveFactory.Open(file))
					{
						if(!arc.IsComplete)
						{
							ErrorDescription = "Update failed: downloaded file is not complete...";
							return false;
						}

						// Find the folder of the editor
						List<string> keys = new List<string>();
						IReader reader = arc.ExtractAllEntries();
						while(reader.MoveToNextEntry()) keys.Add((reader.Entry.IsDirectory ? reader.Entry.Key.TrimEnd('/', '\\') + "/" : reader.Entry.Key));
						string root = FindRoot(keys);
						if(root == null) return false;

						// Unpack it
						int curentry = 0;
						reader = arc.ExtractAllEntries();
						while(reader.MoveToNextEntry())
						{
							if(appclosing) return false;
							string target = TargetPath(reader.Entry.Key, root, unZipTo, reader.Entry.IsDirectory);
							if(target == null) continue;
							if(!reader.Entry.IsDirectory)
							{
								using(FileStream stream = File.Create(target)) reader.WriteEntryTo(stream);
							}
							UpdateProgressBar(new ByteArgs { Downloaded = curentry++, Total = keys.Count }, 1, 2);
						}
					}
				}
				else
				{
					using(ZipArchive arc = new ZipArchive(File.OpenRead(file), ZipArchiveMode.Read))
					{
						List<string> keys = new List<string>();
						foreach(ZipArchiveEntry entry in arc.Entries) keys.Add(entry.FullName);
						string root = FindRoot(keys);
						if(root == null) return false;

						int curentry = 0;
						foreach(ZipArchiveEntry entry in arc.Entries)
						{
							if(appclosing) return false;
							bool isdirectory = entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\");
							string target = TargetPath(entry.FullName, root, unZipTo, isdirectory);
							if(target == null) continue;
							if(!isdirectory)
							{
								using(Stream source = entry.Open())
								using(FileStream stream = File.Create(target))
									source.CopyTo(stream);
							}
							UpdateProgressBar(new ByteArgs { Downloaded = curentry++, Total = keys.Count }, 1, 2);
						}
					}
				}
			}
			catch(Exception e)
			{
				ErrorDescription = "Update failed: failed to unpack the update...\n" + e.Message;
				return false;
			}

			return true;
		}

		// The folder of the package that holds Builder.exe ("" when it is at the top), or null when there is none
		private static string FindRoot(List<string> keys)
		{
			string root = null;
			foreach(string key in keys)
			{
				string path = key.Replace('\\', '/');
				int slash = path.LastIndexOf('/');
				if(!string.Equals(path.Substring(slash + 1), appFileName, StringComparison.OrdinalIgnoreCase)) continue;

				string folder = path.Substring(0, slash + 1);
				if(root == null || folder.Length < root.Length) root = folder;
			}

			if(root == null) ErrorDescription = "Update failed: the downloaded file is not a package of Doom Builder 64 II (there is no " + appFileName + " in it)...";
			return root;
		}

		// Where an entry of the package goes (its folders are made), or null when it is not in the folder of the editor
		private static string TargetPath(string key, string root, string folder, bool isdirectory)
		{
			string path = key.Replace('\\', '/');
			if(!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;
			string relative = path.Substring(root.Length).Trim('/');
			if(relative.Length == 0) return null;

			// Nothing may go outside of the folder
			foreach(string part in relative.Split('/'))
			{
				if(part.Length == 0 || part == "." || part == ".." || part.IndexOf(':') != -1)
					throw new InvalidDataException("invalid path in the package: \"" + key + "\"");
			}

			string target = Path.Combine(folder, relative.Replace('/', '\\'));
			Directory.CreateDirectory(isdirectory ? target : Path.GetDirectoryName(target));
			return target;
		}

		private static bool CheckPremissions(string path)
		{
			// styd: a file is made and removed (the rules of a folder do not tell everything)
			try
			{
				string file = Path.Combine(path, "_updater_write_test.tmp");
				using(File.Create(file)) { }
				File.Delete(file);
				return true;
			}
			catch(Exception)
			{
				return false;
			}
		}

		private void PreDownload()
		{
			if(!Directory.Exists(updateFolder)) Directory.CreateDirectory(updateFolder);
		}

		private void PostDownload()
		{
			string file = Path.Combine(appFolder, appFileName);
			if(!File.Exists(file))
			{
				ErrorDescription = "Unable to located updated executable ('" + file + "')";
				return;
			}

			if(appclosing) return;
			Process.Start(new ProcessStartInfo { FileName = file, WorkingDirectory = appFolder });
		}

		private void WrapUp()
		{
			try
			{
				if(keepbackup)
				{
					// The files that could not be put back are still there: only the rest goes
					if(Directory.Exists(StagingFolder)) Directory.Delete(StagingFolder, true);
					if(File.Exists(PackageFile)) File.Delete(PackageFile);
				}
				else if(Directory.Exists(updateFolder))
				{
					Directory.Delete(updateFolder, true);
				}
			}
			catch(Exception) { }
		}

		// Copies the unpacked files to the folder of the editor; the replaced files are kept, to put them back if anything fails
		private bool MoveFiles(string staging)
		{
			List<string> replaced = new List<string>();
			List<string> added = new List<string>();
			string backup = BackupFolder;

			try
			{
				string[] files = Directory.GetFiles(staging, "*", SearchOption.AllDirectories);
				for(int i = 0; i < files.Length; i++)
				{
					string relative = files[i].Substring(staging.Length);
					string target = Path.Combine(appFolder, relative);

					// The load order of the plugins is the user's (the installer does not replace it either)
					if(File.Exists(target) && string.Equals(relative, @"Plugins\Loadorder.cfg", StringComparison.OrdinalIgnoreCase)) continue;

					Directory.CreateDirectory(Path.GetDirectoryName(target));
					if(File.Exists(target))
					{
						string kept = Path.Combine(backup, relative);
						Directory.CreateDirectory(Path.GetDirectoryName(kept));
						File.Copy(target, kept, true);
						replaced.Add(relative);
					}
					else
					{
						added.Add(relative);
					}

					File.Copy(files[i], target, true);
					UpdateProgressBar(new ByteArgs { Downloaded = i, Total = files.Length }, 1, 2);
				}

				// Empty folders too
				foreach(string folder in Directory.GetDirectories(staging, "*", SearchOption.AllDirectories))
					Directory.CreateDirectory(Path.Combine(appFolder, folder.Substring(staging.Length)));
			}
			catch(Exception e)
			{
				// Put everything back
				List<string> failed = new List<string>();
				foreach(string relative in replaced)
				{
					try { File.Copy(Path.Combine(backup, relative), Path.Combine(appFolder, relative), true); }
					catch(Exception) { failed.Add(relative); }
				}
				foreach(string relative in added)
				{
					try { File.Delete(Path.Combine(appFolder, relative)); }
					catch(Exception) { }
				}

				ErrorDescription = "Update failed: failed to install the files of the update...\n" + e.Message;
				if(failed.Count == 0)
				{
					ErrorDescription += "\n\nThe editor was left as it was.";
				}
				else
				{
					keepbackup = true;
					ErrorDescription += "\n\n" + failed.Count + " file(s) of the editor could not be put back. They are kept in \"" + backup + "\":\n" + string.Join("\n", failed.GetRange(0, Math.Min(10, failed.Count)));
				}

				return false;
			}

			return true;
		}

		private void UpdateProgressBar(ByteArgs e, int step, int totalsteps)
		{
			if(progressbar.InvokeRequired)
			{
				UpdateProgressBarCallback d = UpdateProgressBar;
				progressbar.Invoke(d, new object[] { e, step, totalsteps });
			}
			else
			{
				if(e.Total <= 0) return;
				int stepsize = (int)Math.Round((float)progressbar.Maximum / totalsteps);
				float ratio = (float)e.Downloaded / e.Total;
				int val = (int)Math.Floor(stepsize * step + stepsize * ratio);

				if(val <= progressbar.Maximum)
				{
					progressbar.Value = val;
					TaskbarProgress.SetValue(this.Handle, progressbar.Value, progressbar.Maximum);
				}
				progressbar.Refresh();
				Invalidate();
			}
		}

		#endregion

		#region ======================== Events

		private void MainForm_Load(object sender, EventArgs e)
		{
			Version assemblyversion = Assembly.GetEntryAssembly().GetName().Version;
			this.Text += " v" + assemblyversion.Major + "." + assemblyversion.Revision.ToString("#00");
			handle = this.Handle;

			worker = new BackgroundWorker();
			worker.DoWork += BackgroundWorker;
			worker.RunWorkerCompleted += WorkerOnRunWorkerCompleted;
			worker.WorkerSupportsCancellation = true;
			worker.RunWorkerAsync();
		}

		private void cancel_Click(object sender, EventArgs e)
		{
			// styd: the files are being replaced: that ends first
			if(installing) return;

			ErrorDescription = string.Empty;
			appclosing = true;
			StopBackgroundWorker();
			InvokeClose();
		}

		private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			// styd: the files are being replaced: that ends first
			if(installing && e.CloseReason == CloseReason.UserClosing)
			{
				e.Cancel = true;
				return;
			}

			appclosing = true;
			StopBackgroundWorker();
		}

		private void WebdataOnBytesDownloaded(ByteArgs ba)
		{
			UpdateProgressBar(ba, 0, 2);
		}

		#endregion
	}
}
