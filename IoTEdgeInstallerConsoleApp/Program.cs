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
        public static void WindowsShowProgress(double progress)
        {
            Console.Write(".");
        }

        public static void WindowsShowError(string error)
        {
            Console.WriteLine("Error: " + error);
        }

        public static Collection<string> RunPSCommand(string command)
        {
            Collection<string> returnValues = new Collection<string>();
            using (PowerShell ps = PowerShell.Create())
            {
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
            // Try to elevate to admin
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
                Console.WriteLine("Error: " + ex.Message + " " + Strings.Strings.Admin);
                return;
            }
#endif
            Console.WriteLine(Strings.AboutSubtitle);

            Console.WriteLine();
            Console.WriteLine(Strings.Prerequisits);
            MSAHelper.SignIn(WindowsShowProgress, WindowsShowError, RunPSCommand);

            Console.WriteLine();
            Console.WriteLine(Strings.GatheringIoTHubs);
            AzureIoTHub hub = Installer.GetInstance().DiscoverAzureIoTHubs();
            if (hub != null)
            {
                Console.WriteLine();
                Console.WriteLine(Strings.AzureDeviceId);
                AzureDeviceEntity device = Installer.GetInstance().DiscoverDevicesAsync(hub).Result;
                if (device != null && device.IotEdge)
                {
                    Console.WriteLine();
                    Console.WriteLine(Strings.IoTEdgeModules);
                    Installer.GetInstance().DiscoverIoTEdgeModules(device);
                }

                Installer.GetInstance().GetNicList();
                
                Console.WriteLine();
                Console.WriteLine(Strings.Installing);
                Installer.GetInstance().CreateAzureIoTEdgeDeviceAsync(hub).Wait();

                Console.WriteLine();
                Console.WriteLine(Strings.EnterToExist);
                Console.ReadLine();
            }
        }
    }
}
