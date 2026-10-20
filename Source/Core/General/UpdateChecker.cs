#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

#endregion

namespace CodeImp.DoomBuilder
{
	// styd: a version of Doom Builder 64 II published on GitHub, newer than this editor
	internal class UpdateInfo
	{
		public Version Version;		// The version of the release to install
		public string Notes;		// RTF text: the notes of every release newer than this editor
		public string PageUrl;		// The page of the release on GitHub
		public string PackageUrl;	// Its .zip (or .7z) file
		public long PackageSize;	// The size of that file in bytes (0 when unknown)
	}

	// styd: the update checker of GZDoom Builder (mxd), which read the revisions of GZDoom Builder on its own server.
	// Doom Builder 64 II reads its releases on GitHub instead: the tag of a release is its version (V1.1.0), compared
	// with the version of this editor (AssemblyInformationalVersion in Properties\AssemblyInfo.cs).
	internal static class UpdateChecker
	{
		#region ================== Constants

		private const string NO_UPDATE_REQUIRED = "Your version is up to date.";
		private const string UPDATER_FOLDER = "Doom Builder 64 II Update";

		#endregion

		#region ================== Variables

		private delegate DialogResult ShowWarningMessageDelegate(string message, MessageBoxButtons buttons);

		// The releases of Doom Builder 64 II, newest first (not a constant: the tests give a file of their own)
		private static string releasesurl = "https://api.github.com/repos/Styd051/Doom-Builder-64-II/releases?per_page=100";

		private static BackgroundWorker worker;
		private static bool verbose;

		#endregion

		#region ================== Properties

		// The version of this editor
		internal static Version CurrentVersion { get { return ParseVersion(Application.ProductVersion); } }

		// The folder from which the updater runs, so that it can replace the Updater.exe of the editor too
		internal static string UpdaterFolder { get { return Path.Combine(Path.GetTempPath(), UPDATER_FOLDER); } }

		#endregion

		#region ================== Check

		internal static void PerformCheck(bool verbosemode)
		{
			// Update check already runing?
			if(worker != null && worker.IsBusy)
			{
				if(verbosemode) General.ShowWarningMessage("Update check is already running!", MessageBoxButtons.OK);
				return;
			}

			// Check if we have write access...
			if(!General.CheckWritePremissions(General.AppPath))
			{
				string msg = "Cannot perform update: your user account does not have write access to the destination folder \"" + General.AppPath + "\"\n"
					+ "Move the editor to a folder with write access, or run it as Administrator.";

				if(verbosemode) General.ShowWarningMessage(msg, MessageBoxButtons.OK);
				else General.ErrorLogger.Add(ErrorType.Error, msg);
				return;
			}

			// Start checking
			verbose = verbosemode;
			worker = new BackgroundWorker();
			worker.DoWork += DoWork;
			worker.RunWorkerCompleted += RunWorkerCompleted;
			worker.WorkerSupportsCancellation = true;
			worker.RunWorkerAsync();
		}

		private static void DoWork(object sender, DoWorkEventArgs e)
		{
			string updaterpath = Path.Combine(General.AppPath, "Updater.exe");
			if(!File.Exists(updaterpath))
			{
				ShowResult("Update check failed: \"" + updaterpath + "\" does not exist!");
				e.Cancel = true;
				return;
			}

			// The updater of an earlier update may still run; otherwise its folder is removed
			string result = CleanUpdaterFolder();
			if(!string.IsNullOrEmpty(result))
			{
				ShowResult("Update check failed: " + result);
				e.Cancel = true;
				return;
			}

			// Get local version
			Version localversion = CurrentVersion;
			if(localversion == null)
			{
				ShowResult("Update check failed: the version of the editor (\"" + Application.ProductVersion + "\") is not a version number.");
				e.Cancel = true;
				return;
			}

			// Get the releases
			string json = DownloadText(releasesurl);
			if(json == null)
			{
				ShowResult("Update check failed: failed to retrieve the releases of " + Application.ProductName + ".\nCheck your Internet connection and try again.");
				e.Cancel = true;
				return;
			}

			UpdateInfo info;
			try
			{
				info = FindUpdate(json, localversion);
			}
			catch(Exception ex)
			{
				ShowResult("Update check failed: invalid release information (" + ex.Message + ").");
				e.Cancel = true;
				return;
			}

			if(info == null)
			{
				General.WriteLogLine("Update check: version " + VersionText(localversion) + " is up to date.");
				if(verbose) ShowResult(NO_UPDATE_REQUIRED);
				return;
			}

			General.WriteLogLine("Update check: version " + VersionText(info.Version) + " is available (this is version " + VersionText(localversion) + ").");

			// An ignored update is only shown when the check is asked for
			if(!verbose && IsIgnored(info.Version)) return;

			// Pass data to MainForm
			General.MainWindow.UpdateAvailable(info);
		}

		// Finds the newest release that is newer than the given version and has a file to install.
		// Returns null when there is none.
		internal static UpdateInfo FindUpdate(string json, Version localversion)
		{
			JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
			object[] releases = serializer.DeserializeObject(json) as object[];
			if(releases == null) throw new InvalidDataException("not a list of releases");

			// The published releases newer than this editor (GitHub lists drafts to the owner only, but who knows...)
			List<KeyValuePair<Version, Dictionary<string, object>>> newer = new List<KeyValuePair<Version, Dictionary<string, object>>>();
			foreach(object o in releases)
			{
				Dictionary<string, object> release = o as Dictionary<string, object>;
				if(release == null || GetBool(release, "draft") || GetBool(release, "prerelease")) continue;
				Version version = ParseVersion(GetString(release, "tag_name"));
				if(version == null || version <= localversion) continue;
				newer.Add(new KeyValuePair<Version, Dictionary<string, object>>(version, release));
			}

			// Newest first
			newer.Sort((a, b) => b.Key.CompareTo(a.Key));

			// The newest release with a file to install is the update
			UpdateInfo info = null;
			foreach(KeyValuePair<Version, Dictionary<string, object>> pair in newer)
			{
				Dictionary<string, object> package = FindPackage(pair.Value);
				if(package == null)
				{
					General.WriteLogLine("Update check: release " + GetString(pair.Value, "tag_name") + " has no .zip file, skipped.");
					continue;
				}

				info = new UpdateInfo
				{
					Version = pair.Key,
					PageUrl = GetString(pair.Value, "html_url"),
					PackageUrl = GetString(package, "browser_download_url"),
					PackageSize = GetLong(package, "size")
				};
				break;
			}

			if(info == null || string.IsNullOrEmpty(info.PackageUrl)) return null;

			// The notes of the releases newer than this editor, up to the update (newest first)
			StringBuilder sb = new StringBuilder(@"{\rtf1");
			foreach(KeyValuePair<Version, Dictionary<string, object>> pair in newer)
			{
				if(pair.Key > info.Version) continue;

				sb.Append(@"{\b ").Append(Rtf(GetString(pair.Value, "tag_name")));
				string date = GetString(pair.Value, "published_at");
				if(date.Length >= 10) sb.Append(" | ").Append(Rtf(date.Substring(0, 10)));
				sb.Append(@":}\par ");

				string notes = GetString(pair.Value, "body").Trim();
				if(notes.Length > 0) sb.Append(Rtf(notes)).Append(@"\par ");
				sb.Append(@"\par ");
			}

			sb.Append("}");
			info.Notes = sb.ToString();
			return info;
		}

		// True when the user chose to ignore this version (or a newer one) in the window of an update
		internal static bool IsIgnored(Version version)
		{
			Version ignored = ParseVersion(General.Settings.IgnoredUpdateVersion);
			return (ignored != null && version <= ignored);
		}

		// The file to install: the first .zip of the release, or else its first .7z
		private static Dictionary<string, object> FindPackage(Dictionary<string, object> release)
		{
			object[] assets = (release.ContainsKey("assets") ? release["assets"] as object[] : null);
			if(assets == null) return null;

			foreach(string extension in new[] { ".zip", ".7z" })
			{
				foreach(object o in assets)
				{
					Dictionary<string, object> asset = o as Dictionary<string, object>;
					if(asset != null && GetString(asset, "name").EndsWith(extension, StringComparison.OrdinalIgnoreCase)) return asset;
				}
			}

			return null;
		}

		private static void ShowResult(string message)
		{
			if(!string.IsNullOrEmpty(message))
			{
				if(verbose)
				{
					if(General.MainWindow.InvokeRequired)
						General.MainWindow.Invoke(new ShowWarningMessageDelegate(General.ShowWarningMessage), new object[] { message, MessageBoxButtons.OK });
					else
						General.ShowWarningMessage(message, MessageBoxButtons.OK);
				}
				else if(message != NO_UPDATE_REQUIRED)
				{
					General.ErrorLogger.Add(ErrorType.Error, message);
				}
			}
		}

		private static void RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			worker = null;
		}

		#endregion

		#region ================== Updater

		// Starts the updater from a folder of its own; it downloads and installs the update, then starts the editor again
		internal static bool StartUpdater(UpdateInfo info)
		{
			try
			{
				string folder = UpdaterFolder;
				Directory.CreateDirectory(folder);
				foreach(string name in new[] { "Updater.exe", "Updater.exe.config" })
				{
					string source = Path.Combine(General.AppPath, name);
					if(File.Exists(source)) File.Copy(source, Path.Combine(folder, name), true);
				}

				string args = "-app " + Quote(General.AppPath)
					+ " -version " + VersionText(info.Version)
					+ " -url " + Quote(info.PackageUrl)
					+ " -size " + info.PackageSize.ToString(CultureInfo.InvariantCulture)
					+ " -page " + Quote(info.PageUrl);

				// Working directory must be set
				Process.Start(new ProcessStartInfo { WorkingDirectory = folder, FileName = Path.Combine(folder, "Updater.exe"), Arguments = args, UseShellExecute = false });
				return true;
			}
			catch(Exception e)
			{
				General.ShowWarningMessage("Failed to start the updater: " + e.Message, MessageBoxButtons.OK);
				return false;
			}
		}

		// Removes the folder of the updater, unless the updater runs from it
		private static string CleanUpdaterFolder()
		{
			string folder = UpdaterFolder;
			if(!Directory.Exists(folder)) return string.Empty;

			try
			{
				string fullfolder = Path.GetFullPath(folder).TrimEnd('\\');
				foreach(Process process in Process.GetProcessesByName("Updater"))
				{
					string path;
					try { path = Path.GetDirectoryName(process.MainModule.FileName); }
					catch(Exception) { continue; }

					if(string.Equals(Path.GetFullPath(path).TrimEnd('\\'), fullfolder, StringComparison.OrdinalIgnoreCase))
						return "Updater.exe is already running.";
				}

				Directory.Delete(folder, true);
			}
			catch(Exception e)
			{
				General.WriteLogLine("Update check: failed to remove \"" + folder + "\": " + e.Message);
			}

			return string.Empty;
		}

		// A path or an address on the command line (a backslash before the closing quote would escape it)
		private static string Quote(string text)
		{
			int backslashes = 0;
			while(backslashes < text.Length && text[text.Length - 1 - backslashes] == '\\') backslashes++;
			return "\"" + text + new string('\\', backslashes) + "\"";
		}

		#endregion

		#region ================== Versions

		// "V1.2.0", "v1.2" or "1.2.0-beta" give 1.2.0.0; null when there is no version number
		internal static Version ParseVersion(string text)
		{
			if(string.IsNullOrEmpty(text)) return null;
			text = text.Trim();
			if(text.StartsWith("v", StringComparison.OrdinalIgnoreCase)) text = text.Substring(1);

			// Only the numbers and their dots
			int end = 0;
			while(end < text.Length && ((text[end] >= '0' && text[end] <= '9') || text[end] == '.')) end++;

			string[] parts = text.Substring(0, end).Split('.');
			if(parts.Length > 4) return null;

			// The missing numbers are 0, so that 1.2 and 1.2.0 are the same version
			int[] numbers = new int[4];
			for(int i = 0; i < parts.Length; i++)
			{
				if(!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i])) return null;
			}

			return new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
		}

		// 1.2.0 (the fourth number only when it is not 0)
		internal static string VersionText(Version version)
		{
			return version.ToString(version.Revision != 0 ? 4 : 3);
		}

		#endregion

		#region ================== Downloads

		private static string DownloadText(string url)
		{
			try
			{
				// GitHub only accepts TLS 1.2 or later
				ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

				WebRequest request = WebRequest.Create(url);
				request.Timeout = 15000;
				HttpWebRequest http = request as HttpWebRequest;
				if(http != null)
				{
					// GitHub refuses a request without a user agent
					http.UserAgent = "Doom-Builder-64-II/" + Application.ProductVersion;
					http.Accept = "application/vnd.github+json";
					http.ReadWriteTimeout = 15000;
				}

				using(WebResponse response = request.GetResponse())
				using(Stream stream = response.GetResponseStream())
				{
					if(stream == null) return null;
					using(StreamReader reader = new StreamReader(stream, Encoding.UTF8))
						return reader.ReadToEnd();
				}
			}
			catch(Exception e)
			{
				General.WriteLogLine("Update check: failed to read \"" + url + "\": " + e.Message);
				return null;
			}
		}

		#endregion

		#region ================== JSON and RTF

		private static string GetString(Dictionary<string, object> values, string key)
		{
			object value;
			return (values.TryGetValue(key, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : string.Empty);
		}

		private static bool GetBool(Dictionary<string, object> values, string key)
		{
			object value;
			return (values.TryGetValue(key, out value) && value is bool && (bool)value);
		}

		private static long GetLong(Dictionary<string, object> values, string key)
		{
			object value;
			if(!values.TryGetValue(key, out value) || value == null) return 0;
			try { return Convert.ToInt64(value, CultureInfo.InvariantCulture); }
			catch(Exception) { return 0; }
		}

		// Text in an RTF document
		private static string Rtf(string text)
		{
			StringBuilder sb = new StringBuilder(text.Length);
			foreach(char c in text.Replace("\r\n", "\n").Replace('\r', '\n'))
			{
				switch(c)
				{
					case '\\': sb.Append(@"\\"); break;
					case '{': sb.Append(@"\{"); break;
					case '}': sb.Append(@"\}"); break;
					case '\n': sb.Append(@"\par "); break;
					case '\t': sb.Append(@"\tab "); break;
					default:
						if(c < 128) sb.Append(c);
						else sb.Append(@"\u").Append(((short)c).ToString(CultureInfo.InvariantCulture)).Append('?');
						break;
				}
			}

			return sb.ToString();
		}

		#endregion
	}
}
