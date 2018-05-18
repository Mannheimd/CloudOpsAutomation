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
            foreach (Metadata item in await DropboxTasks.GetOutOfDateItemsAsync("/ExternalUpload/Cloud Data/", new TimeSpan(31,0,0)))
            {
                LogHandler.CreateEntry(SeverityLevel.Trace, item.PathDisplay);
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
                LogHandler.CreateEntry(SeverityLevel.Trace, "Getting contents of " + path + " with recursive flag set to " + recursive);
                return await client.Files.ListFolderAsync(path, recursive: recursive);
            }
            catch
            {
                //TODO: Add error handling
                return null;
            }
        }

        public static async Task<List<Metadata>> GetOutOfDateItemsAsync(string path, TimeSpan maxAge)
        {
            DropboxClient client = ApplicationVariables.dropboxClient;
            List<Metadata> returnList = new List<Metadata>();

            ListFolderResult content = await getFolderContent(path, recursive: true);
            if (content == null)
                return null;

            foreach (Metadata item in content.Entries)
            {
                if (item.IsFile
                    && item.AsFile.ServerModified < DateTime.Now - maxAge)
                {
                    returnList.Add(item);
                }
                else if (item.IsFolder
                    && !new DropboxFolder(item.AsFolder).CheckForInDateContent(maxAge, content.Entries))
                {
                    returnList.Add(item);
                }
            }

            return returnList;
        }
    }

    public class DropboxFolder
    {
        public FolderMetadata metadata { get; private set; }

        public DropboxFolder(FolderMetadata metadata)
        {
            this.metadata = metadata;
        }

        private List<Metadata> GetChildren(IList<Metadata> content)
        {
            LogHandler.CreateEntry(SeverityLevel.Trace, "Fetching children of " + metadata.PathDisplay);

            List<Metadata> returnList = new List<Metadata>();

            foreach (Metadata item in content)
            {
                if (item.PathLower.StartsWith(metadata.PathLower))
                {
                    returnList.Add(item);
                }
            }

            LogHandler.CreateEntry(SeverityLevel.Trace, "Found " + returnList.Count.ToString() + " children of " + metadata.PathDisplay);

            return returnList;
        }

        public bool CheckForInDateContent(TimeSpan maxAge, IList<Metadata> content)
        {
            LogHandler.CreateEntry(SeverityLevel.Trace, "Checking for in-date content in " + metadata.PathDisplay);

            foreach (Metadata item in GetChildren(content))
            {
                if (item.IsFile
                    && item.AsFile.ServerModified >= DateTime.Now - maxAge)
                {
                    LogHandler.CreateEntry(SeverityLevel.Trace, "Found in-date content in " + metadata.PathDisplay);
                    return true;
                }
            }
            LogHandler.CreateEntry(SeverityLevel.Trace, "Did not find in-date content in " + metadata.PathDisplay);
            return false;
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
