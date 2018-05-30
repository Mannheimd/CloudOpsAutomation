using Dropbox.Api;
using Dropbox.Api.Files;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Log_Handler;
using Dropbox.Api.FileRequests;
using Dropbox.Api.Async;
using Newtonsoft.Json;
using System.IO;

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
            foreach (Folder folder in ApplicationVariables.config.folders)
            {
                MessageBox.Show(folder.path + " | " + folder.maxAge);
            }

            //await DropboxTasks.DeleteOutOfDateContentAsync("/ExternalUpload/Cloud Data", new TimeSpan(31, 0, 0, 0));

            Application.Current.Shutdown();
        }
    }

    public static class DropboxTasks
    {
        public static async Task DeleteOutOfDateContentAsync(string path, TimeSpan maxAge)
        {
            List<Metadata> itemsToDelete = await GetOutOfDateItemsAsync(path, maxAge);

            if (itemsToDelete != null
                && itemsToDelete.Count > 0)
            {
                await DeleteItems(itemsToDelete);
            }
            else
            {
                LogHandler.CreateEntry(SeverityLevel.Info, "Nothing to delete");
            }

            LogHandler.CreateEntry(SeverityLevel.Info, "Finished deleting out of date content");
        }

        private static async Task<ListFolderResult> GetFolderContent(string path, bool recursive = false)
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

        private static async Task<List<Metadata>> GetOutOfDateItemsAsync(string folderPath, TimeSpan maxAge)
        {
            LogHandler.CreateEntry(SeverityLevel.Info, "Looking for items in " + folderPath + " older than " + maxAge);

            DropboxClient client = ApplicationVariables.dropboxClient;
            List<Metadata> returnList = new List<Metadata>();

            ListFolderResult content = await GetFolderContent(folderPath, recursive: true);
            if (content == null)
                return null;

            foreach (Metadata item in content.Entries)
            {
                if (item.IsFile)
                {
                    DropboxFile file = new DropboxFile(item.AsFile);
                    if (file.IsDirectDescendant(folderPath)
                        && !file.IsInDate(maxAge))
                    {
                        returnList.Add(item);
                    }
                }
                else if (item.IsFolder)
                {
                    DropboxFolder folder = new DropboxFolder(item.AsFolder);
                    if (folder.metadata.PathLower.ToLower() == folderPath.ToLower())
                        continue;

                    if (await folder.CanBeDeletedAsync(maxAge, content.Entries))
                    {
                        returnList.Add(item);
                    }
                }
            }

            return returnList;
        }

        private static async Task<DeleteBatchResult> DeleteItems(List<Metadata> items)
        {
            LogHandler.CreateEntry(SeverityLevel.Info, "Starting delete of " + items.Count + " items");

            DropboxClient client = ApplicationVariables.dropboxClient;
            DeleteBatchArg deleteBatchArg = new DeleteBatchArg(new List<DeleteArg>());

            foreach (Metadata item in items)
            {
                LogHandler.CreateEntry(SeverityLevel.Trace, "Queueing item for deletion: " + item.PathLower);
                deleteBatchArg.Entries.Add(new DeleteArg(item.PathLower));
            }

            DeleteBatchLaunch batchLaunch = new DeleteBatchLaunch();
            try
            {
                LogHandler.CreateEntry(SeverityLevel.Debug, "Sending delete batch");
                batchLaunch = await client.Files.DeleteBatchAsync(deleteBatchArg);
                LogHandler.CreateEntry(SeverityLevel.Trace, "Delete batch sent");
            }
            catch (Exception e)
            {
                LogHandler.CreateEntry(e, SeverityLevel.Error, "Failed to send delete batch");
                return null;
            }

            PollArg pollArg = new PollArg(batchLaunch.AsAsyncJobId.Value);
            DeleteBatchJobStatus batchStatus = new DeleteBatchJobStatus();
            do
            {
                LogHandler.CreateEntry(SeverityLevel.Trace, "Checking delete batch status");

                try
                {
                    batchStatus = await client.Files.DeleteBatchCheckAsync(pollArg);
                }
                catch (Exception e)
                {
                    LogHandler.CreateEntry(e, SeverityLevel.Error, "Checking on batch status failed");
                }

                if (batchStatus.IsInProgress)
                {
                    LogHandler.CreateEntry(SeverityLevel.Trace, "Delete batch is in progress");
                    await Task.Delay(1000);
                }
            }
            while (batchStatus.IsInProgress);

            LogHandler.CreateEntry(SeverityLevel.Trace, "Exited batch check loop");

            if (batchStatus.IsFailed)
            {
                LogHandler.CreateEntry(SeverityLevel.Error, "Delete batch failed: " + DeleteBatchErrorText(batchStatus.AsFailed.Value));
                return null;
            }
            else if (!batchStatus.IsComplete)
            {
                LogHandler.CreateEntry(SeverityLevel.Error, "Delete batch did not complete; unknown failure");
                return null;
            }

            return batchStatus.AsComplete.Value;
        }

        private static string DeleteBatchErrorText(DeleteBatchError error)
        {
            if (error.IsTooManyWriteOperations)
                return "Too many write operations";
            else
                return "Unknown error";
        }
    }

    public class DropboxFile
    {
        public FileMetadata metadata { get; private set; }

        public DropboxFile(FileMetadata metadata)
        {
            this.metadata = metadata;
        }

        public bool IsDirectDescendant(string folderPath)
        {
            if (folderPath + metadata.Name == metadata.PathLower)
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

        public static string configPath = Environment.CurrentDirectory + "\\config.json";

        private static Config _config;
        public static Config config
        {
            get
            {
                if (_config == null)
                {
                    if (!File.Exists(configPath))
                    {
                        LogHandler.CreateEntry(SeverityLevel.Error, "Config file doesn't exist: " + configPath);
                        return null;
                    }

                    string json = File.ReadAllText(configPath);
                    _config = JsonConvert.DeserializeObject<Config>(json);
                }
                return _config;
            }
        }
    }

    public class Config
    {
        public List<Folder> folders { get; set; }
    }

    public class Folder
    {
        public string path { get; set; }
        public TimeSpan maxAge { get; private set; }
    }
}
