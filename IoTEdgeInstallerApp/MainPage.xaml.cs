using Common;
using System.Windows;

namespace IoTEdgeInstaller
{
    public partial class MainWindow : Window
    {
        private PageFlow _pageFlow;

        public MainWindow()
        {
            // Uncomment to test localization
            //System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("de-DE");

            Title = Strings.AboutSubtitle;

            InitializeComponent();

            _pageFlow = new PageFlow(_NavigationFrame);
            _pageFlow.Navigate(typeof(PageProvisionDeviceWithAzure));
        }
    }
}
