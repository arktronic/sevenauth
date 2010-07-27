using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;

namespace OpenIdTestApp
{
    public partial class MainPage : PhoneApplicationPage
    {
        // Constructor
        public MainPage()
        {
            InitializeComponent();
            oidLogin.LoginComplete += oidLogin_LoginComplete;
            oidLogin.NavigationStarted += oidLogin_NavigationStarted;
            oidLogin.NavigationCompleted += oidLogin_NavigationCompleted;
            oidSelect.OpenIdSelected += oidSelect_OpenIdSelected;
        }

        void oidSelect_OpenIdSelected(object sender, wp7openid.OpenIdSelector.OpenIdSelectedEventArgs e)
        {
            if(string.IsNullOrEmpty(e.OpenId))
            {
                MessageBox.Show("Canceled.", "Uh-oh!", MessageBoxButton.OK);
                return;
            }

            oidSelect.Visibility = Visibility.Collapsed;
            ShowWaitPanel(true);
            oidLogin.DoLogin(e.OpenId);
        }

        void oidLogin_NavigationStarted(object sender, EventArgs e)
        {
            oidLogin.Visibility = Visibility.Collapsed;
            ShowWaitPanel(true);
        }

        void oidLogin_NavigationCompleted(object sender, EventArgs e)
        {
            oidLogin.Visibility = Visibility.Visible;
            ShowWaitPanel(false);
        }

        void oidLogin_LoginComplete(object sender, wp7openid.OpenIdLogin.OpenIdLoginCompleteEventArgs e)
        {
            if(!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<object, wp7openid.OpenIdLogin.OpenIdLoginCompleteEventArgs>(oidLogin_LoginComplete), sender, e);
                return;
            }
            oidLogin.Visibility = Visibility.Collapsed;
            ShowWaitPanel(false);
            if (e.Success)
                MessageBox.Show("OpenID authenticated as:\r\n" + e.AuthenticatedOpenId, "Success!", MessageBoxButton.OK);
            else
                MessageBox.Show("Reason: " + e.FailureReason.Message, "Failed", MessageBoxButton.OK);
        }

        void ShowWaitPanel(bool show)
        {
            if(show)
            {
                //WaitTextBlock.Opacity = 0;
                WaitPanel.Visibility = Visibility.Visible;
                if(WaitStoryboard.GetCurrentState() == ClockState.Stopped) WaitStoryboard.Begin();
            }
            else
            {
                WaitPanel.Visibility = Visibility.Collapsed;
            }
        }
    }
}
