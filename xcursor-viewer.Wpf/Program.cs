using System;
using Eto.Forms;

namespace xcursor_viewer.Wpf
{
	class Program
	{
		[STAThread]
		public static void Main(string[] args)
		{
			new Application(Eto.Platforms.Wpf).Run(new MainForm(args));
		}
	}
}
