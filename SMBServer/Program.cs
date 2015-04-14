using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace SMBServer
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Console.WriteLine("starting");
            var server = new Server();
            Console.WriteLine("started");
            while (true) { };

        }

        public static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            HandleUnhandledException(e.Exception);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject != null)
            {
                Exception ex = (Exception)e.ExceptionObject;
                HandleUnhandledException(ex);
            }
        }

        private static void HandleUnhandledException(Exception ex)
        {
            string message = String.Format("Exception: {0}: {1} Source: {2} {3}", ex.GetType(), ex.Message, ex.Source, ex.StackTrace);
            MessageBox.Show(message, "Error");
            Application.Exit();
        }
    }
}