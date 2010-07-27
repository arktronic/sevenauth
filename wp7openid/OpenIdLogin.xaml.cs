using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace wp7openid
{
    public partial class OpenIdLogin : UserControl
    {
        public class OpenIdLoginCompleteEventArgs : EventArgs
        {
            public bool Success { get; set; }
            public string AuthenticatedOpenId { get; set; }
            public Exception FailureReason { get; set; }
        }

        public event EventHandler<OpenIdLoginCompleteEventArgs> LoginComplete;
        public event EventHandler NavigationStarted;
        public event EventHandler NavigationCompleted;

        public string LocalUrl { get; set; }

        private string _userSpecifiedUri;
        private string _claimedId;
        private string _resolvedUri;

        public OpenIdLogin()
        {
            InitializeComponent();
            LocalUrl = "http://wp7openid.local/login";
        }

        public void DoLogin(string userSpecifiedUri)
        {
            _userSpecifiedUri = userSpecifiedUri;
            Utility.DiscoverProviderUri(userSpecifiedUri, GotProviderUrl);
        }

        internal void GotProviderUrl(ProviderDiscoveryData data)
        {
            // If no success, then we're done here.
            if (!data.Success)
            {
                // Signal a failure.
                if (LoginComplete != null) LoginComplete(this, new OpenIdLoginCompleteEventArgs { Success = false, FailureReason = data.FailureReason });
                return;
            }

            // Do we have a claimed ID?
            if (data.DiscoveredClaimedIdentifier)
            {
                // The URL the user entered shall be used to identify that user.
                _claimedId = _userSpecifiedUri;
            }

            // Proceed to login.
            _resolvedUri = data.ProviderUri;
            var fullUrl = ConstructFullAuthUrl(data.ProviderUri, data.OpLocalIdentity, LocalUrl);
            browser.Navigate(new Uri(fullUrl));
        }

        private void ContinueLogin(string passedUrlQuery)
        {
            // Process the query.
            if (passedUrlQuery.StartsWith("?") || passedUrlQuery.StartsWith("&"))
                passedUrlQuery = passedUrlQuery.Substring(1);
            var qry = Utility.SplitUrlQuery(passedUrlQuery);

            // Is there a cancel flag?
            if (qry.ContainsKey("openid.mode") && qry["openid.mode"] == "cancel")
            {
                // Login is canceled.
                if (LoginComplete != null) LoginComplete(this, new OpenIdLoginCompleteEventArgs { Success = false, FailureReason = new Exception("Login was canceled.") });
                return;
            }

            // Modify the query as needed.
            if (qry.ContainsKey("openid.mode"))
                qry.Remove("openid.mode");
            qry.Add("openid.mode", "check_authentication");

            // Do we already have a claimed ID?
            if (_claimedId == null)
            {
                // We need to get the claimed ID from the query.
                if (qry.ContainsKey("openid.claimed_id"))
                {
                    _claimedId = qry["openid.claimed_id"];
                }
                else
                {
                    // We have no claimed ID. Fail.
                    if (LoginComplete != null) LoginComplete(this, new OpenIdLoginCompleteEventArgs { Success = false, FailureReason = new Exception("No claimed identifier was found.") });
                    return;
                }
            }

            var fullUrl = new StringBuilder(_resolvedUri);
            fullUrl.Append(_resolvedUri.Contains("?") ? "&" : "?");
            fullUrl.Append(qry.ToUrlQuery());

            var verifyRequest = (HttpWebRequest)WebRequest.Create(new Uri(fullUrl.ToString()));
            verifyRequest.Method = "GET";
            verifyRequest.BeginGetResponse(GotVerifyRequestResponse, verifyRequest);
        }

        private void GotVerifyRequestResponse(IAsyncResult result)
        {
            // Get all the variables we need.
            var verifyRequest = (HttpWebRequest)result.AsyncState;
            var response = (HttpWebResponse)verifyRequest.EndGetResponse(result);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                // Error out.
                var args = new OpenIdLoginCompleteEventArgs { AuthenticatedOpenId = "", Success = false, FailureReason = new Exception("Verification response returned bad status code: " + response.StatusCode) };
                if (LoginComplete != null) LoginComplete(this, args);
                return;
            }

            // Proceed with verification.
            var stream = response.GetResponseStream();
            var bytes = new byte[stream.Length];
            stream.Read(bytes, 0, (int)stream.Length);
            var statusText = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
            stream.Close();
            var status = Utility.SplitKeyValueForm(statusText);
            if (status.ContainsKey("is_valid") && status["is_valid"] == "true")
            {
                // Hooray! It's good!
                if (LoginComplete != null) LoginComplete(this, new OpenIdLoginCompleteEventArgs { Success = true, AuthenticatedOpenId = _claimedId });
                return;
            }
            // Something has gone wrong. Signal a failure.
            if (LoginComplete != null) LoginComplete(this, new OpenIdLoginCompleteEventArgs { Success = false, FailureReason = new Exception("OpenID verification failed.") });
        }

        private static string ConstructFullAuthUrl(string providerUrl, string opLocalId, string returnToUrl)
        {
            // Determine the query parts we need.
            if (string.IsNullOrEmpty(opLocalId)) opLocalId = "http://specs.openid.net/auth/2.0/identifier_select";
            var qry = new Dictionary<string, string>
                          {
                              {"openid.ns", "http://specs.openid.net/auth/2.0"},
                              {"openid.claimed_id", "http://specs.openid.net/auth/2.0/identifier_select"},
                              {"openid.identity", opLocalId},
                              {"openid.mode", "checkid_setup"},
                              {"openid.return_to", returnToUrl}
                          };
            // Create and return the full URL.
            var fullUrl = new StringBuilder(providerUrl);
            fullUrl.Append(providerUrl.Contains("?") ? "&" : "?");
            fullUrl.Append(qry.ToUrlQuery());

            return fullUrl.ToString();
        }

        private void browser_Navigating(object sender, Microsoft.Phone.Controls.NavigatingEventArgs e)
        {
            if (e.Uri.AbsoluteUri.StartsWith(LocalUrl))
            {
                ContinueLogin(e.Uri.AbsoluteUri.Substring(LocalUrl.Length));
                e.Cancel = true;
                return;
            }

            if (NavigationStarted != null) NavigationStarted(this, new EventArgs());
        }

        private void browser_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            if (NavigationCompleted != null) NavigationCompleted(this, new EventArgs());
        }
    }
}
