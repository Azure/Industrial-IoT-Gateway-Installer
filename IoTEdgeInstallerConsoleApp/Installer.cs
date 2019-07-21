using IoTEdgeInstallerConsoleApp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Net.NetworkInformation;
using System.Threading;

namespace IoTEdgeInstaller
{
    class Installer
    {
        private static Installer _instance = null;
        private static object _creationLock = new object();
               
        private List<NicsEntity> _nicList = new List<NicsEntity>();
        private int _selectedNicIndex = 0;
        
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

        public AzureIoTHub SelectIoTHub(List<AzureIoTHub> hubList)
        {
            for (int i = 0; i < hubList.Count; i++)
            {
                var hub = hubList[i];
                Console.WriteLine(i.ToString() + ": " + hub.DisplayName);
            }

            bool selectionSuccessful = false;
            uint index = 0;
            while (!selectionSuccessful)
            {
                Console.WriteLine();
                Console.WriteLine(Strings.IoTHubs + " [0..n]");

                string selection = Console.ReadLine();
                if (uint.TryParse(selection, out index))
                {
                    if (index < hubList.Count)
                    {
                        selectionSuccessful = true;
                    }
                }
            }

            return hubList[(int) index];
        }

        public void SelectNic()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in interfaces)
                {
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                     || nic.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet
                     || nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    {
                        var entity = new NicsEntity
                        {
                            Name = nic.Name,
                            Description = nic.Description
                        };
                        _nicList.Add(entity);
                    }
                }

                Console.WriteLine();
                Console.WriteLine(Strings.Nic + " [0..n]:");

                for (int i = 0; i < _nicList.Count; i++)
                {
                    var nic = _nicList[i];
                    Console.WriteLine(i.ToString() + ": " + nic.Name + " (" + nic.Description + ")");
                }

                bool selectionSuccessful = false;
                uint index = 0;
                while (!selectionSuccessful)
                {
                    Console.WriteLine();
                    string selection = Console.ReadLine();
                    if (uint.TryParse(selection, out index))
                    {
                        if (index < _nicList.Count)
                        {
                            selectionSuccessful = true;
                        }
                    }
                }

                _selectedNicIndex = (int)index;
            }
        }

        private bool SetupPrerequisits()
        {
            try
            {
                PowerShell PS = PowerShell.Create();
                PS.Streams.Warning.DataAdded += PSWarningStreamHandler;
                PS.Streams.Error.DataAdded += PSErrorStreamHandler;
                PS.Streams.Information.DataAdded += PSInfoStreamHandler;

                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
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
                    // check if Hyper-V is enabled
                    PS.AddScript("$hyperv = Get-WindowsOptionalFeature -FeatureName Microsoft-Hyper-V-All -Online");
                    PS.AddScript("if($hyperv.State -eq 'Enabled') { write 'enabled' }");
                    Collection<PSObject> results = PS.Invoke();
                    PS.Streams.ClearStreams();
                    PS.Commands.Clear();
                    if (results.Count == 0)
                    {
                        Console.WriteLine("Error: " + Strings.VSwitchSetupFailed);
                        return false;
                    }
                    bool enabled = false;
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
                    
                    // setup Hyper-V switch
                    PS.AddScript("get-vmswitch");
                    results = PS.Invoke();
                    PS.Streams.ClearStreams();
                    PS.Commands.Clear();
                    if (results.Count == 0)
                    {
                        Console.WriteLine("Error: " + Strings.VSwitchSetupFailed);
                        return false;
                    }

                    foreach (var vmSwitch in results)
                    {
                        if (vmSwitch.ToString().Contains("(Name = 'host')"))
                        {
                            Console.WriteLine(Strings.VSwitchExists);
                            return true;
                        }
                    }

                    PS.AddScript($"new-vmswitch -name host -NetAdapterName {_nicList.ElementAt(_selectedNicIndex).Name} -AllowManagementOS $true");
                    results = PS.Invoke();
                    PS.Streams.ClearStreams();
                    PS.Commands.Clear();
                    if (results.Count == 0)
                    {
                        Console.WriteLine("Error: " + Strings.VSwitchSetupFailed);
                        return false;
                    }

                    // It takes about 10 seconds for the network interfaces to come back up
                    Thread.Sleep(10000);
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

        private bool InstallIoTEdge(AzureDeviceEntity deviceEntity, AzureIoTHub iotHub, bool installIIoTModules)
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
                        newProcessInfo.Arguments = $"Invoke-WebRequest -useb aka.ms/iotedge-win | Invoke-Expression; Install-IoTEdge -ContainerOs Windows -Manual -DeviceConnectionString 'HostName={iotHub.Name}.azure-devices.net;DeviceId={deviceEntity.Id};SharedAccessKey={deviceEntity.PrimaryKey}' -SkipBatteryCheck";
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
                    $"sudo sed -i 's/<ADD DEVICE CONNECTION STRING HERE>/HostName={iotHub.Name}.azure-devices.net;DeviceId={deviceEntity.Id};SharedAccessKey={deviceEntity.PrimaryKey}/g' /etc/iotedge/config.yaml".Bash();
                    "sudo systemctl restart iotedge".Bash();
                }
                else
                {
                    Console.WriteLine(Strings.OSNotSupported);
                    return false;
                }

                if (installIIoTModules)
                {
                    Console.WriteLine(Strings.Deployment);

                    if (!AzureIoT.CreateDriveMappingDirectory())
                    {
                        Console.WriteLine("Error: " + Strings.DeployFailed);
                        return false;
                    }

                    if (!AzureIoT.LoadDeploymentManifest())
                    {
                        Console.WriteLine("Error: " + Strings.DeployFailed);
                        return false;
                    }

                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        PS.AddScript($"Az iot edge set-modules --device-id {deviceEntity.Id} --hub-name {iotHub.Name} --content ./{AzureIoT.DeploymentManifestNameWindows}");
                    }
                    else if (Environment.OSVersion.Platform == PlatformID.Unix)
                    {
                        PS.AddScript($"Az iot edge set-modules --device-id {deviceEntity.Id} --hub-name {iotHub.Name} --content ./{AzureIoT.DeploymentManifestNameLinux}");
                    }
                    else
                    {
                        Console.WriteLine(Strings.OSNotSupported);
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

        public void CreateAzureIoTEdgeDevice(AzureIoTHub azureIoTHub, string azureCreateId, bool installIIoTModules)
        {
            PowerShell PS = PowerShell.Create();
            PS.Streams.Warning.DataAdded += PSWarningStreamHandler;
            PS.Streams.Error.DataAdded += PSErrorStreamHandler;
            PS.Streams.Information.DataAdded += PSInfoStreamHandler;

            if (!SetupPrerequisits())
            {
                Console.WriteLine("Error: " + Strings.PreRequisitsFailed);
                return;
            }
           
            // first set the Azure subscription for the selected IoT Hub
            PS.AddScript($"Az account set --subscription '{azureIoTHub.SubscriptionName}'");
            Collection<PSObject> results = PS.Invoke();
            PS.Streams.ClearStreams();
            PS.Commands.Clear();

            // check if device exists already
            var deviceEntity = azureIoTHub.GetDevice(Program.RunPSCommand, azureCreateId);
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
                    azureIoTHub.DeleteDevice(Program.RunPSCommand, azureCreateId);
                }
                else
                {
                    return;
                }
            }

            try
            {
                // create the device
                azureIoTHub.CreateIoTEdgeDevice(Program.RunPSCommand, azureCreateId);

                // retrieve the newly created device
                deviceEntity = azureIoTHub.GetDevice(Program.RunPSCommand, azureCreateId);
                if (deviceEntity != null)
                {
                    if (!InstallIoTEdge(deviceEntity, azureIoTHub, installIIoTModules))
                    {
                        // installation failed so delete the device again
                        azureIoTHub.DeleteDevice(Program.RunPSCommand, azureCreateId);
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
                    azureIoTHub.DeleteDevice(Program.RunPSCommand, azureCreateId);
                }
                catch (Exception ex2)
                {
                    Console.WriteLine("Error: " + ex2.Message);
                }
            }
        }
    }
}
