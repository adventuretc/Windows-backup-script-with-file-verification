using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;

namespace backup
{
	class Job
	{
		public String path;
		public bool isFile;
		public bool pathExists = true;

		public String directoryPath;
		public String fileName; //isFile

		public String destinationPath;

		public bool copyError = false;
		public bool verificationError = false;
	}

	class Program
	{
		static String destinationRoot;
		static StreamWriter copyLogWriter;
		static StreamWriter copyErrorWriter;
		static StreamWriter verificationLogWriter;
		static StreamWriter verificationErrorWriter;
		static String logRoot;

		static bool logToFile = false;
		static bool logToConsole = false;

		static void Main(string[] args)
		{
			bool stop = false;

			StreamReader reader;
			using (reader = new StreamReader("destination.txt"))
			{
				destinationRoot = reader.ReadToEnd();
			}

			//strip closing \
			destinationRoot = destinationRoot.TrimEnd(Path.DirectorySeparatorChar);

			logRoot = destinationRoot + Path.DirectorySeparatorChar;

			String logging;

			using (reader = new StreamReader("options.txt"))
			{
				logging = reader.ReadLine();
			}

			switch (logging)
			{
				case "logging: default":
				{
					break; //log errors to console
				}
				case "logging: file":
				{
					logToFile = true; //log everything to file
					break;
				}
				case "logging: console":
				{
					logToConsole = true; //log everything to console
					break;
				}
				default:
				{
					break;
				}
			}

			String robocopyUserArguments;
			using (reader = new StreamReader("robocopy-arguments.txt"))
			{
				robocopyUserArguments = reader.ReadLine();
			}

			if (!Directory.Exists(destinationRoot))
			{
				stop = true;
			}

			if (stop)
			{
				Console.WriteLine("Error, destination doesn't exist.");
				Console.WriteLine("Press return to exit.");
				Console.ReadLine();
				return;
			}

			List<String> sources;
			using (reader = new StreamReader("sources.txt"))
			{
				sources = new List<String>();

				while (reader.Peek() >= 0)
				{
					sources.Add(reader.ReadLine());
				}
			}
			
			List<Job> jobs = new List<Job>(sources.Count);

			for (int i = 0; i < sources.Count; i++)
			{
				Job ajob = new Job();
				ajob.path = sources.ElementAt(i);

				if (File.Exists(ajob.path))
				{
					ajob.isFile = true;
				}
				else if (Directory.Exists(ajob.path))
				{
					ajob.isFile = false;
				}
				else
				{
					ajob.pathExists = false;
				}

				jobs.Add(ajob);
			}

			//if any of the files don't exist, stop and print them
			for (int i = 0; i < jobs.Count; i++)
			{
				Job ajob = jobs.ElementAt(i);
				if (!ajob.pathExists)
				{
					stop = true;
					Console.WriteLine("Error, nonexistent path:");
					Console.WriteLine(ajob.path);
				}
			}

			if (stop)
			{
				Console.WriteLine();
				Console.WriteLine("Press return to continue");
				Console.ReadLine();
				stop = false;
			}

			//drop jobs with nonexistent paths
			for (int i = 0; i < jobs.Count; i++)
			{
				Job ajob = jobs.ElementAt(i);
				if (!ajob.pathExists)
				{
					jobs.Remove(ajob);
				}
			}

			//set directory path, filename, destination path
			for (int i = 0; i < jobs.Count; i++)
			{
				Job ajob = jobs.ElementAt(i);

				ajob.path.TrimEnd(Path.DirectorySeparatorChar);

				if (ajob.isFile)
				{
					ajob.directoryPath = ajob.path.Substring(0, (ajob.path.LastIndexOf(Path.DirectorySeparatorChar) + 1));
					ajob.fileName = ajob.path.Replace(ajob.directoryPath, "");
				}
				else
				{
					ajob.directoryPath = ajob.path;
				}

				//trim trailing \
				ajob.directoryPath = ajob.directoryPath.TrimEnd(Path.DirectorySeparatorChar);
				
				//the drive letter is made into a folder
				ajob.destinationPath = destinationRoot + Path.DirectorySeparatorChar + ajob.directoryPath.Replace(":", "");
				ajob.destinationPath = ajob.destinationPath.TrimEnd(Path.DirectorySeparatorChar);
			}

			//jobs prepared
			Console.WriteLine("All backup tasks prepared.");

			//execute copy
			if (logToFile)
			{
				copyErrorWriter = new StreamWriter(logRoot + "copy-error.txt", true);
				copyLogWriter = new StreamWriter(logRoot + "copy.txt", true);
			}

			for (int i = 0; i < jobs.Count; i++)
			{
				Job ajob = jobs.ElementAt(i);

				//robocopy needs spaces before trailing "s, bug
				String robocopyArguments = "\"" + ajob.directoryPath + " \"" + " " + "\"" + ajob.destinationPath + " \""; // <1> <2>

				if (ajob.isFile)
				{
					robocopyArguments += " " + ajob.fileName; // <1> <2> [3]
				}


				//robocopy cannot output unicode, /unicode is bugged
				using (Process p = new Process())
				{
					p.StartInfo.FileName = "robocopy.exe";
					p.StartInfo.UseShellExecute = false;
					p.StartInfo.RedirectStandardOutput = true;
					//p.StartInfo.RedirectStandardError = true; //robocopy doesn't use this
					p.StartInfo.Arguments = robocopyArguments + " " + robocopyUserArguments;

					p.Start();
					
					string output = p.StandardOutput.ReadToEnd();

					//decide whether this is an error message
					if (output.Contains("ERROR"))
					{
						if (logToFile)
						{
							SaveLog(copyErrorWriter, output);
						}
						else
						{
							Console.Write(output);
						}
					}
					else
					{
						if (logToFile)
						{
							SaveLog(copyLogWriter, output);
						}
						else if (logToConsole)
						{
							Console.Write(output);
						}
					}

					p.WaitForExit();

					if (p.ExitCode != 0)
					{
						ajob.copyError = true;
					}
				}
			}

			for (int i = 0; i < jobs.Count; i++)
			{
				Job ajob = jobs.ElementAt(i);

				if (ajob.copyError)
				{
					stop = true;
					Console.WriteLine();
					Console.WriteLine("Error, copying failed:");
					Console.WriteLine(ajob.path);
				}
			}

			if (stop)
			{
				Console.WriteLine();
				Console.WriteLine("Press return to continue");
				Console.ReadLine();
				stop = false;
			}
			else
			{
				Console.WriteLine();
				Console.WriteLine("Copying finished ok.");
			}

			//execute file compare
			if (logToFile)
			{
				verificationErrorWriter = new StreamWriter(logRoot + "verification-error.txt", true);
				verificationLogWriter = new StreamWriter(logRoot + "verification.txt", true);
			}

			for (int i = 0; i < jobs.Count; i++)
			{
				Job ajob = jobs.ElementAt(i);
				String arguments;

				if (ajob.isFile)
				{
					arguments = "\"" + ajob.directoryPath + Path.DirectorySeparatorChar + ajob.fileName + "\"" + " " + "\"" + ajob.destinationPath + Path.DirectorySeparatorChar + ajob.fileName + "\""; // <1>\file <2>\file
				}
				else
				{
					arguments = "\"" + ajob.directoryPath + Path.DirectorySeparatorChar + "*" + "\"" + " " + "\"" + ajob.destinationPath + Path.DirectorySeparatorChar + "*" + "\""; // <1>\* <2>\*
				}
				
				//robocopy cannot output unicode, /unicode is bugged
				using (Process p = new Process())
				{
					p.StartInfo.FileName = "fc.exe";
					p.StartInfo.UseShellExecute = false;
					p.StartInfo.RedirectStandardOutput = true;
					p.StartInfo.RedirectStandardError = true;
					p.StartInfo.CreateNoWindow = true;
					p.StartInfo.Arguments = "/B " + arguments; // b=binary; 

					p.Start();



					string output = p.StandardError.ReadToEnd();

					if (logToFile)
					{
						SaveLog(verificationErrorWriter, output);
					}
					else
					{
						Console.Write(output);
					}

					output = p.StandardOutput.ReadToEnd();

					if (logToFile)
					{
						SaveLog(verificationLogWriter, output);
					}
					else if (logToConsole)
					{
						Console.Write(output);
					}

					p.WaitForExit();

					if (p.ExitCode != 0)
					{
						ajob.verificationError = true;
					}
				}
			}

			for (int i = 0; i < jobs.Count; i++)
			{
				Job ajob = jobs.ElementAt(i);

				if (ajob.verificationError)
				{
					stop = true;
					Console.WriteLine();
					Console.WriteLine("Error, verification failed:");
					Console.WriteLine(ajob.path);
				}
			}

			if (stop)
			{
				Console.WriteLine();
				Console.WriteLine("Press return to continue");
				Console.ReadLine();
				stop = false;
			}
			else
			{
				Console.WriteLine();
				Console.WriteLine("Verification finished ok.");
			}

			Console.WriteLine();
			Console.WriteLine("Press return to exit.");
			Console.ReadLine();
		}

		private static void SaveLog(StreamWriter writer, string output)
		{
			writer.Write(output);
			writer.Flush();
		}

	} //class
} //namespace
