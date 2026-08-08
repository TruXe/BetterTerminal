using System;
using System.Net;

namespace BetterTerminal.Updating
{
    internal sealed class ReleaseInfo
    {
        public ReleaseInfo(Version version, string assetLocation)
        {
            Version = version;
            AssetLocation = assetLocation;
        }

        public Version Version { get; private set; }

        /// <summary>An https download URL, or a local path under the test hook.</summary>
        public string AssetLocation { get; private set; }
    }

    /// <summary>
    /// Finds the latest published release without an API token or JSON. The web endpoint
    /// /releases/latest answers a 302 to /releases/tag/&lt;tag&gt;, so a single request with redirects
    /// turned off yields the tag from the Location header and nothing is downloaded to read it.
    ///
    /// Shared: the service polls it in the background and the application asks it directly at start,
    /// so a new release is seen at once without waiting on the service's next poll.
    /// </summary>
    internal static class ReleaseFeed
    {
        public static ReleaseInfo Latest()
        {
            string feed = UpdateShared.FeedOverride();
            if (!string.IsNullOrEmpty(feed))
            {
                Version overridden = UpdateShared.ParseTag(feed);
                string asset = UpdateShared.AssetOverride();
                return overridden == null || string.IsNullOrEmpty(asset)
                    ? null
                    : new ReleaseInfo(overridden, asset);
            }

            string tag = LatestTag();
            if (tag == null)
            {
                return null;
            }

            Version version = UpdateShared.ParseTag(tag);
            return version == null ? null : new ReleaseInfo(version, UpdateShared.AssetUrl(tag));
        }

        private static string LatestTag()
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(UpdateShared.LatestReleaseUrl);
            request.AllowAutoRedirect = false;
            request.UserAgent = UpdateShared.UserAgent;
            request.Timeout = 15000;

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    return TagFromLocation(response.Headers["Location"]);
                }
            }
            catch (WebException error)
            {
                // A redirect with auto-redirect off is not an error, but some stacks still surface it
                // as one; the response it carries has the Location that answers the question.
                HttpWebResponse response = error.Response as HttpWebResponse;
                if (response == null)
                {
                    return null;
                }

                using (response)
                {
                    return TagFromLocation(response.Headers["Location"]);
                }
            }
        }

        private static string TagFromLocation(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return null;
            }

            int lastSlash = location.LastIndexOf('/');
            return lastSlash >= 0 && lastSlash < location.Length - 1
                ? location.Substring(lastSlash + 1)
                : null;
        }
    }
}
