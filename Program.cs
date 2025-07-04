using System;
using System.Windows.Forms;

namespace PowerCopyWinform
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            FormMain form;
            if (args.Length >= 2)
            {
                var action = args[0];
                var sourceFile = args[1];
                form = new FormMain(action, sourceFile);
            }
            else
            {
                form = new FormMain();
            }

            Application.Run(form);
        }
    }
}