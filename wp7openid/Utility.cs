using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.IO;
using System.Xml.Linq;
using Majestic12;

namespace wp7openid
{
    public static class Utility
    {
        private const string _XRD_XMLNS = @"xri://$xrd*($v*2.0)";
        private const string _OPENID_XMLNS = @"http://openid.net/xmlns/1.0";

        #region Provider discovery
        /// <summary>
        /// This function starts the process of discovering the user's OpenID provider based on the URL
        /// passed in.
        /// </summary>
        /// <param name="userSelectedUrl">The URL the user typed in or selected.</param>
        /// <param name="discoveryCompletedDelegate">The delegate to call once discovery completes.</param>
        /// <returns>Whether the discovery was started successfully.</returns>
        internal static bool DiscoverProviderUri(string userSelectedUrl, Action<ProviderDiscoveryData> discoveryCompletedDelegate)
        {
            // Retrieve the URL specified by the user.
            try
            {
                // Create the request.
                var request = CreateDiscoveryWebRequest(userSelectedUrl);
                if (request == null) return false;
                // Initiate the request.
                request.BeginGetResponse(GotDiscoveryPage, new object[] { request, discoveryCompletedDelegate });
                return true;
            }
            catch (Exception)
            {
                return false;
            }

            /*
            // TODO: Actual proper resolution.
            // For now, assume Google.
            discoveryCompletedDelegate("https://www.google.com/accounts/o8/ud");
            //resolveDelegate("http://www.myopenid.com/server");
            */

        }

        static void GotDiscoveryPage(IAsyncResult result)
        {
            var stateObjects = (object[])result.AsyncState;
            var request = (HttpWebRequest)stateObjects[0];
            var callback = (Action<ProviderDiscoveryData>)stateObjects[1];

            HttpWebResponse response;

            try
            {
                response = (HttpWebResponse)request.EndGetResponse(result);
            }
            catch (Exception ex)
            {
                // Signal a failure.
                callback(new ProviderDiscoveryData { Success = false, FailureReason = ex });
                return;
            }

            // Let's take a look at this response.

            // Do we have an XRDS document on our hands?
            if (response.ContentType.StartsWith("application/xrds+xml"))
            {
                // We do. Get the contents and send them off for processing. Then we're done here.
                var reader = new StreamReader(response.GetResponseStream());
                var xrdsData = reader.ReadToEnd();
                response.Close();
                ProcessXrds(xrdsData, callback);
                return;
            }

            // Look for a telling header.
            if (!string.IsNullOrEmpty(response.Headers["X-XRDS-Location"]))
            {
                // We know where to look. Create a new request to get that document, and point its callback right back to this function.
                var newRequest = CreateDiscoveryWebRequest(response.Headers["X-XRDS-Location"]);
                response.Close();
                if (newRequest == null)
                {
                    // Signal a failure.
                    callback(new ProviderDiscoveryData { Success = false });
                    return;
                }
                newRequest.BeginGetResponse(GotDiscoveryPage, new object[] { newRequest, callback });
                return;
            }

            // So much for keeping it simple. Now we've got to parse HTML to figure out something about OpenID at this URL.
            // Read the HTML.
            var reader2 = new StreamReader(response.GetResponseStream());
            var htmlData = reader2.ReadToEnd();
            response.Close();

            // Initialize the HTML parser.
            var parser = new HTMLparser();
            parser.SetChunkHashMode(false);
            parser.bDecodeEntities = true;
            parser.Init(htmlData);

            // Go though every chunk and look for useful tags.
            HTMLchunk chunk;
            string xrdsPointer = null;
            string openid2Provider = null;
            string openid2OpLocal = null;
            while ((chunk = parser.ParseNextTag()) != null)
            {
                if (chunk.oType != HTMLchunkType.OpenTag && chunk.oType != HTMLchunkType.CloseTag) continue;

                if (chunk.sTag != "meta" && chunk.sTag != "link") continue;

                // Convert the params to a dictionary, with keys being lowercase.
                var dict = new Dictionary<string, string>();
                for (var i = 0; i < chunk.iParams; i++)
                    dict[chunk.sParams[i].ToLower().Trim()] = chunk.sValues[i];

                // Do we have a META tag?
                if (chunk.sTag == "meta")
                {
                    // Do we have an XRDS pointer?
                    if (dict.ContainsKey("http-equiv") && dict.ContainsKey("content") &&
                        dict["http-equiv"].Equals("X-XRDS-Location", StringComparison.CurrentCultureIgnoreCase))
                        xrdsPointer = dict["content"];
                }
                else if (chunk.sTag == "link")
                {
                    if (dict.ContainsKey("rel") && dict.ContainsKey("href"))
                    {
                        // There are certain RELs we care about.
                        if (dict["rel"].Contains("openid2.provider")) openid2Provider = dict["href"];
                        else if (dict["rel"].Contains("openid2.local_id")) openid2OpLocal = dict["href"];
                    }
                }
            }

            // Do we have needed LINKs?
            if (openid2Provider != null)
            {
                // Yes we do! Signal success.
                callback(new ProviderDiscoveryData { Success = true, DiscoveredClaimedIdentifier = true, ProviderUri = openid2Provider, OpLocalIdentity = openid2OpLocal });
                return;
            }

            // Do we have an XRDS pointer?
            if (xrdsPointer != null)
            {
                // Yes we do! Retrieve that and point back to this function.
                var newRequest = CreateDiscoveryWebRequest(xrdsPointer);
                if (newRequest == null)
                {
                    // Signal a failure.
                    callback(new ProviderDiscoveryData { Success = false });
                    return;
                }
                newRequest.BeginGetResponse(GotDiscoveryPage, new object[] { newRequest, callback });
                return;
            }

            // We got nothing :(
            callback(new ProviderDiscoveryData { Success = false, FailureReason = new Exception("Could not find OpenID endpoint.") });
        }

        static void ProcessXrds(string xrds, Action<ProviderDiscoveryData> callback)
        {
            XElement xml;
            try
            {
                // Load the XML.
                xml = XElement.Parse(xrds);
            }
            catch (Exception ex)
            {
                // Signal a failure.
                callback(new ProviderDiscoveryData { Success = false, FailureReason = ex });
                return;
            }

            // Per discovery rules, we must first look for an OP Identifier service element.
            var opId = from x in xml.Elements(XName.Get("XRD", _XRD_XMLNS)).Elements(XName.Get("Service", _XRD_XMLNS)).Elements(XName.Get("Type", _XRD_XMLNS))
                       where x.Value == "http://specs.openid.net/auth/2.0/server"
                       orderby x.Parent.Attribute(XName.Get("priority", _XRD_XMLNS))
                       select x.Parent;
            if (opId.Count() > 0)
            {
                // We've got one.
                var firstOpId = opId.First();
                if (firstOpId.Element(XName.Get("URI", _XRD_XMLNS)) != null)
                {
                    var opIdUri = firstOpId.Element(XName.Get("URI", _XRD_XMLNS)).Value;

                    // Signal success.
                    callback(new ProviderDiscoveryData { Success = true, DiscoveredClaimedIdentifier = false, ProviderUri = opIdUri });
                    return;
                }
            }

            // Look for a claimed identifier service element.
            var claimedId = from x in xml.Elements(XName.Get("XRD", _XRD_XMLNS)).Elements(XName.Get("Service", _XRD_XMLNS)).Elements(XName.Get("Type", _XRD_XMLNS))
                            where x.Value == "http://specs.openid.net/auth/2.0/signon"
                            orderby x.Parent.Attribute(XName.Get("priority", _XRD_XMLNS))
                            select x.Parent;
            if (claimedId.Count() > 0)
            {
                // We've got one.
                var firstClaimedId = claimedId.First();
                if (firstClaimedId.Element(XName.Get("URI", _XRD_XMLNS)) != null)
                {
                    var opIdUri = firstClaimedId.Element(XName.Get("URI", _XRD_XMLNS)).Value;
                    string opLocal = null;
                    if (firstClaimedId.Element(XName.Get("LocalID", _XRD_XMLNS)) != null) opLocal = firstClaimedId.Element(XName.Get("LocalID", _XRD_XMLNS)).Value;

                    // Signal success.
                    callback(new ProviderDiscoveryData { Success = true, DiscoveredClaimedIdentifier = true, ProviderUri = opIdUri, OpLocalIdentity = opLocal });
                    return;
                }
            }

            // Give up and signal a failure.
            callback(new ProviderDiscoveryData { Success = false, FailureReason = new Exception("Could not find compatible OpenID endpoint.")});
        }
        #endregion

        #region Helper methods
        internal static Dictionary<string, string> SplitUrlQuery(string queryOnly)
        {
            var parts = queryOnly.Split('&');
            return parts.Select(part => part.Split('='))
                            .Where(keyValuePair => keyValuePair.Length == 2)
                            .ToDictionary(keyValuePair => HttpUtility.UrlDecode(keyValuePair[0]), keyValuePair => HttpUtility.UrlDecode(keyValuePair[1]));
        }

        internal static Dictionary<string,string> SplitKeyValueForm(string data)
        {
            var parts = data.Split('\n');
            return parts.Select(part => part.Split(':'))
                            .Where(keyValuePair => keyValuePair.Length == 2)
                            .ToDictionary(keyValuePair => keyValuePair[0].Trim(), keyValuePair => keyValuePair[1].Trim());
        }

        private static HttpWebRequest CreateDiscoveryWebRequest(string url)
        {
            // Create the request object.
            var request = WebRequest.Create(url) as HttpWebRequest;
            if (request == null) return null;
            // Set the request's properties appropriately.
            request.Accept = "application/xrds+xml,text/html,*/*";
            request.UserAgent = "wp7openid";
            // Return the object.
            return request;
        }
        #endregion
    }
}
