using IoTEdgeInstaller;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Management.Automation;
using System.Reflection;
using System.Security.Principal;

namespace IoTEdgeInstallerConsoleApp
{
    class Program
    {
        public static void ConsoleShowProgress(double progress, bool isAbsolute)
        {
            Console.Write(".");
        }

        public static void ConsoleShowError(string error)
        {
            Console.WriteLine("Error: " + error);
        }

        private static void PSErrorStreamHandler(object sender, DataAddedEventArgs e)
        {
            string text = ((PSDataCollection<ErrorRecord>)sender)[e.Index].ToString();

            // supress encoding exceptions
            if (!text.Contains("Exception setting \"OutputEncoding\""))
            {
                Console.WriteLine(text);
            }
        }

        private static void PSWarningStreamHandler(object sender, DataAddedEventArgs e)
        {
            Console.WriteLine(((PSDataCollection<WarningRecord>)sender)[e.Index].ToString());
        }

        private static void PSInfoStreamHandler(object sender, DataAddedEventArgs e)
        {
            Console.WriteLine(((PSDataCollection<InformationRecord>)sender)[e.Index].ToString());
        }

        public static Collection<string> RunPSCommand(string command)
        {
            Collection<string> returnValues = new Collection<string>();
            using (PowerShell ps = PowerShell.Create())
            {
                ps.Streams.Warning.DataAdded += PSWarningStreamHandler;
                ps.Streams.Error.DataAdded += PSErrorStreamHandler;
                ps.Streams.Information.DataAdded += PSInfoStreamHandler;

                ps.AddScript(command);
                Collection<PSObject> results = ps.Invoke();
                ps.Streams.ClearStreams();
                ps.Commands.Clear();

                foreach (PSObject result in results)
                {
                    returnValues.Add(result.ToString());
                }
            }

            return returnValues;
        }

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
            MSAHelper.SignIn(ConsoleShowProgress, ConsoleShowError, RunPSCommand);

            if (MSAHelper.CurrentState == SigninStates.SignedIn)
            {
                Console.WriteLine();
                Console.WriteLine(Strings.GatheringIoTHubs);
                AzureIoTHub hub = Installer.GetInstance().DiscoverAzureIoTHubs();
                if (hub != null)
                {
                    Installer.GetInstance().GetNicList();

                    string azureDeviceID =  Environment.MachineName;
                    Console.WriteLine();
                    Console.WriteLine(Strings.AzureCreateDeviceIdDesc + " (" + Strings.UseHostname + " " + azureDeviceID + ")");
                    string input = Console.ReadLine();
                    if (input != string.Empty)
                    {
                        azureDeviceID = input;
                    }

                    char decision = 'a';
                    while (decision != 'y' && decision != 'n')
                    {
                        Console.WriteLine();
                        Console.Write(Strings.InstallIIoT + "? [y/n]: ");
                        decision = Console.ReadKey().KeyChar;
                    }

                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine(Strings.Installing);
                    Installer.GetInstance().CreateAzureIoTEdgeDevice(hub, azureDeviceID, decision == 'y');

                    Console.WriteLine();
                    Console.WriteLine(Strings.EnterToExist);
                    Console.ReadLine();
                }
            }
        }
    }
}
