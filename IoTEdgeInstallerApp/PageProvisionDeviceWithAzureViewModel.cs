﻿using Common;
using Microsoft.Azure.Devices;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Management.Automation;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace IoTEdgeInstaller
{
    class PageProvisionDeviceWithAzureViewModel : INotifyPropertyChanged
    {
        private PageProvisionDeviceWithAzure _parentPage;

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
            MainProgressVisibility = Visibility.Visible;
        }

        public void CreateAzureIoTEdgeDevice()
        {
            if (!_parentPage._isSaaSSetup)
            {
                if ((DisplayName == null)
                 || (DisplayName == string.Empty)
                 || !DisplayName.StartsWith("HostName=")
                 || !DisplayName.Contains(".azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey="))
                {
                    MessageBox.Show(Strings.IoTHubs, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    QueueUserWorkItem(CreateAzureIoTEdgeDevice, new AzureIoTHub(DisplayName));
                }
            }
            else
            {
                QueueUserWorkItem(CreateAzureIoTEdgeDevice, null);
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

        private bool CheckPreReqs()
        {
            try
            {
                bool useLCOW = false;
                _parentPage.Dispatcher.Invoke(() =>
                {
                    useLCOW = (_parentPage.UseLCoW.IsChecked == true);
                });

                // check if we are on 1809 build 17763, which is the only supported version of Windows 10 for IoT Edge on Windows containers
                if ((Environment.OSVersion.Version.Build != 17763) && !useLCOW)
                {
                    MessageBox.Show(Strings.OSNotSupported, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                
                PowerShell PS = PowerShell.Create();

                // check if bitlocker is enabled
                PS.AddScript("manage-bde -status C:");
                Collection<PSObject> results = PS.Invoke();
                PS.Streams.ClearStreams();
                PS.Commands.Clear();
                if (results.Count == 0)
                {
                    MessageBox.Show(Strings.BitLockerStatus, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show(Strings.BitlockerDisabled, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                // check if Hyper-V is enabled
                PS.AddScript("$hyperv = Get-WindowsOptionalFeature -FeatureName Microsoft-Hyper-V -Online");
                PS.AddScript("if($hyperv.State -eq 'Enabled') { write 'enabled' }");
                results = PS.Invoke();
                PS.Streams.ClearStreams();
                PS.Commands.Clear();
                if (results.Count == 0)
                {
                    MessageBox.Show(Strings.HyperVNotEnabled, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show(Strings.HyperVNotEnabled, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // check if Docker Desktop is running
                if (useLCOW)
                {
                    PS.AddScript("docker -v");
                    results = PS.Invoke();
                    PS.Streams.ClearStreams();
                    PS.Commands.Clear();
                    if (results.Count == 0)
                    {
                        MessageBoxResult result = MessageBox.Show("Docker was not detected. Do you want to install it now?", Strings.AboutSubtitle, MessageBoxButton.YesNo, MessageBoxImage.Error);
                        if (result == MessageBoxResult.Yes)
                        {
                            System.Diagnostics.Process.Start("https://download.docker.com/win/stable/Docker%20Desktop%20Installer.exe");
                        }
                        
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
  
            return true;
        }

        private bool InstallIoTEdge(Device deviceEntity, AzureIoTHub iotHub)
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

            bool useLCOW = false;
            bool isSaaS = false;
            string idScope = string.Empty;
            string deviceID = string.Empty;
            string primaryKey = string.Empty;
            _parentPage.Dispatcher.Invoke(() =>
            {
                useLCOW = (_parentPage.UseLCoW.IsChecked == true);
                isSaaS = _parentPage._isSaaSSetup;
                idScope = _parentPage.DPS_IDScope.Text;
                deviceID = _parentPage.DPS_DeviceID.Text;
                primaryKey = _parentPage.DPS_PrimaryKey.Text;
            });

            string os = "Windows";
            if (useLCOW)
            {
                os = "Linux";
            }

            if (isSaaS)
            {
                PS.AddScript($"Invoke-WebRequest -useb aka.ms/iotedge-win | Invoke-Expression; Install-IoTEdge -ContainerOs {os} -Dps -ScopeId {idScope} -RegistrationId {deviceID} -SymmetricKey {primaryKey} -SkipBatteryCheck");
            }
            else
            {
                if (deviceEntity != null)
                {
                    PS.AddScript($"Invoke-WebRequest -useb aka.ms/iotedge-win | Invoke-Expression; Install-IoTEdge -ContainerOs {os} -Manual -DeviceConnectionString 'HostName={iotHub.Name}.azure-devices.net;DeviceId={deviceEntity.Id};SharedAccessKey={deviceEntity.Authentication.SymmetricKey.PrimaryKey}' -SkipBatteryCheck");
                }
                else
                {
                    MessageBox.Show(Strings.InstallFailed, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }

            Collection<PSObject> results = PS.Invoke();
            PS.Streams.ClearStreams();
            PS.Commands.Clear();
            if (results.Count == 0)
            {
                MessageBox.Show(Strings.InstallFailed, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!Tools.CreateDriveMappingDirectory())
            {
                MessageBox.Show(Strings.DeployFailed, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            OutputLB += (Strings.Completed + "\n" + Strings.Reboot + "\n");
            return true;
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

        private void CreateAzureIoTEdgeDevice(Object threadContext)
        {
            _parentPage.progressBar.Dispatcher.Invoke(() => _parentPage.progressBar.IsIndeterminate = true, DispatcherPriority.Background);
            SetUIState(ShowMainProgressUI);
            _parentPage.CreateDescription.Dispatcher.Invoke(() => _parentPage.CreateDescription.Text = Strings.Installing, DispatcherPriority.Background);
            
            PowerShell PS = PowerShell.Create();
            var azureIoTHub = threadContext as AzureIoTHub;

            if (!CheckPreReqs())
            {
                MessageBox.Show(Strings.PreRequisitsFailed, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                if (!_parentPage._isSaaSSetup)
                {
                    // check if device exists already
                    Device deviceEntity = azureIoTHub.GetDeviceAsync(_azureCreateId).Result;
                    if (deviceEntity != null)
                    {
                        MessageBoxResult result = MessageBox.Show(Strings.DeletedDevice, Strings.AboutSubtitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes)
                        {
                            azureIoTHub.DeleteDeviceAsync(_azureCreateId).Wait();
                        }
                        else
                        {
                            SetUIState(HideMainProgressUI);
                            _parentPage.CreateDescription.Dispatcher.Invoke(() => _parentPage.CreateDescription.Text = Strings.NewAzureDevice, DispatcherPriority.Background);
                            return;
                        }
                    }

                    try
                    {
                        bool useLCOW = false;
                        _parentPage.Dispatcher.Invoke(() =>
                        {
                            useLCOW = (_parentPage.UseLCoW.IsChecked == true);
                        });

                        string os = "Windows";
                        if (useLCOW)
                        {
                            os = "Linux";
                        }

                        // create the device
                        azureIoTHub.CreateIoTEdgeDeviceAsync(_azureCreateId, os).Wait();

                        // retrieve the newly created device
                        deviceEntity = azureIoTHub.GetDeviceAsync(_azureCreateId).Result;
                        if (deviceEntity != null)
                        {
                            if (!InstallIoTEdge(deviceEntity, azureIoTHub))
                            {
                                // installation failed so delete the device again
                                azureIoTHub.DeleteDeviceAsync(_azureCreateId).Wait();
                            }
                        }
                        else
                        {
                            MessageBox.Show(Strings.CreateFailed, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        // format restart requests corrrectly
                        if (ex.Message.Contains("restart the computer"))
                        {
                            MessageBox.Show(Strings.Reboot2, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show(ex.Message, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                        }

                        try
                        {
                            // installation failed so delete the device again (if neccessary)
                            azureIoTHub.DeleteDeviceAsync(_azureCreateId).Wait();

                        }
                        catch (Exception ex2)
                        {
                            MessageBox.Show(ex2.Message, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                else
                {
                    try
                    {
                        InstallIoTEdge(null, null);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }

            SetUIState(HideMainProgressUI);
            _parentPage.CreateDescription.Dispatcher.Invoke(() => _parentPage.CreateDescription.Text = Strings.NewAzureDevice, DispatcherPriority.Background);
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

        private string _displayName;

        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (value != _displayName)
                {
                    _displayName = value;
                    NotifyPropertyChanged("DisplayName");
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
