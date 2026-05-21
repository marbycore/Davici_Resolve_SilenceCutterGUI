using System;
using System.Windows.Forms;

namespace SilenceCutterGUI
{
    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // Pasamos los argumentos de CLI a nuestro Formulario principal
            Application.Run(new Form1(args));
        }
    }
}