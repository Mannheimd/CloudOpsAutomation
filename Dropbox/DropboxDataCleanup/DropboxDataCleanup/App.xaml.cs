using Dropbox.Api;
using Dropbox.Api.Files;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Log_Handler;
using Dropbox.Api.FileRequests;

namespace DropboxDataCleanup
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            Startup += AppStartup;
        }

        void AppStartup(object sender, StartupEventArgs e)
        {
            RunHandler.Run();
        }
    }

    public class RunHandler
    {
        public static async void Run()
        {
            foreach (Metadata item in await DropboxTasks.GetOutOfDateItemsAsync("/ExternalUpload/Cloud Data/", new TimeSpan(31,0,0,0)))
            {
                LogHandler.CreateEntry(SeverityLevel.Debug, item.PathDisplay);
            }

            Application.Current.Shutdown();
        }
    }

    public static class DropboxTasks
    {
        private static async Task<ListFolderResult> getFolderContent(string path, bool recursive = false)
        {
            DropboxClient client = ApplicationVariables.dropboxClient;

            try
            {
                LogHandler.CreateEntry(SeverityLevel.Debug, "Getting contents of " + path + " with recursive flag set to " + recursive);
                return await client.Files.ListFolderAsync(path, recursive: recursive);
            }
            catch
            {
                //TODO: Add error handling
                return null;
            }
        }

        public static async Task<List<Metadata>> GetOutOfDateItemsAsync(string folderPathWithSlash, TimeSpan maxAge)
        {
            DropboxClient client = ApplicationVariables.dropboxClient;
            List<Metadata> returnList = new List<Metadata>();

            ListFolderResult content = await getFolderContent(folderPathWithSlash, recursive: true);
            if (content == null)
                return null;

            foreach (Metadata item in content.Entries)
            {
                if (item.IsFile)
                {
                    DropboxFile file = new DropboxFile(item.AsFile);
                    if (file.IsDirectDescendant(folderPathWithSlash)
                        && !file.IsInDate(maxAge))
                    {
                        returnList.Add(item);
                    }
                }
                else if (item.IsFolder)
                {
                    DropboxFolder folder = new DropboxFolder(item.AsFolder);
                    if (folder.metadata.PathLower + "/" == folderPathWithSlash)
                        continue;

                    if (await folder.CanBeDeletedAsync(maxAge, content.Entries))
                    {
                        returnList.Add(item);
                    }
                }
            }

            return returnList;
        }
    }

    public class DropboxFile
    {
        public FileMetadata metadata { get; private set; }

        public DropboxFile(FileMetadata metadata)
        {
            this.metadata = metadata;
        }

        public bool IsDirectDescendant(string folderPathWithSlash)
        {
            if (folderPathWithSlash + metadata.Name == metadata.PathLower)
                return true;
            else
                return false;
        }

        public bool IsInDate(TimeSpan maxAge)
        {
            if (metadata.ServerModified < DateTime.Now - maxAge)
                return false;
            else
                return true;
        }
    }

    public class DropboxFolder
    {
        public FolderMetadata metadata { get; private set; }

        public DropboxFolder(FolderMetadata metadata)
        {
            this.metadata = metadata;
        }

        public async Task<bool> CanBeDeletedAsync(TimeSpan maxAge, IList<Metadata> content)
        {
            return (IsNotEmpty(content) && HasNoInDateContent(maxAge, content));
            // Did make an async call to HasNoOpenRequests, removed that due to Dropbox API limitation
        }

        private List<Metadata> GetChildren(IList<Metadata> content)
        {
            LogHandler.CreateEntry(SeverityLevel.Debug, "Fetching children of " + metadata.PathLower);

            List<Metadata> returnList = new List<Metadata>();

            foreach (Metadata item in content)
            {
                if (item.PathLower.StartsWith(metadata.PathLower)
                    && item.PathLower != metadata.PathLower)
                {
                    LogHandler.CreateEntry(SeverityLevel.Trace, "Found child " + item.PathLower);
                    returnList.Add(item);
                }
            }

            LogHandler.CreateEntry(SeverityLevel.Debug, "Found " + returnList.Count.ToString() + " children of " + metadata.PathLower);

            return returnList;
        }

        private bool HasNoInDateContent(TimeSpan maxAge, IList<Metadata> content)
        {
            LogHandler.CreateEntry(SeverityLevel.Debug, "Checking for content in " + metadata.PathLower + " modified after " + (DateTime.Now - maxAge));

            foreach (Metadata item in GetChildren(content))
            {
                if (item.IsFile)
                {
                    DropboxFile file = new DropboxFile(item.AsFile);
                    if (file.IsInDate(maxAge))
                    {
                        LogHandler.CreateEntry(SeverityLevel.Trace, "Found in-date item: " + file.metadata.PathLower);
                        LogHandler.CreateEntry(SeverityLevel.Debug, "Found in-date content in " + metadata.PathLower);
                        return false;
                    }
                    else
                    {
                        LogHandler.CreateEntry(SeverityLevel.Trace, "Found out-of-date item: " + file.metadata.PathLower + " modified " + file.metadata.ServerModified);
                    }
                }
            }
            LogHandler.CreateEntry(SeverityLevel.Debug, "Did not find in-date content in " + metadata.PathLower);
            return true;
        }

        /// <summary>
        /// Only checks for open requests by the active user. This is a limitation of the Dropbox API.
        /// </summary>
        /// <returns></returns>
        private async Task<bool> HasNoOpenRequests()
        {
            DropboxClient client = ApplicationVariables.dropboxClient;

            ListFileRequestsResult requests = await client.FileRequests.ListAsync();
            foreach (FileRequest request in requests.FileRequests)
            {
                if (request.Destination == metadata.PathLower)
                    return false;
            }

            return true;
        }

        private bool IsNotEmpty(IList<Metadata> content)
        {
            LogHandler.CreateEntry(SeverityLevel.Trace, "Checking if folder is empty: " + metadata.PathLower);

            List<Metadata> children = GetChildren(content);
            if (children == null || children.Count == 0)
            {
                LogHandler.CreateEntry(SeverityLevel.Info, "Folder is empty: " + metadata.PathLower);
                return false;
            }
            else
            {
                LogHandler.CreateEntry(SeverityLevel.Trace, "Folder is not empty: " + metadata.PathLower);
                return true;
            }
        }
    }

    public static class ApplicationVariables
    {
        private static DropboxClient _dropboxClient;
        public static DropboxClient dropboxClient
        {
            get
            {
                if (_dropboxClient == null)
                {
                    _dropboxClient = DropboxAuth.SetupClient();
                }
                return _dropboxClient;
            }
        }
    }
}
