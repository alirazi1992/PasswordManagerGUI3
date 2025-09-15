using System;
using System.Windows.Forms;
using SQLitePCL;  // from SQLitePCLRaw.bundle_e_sqlite3

namespace PasswordManagerGUI3
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Initialize native SQLite engine
            try { Batteries_V2.Init(); } catch { Batteries.Init(); }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
