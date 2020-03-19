using System.Windows;
using System.Windows.Controls;

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
