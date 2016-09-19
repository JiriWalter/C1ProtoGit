//Author:   walter
//Company:  J.K.R. spol. s r.o.
//Date:     11/14/2013 1:41:31 PM

using System;


namespace BuildAllSolution
{
	/// <summary>
	/// Příznaky, v jakém režimu spustit
	/// </summary>
	[Flags]
	public enum BuildParameter
	{
		/// <summary>
		/// Volba nebyla specifikována
		/// </summary>
		Undefined = 0,
		/// <summary>
		/// Zahrnout SLN: ETL, UPSIZE, UPSIZENULA 
		/// </summary>
		Upsize = 1,
		Tests = 2,		
		Modules = 4,
		/// <summary>
		/// Pouze Core
		/// </summary>
		Core = 8,
		/// <summary>
		/// Pouze pluginy 
		/// </summary>
		Plugins = 16,
		/// <summary>
		/// Zahrnout upsize i testy 
		/// </summary>
		Config = 32,
		/// <summary>
		/// Nezahrnovat žádné volitelné varianty
		/// </summary>
		Minimum = 64,
		All = Upsize | Tests,
	}
}
