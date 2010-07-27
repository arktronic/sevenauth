using System;

namespace wp7openid
{
    public class ProviderDiscoveryData
    {
        /// <summary>
        /// Whether discovery succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// When true, the discovery is for a claimed identifier, so the original URL provided by the user is the OpenID to store.
        /// When false, the discovery is for an OP identifier, so whatever the OP ends up telling us will be the OpenID to store.
        /// </summary>
        public bool DiscoveredClaimedIdentifier { get; set; }

        /// <summary>
        /// Optional string to give to the OP when authenticating.
        /// </summary>
        public string OpLocalIdentity { get; set; }

        /// <summary>
        /// The discovered URI.
        /// </summary>
        public string ProviderUri { get; set; }

        /// <summary>
        /// The reason, if any, for Success to equal False.
        /// </summary>
        public Exception FailureReason { get; set; }
    }
}
