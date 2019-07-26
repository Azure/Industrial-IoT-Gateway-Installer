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

            CreateDescription.Text = Strings.Prerequisits;
            InstallButton.Content = Strings.CreateEdgeButton;
            IoTHubTitle.Text = Strings.IoTHubs;
            IoTEdgeTitle.Text = Strings.IoTEdgeModules;
            CreateOptionsTitle.Text = Strings.AzureCreateDeviceIdDesc;
            NicsTitle.Text = Strings.Nic;
            CheckBox.Content = Strings.InstallIIoT;
        }

        private void _pageFlow_PageChange(object sender, PageChangeCancelEventArgs e)
        {
            if (e.CurrentPage == this)
            {
                e.Close = true;
            }
        }

        public void WindowsShowProgress(double progress, bool isAbsolute)
        {
            if (isAbsolute)
            {
                progressBar.Dispatcher.Invoke(() => progressBar.Value = progress, DispatcherPriority.Background);
            }
            else
            {
                progressBar.Dispatcher.Invoke(() => progressBar.Value += progress, DispatcherPriority.Background);
            }
        }

        public void WindowsShowError(string error)
        {
            MessageBox.Show(error, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void PSErrorStreamHandler(object sender, DataAddedEventArgs e)
        {
            string text = ((PSDataCollection<ErrorRecord>)sender)[e.Index].ToString();

            // supress encoding exceptions
            if (!text.Contains("Exception setting \"OutputEncoding\""))
            {
                _viewModel.OutputLB += text;
                _viewModel.OutputLB += "\n";
            }
        }

        private void PSWarningStreamHandler(object sender, DataAddedEventArgs e)
        {
            _viewModel.OutputLB += ((PSDataCollection<WarningRecord>)sender)[e.Index].ToString();
            _viewModel.OutputLB += "\n";
        }

        private void PSInfoStreamHandler(object sender, DataAddedEventArgs e)
        {
            _viewModel.OutputLB += ((PSDataCollection<InformationRecord>)sender)[e.Index].ToString();
            _viewModel.OutputLB += "\n";
        }

        public Collection<string> RunPSCommand(string command)
        {
            Collection<string> returnValues = new Collection<string>();
            using (PowerShell ps = PowerShell.Create())
            {
                //ps.Streams.Warning.DataAdded += PSWarningStreamHandler;
                //ps.Streams.Error.DataAdded += PSErrorStreamHandler;
                //ps.Streams.Information.DataAdded += PSInfoStreamHandler;

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
            _viewModel.OutputLB = string.Empty;
            OutputBox.Width = 430;
            if (_viewModel.AzureIoTHubs.Count == 0)
            {
                MessageBox.Show(Strings.ConnectToAzure_NoAzureIoTHub, Strings.AboutSubtitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ButtonCreateAzureCreateEdge_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CreateAzureIoTEdgeDevice();
        }
    }
}
