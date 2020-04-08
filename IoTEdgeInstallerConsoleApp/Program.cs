using Common;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;

namespace IoTEdgeInstaller
{
    class Program
    {
        static void Main(string[] args)
        {
#if !DEBUG
            // Try to elevate to admin on Windows
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                try
                {
                    var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                    if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                    {
                        var processInfo = new ProcessStartInfo("dotnet.exe");
                        processInfo.Arguments = Assembly.GetExecutingAssembly().Location;
                        processInfo.UseShellExecute = true;
                        processInfo.Verb = "runas";
                        Process.Start(processInfo);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message + " " + Strings.Admin);
                    Console.WriteLine(Strings.EnterToExist);
                    Console.ReadLine();
                    return;
                }
            }
#endif
            Console.WriteLine(Strings.AboutSubtitle);
            Console.WriteLine();
            Console.WriteLine(Strings.Prerequisits);
            
            string azureDeviceID = Environment.MachineName;
            Console.WriteLine();
            Console.WriteLine(Strings.AzureCreateDeviceIdDesc + " (" + Strings.UseHostname + " " + azureDeviceID + ")");
            string input = Console.ReadLine();
            if (input != string.Empty)
            {
                azureDeviceID = input;
            }

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(Strings.Installing);
            Installer.GetInstance().CreateAzureIoTEdgeDevice(azureDeviceID);

            Console.WriteLine();
            Console.WriteLine(Strings.EnterToExist);
            Console.ReadLine();
        }
    }
}
