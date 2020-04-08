using Common;
using System;
using System.Collections.Generic;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace IoTEdgeInstaller
{
    public partial class PageProvisionDeviceWithAzure : Page
    {
        private readonly PageFlow _pageFlow;
        private PageProvisionDeviceWithAzureViewModel _viewModel;
        
        public PageProvisionDeviceWithAzure(PageFlow pageFlow)
        {
            _pageFlow = pageFlow;
            _viewModel = new PageProvisionDeviceWithAzureViewModel(this);
            DataContext = _viewModel;

            InitializeComponent();

            CreateDescription.Text = Strings.NewAzureDevice;
            InstallButton.Content = Strings.CreateEdgeButton;
            IoTHubTitle.Text = Strings.IoTHubs;
            IoTHubHint.Text = Strings.IoTHubsHint;
            CreateOptionsTitle.Text = Strings.AzureCreateDeviceIdDesc;

            // periodically update the module status (every 3 seconds)
            Timer timer = new Timer(3000);
            timer.Elapsed += Timer_Elapsed;
            timer.AutoReset = true;
            timer.Start();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                string statusText = Strings.ModulesStatus + "\r\n";
                AzureIoTHub hub = new AzureIoTHub(_viewModel.DisplayName);
                List<KeyValuePair<string, string>> modulesStatus = hub.GetDeviceModulesStatusAsync(_viewModel.AzureCreateId).Result;
                if ((modulesStatus != null) && (modulesStatus.Count > 0))
                {
                    foreach (KeyValuePair<string,string> moduleStatus in modulesStatus)
                    {
                        statusText += (moduleStatus.Key + ": " + moduleStatus.Value + "\r\n");
                    }
                }
                else
                {
                    statusText += Strings.NotAvailable;
                }

                ModulesStatus.Dispatcher.Invoke(() => ModulesStatus.Text = statusText, DispatcherPriority.Background);
            }
            catch (Exception)
            {
                // do nothing
            }
        }

        private void _pageFlow_PageChange(object sender, PageChangeCancelEventArgs e)
        {
            if (e.CurrentPage == this)
            {
                e.Close = true;
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _pageFlow.PageChange += _pageFlow_PageChange;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _pageFlow.PageChange -= _pageFlow_PageChange;
        }

        private void ButtonCreateAzureCreateEdge_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CreateAzureIoTEdgeDevice();
        }
    }
}
