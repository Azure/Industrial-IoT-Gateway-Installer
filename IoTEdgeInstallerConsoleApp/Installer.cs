using Microsoft.Azure.Devices;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Management.Automation;

namespace IoTEdgeInstaller
{
    class Installer
    {
        private static Installer _instance = null;
        private static object _creationLock = new object();
               
       
        private Installer() { }

        public static Installer GetInstance()
        {
            if (_instance == null)
            {
                lock (_creationLock)
                {
                    if (_instance == null)
                    {
                        _instance = new Installer();
                    }
                }
            }

            return _instance;
        }

        private bool SetupPrerequisits()
        {
            try
            {
                PowerShell PS = PowerShell.Create();
           
                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    "sudo apt-get update".Bash();
                    "sudo apt --assume-yes install curl".Bash(); 
                    "curl -N https://packages.microsoft.com/config/ubuntu/18.04/prod.list > ./microsoft-prod.list".Bash();
                    "sudo cp ./microsoft-prod.list /etc/apt/sources.list.d/".Bash();
                    "curl -N https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.gpg".Bash();
                    "sudo cp ./microsoft.gpg /etc/apt/trusted.gpg.d/".Bash();
                    "sudo apt-get update".Bash();
                    "sudo apt-get --assume-yes install moby-engine".Bash();
                    "sudo apt-get --assume-yes install moby-cli".Bash();
                }
                else if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    // check if we are on 1809 build 17763, which is the only supported version of Windows 10 for IoT Edge
                    if (Environment.OSVersion.Version.Build != 17763)
                    {
                        Console.WriteLine("Error: " + Strings.OSNotSupported);
                        return false;
                    }
                    
                    // check if bitlocker is enabled
                    PS.AddScript("manage-bde -status C:");
                    Collection<PSObject> results = PS.Invoke();
                    PS.Streams.ClearStreams();
                    PS.Commands.Clear();
                    if (results.Count == 0)
                    {
                        Console.WriteLine("Error: " + Strings.BitLockerStatus);
                    }
                    bool enabled = false;
                    foreach (var result in results)
                    {
                        if (result.ToString().Contains("Protection On"))
                        {
                            enabled = true;
                            break;
                        }
                    }
                    if (!enabled)
                    {
                        Console.WriteLine(Strings.BitlockerDisabled);
                    }

                    // check if Hyper-V is enabled
                    PS.AddScript("$hyperv = Get-WindowsOptionalFeature -FeatureName Microsoft-Hyper-V-All -Online");
                    PS.AddScript("if($hyperv.State -eq 'Enabled') { write 'enabled' }");
                    results = PS.Invoke();
                    PS.Streams.ClearStreams();
                    PS.Commands.Clear();
                    if (results.Count == 0)
                    {
                        Console.WriteLine("Error: " + Strings.HyperVNotEnabled);
                        return false;
                    }
                    enabled = false;
                    foreach (var result in results)
                    {
                        if (result.ToString().Contains("enabled"))
                        {
                            enabled = true;
                            break;
                        }
                    }
                    if (!enabled)
                    {
                        Console.WriteLine("Error: " + Strings.HyperVNotEnabled);
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine(Strings.OSNotSupported);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return false;
            }
  
            return true;
        }

        private bool InstallIoTEdge(Device deviceEntity, AzureIoTHub iotHub)
        {
            if (deviceEntity != null)
            {
                PowerShell PS = PowerShell.Create();
                PS.Streams.Warning.DataAdded += PSWarningStreamHandler;
                PS.Streams.Error.DataAdded += PSErrorStreamHandler;
                PS.Streams.Information.DataAdded += PSInfoStreamHandler;

                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    try
                    {
                        var newProcessInfo = new ProcessStartInfo
                        {
                            FileName = Environment.SystemDirectory + "\\WindowsPowerShell\\v1.0\\powershell.exe"
                        };

                        Console.WriteLine(Strings.Uninstall);
                        newProcessInfo.Arguments = "Invoke-WebRequest -useb aka.ms/iotedge-win | Invoke-Expression; Uninstall-IoTEdge -Force";
                        var process = Process.Start(newProcessInfo);
                        process.WaitForExit();
                        if (process.ExitCode != 0)
                        {
                            Console.WriteLine("Error: " + Strings.UninstallFailed);
                            return false;
                        }

                        Console.WriteLine(Strings.Install);
                        newProcessInfo.Arguments = $"Invoke-WebRequest -useb aka.ms/iotedge-win | Invoke-Expression; Install-IoTEdge -ContainerOs Windows -Manual -DeviceConnectionString 'HostName={iotHub.Name}.azure-devices.net;DeviceId={deviceEntity.Id};SharedAccessKey={deviceEntity.Authentication.SymmetricKey.PrimaryKey}' -SkipBatteryCheck";
                        process = Process.Start(newProcessInfo);
                        process.WaitForExit();
                        if (process.ExitCode != 0)
                        {
                            Console.WriteLine("Error: " + Strings.InstallFailed);
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                        return false;
                    }

                }
                else if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    "sudo apt-get update".Bash();
                    "sudo apt-get --assume-yes install iotedge".Bash();
                    $"sudo sed -i 's/<ADD DEVICE CONNECTION STRING HERE>/HostName={iotHub.Name}.azure-devices.net;DeviceId={deviceEntity.Id};SharedAccessKey={deviceEntity.Authentication.SymmetricKey.PrimaryKey}/g' /etc/iotedge/config.yaml".Bash();
                    "sudo systemctl restart iotedge".Bash();
                }
                else
                {
                    Console.WriteLine(Strings.OSNotSupported);
                    return false;
                }

                Console.WriteLine(Strings.Deployment);

                if (!Tools.CreateDriveMappingDirectory())
                {
                    Console.WriteLine("Error: " + Strings.DeployFailed);
                    return false;
                }

                Collection<PSObject> results = PS.Invoke();
                PS.Streams.ClearStreams();
                PS.Commands.Clear();
                if (results.Count == 0)
                {
                    Console.WriteLine("Error: " + Strings.DeployFailed);
                    return false;
                }
            
                Console.WriteLine();
                Console.WriteLine(Strings.Completed);
                Console.WriteLine(Strings.Reboot);
                return true;
            }

            return false;
        }

        private void PSErrorStreamHandler(object sender, DataAddedEventArgs e)
        {
            string text = ((PSDataCollection<ErrorRecord>)sender)[e.Index].ToString();

            // supress encoding exceptions
            if (!text.Contains("Exception setting \"OutputEncoding\""))
            {
                Console.WriteLine(text);
            }
        }

        private void PSWarningStreamHandler(object sender, DataAddedEventArgs e)
        {
            Console.WriteLine(((PSDataCollection<WarningRecord>)sender)[e.Index].ToString());
        }

        private void PSInfoStreamHandler(object sender, DataAddedEventArgs e)
        {
            Console.WriteLine(((PSDataCollection<InformationRecord>)sender)[e.Index].ToString());
        }

        public void CreateAzureIoTEdgeDevice(string azureCreateId)
        {
            PowerShell PS = PowerShell.Create();
         
            if (!SetupPrerequisits())
            {
                Console.WriteLine("Error: " + Strings.PreRequisitsFailed);
                return;
            }

            string connectionString = string.Empty;
            while ((connectionString == string.Empty)
              && !connectionString.StartsWith("Hostname=")
              && !connectionString.Contains(".azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey="))
            {
                Console.WriteLine("\n" + Strings.IoTHubs + ":");
                connectionString = Console.ReadLine();
            }
            AzureIoTHub azureIoTHub = new AzureIoTHub(connectionString);
           
            // check if device exists already
            var deviceEntity = azureIoTHub.GetDeviceAsync(azureCreateId).Result;
            if (deviceEntity != null)
            {
                char decision = 'a';
                while (decision != 'y' && decision != 'n')
                {
                    Console.WriteLine();
                    Console.Write(Strings.DeletedDevice + " [y/n]: ");
                    decision = Console.ReadKey().KeyChar;
                }
                Console.WriteLine();

                if (decision == 'y')
                {
                    azureIoTHub.DeleteDeviceAsync(azureCreateId).Wait();
                }
                else
                {
                    return;
                }
            }

            try
            {
                // create the device
                azureIoTHub.CreateIoTEdgeDeviceAsync(azureCreateId).Wait();

                // retrieve the newly created device
                deviceEntity = azureIoTHub.GetDeviceAsync(azureCreateId).Result;
                if (deviceEntity != null)
                {
                    if (!InstallIoTEdge(deviceEntity, azureIoTHub))
                    {
                        // installation failed so delete the device again
                        azureIoTHub.DeleteDeviceAsync(azureCreateId).Wait();
                    }
                }
                else
                {
                    Console.WriteLine("Error: " + Strings.CreateFailed);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);

                try
                {
                    // installation failed so delete the device again (if neccessary)
                    azureIoTHub.DeleteDeviceAsync(azureCreateId).Wait();
                }
                catch (Exception ex2)
                {
                    Console.WriteLine("Error: " + ex2.Message);
                }
            }
        }
    }
}
