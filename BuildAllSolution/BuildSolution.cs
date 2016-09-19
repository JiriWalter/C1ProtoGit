using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Collections;

namespace BuildAllSolution
{
	public static class BuildSolution
	{

		private static readonly string[] _SOLUTIONS = { "Byznys.Server.Host.sln", "Byznys.Client.Desktop.sln", "Server.WpfTestHost.sln" };

		private static void RefreshSettings()
		{
			// Do the refresh
			DataView DataV;
			DataSet DataXML;
			DataXML = new DataSet();
			if (File.Exists("LocalSettingsForBuildAllSolution.xml"))
			{
				DataXML.ReadXml("LocalSettingsForBuildAllSolution.xml");
			}
			else
			{
				DataXML.ReadXml("SettingsForBuildAllSolution.xml");
			}

			DataV = new DataView(DataXML.Tables["SolutionSettingsFile"]);

			string buildPlatform = "Auto";
			try
			{
				buildPlatform = DataV[0]["MSBuildPlatform"].ToString();
			}
			catch
			{ }

			if (buildPlatform == "x86")
			{
				_MSBuildFile = DataV[0]["MSBuildFile32"].ToString();
			}
			else if (buildPlatform == "x64")
			{
				_MSBuildFile = DataV[0]["MSBuildFile64"].ToString();
			}
			else
			{
				if (System.Environment.Is64BitOperatingSystem)
				{
					_MSBuildFile = DataV[0]["MSBuildFile64"].ToString();
				}
				else
				{
					_MSBuildFile = DataV[0]["MSBuildFile32"].ToString();
				}
			}

            // Hack pro Visual Studio 2015 clean install:
            if (Directory.Exists(@"c:\Program Files (x86)\MSBuild"))
            {
                var latestVersionDirectory = Directory.GetDirectories(@"c:\Program Files (x86)\MSBuild", "*", SearchOption.TopDirectoryOnly)
                    .Where(directory => File.Exists(Path.Combine(directory, "Bin", "MSBuild.exe")))
                    .OrderByDescending(s => s).FirstOrDefault();

                if (!string.IsNullOrEmpty(latestVersionDirectory))
                {
                    var amd64Build = Path.Combine(latestVersionDirectory, @"Bin\amd64\MSBuild.exe");
                    if (_MSBuildFile == DataV[0]["MSBuildFile64"].ToString() && File.Exists(amd64Build))
                    {
                        _MSBuildFile = amd64Build;
                    }
                    else
                    {
                        _MSBuildFile = Path.Combine(latestVersionDirectory, @"Bind\MSBuild.exe");
                    }
                }
            }

			_DefaultFolder = DataV[0]["DefaultFolder"].ToString();
            _MSBuildParameter = DataV[0]["MSBuildParameter"].ToString();
			if (DataV[0]["OutputFile"].ToString() == "True")
			{
				_OutputFile = true;
			}
			else
			{
				_OutputFile = false;
			}

			if (DataV[0]["OutputConsole"].ToString() == "True")
			{
				_OutputConsole = true;
			}
			else
			{
				_OutputConsole = false;
			}
		}

		private static string _quickProgressInfo = string.Empty;
        private static List<string> _ErrSolution = new List<string>();
        private static List<int> _ErrCount = new List<int>();

		private static string _MSBuildFile;
		private static string _DefaultFolder;
		private static string _MSBuildParameter;
		private static Boolean _OutputFile;
		private static Boolean _OutputConsole;

		public static Boolean BuildFromXML(string xmlName, string param)
		{
			BuildParameter parameter;
			switch ((param ?? string.Empty).ToLower())
			{
				case "t": parameter = BuildParameter.Minimum; break;
				case "u": parameter = BuildParameter.Upsize; break;
				case "m": parameter = BuildParameter.Modules; break;
				case "c": parameter = BuildParameter.Core; break;
				case "p": parameter = BuildParameter.Plugins; break;
				case "v": parameter = BuildParameter.Config; break;
				default: parameter = BuildParameter.All; break;
			}
			return BuildFromXML(xmlName, parameter);
		}

		public static Boolean BuildFromXML(string xmlName, BuildParameter parameter)
		{

            RefreshSettings();

			DataView DataV;
			DataSet DataXML;
			DataXML = new DataSet();
			if (File.Exists(xmlName))
				DataXML.ReadXml(xmlName);
			else
			{
				var oldForegroundColor = Console.ForegroundColor;
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("Chyba: Soubor {0}, neexistuje nebo nejde přečíst", xmlName);
				Console.ForegroundColor = oldForegroundColor;
				return false;
			}

			var table = DataXML.Tables["SolutionFile"];
			
			
			// Build pouze vybrané části
			if(parameter.HasFlag(BuildParameter.Modules))
			{
				table = FilterPart(table,"Modules");
			}
			else if (parameter.HasFlag(BuildParameter.Plugins))
			{
				table = FilterPart(table, "Plugins");
			}
			else if (parameter.HasFlag(BuildParameter.Core))
			{
				table = FilterPart(table, "Core");
			}

			parameter = ShouldAddTestFlag(parameter);

			if (!parameter.HasFlag(BuildParameter.Tests))
			{
				CreateNoTest(table);
			}
			if (parameter.HasFlag(BuildParameter.Core) && parameter.HasFlag(BuildParameter.Minimum))
			{
				table.Rows.Remove(table.Rows.OfType<DataRow>().First(row => ((string)row["name"]).Contains("Entities")));
				table.Rows.Remove(table.Rows.OfType<DataRow>().First(row => ((string)row["name"]).Contains("WpfTestHost")));
			}

			DataV = new DataView(table);
			
			Console.WriteLine("Počet nalezených projektů určených buildu: " + DataV.Count);

			_quickProgressInfo = string.Empty;

			foreach (DataRowView drv in DataV)
			{
				if (table.Columns.Contains("includeIn"))
				{
					BuildParameter includeParameter;
					if (Enum.TryParse(drv["includeIn"].ToString(), out includeParameter) && !parameter.HasFlag(includeParameter))
					{
						_quickProgressInfo += "-";
						continue;
					}
				}

				ReportCurrentState(DataV);
				Console.WriteLine("Provádím Build: " + drv["name"].ToString() + " ...");
				ProcessStartInfo psi = new ProcessStartInfo(_MSBuildFile);

				psi.Arguments = "\"" + drv["folder"].ToString() + "\\" + drv["name"].ToString() + "\"" + " " + _MSBuildParameter;

				psi.WorkingDirectory = _DefaultFolder;
				psi.UseShellExecute = false;
				psi.RedirectStandardOutput = true;

				Process compiler = new Process();
				compiler.StartInfo = psi;
				string protokol = "";
				try
				{
					compiler.Start();

					//Process.Start(psi);

					protokol = compiler.StandardOutput.ReadToEnd();
					compiler.WaitForExit();
				}
				catch
				{
					var oldForegroundColor = Console.ForegroundColor;
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("{0} nelze zkompilovat,  neexistující lokace", drv["name"].ToString());
					Console.ForegroundColor = oldForegroundColor;
					RememberErrors(drv["name"].ToString(), protokol);
					return false;
				}
				;
				if (_OutputConsole)
					Console.WriteLine(protokol);

				if (_OutputFile)
				{
					string protokolFile = "Build." + drv["name"].ToString() + ".txt";
					if (File.Exists(protokolFile))
						File.Delete(protokolFile);

					File.WriteAllText(protokolFile, protokol);

				}

				string solution = drv["name"].ToString();
				RememberErrors(solution, protokol);
			}

			if (!parameter.HasFlag(BuildParameter.Tests))
			{
				RemoveNoTest(DataXML.Tables["SolutionFile"]);
			}

			return true;
		}

		private static void ReportCurrentState(DataView DataV)
		{
			var currentQuickProgressInfo = _quickProgressInfo.PadRight(DataV.Count, '.');

			Console.WriteLine("\r\n");
			foreach (var c in currentQuickProgressInfo)
			{
				switch (c)
				{
					case 'v':
						Console.ForegroundColor = ConsoleColor.Green;
						break;
					case 'x':
						Console.ForegroundColor = ConsoleColor.Red;
						break;
					default:
						Console.ForegroundColor = ConsoleColor.Gray;
						break;
				}
				Console.Write(c);
			}
			Console.ForegroundColor = ConsoleColor.Gray;
			Console.WriteLine("\r\n");

			if (_ErrSolution.Count > 0)
			{
				var _oldForegroundColor = Console.ForegroundColor;
				Console.ForegroundColor = ConsoleColor.Red;

				for (var i = 0; i < _ErrSolution.Count; i++)
				{
					Console.WriteLine("{0} ({1})", _ErrSolution[i], _ErrCount[i]);
				}

				Console.ForegroundColor = _oldForegroundColor;
			}

			Console.WriteLine(Environment.NewLine);

		}

		private static BuildParameter ShouldAddTestFlag(BuildParameter parameter)
		{
			ArrayList flagy = new ArrayList { BuildParameter.Core, BuildParameter.Modules, BuildParameter.Plugins, BuildParameter.Config };
			string choice;
			if (flagy.Contains(parameter))
			{
				var oldForegroundColor = Console.ForegroundColor;
				Console.ForegroundColor = ConsoleColor.Green;
				if (parameter == BuildParameter.Core)
				{
					Console.WriteLine("Chcete spustit i testy? (A/N/M)");
				}
				else
				{
					Console.WriteLine("Chcete spustit i testy? (A/N)");
				}
				Console.ForegroundColor = oldForegroundColor;
				while ((choice = Console.ReadKey().KeyChar.ToString().ToUpper()) != "A" && choice != "N" && (choice != "M" || parameter != BuildParameter.Core))
				{
					Console.ForegroundColor = ConsoleColor.Green;
					Console.WriteLine(" - Zadali jste nesprávnou volbu, zadejte prosím znovu");
					Console.ForegroundColor = oldForegroundColor;
				}
				if (choice == "A")
				{
					parameter = parameter | BuildParameter.Tests;
				}
				if (choice == "M")
				{
					parameter = parameter | BuildParameter.Minimum;
				}
				Console.WriteLine();
			}
				return parameter;
		}

		private static DataTable FilterPart(DataTable table, string part)
		{
			DataTable tmpTable = new DataTable(table.TableName);
			tmpTable = table.Clone();
			foreach (DataRow row in table.Rows)
			{
				string folderName = row["folder"] as string;
				string name = row["name"] as string;
				if (folderName.Contains(part) || _SOLUTIONS.Contains(name))
				{
					tmpTable.ImportRow(row);
				}				
			}
			return tmpTable;
		}

		private const string NoTestPrefix = "_notest_";

		private static void CreateNoTest(DataTable table)
		{
			Console.WriteLine("{0} Vytvářím SLN bez testů...", DateTime.Now);
			foreach (DataRow row in table.Rows)
			{
				var fileName = row["name"] as string;
				var folderName = row["folder"] as string;
				if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(folderName))
				{
					continue;
				}
				var path = Path.Combine(folderName, fileName);
				if (!File.Exists(path))
				{
					Console.WriteLine("SKIP {0}", fileName);
					continue;
				}

				var lines = File.ReadAllLines(path);
				for (int index = 0; index < lines.Length - 1; index++)
				{
					var line = lines[index];
					if (!line.StartsWith("Project(") || !line.Contains(" = "))
					{
						continue;
					}

					var arguments = line.Substring(line.IndexOf(" = ") + 3).Replace("\"", "").Replace(" ", "").Split(',');
					if (arguments[0].EndsWith(".Tests"))
					{
						lines[index] = lines[index + 1] = string.Empty;
					}
				}

				string targetFileName = string.Format("{0}{1}", NoTestPrefix, fileName);
				File.WriteAllLines(Path.Combine(folderName, targetFileName), lines);
				row["name"] = targetFileName;
			}
			Console.WriteLine("{0} Pokračuji...", DateTime.Now);
		}

		private static void RemoveNoTest(DataTable table)
		{
			foreach (DataRow row in table.Rows)
			{
				var fileName = row["name"] as string;
				var folderName = row["folder"] as string;
				if (string.IsNullOrEmpty(fileName) || !fileName.StartsWith(NoTestPrefix) || string.IsNullOrEmpty(folderName))
				{
					continue;
				}
				var path = Path.Combine(folderName, fileName);
				if (File.Exists(path))
				{
					File.Delete(path);
				}
			}
		}

        private static void RememberErrors(String solution, String protokol) 
        {
			if (String.IsNullOrEmpty(protokol))
			{
				_ErrSolution.Add(solution);
				_ErrCount.Add(1);
				_quickProgressInfo += "x";
				return;
			}

            const int KolikRadkuOdKonce = 4;
            string[] radky = protokol.Split('\n');            
            int PocetRadku = radky.Count();
            if (PocetRadku > KolikRadkuOdKonce)
            {
                //int PocetChyb = Convert.ToInt16(System.Text.RegularExpressions.Regex.Replace(radky[PocetRadku - KolikRadkuOdKonce], "[^0-9]+", "", System.Text.RegularExpressions.RegexOptions.Compiled));
                short PocetChyb = 0;
                string Radek = System.Text.RegularExpressions.Regex.Replace(radky[PocetRadku - KolikRadkuOdKonce], "[^0-9]+", "", System.Text.RegularExpressions.RegexOptions.Compiled);
                bool result = Int16.TryParse(Radek, out PocetChyb);
                if (!result)
                {
                    PocetChyb = 0;
                }

				if (radky[4].Contains("error"))
				{
					_ErrSolution.Add(solution);
					_ErrCount.Add(1);
					_quickProgressInfo += "x";
					return;
				}

                if (PocetChyb > 0 )
                {
                    _ErrSolution.Add(solution);
                    _ErrCount.Add(PocetChyb);
					_quickProgressInfo += "x";
	                return;
                }
            }
	        _quickProgressInfo += "v";
        }

        public static void PrintErrorSummary()
        {
            var _oldForegroundColor = Console.ForegroundColor;

            if (_ErrSolution.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Build následujících solutions proběhl s chybami:");
                Console.WriteLine(new String('=', 74));
                Console.WriteLine("Solution".PadRight(60, ' ') + " | " + "Počet chyb");
                Console.WriteLine(new String('=', 74));

                for (int i = 0; i < _ErrSolution.Count; i++)
                {
                    Console.WriteLine("{0} | {1}", _ErrSolution[i].PadRight(60, ' '), _ErrCount[i].ToString().PadLeft(10, ' '));
                }

                Console.ForegroundColor = _oldForegroundColor;
            }
            else 
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Build všech solutions proběhl úspěšně.");
                Console.ForegroundColor = _oldForegroundColor;
            }
        }
	}
}
