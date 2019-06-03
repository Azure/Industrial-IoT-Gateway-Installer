using System;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace IoTEdgeInstaller
{
    /// <summary>
    /// Interaction logic for PageProvisionDeviceWithAzure.xaml
    /// </summary>
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
        }

        private void _pageFlow_PageChange(object sender, PageChangeCancelEventArgs e)
        {
            if (e.CurrentPage == this)
            {
                e.Close = true;
            }
        }

        public void WindowsShowProgress(double progress)
        {
            progressBar.Dispatcher.Invoke(() => progressBar.Value = progress, DispatcherPriority.Background);
        }

        public void WindowsShowError(string error)
        {
            MessageBox.Show(error, Strings.Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public Collection<string> RunPSCommand(string command)
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

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _pageFlow.PageChange += _pageFlow_PageChange;
            _viewModel.HubEnumerationComplete += _viewModel_HubEnumerationComplete;
            
            Task.Run(async () =>
            {
                if (MSAHelper.SignIn(WindowsShowProgress, WindowsShowError, RunPSCommand))
                {
                    await _viewModel.GetAzureIoTHubsAsync();
                }
                else
                {
                    _viewModel.SetUIState(_viewModel.HideMainProgressUI);
                }
            });
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _pageFlow.PageChange -= _pageFlow_PageChange;
            _viewModel.HubEnumerationComplete -= _viewModel_HubEnumerationComplete;
        }

        private void _viewModel_HubEnumerationComplete(object sender, EventArgs e)
        {
            if (_viewModel.AzureIoTHubs.Count == 0)
            {
                MessageBox.Show(Strings.Strings.ConnectToAzure_NoAzureIoTHub, Strings.Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void comboBoxModules_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var device = ((ListBox)sender).SelectedItem as AzureModuleEntity;
        }
                 
        private void ButtonCreateAzureCreateEdge_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CreateAzureIoTEdgeDevice();
        }

        private void comboBoxIotHub_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var azureIotHub = ((ComboBox)sender).SelectedItem as AzureIoTHub;
            _viewModel.DiscoverDevices(azureIotHub);
        }

        private void comboBoxDeviceId_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var azureDeviceEntity = ((ListBox)sender).SelectedItem as AzureDeviceEntity;
            _viewModel.DiscoverIoTEdgeModules(azureDeviceEntity);
        }

        private void comboBoxNics_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }
    }
}
