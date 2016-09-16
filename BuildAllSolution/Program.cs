using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.IO;

namespace BuildAllSolution
{
	class Program
	{
		private const string NAZEVPROCESU = "Byznys.Core.Server.WpfTestHost";
		
		static void Main(string[] args)
		{
			Console.WriteLine("Větev: Test test");


			var parameter = string.Empty;
			if (args.Count()>0 && !String.IsNullOrEmpty(args[0]))
			{
				parameter = args[0].ToLower();
				string popisSpusteni;
				switch (parameter)
				{
					case "m":
						popisSpusteni = "Moduly";
						break;
					case "c":
						popisSpusteni = "Core";
						break;
					case "p":
						popisSpusteni = "Pluginy";
						break;
					case "t":
						popisSpusteni = "Bez testů";
						break;					
					case "k":
						popisSpusteni = "Kompletní";
						break;
					default:
						parameter = string.Empty;
						popisSpusteni = "Chybné přímé spuštění - vyber z volby níže:";
						break;
				}
				Console.WriteLine(String.Format("Build spuštěn s parametrem {0} - {1}:",parameter.ToUpper(),popisSpusteni));
			}
			if (IsProcessRunning(NAZEVPROCESU))
			{
				var oldForegroundColor = Console.ForegroundColor;
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("POZOR!!!\nPřed spuštěním buildu je potřeba ukončit proces '{0}'\rPOZOR!!!\n", NAZEVPROCESU);
				Console.ForegroundColor = oldForegroundColor;
			}
			if (string.IsNullOrEmpty(parameter))
			{
				Console.WriteLine("Stiskni klávesu pro build všech projektů:");
				Console.WriteLine("-----------------------------------------------------");
				Console.WriteLine(" (M) pro režim pouze Moduly");
				Console.WriteLine(" (C) pro režim pouze Core");
				Console.WriteLine(" (P) pro režim pouze Pluginy");
				Console.WriteLine(" (T) pro kompilování bez testů a upsize");
				Console.WriteLine(" (U) pro kompilování včetně upsize, bez testů");
				Console.WriteLine(" (V) pro kompilování s vlastní konfigurací (Data*.xml)");
				Console.WriteLine(" Jinak kompletní build všech projektů (např. enterem, K pro přímé spuštění)");
				Console.WriteLine("-----------------------------------------------------");
				Console.WriteLine("Přímé spuštění je možné s příslušným parametrem z příkazové řádky.");
				var input = Console.ReadKey();
				Console.WriteLine();
				parameter = input.KeyChar.ToString().ToLower();
			}

			var stopwatch = new Stopwatch();
			stopwatch.Start();

			string xmlDataFile = "DataForBuildAllSolution.xml";

			if(parameter == "v")
			{
				xmlDataFile = CustomConfigFile();
			}				
			
			BuildSolution.BuildFromXML(xmlDataFile, parameter);
			
			stopwatch.Stop();
			Console.WriteLine("Dokončen build všech projektů.");
			Console.WriteLine("Celkový čas: {0}", stopwatch.Elapsed);
            BuildSolution.PrintErrorSummary();

			// Vyprázdnit zásobník náhodných kláves:
			while (Console.KeyAvailable)
				Console.ReadKey();

            Console.WriteLine("Stiskněte klávesu pro ukončení programu...");
			Console.ReadKey();
		}

		private static string CustomConfigFile()
		{
			var oldForegroundColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("Dostupné soubory s předdefinovaným tvarem DataForBuild*.xml");
			Console.ForegroundColor = oldForegroundColor;
			var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "DataForBuild*.xml");
			int index = 1;
			foreach (var file in files)
			{
				Console.WriteLine(index++ + " " + Path.GetFileName(file));
			}
			int choice=files.Length;
			while (!Int32.TryParse(Console.ReadKey().KeyChar.ToString(), out choice) || choice > files.Length || choice < 1)
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine(" - Zadali jste nesprávnou volbu, zadejte prosím znovu");
				Console.ForegroundColor = oldForegroundColor;
			}
			Console.WriteLine();
			return files[choice-1];
		}

		private static bool IsProcessRunning(string process)
		{
			return (System.Diagnostics.Process.GetProcessesByName(process).Length != 0);
		}
	}
}
