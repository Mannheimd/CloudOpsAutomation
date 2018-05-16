using Dropbox.Api;
using Dropbox.Api.Files;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

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
            MessageBox.Show("Hello");
            Current.Shutdown();
        }
    }

    public static class DropboxTasks
    {
        public static async List<Metadata> GetOutOfDateItemsAsync(string path, TimeSpan maxAge)
        {
            DropboxClient client = ApplicationVariables.dropboxClient;
            List<Metadata> returnList = new List<Metadata>();

            ListFolderResult content;
            try
            {
                content = await client.Files.ListFolderAsync(path, recursive: true);
            }
            catch
            {
                //TODO: Add error handling
                return null;
            }

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
                    && !CheckForInDateContent(item.AsFolder, maxAge))
                {

                }
            }
        }

        private static bool CheckForInDateContent(FolderMetadata folder, TimeSpan maxAge)
        {
            
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
