using System.Windows;

namespace IoTEdgeInstaller
{
    public partial class MainWindow : Window
    {
        private PageFlow _pageFlow;

        public MainWindow()
        {
            // Uncomment to test localization
            // System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("de-DE");

            InitializeComponent();

            Title = Strings.Strings.AboutSubtitle;

            _pageFlow = new PageFlow(_NavigationFrame);
            _pageFlow.Navigate(typeof(PageProvisionDeviceWithAzure));
        }
    }
}
