using IoTEdgeInstallerConsoleApp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace IoTEdgeInstaller
{
    class Installer
    {
        private static Installer _instance = null;
        private static object _creationLock = new object();

        public string AzureCreateId = Environment.MachineName;
               
        private List<NicsEntity> nics = new List<NicsEntity>();
        private int selectedNicIndex = 0;
        
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

        public async Task CreateAzureIoTEdgeDeviceAsync(AzureIoTHub hub, bool installIIoTModules)
        {
            if (MSAHelper.CurrentState == SigninStates.SignedIn)
            {
                if (hub != null)
                {
                    await CreateAzureIoTEdgeDeviceAsync(hub, AzureCreateId, installIIoTModules);
                }
            }
        }

        public async Task<AzureDeviceEntity> DiscoverDevicesAsync(AzureIoTHub iotHub)
        {
            if (MSAHelper.CurrentState == SigninStates.SignedIn)
            {
                return await GetAzureDevicesAsync(iotHub);
            }

            return null;
        }

        public void DiscoverIoTEdgeModules(AzureDeviceEntity device)
        {
            if (MSAHelper.CurrentState == SigninStates.SignedIn)
            {
                GetAzureIoTEdgeModules(device);
            }
        }
  
        private AzureIoTHub ShowAzureIoTHubList(List<AzureIoTHub> hubList)
        {
            for (int i = 0; i < hubList.Count; i++)
            {
                var hub = hubList[i];
                Console.WriteLine(i.ToString() + ": " + hub.Name);
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

        public AzureIoTHub DiscoverAzureIoTHubs()
        {
            return ShowAzureIoTHubList(AzureIoT.GetIotHubList(Program.ConsoleShowProgress, Program.ConsoleShowError, Program.RunPSCommand));
        }

        private AzureDeviceEntity ShowAzureDeviceList(List<AzureDeviceEntity> deviceList)
        {
            deviceList.Sort();

            foreach (var device in deviceList)
            {
                if (device.IotEdge)
                {
                    Console.WriteLine(device.Id + " (IoT Edge)");
                }
                else
                {
                    Console.WriteLine(device.Id);
                }
            }

            return null;
        }

        private async Task<AzureDeviceEntity> GetAzureDevicesAsync(AzureIoTHub azureIoTHub)
        {
            if (azureIoTHub != null)
            {
                return ShowAzureDeviceList(await azureIoTHub.GetDevicesAsync(Program.ConsoleShowError));
            }

            return null;
        }

        private void ShowAzureIoTEdgeModuleList(IList<AzureModuleEntity> moduleList)
        {
            foreach (var module in moduleList)
            {
                Console.WriteLine(module.Id + " " + module.ConnectionState.ToString());
            }
        }

        private void GetAzureIoTEdgeModules(AzureDeviceEntity azureDeviceEntity)
        {
            if (azureDeviceEntity != null)
            {
                if (azureDeviceEntity.IotEdge)
                {
                    ShowAzureIoTEdgeModuleList(azureDeviceEntity.Modules);
                }
            }
        }

        public void GetNicList()
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
                    nics.Add(entity);
                }
            }

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                ShowNicList(nics);
            }
        }

        private void ShowNicList(IList<NicsEntity> nicList)
        {
            Console.WriteLine();
            Console.WriteLine(Strings.Nic + " [0..n]:");

            for (int i = 0; i < nicList.Count; i++)
            {
                var nic = nicList[i];
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
                    if (index < nicList.Count)
                    {
                        selectionSuccessful = true;
                    }
                }
            }

            selectedNicIndex = (int)index;
        }

        private bool SetupPrerequisits()
        {
            try
            {
                PowerShell PS = PowerShell.Create();

                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    // TODO:
                    //"curl https://packages.microsoft.com/config/ubuntu/18.04/prod.list > ./microsoft-prod.list";
                    //"sudo cp ./microsoft-prod.list /etc/apt/sources.list.d/";
                    //"curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.gpg";
                    //"sudo cp ./microsoft.gpg /etc/apt/trusted.gpg.d/";
                    //"sudo apt-get update";
                    //"sudo apt-get --assume-yes install moby-engine";
                    //"sudo apt-get --assume-yes install moby-cli";
                }
                else if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    // setup Hyper-V switch
                    PS.AddScript("get-vmswitch");
                    Collection<PSObject> results = PS.Invoke();
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

                    PS.AddScript($"new-vmswitch -name host -NetAdapterName {nics.ElementAt(selectedNicIndex).Name} -AllowManagementOS $true");
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
                        newProcessInfo.Arguments = $"Invoke-WebRequest -useb aka.ms/iotedge-win | Invoke-Expression; Install-IoTEdge -ContainerOs Windows -Manual -DeviceConnectionString 'HostName={iotHub.Name.Substring(0, iotHub.Name.IndexOf(" "))}.azure-devices.net;DeviceId={deviceEntity.Id};SharedAccessKey={deviceEntity.PrimaryKey}' -SkipBatteryCheck";
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
                    // TODO:
                    //"sudo apt-get update";
                    //"sudo apt-get --assume-yes install iotedge";
                    //$"sudo sed -i 's/<ADD DEVICE CONNECTION STRING HERE>/HostName={iotHub.Name.Substring(0, iotHub.Name.IndexOf(" "))}.azure-devices.net;DeviceId={deviceEntity.Id};SharedAccessKey={deviceEntity.PrimaryKey}/g' /etc/iotedge/config.yaml";
                    //"sudo systemctl restart iotedge";
                }
                else
                {
                    Console.WriteLine(Strings.OSNotSupported);
                    return false;
                }

                if (installIIoTModules)
                {
                    Console.WriteLine(Strings.Deployment);

                    // first set the Azure subscription for the selected IoT Hub
                    string cmd = $"Az account set --subscription '{iotHub.SubscriptionName}'";
                    PS.AddScript(cmd);
                    Collection<PSObject> results = PS.Invoke();
                    PS.Streams.ClearStreams();
                    PS.Commands.Clear();

                    cmd = $"Az iot edge set-modules --device-id {deviceEntity.Id} --hub-name {iotHub.Name.Substring(0, iotHub.Name.IndexOf(" "))} --content ./iiotedgedeploymentmanifest.json";
                    PS.AddScript(cmd);
                    results = PS.Invoke();
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

        private async Task CreateAzureIoTEdgeDeviceAsync(AzureIoTHub azureIoTHub, string azureCreateId, bool installIIoTModules)
        {
            PowerShell PS = PowerShell.Create();
            PS.Streams.Warning.DataAdded += PSWarningStreamHandler;
            PS.Streams.Error.DataAdded += PSErrorStreamHandler;
            PS.Streams.Information.DataAdded += PSInfoStreamHandler;

            try
            {
                if (!SetupPrerequisits())
                {
                    Console.WriteLine("Error: " + Strings.PreRequisitsFailed);
                }
                else
                {
                    // create the device
                    await azureIoTHub.CreateDeviceAsync(azureCreateId, true);

                    // retrieve the newly created device
                    var deviceEntity = await azureIoTHub.GetDeviceAsync(azureCreateId);
                    if (deviceEntity != null)
                    {
                        if (!InstallIoTEdge(deviceEntity, azureIoTHub, installIIoTModules))
                        {
                            // installation failed so delete the device again
                            await azureIoTHub.DeleteDeviceAsync(azureCreateId);
                            Console.WriteLine(Strings.DeletedDevice);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Error: " + Strings.CreateFailed);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);

                try
                {
                    // installation failed so delete the device again (if neccessary)
                    await azureIoTHub.DeleteDeviceAsync(azureCreateId);
                    Console.WriteLine(Strings.DeletedDevice);
                }
                catch (Exception ex2)
                {
                    Console.WriteLine("Error: " + ex2.Message);
                }
            }
        }
    }
}
