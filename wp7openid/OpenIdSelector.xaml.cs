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

namespace wp7openid
{
    public partial class OpenIdSelector : UserControl
    {
        public OpenIdSelector()
        {
            InitializeComponent();
        }

        public class OpenIdSelectedEventArgs : EventArgs
        {
            public string OpenId { get; set; }
        }

        public event EventHandler<OpenIdSelectedEventArgs> OpenIdSelected;

        private void RaiseSelectedEvent(string openId)
        {
            if (OpenIdSelected != null) OpenIdSelected(this, new OpenIdSelectedEventArgs { OpenId = openId });
        }

        private void GoogleSignInButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseSelectedEvent("https://www.google.com/accounts/o8/id");
        }

        private void YahooSignInButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseSelectedEvent("http://yahoo.com/");
        }

        private void MyOpenIdSignInButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseSelectedEvent("http://myopenid.com/");
        }

        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            if(string.IsNullOrEmpty(OpenIdUriTextBox.Text))
            {
                MessageBox.Show("Please enter an OpenID URI.");
                return;
            }

            RaiseSelectedEvent(OpenIdUriTextBox.Text);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseSelectedEvent(null);
        }
    }
}
