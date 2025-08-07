using System;
using Eto.Forms;

namespace xcursor_viewer.Gtk {
    class Program {
        [STAThread]
        public static void Main(string[] args) {
            new Application(Eto.Platforms.Gtk).Run(new MainForm(args));
        }
    }
}
