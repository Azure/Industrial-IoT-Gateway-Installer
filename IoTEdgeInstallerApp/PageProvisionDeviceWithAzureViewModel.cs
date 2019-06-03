using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Management.Automation;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace IoTEdgeInstaller
{
    class PageProvisionDeviceWithAzureViewModel : INotifyPropertyChanged
    {
        private PageProvisionDeviceWithAzure _parentPage;

        public event EventHandler HubEnumerationComplete;

        public delegate void SetUIStateType();

        public static bool QueueUserWorkItem(WaitCallback callBack, object state)
        {
            void safeCallback(object o)
            {
                callBack(o);
            }

            return ThreadPool.QueueUserWorkItem(safeCallback, state);
        }

        public PageProvisionDeviceWithAzureViewModel(PageProvisionDeviceWithAzure parentPage)
        {
            _parentPage = parentPage;

            AzureCreateId = Environment.MachineName;
            SelectedLocationIndex = 0;
            MainProgressVisibility = Visibility.Visible;

            AzureIoTHubs = new ObservableCollection<AzureIoTHub>();
            AzureDevices = new ObservableCollection<AzureDeviceEntity>();
            IoTEdgeModules = new ObservableCollection<AzureModuleEntity>();
            Nics = new ObservableCollection<NicsEntity>();
        }

        public void CreateAzureIoTEdgeDevice()
        {
            if (MSAHelper.CurrentState == SigninStates.SignedIn)
            {
                var azureIoTHub = AzureIoTHubs.ElementAt(SelectedAzureIoTHubIndex);
                if (azureIoTHub != null)
                {
                    QueueUserWorkItem(CreateAzureIoTEdgeDeviceAsync, azureIoTHub);
                }
            }
        }

        public void DiscoverDevices(AzureIoTHub iotHub)
        {
            if (MSAHelper.CurrentState == SigninStates.SignedIn)
            {
                QueueUserWorkItem(GetAzureDevicesAsync, iotHub);
            }
        }

        public void DiscoverIoTEdgeModules(AzureDeviceEntity device)
        {
            if (MSAHelper.CurrentState == SigninStates.SignedIn)
            {
                QueueUserWorkItem(GetAzureIoTEdgeModulesAsync, device);
            }
        }
        
        public void SetUIState(SetUIStateType uiDelegate)
        {
            if (Application.Current.Dispatcher.CheckAccess())
            {
                uiDelegate();
            }
            else
            {
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    uiDelegate();
                }));
            }
        }

        public void ShowMainProgressUI()
        {
            MainProgressVisibility = Visibility.Visible;
        }

        public void HideMainProgressUI()
        {
            MainProgressVisibility = Visibility.Hidden;
        }

        private void SetAzureIoTHubList(List<AzureIoTHub> hubList)
        {
            SetUIState(HideMainProgressUI);
            _parentPage.CreateDescription.Dispatcher.Invoke(() => _parentPage.CreateDescription.Text = Strings.NewAzureDevice, DispatcherPriority.Background);

            AzureIoTHubs.Clear();
            foreach (var hub in hubList)
            {
                AzureIoTHubs.Add(hub);
            }

            HubEnumerationComplete?.Invoke(this, EventArgs.Empty);
        }

        public async Task GetAzureIoTHubsAsync()
        {
            SetUIState(ShowMainProgressUI);
            _parentPage.CreateDescription.Dispatcher.Invoke(() => _parentPage.CreateDescription.Text = Strings.GatheringIoTHubs, DispatcherPriority.Background);

            var hubList = AzureIoT.GetIotHubList(_parentPage.WindowsShowProgress, _parentPage.WindowsShowError, _parentPage.RunPSCommand);
            
            if (Application.Current.Dispatcher.CheckAccess())
            {
                SetAzureIoTHubList(hubList);
            }
            else
            {
                await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    SetAzureIoTHubList(hubList);
                }));
            }

            await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                SetNicList();
            }));

            ChangeDevice = AzureIoTHubs.Count == 0 ? false : true;
        }

        private void SetAzureDeviceList(List<AzureDeviceEntity> deviceList)
        {
            SetUIState(HideMainProgressUI);

            deviceList.Sort();

            AzureDevices.Clear();
            foreach (var device in deviceList)
            {
                AzureDevices.Add(device);
            }

            SelectedAzureDeviceIndex = 0;
        }

        private async void GetAzureDevicesAsync(Object threadContext)
        {
            _parentPage.progressBar.Dispatcher.Invoke(() => _parentPage.progressBar.IsIndeterminate = true, DispatcherPriority.Background);
            SetUIState(ShowMainProgressUI);

            if (threadContext is AzureIoTHub azureIoTHub)
            {
                var deviceList = await azureIoTHub.GetDevicesAsync(_parentPage.WindowsShowError);

                if (Application.Current.Dispatcher.CheckAccess())
                {
                    SetAzureDeviceList(deviceList);
                }
                else
                {
                    await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        SetAzureDeviceList(deviceList);
                    }));
                }
            }
        }

        private void SetAzureIoTEdgeModuleList(IList<AzureModuleEntity> moduleList)
        {
            SetUIState(HideMainProgressUI);

            IoTEdgeModules.Clear();
            foreach (var module in moduleList)
            {
                IoTEdgeModules.Add(module);
            }

            SelectedAzureModuleIndex = 0;
        }

        private async void GetAzureIoTEdgeModulesAsync(Object threadContext)
        {
            _parentPage.progressBar.Dispatcher.Invoke(() => _parentPage.progressBar.IsIndeterminate = true, DispatcherPriority.Background);
            SetUIState(ShowMainProgressUI);

            if (threadContext is AzureDeviceEntity azureDeviceEntity)
            {
                IoTEdgeDevice = azureDeviceEntity.IotEdge;
                if (azureDeviceEntity.IotEdge)
                {

                    if (Application.Current.Dispatcher.CheckAccess())
                    {
                        SetAzureIoTEdgeModuleList(azureDeviceEntity.Modules);
                    }
                    else
                    {
                        await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            SetAzureIoTEdgeModuleList(azureDeviceEntity.Modules);
                        }));
                    }
                }
                else
                {
                    SetUIState(HideMainProgressUI);
                }
            }
        }

        private void SetNicList()
        {
            Nics.Clear();
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var nic in nics)
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
                    Nics.Add(entity);
                }
            }

            SelectedNicIndex = 0;
        }

        private bool SetupHyperVSwitch()
        {
            try
            {
                // check if setup already
                PowerShell PS = PowerShell.Create();
                PS.AddScript("get-vmswitch");
                Collection<PSObject> results = PS.Invoke();
                PS.Streams.ClearStreams();
                PS.Commands.Clear();
                if (results.Count == 0)
                {
                    MessageBox.Show(Strings.VSwitchSetupFailed, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                foreach(var vmSwitch in results)
                {
                    if (vmSwitch.ToString().Contains("(Name = 'host')"))
                    {
                        OutputLB += (Strings.VSwitchExists + "\n");
                        return true;
                    }
                }

                PS.AddScript($"new-vmswitch -name host -NetAdapterName {Nics.ElementAt(SelectedNicIndex).Name} -AllowManagementOS $true");
                results = PS.Invoke();
                PS.Streams.ClearStreams();
                PS.Commands.Clear();
                if (results.Count == 0)
                {
                    MessageBox.Show(Strings.VSwitchSetupFailed, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // It takes about 10 seconds for the network interfaces to come back up
                Thread.Sleep(10000);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
  
            return true;
        }

        private bool InstallIoTEdge(AzureDeviceEntity deviceEntity, AzureIoTHub iotHub)
        {
            if (deviceEntity != null)
            {
                PowerShell PS = PowerShell.Create();
                PS.Streams.Warning.DataAdded += PSWarningStreamHandler;
                PS.Streams.Error.DataAdded += PSErrorStreamHandler;
                PS.Streams.Information.DataAdded += PSInfoStreamHandler;

                OutputLB += (Strings.Uninstall + "\n");
                try
                {
                    PS.AddScript("Invoke-WebRequest -useb aka.ms/iotedge-win | Invoke-Expression; Uninstall-IoTEdge -Force");
                    Collection<PSObject> results1 = PS.Invoke();
                    PS.Streams.ClearStreams();
                    PS.Commands.Clear();
                    if (results1.Count == 0)
                    {
                        MessageBox.Show(Strings.UninstallFailed, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    PS.Streams.ClearStreams();
                    PS.Commands.Clear();
                    return false;
                }

                OutputLB += (Strings.Install + "\n");
                string cmd = $"Invoke-WebRequest -useb aka.ms/iotedge-win | Invoke-Expression; Install-IoTEdge -ContainerOs Windows -Manual -DeviceConnectionString 'HostName={iotHub.Name.Substring(0, iotHub.Name.IndexOf(" "))}.azure-devices.net;DeviceId={deviceEntity.Id};SharedAccessKey={deviceEntity.PrimaryKey}' -SkipBatteryCheck";
                PS.AddScript(cmd);
                Collection<PSObject> results = PS.Invoke();
                PS.Streams.ClearStreams();
                PS.Commands.Clear();
                if (results.Count == 0)
                {
                    MessageBox.Show(Strings.InstallFailed, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                if (_parentPage.CheckBox.IsChecked == true)
                {
                    OutputLB += (Strings.Deployment + "\n");

                    // first set the Azure subscription for the selected IoT Hub
                    cmd = $"Az account set --subscription '{iotHub.SubscriptionName}'";
                    PS.AddScript(cmd);
                    results = PS.Invoke();
                    PS.Streams.ClearStreams();
                    PS.Commands.Clear();

                    cmd = $"Az iot edge set-modules --device-id {deviceEntity.Id} --hub-name {iotHub.Name.Substring(0, iotHub.Name.IndexOf(" "))} --content ./iiotedgedeploymentmanifest.json";
                    PS.AddScript(cmd);
                    results = PS.Invoke();
                    PS.Streams.ClearStreams();
                    PS.Commands.Clear();
                    if (results.Count == 0)
                    {
                        MessageBox.Show(Strings.DeployFailed, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                }

                OutputLB += (Strings.Completed + "\n" + Strings.Reboot + "\n");
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
                OutputLB += text;
                OutputLB += "\n";
            }
        }

        private void PSWarningStreamHandler(object sender, DataAddedEventArgs e)
        {
            OutputLB += ((PSDataCollection<WarningRecord>)sender)[e.Index].ToString();
            OutputLB += "\n";
        }

        private void PSInfoStreamHandler(object sender, DataAddedEventArgs e)
        {
            OutputLB += ((PSDataCollection<InformationRecord>)sender)[e.Index].ToString();
            OutputLB += "\n";
        }

        private async void CreateAzureIoTEdgeDeviceAsync(Object threadContext)
        {
            _parentPage.progressBar.Dispatcher.Invoke(() => _parentPage.progressBar.IsIndeterminate = true, DispatcherPriority.Background);
            SetUIState(ShowMainProgressUI);
            _parentPage.CreateDescription.Dispatcher.Invoke(() => _parentPage.CreateDescription.Text = Strings.Installing, DispatcherPriority.Background);


            PowerShell PS = PowerShell.Create();
            PS.Streams.Warning.DataAdded += PSWarningStreamHandler;
            PS.Streams.Error.DataAdded += PSErrorStreamHandler;
            PS.Streams.Information.DataAdded += PSInfoStreamHandler;

            var azureIoTHub = threadContext as AzureIoTHub;
            try
            {
                if (!SetupHyperVSwitch())
                {
                    MessageBox.Show(Strings.PreRequisitsFailed, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    // create the device
                    await azureIoTHub.CreateDeviceAsync(_azureCreateId, true);

                    // retrieve the newly created device
                    var deviceEntity = await azureIoTHub.GetDeviceAsync(_azureCreateId);
                    if (deviceEntity != null)
                    {
                        if (!InstallIoTEdge(deviceEntity, azureIoTHub))
                        {
                            // installation failed so delete the device again
                            await azureIoTHub.DeleteDeviceAsync(_azureCreateId);
                        }
                    }
                    else
                    {
                        MessageBox.Show(Strings.CreateFailed, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    // refresh Azure device list
                    QueueUserWorkItem(GetAzureDevicesAsync, azureIoTHub);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);

                try
                {
                    // installation failed so delete the device again (if neccessary)
                    await azureIoTHub.DeleteDeviceAsync(_azureCreateId);
                    MessageBox.Show(Strings.DeletedDevice, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Exclamation);

                    // refresh Azure device list
                    QueueUserWorkItem(GetAzureDevicesAsync, azureIoTHub);
                }
                catch (Exception ex2)
                {
                    MessageBox.Show(ex2.Message, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            SetUIState(HideMainProgressUI);
            _parentPage.CreateDescription.Dispatcher.Invoke(() => _parentPage.CreateDescription.Text = string.Empty, DispatcherPriority.Background);
        }

        // list of Azure IoT hub shown in UI
        public ObservableCollection<AzureIoTHub> AzureIoTHubs { get; }

        // list of Azure devices shown in UI
        public ObservableCollection<AzureDeviceEntity> AzureDevices { get; }

        // list of IoTEdge modules shown in UI
        public ObservableCollection<AzureModuleEntity> IoTEdgeModules { get; }

        // list of Nics shown in UI
        public ObservableCollection<NicsEntity> Nics { get; }

        // control whether or not user can change either Azure or IoTCore device selection
        private bool _canChangeDevice = false;

        public bool ChangeDevice
        {
            get => _canChangeDevice;
            set
            {
                if (value != _canChangeDevice)
                {
                    _canChangeDevice = value;
                    NotifyPropertyChanged("CanChangeDevice");
                }
            }
        }

        public Visibility CanChangeDevice => _canChangeDevice ? Visibility.Visible : Visibility.Collapsed;

        private bool _ioTEdgeDevice = false;

        public bool IoTEdgeDevice
        {
            get => _ioTEdgeDevice;
            set
            {
                if (value != _ioTEdgeDevice)
                {
                    _ioTEdgeDevice = value;
                    NotifyPropertyChanged("IsIoTEdgeDevice");
                }
            }
        }

        public Visibility IsIoTEdgeDevice => _ioTEdgeDevice ? Visibility.Visible : Visibility.Collapsed;

        private int _selectedAzureModuleIndex = 0;

        public int SelectedAzureModuleIndex
        {
            get => _selectedAzureModuleIndex;
            set
            {
                if (value != _selectedAzureModuleIndex)
                {
                    _selectedAzureModuleIndex = value;
                    NotifyPropertyChanged("SelectedAzureModuleIndex");
                }
            }
        }

        // index of the selected Azure device
        private int _selectedAzureDeviceIndex = 0;

        public int SelectedAzureDeviceIndex
        {
            get => _selectedAzureDeviceIndex;
            set
            {
                if (value != _selectedAzureDeviceIndex)
                {
                    _selectedAzureDeviceIndex = value;
                    NotifyPropertyChanged("SelectedAzureDeviceIndex");
                }
            }
        }

        // index of the selected Azure Iot Hub
        private int _selectedAzureIoTHubIndex = 0;

        public int SelectedAzureIoTHubIndex
        {
            get => _selectedAzureIoTHubIndex;
            set
            {
                if (value != _selectedAzureIoTHubIndex)
                {
                    _selectedAzureIoTHubIndex = value;
                    NotifyPropertyChanged("SelectedAzureIoTHubIndex");
                }
            }
        }

        // index of the selected Azure Iot Hub
        private int _selectedNicIndex = 0;

        public int SelectedNicIndex
        {
            get => _selectedNicIndex;
            set
            {
                if (value != _selectedNicIndex)
                {
                    _selectedNicIndex = value;
                    NotifyPropertyChanged("SelectedNicIndex");
                }
            }
        }

        private string _azureCreateDescription;

        public string AzureCreateDescription
        {
            get => _azureCreateDescription;
            set
            {
                if (value != _azureCreateDescription)
                {
                    _azureCreateDescription = value;
                    NotifyPropertyChanged("AzureCreateDescription");
                }
            }
        }

        private Visibility _azureCreateVisibility = Visibility.Hidden;

        public Visibility AzureCreateVisibility
        {
            get => _azureCreateVisibility;
            set
            {
                if (value != _azureCreateVisibility)
                {
                    _azureCreateVisibility = value;
                    NotifyPropertyChanged("AzureCreateVisibility");
                }
            }
        }

        private string _azureCreateIdDesc;
        public string AzureCreateIdDesc
        {
            get => _azureCreateIdDesc;
            set
            {
                if (value != _azureCreateIdDesc)
                {
                    _azureCreateIdDesc = value;
                    NotifyPropertyChanged("AzureCreateIdDesc");
                }
            }
        }

        private string _azureCreateId;

        public string AzureCreateId
        {
            get => _azureCreateId;
            set
            {
                if (value != _azureCreateId)
                {
                    _azureCreateId = value;
                    NotifyPropertyChanged("AzureCreateId");
                }
            }
        }

        private string _outputLB;

        public string OutputLB
        {
            get => _outputLB;
            set
            {
                if (value != _outputLB)
                {
                    _outputLB = value;
                    NotifyPropertyChanged("OutputLB");
                }
            }
        }

        private int _selectedLocationIndex = 0;

        public int SelectedLocationIndex
        {
            get => _selectedLocationIndex;
            set
            {
                if (value != _selectedLocationIndex)
                {
                    _selectedLocationIndex = value;
                    NotifyPropertyChanged("SelectedLocationIndex");
                }
            }
        }

        private Visibility _mainProgressVisibility = Visibility.Hidden;

        public Visibility MainProgressVisibility
        {
            get => _mainProgressVisibility;
            set
            {
                if (value != _mainProgressVisibility)
                {
                    _mainProgressVisibility = value;
                    NotifyPropertyChanged("MainProgressVisibility");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string info)
        {
            if (PropertyChanged != null)
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(info));
                }
                else
                {
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        PropertyChanged(this, new PropertyChangedEventArgs(info));
                    }));
                }
            }
        }
    }
}
