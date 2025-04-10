using System;
using System.Runtime.InteropServices;

namespace SonB
{
    public static class ConsoleNamer
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetConsoleTitle(string lpConsoleTitle);

        public static void SetTitle(string title)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Thread.Sleep(100); //overrites automated dir name
                    SetConsoleTitle(title);
                }
                else
                {
                    Console.Title = title;
                }
            }
            catch
            {
                Console.Title = title;
            }
        }
    }
}