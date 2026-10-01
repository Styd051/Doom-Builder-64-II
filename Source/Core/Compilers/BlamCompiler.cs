
#region ================== Copyright (c) 2007 Pascal vd Heiden

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using CodeImp.DoomBuilder.Config;

#endregion

namespace CodeImp.DoomBuilder.Compilers
{
	// villsa. Doom 64 macro script compiler (BLAM)
	internal sealed class BlamCompiler : Compiler
	{
		#region ================== Constants

		private const string BLAM_ERROR_FILE = "blam_err.txt";

		private static readonly Regex includeregex = new Regex("^\\s*#include\\s+\"([^\"]+)\"", RegexOptions.Multiline | RegexOptions.IgnoreCase);

		#endregion

		#region ================== Constructor

		// Constructor. The include files that come with the compiler are copied to the temporary directory.
		public BlamCompiler(CompilerInfo info) : base(info, true)
		{
		}

		// Disposer
		public override void Dispose()
		{
			// Not already disposed?
			if(!isdisposed)
			{
				// Clean up

				// Done
				base.Dispose();
			}
		}

		#endregion

		#region ================== Methods

		// BLAM looks for include files in a single directory, which is the temporary directory (%PT).
		// This puts the include files from the directory of the map in there as well, so that a file
		// next to the map is used instead of the one with the same name that comes with the compiler.
		private void CopyMapIncludes(string scriptfile, string sourcedir, HashSet<string> done)
		{
			foreach(Match m in includeregex.Matches(File.ReadAllText(scriptfile)))
			{
				string name = m.Groups[1].Value;
				if(!done.Add(name)) continue;

				string mapfile = Path.Combine(sourcedir, name);
				string tempfile = Path.Combine(this.tempdir.FullName, name);
				if(File.Exists(mapfile) && (string.Compare(Path.GetFullPath(mapfile), Path.GetFullPath(tempfile), true) != 0))
					File.Copy(mapfile, tempfile, true);

				if(File.Exists(tempfile)) CopyMapIncludes(tempfile, sourcedir, done);
			}
		}

		// This runs the compiler
		public override bool Run()
		{
			Process process;
			string sourcedir = Path.GetDirectoryName(sourcefile);

			try
			{
				CopyMapIncludes(Path.Combine(this.workingdir, inputfile), sourcedir, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
			}
			catch(Exception e)
			{
				ReportError(new CompilerError("Unable to prepare the include files. " + e.GetType().Name + ": " + e.Message));
				return true;
			}

			// Create parameters
			string args = this.parameters;
			args = args.Replace("%FI", inputfile);
			args = args.Replace("%FO", outputfile);
			args = args.Replace("%FS", sourcefile);
			args = args.Replace("%PT", this.tempdir.FullName);
			args = args.Replace("%PS", sourcedir);

			// Setup process info
			ProcessStartInfo processinfo = new ProcessStartInfo();
			processinfo.Arguments = args;
			processinfo.FileName = Path.Combine(info.Path, info.ProgramFile);
			processinfo.CreateNoWindow = true;
			processinfo.ErrorDialog = false;
			processinfo.UseShellExecute = false;
			processinfo.WindowStyle = ProcessWindowStyle.Hidden;
			processinfo.WorkingDirectory = this.workingdir;

			// Output info
			General.WriteLogLine("Running compiler...");
			General.WriteLogLine("Program:    " + processinfo.FileName);
			General.WriteLogLine("Arguments:  " + processinfo.Arguments);

			try
			{
				// Start the compiler
				process = Process.Start(processinfo);
			}
			catch(Exception e)
			{
				// Unable to start the compiler
				General.ShowErrorMessage("Unable to start the compiler (" + info.Name + "). " + e.GetType().Name + ": " + e.Message, MessageBoxButtons.OK);
				return false;
			}

			// Wait for compiler to complete
			process.WaitForExit();
			TimeSpan deltatime = TimeSpan.FromTicks(process.ExitTime.Ticks - process.StartTime.Ticks);
			General.WriteLogLine("Compiler process has finished.");
			General.WriteLogLine("Compile time: " + deltatime.TotalSeconds.ToString("########0.00") + " seconds");

			// Now find the error file
			string errfile = Path.Combine(this.workingdir, BLAM_ERROR_FILE);
			if(File.Exists(errfile))
			{
				try
				{
					// Every line is an error
					foreach(string line in File.ReadAllLines(errfile))
						ReportError(new CompilerError(line, Path.Combine(this.workingdir, inputfile)));
				}
				catch(Exception e)
				{
					// Error reading errors (ironic, isn't it)
					ReportError(new CompilerError("Failed to retrieve compiler error report. " + e.GetType().Name + ": " + e.Message));
				}
			}

			return true;
		}

		#endregion
	}
}
