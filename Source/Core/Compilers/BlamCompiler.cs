
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

		// An error in a script is two lines in the error file: "file : message", then "(line = N, pos = M)"
		private const string ERROR_FILE_SEPARATOR = " : ";
		private static readonly Regex errorlineregex = new Regex("^\\(line = ([0-9]+), pos = [0-9]+\\)$", RegexOptions.CultureInvariant);

		private static readonly Regex includeregex = new Regex("^\\s*#include\\s+\"([^\"]+)\"", RegexOptions.Multiline | RegexOptions.IgnoreCase);

		#endregion

		#region ================== Variables

		// The include files next to the map that were copied to the temporary directory, by their name in #include
		private readonly Dictionary<string, string> mapincludes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
				{
					File.Copy(mapfile, tempfile, true);
					mapincludes[name] = Path.GetFullPath(mapfile);
				}

				if(File.Exists(tempfile)) CopyMapIncludes(tempfile, sourcedir, done);
			}
		}

		// The file of an error. BLAM gives the script by the name it was given (%FI), and an include file with the
		// directory of -i (%PT) before its name: that is the include next to the map if it was copied there,
		// otherwise the one that comes with the compiler.
		private string GetErrorFile(string file)
		{
			if(string.Compare(file, inputfile, true) == 0) return Path.Combine(this.workingdir, inputfile);

			string temppath = this.tempdir.FullName + Path.DirectorySeparatorChar;
			if(!file.StartsWith(temppath, StringComparison.OrdinalIgnoreCase)) return file;

			string name = file.Substring(temppath.Length);
			string mapfile;
			if(mapincludes.TryGetValue(name, out mapfile)) return mapfile;

			string compilerfile = Path.Combine(info.Path, name);
			return (File.Exists(compilerfile) ? compilerfile : file);
		}

		// This reports the errors of the lines of the error file. An error in a script has its file and its line;
		// an error of the program itself (a file it cannot write...) is one line, given for the script.
		private void ReportErrors(string[] lines)
		{
			string inputpath = Path.Combine(this.workingdir, inputfile);

			for(int i = 0; i < lines.Length; i++)
			{
				if(lines[i].Trim().Length == 0) continue;

				int separator = lines[i].IndexOf(ERROR_FILE_SEPARATOR, StringComparison.Ordinal);
				Match m = (i + 1 < lines.Length ? errorlineregex.Match(lines[i + 1].Trim()) : Match.Empty);
				int linenumber;

				if(separator > 0 && m.Success && int.TryParse(m.Groups[1].Value, out linenumber))
				{
					// BLAM counts the lines from 1
					ReportError(new CompilerError(lines[i].Substring(separator + ERROR_FILE_SEPARATOR.Length),
						GetErrorFile(lines[i].Substring(0, separator)), linenumber - 1));
					i++;
				}
				else
				{
					ReportError(new CompilerError(lines[i], inputpath));
				}
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
					ReportErrors(File.ReadAllLines(errfile));
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
