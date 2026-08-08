using System;
using System.IO;
using System.Net;

namespace BetterTerminal.Updating
{
    /// <summary>
    /// Brings the release asset into a staging folder and hands back its path, but only once it is on
    /// disk in full and its file version really is newer than what is installed. A partial or
    /// mislabelled download is discarded rather than staged, so nothing is ever run as an upgrade
    /// that is not actually the upgrade it claims to be.
    ///
    /// Shared by the service, which stages into ProgramData for the app-closed case, and the
    /// application, which stages into its own profile for the notice it raises itself; the folder is
    /// the caller's to choose.
    /// </summary>
    internal static class UpdateDownloader
    {
        public static string Stage(ReleaseInfo release, Version installed, string stagingDirectory)
        {
            Directory.CreateDirectory(stagingDirectory);

            string destination = Path.Combine(stagingDirectory, UpdateShared.StagedFileName(release.Version));

            Version already = UpdateShared.FileVersion(destination);
            if (already != null && already == UpdateShared.Normalize(release.Version))
            {
                return destination;
            }

            string partial = destination + ".part";
            try
            {
                Fetch(release.AssetLocation, partial);

                Version staged = UpdateShared.FileVersion(partial);
                if (staged == null || !UpdateShared.IsNewer(staged, installed))
                {
                    return null;
                }

                if (File.Exists(destination))
                {
                    File.Delete(destination);
                }

                File.Move(partial, destination);
                return destination;
            }
            catch (WebException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            finally
            {
                if (File.Exists(partial))
                {
                    try
                    {
                        File.Delete(partial);
                    }
                    catch (IOException)
                    {
                    }
                }
            }
        }

        private static void Fetch(string location, string destination)
        {
            if (UpdateShared.LooksLikeLocalPath(location))
            {
                File.Copy(location, destination, true);
                return;
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(location);
            request.UserAgent = UpdateShared.UserAgent;
            request.Timeout = 30000;

            // The asset URL redirects to a content host; letting the request follow it is the point.
            request.AllowAutoRedirect = true;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream input = response.GetResponseStream())
            using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write))
            {
                if (input == null)
                {
                    throw new WebException("The release asset returned no content.");
                }

                input.CopyTo(output);
            }
        }
    }
}
