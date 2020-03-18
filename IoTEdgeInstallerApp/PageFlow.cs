using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Linq;
using System.ComponentModel;

namespace IoTEdgeInstaller
{
    public class PageChangeCancelEventArgs : CancelEventArgs
    {
        public PageChangeCancelEventArgs(Page currentPage, Page newPage)
        {
            CurrentPage = currentPage;
            NewPage = newPage;
        }

        public Page CurrentPage { get; private set; }

        public Page NewPage { get; private set; }

        public bool Close { get; set; }
    }

    public class PageFlow
    {
        private Dictionary<string, Page> _appPages = new Dictionary<string, Page>();
        private Frame _navigationFrame;
        public Page CurrentPage { get; private set; }

        public PageFlow(Frame navigationFrame)
        {
            _navigationFrame = navigationFrame;
            _navigationFrame.Navigating += _navigationFrame_Navigating;
            _navigationFrame.Navigated += _navigationFrame_Navigated;
        }

        private void _navigationFrame_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            CurrentPage = e.Content as Page;
        }

        public delegate void PageFlowCancelEventHandler(object sender, PageChangeCancelEventArgs e);

        public event PageFlowCancelEventHandler PageChange;

        private void _navigationFrame_Navigating(object sender, System.Windows.Navigation.NavigatingCancelEventArgs e)
        {
            if (PageChange != null)
            {
                PageChangeCancelEventArgs args = new PageChangeCancelEventArgs(CurrentPage, e.Content as Page);
                PageChange(this, args);

                if (args.Close && CurrentPage != null)
                {
                    foreach (var item in _appPages.Where(kvp => kvp.Value == CurrentPage).ToList())
                    {
                        _appPages.Remove(item.Key);
                    }
                }
            }
        }

        public void GoBack()
        {
            _navigationFrame.GoBack();
        }

        public void Close(Page caller)
        {
            GoBack();

            foreach (var item in _appPages.Where(kvp => kvp.Value == caller).ToList())
            {
                _appPages.Remove(item.Key);
            }
        }

        public void Navigate(Type pageType, params object[] arguments)
        {
            Page page;
            string pageName = $"{pageType}:{string.Join(":", arguments)}";
            var fullParameters = (new object[] { this }).Concat(arguments).ToArray();

            if (!_appPages.TryGetValue(pageName, out page))
            {
                page = Activator.CreateInstance(pageType, fullParameters) as Page;
                System.Diagnostics.Debug.Assert(page != null);

                _appPages.Add(pageName, page);
            }

            _navigationFrame.Navigate(page);
        }
    }
}
