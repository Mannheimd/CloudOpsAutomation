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
                MessageBox.Show(item.IsFile + " " + item.PathDisplay);
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
                Loggy.Log("Reading content of " + path);
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

            ListFolderResult content = await getFolderContent(path);
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
                    && !await CheckForInDateContent(item.AsFolder, maxAge))
                {
                    returnList.Add(item);
                }
            }

            return returnList;
        }

        private static async Task<bool> CheckForInDateContent(FolderMetadata folder, TimeSpan maxAge)
        {
            Loggy.Log("Checking for content in " + folder.PathDisplay);

            ListFolderResult content = await getFolderContent(folder.PathLower, recursive: true);

            foreach (Metadata item in content.Entries)
            {
                if (item.IsFile
                    && item.AsFile.ServerModified >= DateTime.Now - maxAge)
                    return true;
            }
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

    public static class Loggy
    {
        static string logFilePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "loggy.txt";

        public static void Log(string message)
        {
            using (StreamWriter writer = new StreamWriter(logFilePath))
            {
                writer.WriteLine(message);
            }
        }
    }
}
