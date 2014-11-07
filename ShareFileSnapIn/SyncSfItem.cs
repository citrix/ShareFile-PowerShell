using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using ShareFile.Api;
using ShareFile.Api.Models;
using System.IO;
using ShareFile.Api.Client;
using ShareFile.Api.Client.Transfers;
using ShareFile.Api.Client.FileSystem;
using ShareFile.Api.Client.Transfers.Uploaders;
using System.Threading;
using ShareFile.Api.Powershell.Parallel;
using ShareFile.Api.Powershell.Log;
using ShareFile.Api.Client.Requests;
using ShareFile.Api.Client.Exceptions;
using ShareFile.Api.Powershell.Properties;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace ShareFile.Api.Powershell
{
    /// <summary>
    /// Sync-SFItem command to sync/copy/move items between local and sharefile locations
    /// </summary>
    [Cmdlet("Sync", Noun, SupportsShouldProcess = true, DefaultParameterSetName = SetNameAttr)]
    public class SyncSfItem : PSCmdlet
    {
        #region Local Variables

        private const string Noun = "SfItem";

        // different Parameter Set Names for different type of behaviors of command
        private const string SetNameAttr = "SetAttr";
        private const string SetNamePos = "SetPos";
        private const string SetNameHelp = "SetHelp";

        private const string DefaultSharefileFolder = "My Files & Folders";

        private FileSupport FileSupport;

        // Keep track of abandone/resume functionality if disconnected meanwhile any operation
        private Resume.ResumeSupport ResumeSupport { get; set; }

        #endregion

        #region Command Arguments

        /// <summary>
        /// Sharefile location
        /// </summary>
        [Alias("SF", "ShareFile")]
        [Parameter(Mandatory = true, ParameterSetName = SetNameAttr)]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = SetNamePos)]
        public string ShareFilePath { get; set; }

        /// <summary>
        /// Local location
        /// </summary>
        [Alias("L", "Local", "Path")]
        [Parameter(Position = 1, ParameterSetName = SetNamePos)]
        [Parameter(Mandatory = false, ParameterSetName = SetNameAttr)]
        public string LocalPath { get; set; }

        /// <summary>
        /// Download param flag
        /// </summary>
        [Alias("Down", "D")]
        [Parameter()]
        public SwitchParameter Download { get; set; }

        /// <summary>
        /// Upload param flag
        /// </summary>
        [Alias("Up", "U")]
        [Parameter()]
        public SwitchParameter Upload { get; set; }

        /// <summary>
        /// Synchronize param flag
        /// </summary>
        [Alias("Sync", "S")]
        [Parameter()]
        public SwitchParameter Synchronize { get; set; }

        /// <summary>
        /// Recursive/Deep param flag
        /// </summary>
        [Alias("Deep", "R")]
        [Parameter()]
        public SwitchParameter Recursive { get; set; }

        /// <summary>
        /// Create root folder param flag
        /// </summary>
        [Alias("CR")]
        [Parameter()]
        public SwitchParameter CreateRoot { get; set; }

        /// <summary>
        /// Overwrite param flag
        /// </summary>
        [Alias("O")]
        [Parameter()]
        public SwitchParameter OverWrite { get; set; }

        /// <summary>
        /// Move (Cut+Paste) param flag
        /// </summary>
        [Alias("M")]
        [Parameter()]
        public SwitchParameter Move { get; set; }

        /// <summary>
        /// Keep Folders param flag (in case of move just delete files from source location)
        /// </summary>
        [Alias("KF")]
        [Parameter()]
        public SwitchParameter KeepFolders { get; set; }

        /// <summary>
        /// Strict param flag
        /// </summary>
        public SwitchParameter Strict { get; set; }

        /// <summary>
        /// Version param flag
        /// </summary>
        [Alias("Version", "V")]
        [Parameter(ParameterSetName = SetNameHelp, Mandatory = false)]
        public SwitchParameter Help { get; set; }

        /// <summary>
        /// Details/Description text to upload to Sharefile location
        /// </summary>
        [Parameter()]
        public String Details { get; set; }

        #endregion

        /// <summary>
        /// Sync-SFItem Command is invoked
        /// </summary>
        protected override void ProcessRecord()
        {
            FileSupport = new FileSupport(TestMethod);
            ProviderInfo providerInfo;
            PSDriveInfo driveInfo;

            if (Upload && Download)
            {
                throw new PSArgumentException("Provide only one switch to Upload or Download the files");
            }

            if (!string.IsNullOrEmpty(ShareFilePath))
            {
                if (!Upload && !Download)
                {
                    throw new PSArgumentException("Upload or Download switch must be specified");
                }

                if (string.IsNullOrEmpty(LocalPath))
                {
                    // use current user directory location if Local path is not specified in arguments
                    LocalPath = this.SessionState.Path.CurrentFileSystemLocation.Path;
                }

                if (Synchronize)
                {
                    Recursive = true;
                }

                if (Strict)
                {
                    Move = true;
                }

                if (Move)
                {
                    Recursive = true;
                }

                ActionType actionType = OverWrite ? ActionType.Force : (Synchronize ? ActionType.Sync : ActionType.None);

                int transactionId = new Random((int)DateTime.Now.Ticks).Next();

                // if current user directory is local storage and ShareFile path provided withouth drive letter then append the drive letter with sharefile location
                if (this.SessionState.Path.CurrentLocation.Provider.ImplementingType != typeof(ShareFileProvider) && ShareFilePath.IndexOf(":") < 1)
                {
                    Collection<PSDriveInfo> providerDrives = this.SessionState.Drive.GetAll();// ForProvider("ShareFile");
                    foreach (PSDriveInfo driveObj in providerDrives)
                    {
                        if (driveObj.Provider.ImplementingType == typeof(ShareFileProvider))
                        {
                            if (ShareFilePath.StartsWith("/") || ShareFilePath.StartsWith(@"\"))
                            {
                                ShareFilePath = ShareFilePath.Substring(1);
                            }
                            string sfDrive = String.Format("{0}:/", driveObj.Name);
                            ShareFilePath = Path.Combine(sfDrive, ShareFilePath);
                            break;
                        }
                    }
                }

                var sourcePath = Upload ? LocalPath : ShareFilePath;

                // it will resolve paths if wildcards are used e.g. "D:\\*.txt" then get paths of all files with txt extension from D:\\ location
                var resolvedPaths = this.GetResolvedProviderPathFromPSPath(sourcePath, out providerInfo);

                if (Download)
                {
                    this.SessionState.Path.GetUnresolvedProviderPathFromPSPath(sourcePath, out providerInfo, out driveInfo);

                    var client = ((ShareFileDriveInfo)driveInfo).Client;

                    StartDownload(client, driveInfo, resolvedPaths, actionType);
                }
                else
                {
                    var unresolvedPath = this.SessionState.Path.GetUnresolvedProviderPathFromPSPath(ShareFilePath, out providerInfo, out driveInfo);

                    var client = ((ShareFileDriveInfo)driveInfo).Client;
                    Item targetItem = ShareFileProvider.GetShareFileItem((ShareFileDriveInfo)driveInfo, unresolvedPath);

                    if (targetItem == null && !unresolvedPath.StartsWith(String.Format(@"\{0}\", DefaultSharefileFolder)))
                    {
                        string updatedPath = String.Format(@"\{0}\{1}", DefaultSharefileFolder, unresolvedPath);
                        targetItem = ShareFileProvider.GetShareFileItem((ShareFileDriveInfo)driveInfo, updatedPath, null, null);
                    }
                    //else if (targetItem == null)
                    //{
                    //    targetItem = ShareFileProvider.GetShareFileItem((ShareFileDriveInfo)driveInfo, ShareFilePath, null, null);
                    //}

                    if (targetItem == null)
                    {
                        throw new FileNotFoundException("Destination path not found on ShareFile server.");
                    }

                    // if user didn't specify the Sharefile HomeFolder in path then appending in path
                    // e.g. if user tries sf:/Folder1 as sharefile target then resolve this path to sf:/My Files & Folders/Folder1
                    if ((targetItem as Folder).Info.IsAccountRoot == true)
                    {
                        string updatedPath = String.Format(@"\{0}\{1}", DefaultSharefileFolder, unresolvedPath);
                        targetItem = ShareFileProvider.GetShareFileItem((ShareFileDriveInfo)driveInfo, updatedPath, null, null);
                    }

                    StartUpload(client, transactionId, targetItem, resolvedPaths, actionType);
                }

                WriteObject("Sync operation successfully completed.");
            }

            if (Help)
            {
                WriteObject("SFCLI version " + Resources.Version);
            }
        }

        #region Download & methods

        /// <summary>
        /// Start download process
        /// </summary>
        private void StartDownload(ShareFileClient client, PSDriveInfo driveInfo, ICollection<string> resolvedPaths, ActionType actionType)
        {
            int transactionId = new Random((int)DateTime.Now.Ticks).Next();
            Logger.Instance.Info("Downloading files from ShareFile server.");

            ActionManager actionManager = new ActionManager();
            bool firstIteration = true;

            foreach (string path in resolvedPaths)
            {
                var item = ShareFileProvider.GetShareFileItem((ShareFileDriveInfo)driveInfo, path, null, null);

                // if user didn't specify the Sharefile HomeFolder in path then append in path
                // e.g. if user tries sf:/Folder1 as sharefile source then resolve this path to sf:/My Files & Folders/Folder1
                if (item == null && !path.StartsWith(String.Format(@"\{0}\", DefaultSharefileFolder)))
                {
                    string updatedPath = String.Format(@"\{0}\{1}", DefaultSharefileFolder, path);
                    item = ShareFileProvider.GetShareFileItem((ShareFileDriveInfo)driveInfo, updatedPath, null, null);
                }

                if (item == null)
                {
                    throw new FileNotFoundException("'" + path + "' not found on current path.");
                }

                var target = new DirectoryInfo(LocalPath);

                // if create root folder flag is specified then create a container folder first
                if (firstIteration && CreateRoot)
                {
                    Models.Folder parentFolder = client.Items.GetParent(item.url).Execute() as Folder;

                    target = CreateLocalFolder(target, parentFolder);
                    firstIteration = false;
                }

                if (item is Models.Folder)
                {
                    // if user downloading the root drive then download its root folders
                    if ((item as Folder).Info.IsAccountRoot == true)
                    {
                        var children = client.Items.GetChildren(item.url).Execute();
                        foreach (var child in children.Feed)
                        {
                            if (child is Models.Folder)
                            {
                                DownloadRecursive(client, transactionId, child, target, actionType);
                            }
                        }
                    }
                    else
                    {
                        DownloadRecursive(client, transactionId, item, target, actionType);
                    }
                }
                else if (item is Models.File)
                {
                    DownloadAction downloadAction = new DownloadAction(FileSupport, client, transactionId, (Models.File)item, target, actionType);
                    actionManager.AddAction(downloadAction);
                }
            }

            actionManager.Execute();

            // on move remove source files
            if (Move)
            {
                foreach (string path in resolvedPaths)
                {
                    var item = ShareFileProvider.GetShareFileItem((ShareFileDriveInfo)driveInfo, path, null, null);
                    var target = new DirectoryInfo(LocalPath);

                    // if strict flag is specified then also clean the target files which are not in source
                    if (Strict)
                    {
                        DeleteLocalStrictRecursive(client, item, target);
                    }

                    DeleteShareFileItemRecursive(client, item, CreateRoot && Recursive);
                }
            }
        }

        /// <summary>
        /// Download all items recursively
        /// </summary>
        private void DownloadRecursive(ShareFileClient client, int downloadId, Models.Item source, DirectoryInfo target, ActionType actionType)
        {
            if (source is Models.Folder)
            {
                var subdir = CreateLocalFolder(target, source as Folder);

                var children = client.Items.GetChildren(source.url).Execute();

                if (children != null)
                {
                    ActionManager actionManager = new ActionManager();

                    foreach (var child in children.Feed)
                    {
                        if (child is Models.Folder && Recursive)
                        {
                            DownloadRecursive(client, downloadId, child, subdir, actionType);
                        }
                        else if (child is Models.File)
                        {
                            DownloadAction downloadAction = new DownloadAction(FileSupport, client, downloadId, (Models.File)child, subdir, actionType);
                            actionManager.AddAction(downloadAction);
                        }
                    }

                    actionManager.Execute();
                }
            }
        }

        /// <summary>
        /// Delete the Sharefile items if Move flag is used
        /// </summary>
        private void DeleteShareFileItemRecursive(ShareFileClient client, Models.Item source, bool deleteFolder)
        {
            if (source is Models.Folder)
            {
                var children = client.Items.GetChildren(source.url).Execute();

                if (children != null)
                {
                    foreach (var child in children.Feed)
                    {
                        if (child is Models.File || Recursive)
                        {
                            DeleteShareFileItemRecursive(client, child, !KeepFolders);
                        }
                    }
                }
            }

            if (source is Models.File || deleteFolder)
            {
                RemoveShareFileItem(client, source);
            }
        }

        /// <summary>
        /// Clean the target folders in case of Strict flag
        /// </summary>
        private void DeleteLocalStrictRecursive(ShareFileClient client, Models.Item source, DirectoryInfo target)
        {

            var directories = target.GetDirectories();
            var children = client.Items.GetChildren(source.url).Execute();

            foreach (DirectoryInfo directory in directories)
            {
                foreach (var child in children.Feed)
                {
                    if (child is Models.Folder && child.Name.Equals(directory.Name))
                    {
                        DeleteLocalStrictRecursive(client, child, directory);
                        break;
                    }
                }
            }

            var files = target.GetFiles();

            foreach (FileInfo file in files)
            {
                bool found = false;
                foreach (var child in children.Feed)
                {
                    if (child is Models.File && child.Name.Equals(file.Name))
                    {
                        found = true;
                    }
                }

                if (!found)
                {
                    RemoveLocalItem(file);
                }
            }
        }

        /// <summary>
        /// Create local folder
        /// </summary>
        private DirectoryInfo CreateLocalFolder(DirectoryInfo target, Folder source)
        {
            string sourceFolderName = string.Empty;

            // if source is user's Home/Root folder then specify default name
            if (source.Info.IsAHomeFolder == true && source.Info.IsAStartFolder == true)
            {
                sourceFolderName = DefaultSharefileFolder;
            }
            else
            {
                sourceFolderName = source.FileName;
            }
            var subdirCheck = new DirectoryInfo(System.IO.Path.Combine(target.FullName, sourceFolderName));

            if (subdirCheck.Exists)
            {
                if (Synchronize)
                    return subdirCheck;

                if (!OverWrite)
                    throw new IOException("Path " + subdirCheck.FullName + " already exists. Use -Overwrite to ignore");
            }

            return target.CreateSubdirectory(sourceFolderName);
        }

        #endregion

        #region Upload & methods

        /// <summary>
        /// Start Upload to Sharefile location
        /// </summary>
        private void StartUpload(ShareFileClient client, int uploadId, Models.Item target, ICollection<string> resolvedPaths, ActionType actionType)
        {

            int transactionId = new Random((int)DateTime.Now.Ticks).Next();
            Logger.Instance.Info("Uploading files to ShareFile server.");

            ActionManager actionManager = new ActionManager();
            bool firstIteration = true;

            foreach (string path in resolvedPaths)
            {
                FileAttributes attr = System.IO.File.GetAttributes(path);
                FileSystemInfo source = ((attr & FileAttributes.Directory) == FileAttributes.Directory) ? new DirectoryInfo(path) : source = new FileInfo(path);

                // create an extra parent folder if CreateRoot flag is specified on target location
                if (firstIteration && CreateRoot)
                {
                    DirectoryInfo parentFolder = Directory.GetParent(path);
                    var newFolder = new Models.Folder() { Name = parentFolder.Name };
                    target = client.Items.CreateFolder(target.url, newFolder, OverWrite, false).Execute();
                    firstIteration = false;
                }

                if (source is DirectoryInfo)
                {
                    UploadRecursive(client, uploadId, source, target, actionType);
                }
                else
                {
                    IAction uploadAction = new UploadAction(FileSupport, client, source, target, Details, actionType);
                    actionManager.AddAction(uploadAction);
                }
            }

            actionManager.Execute();

            if (Move)
            {
                foreach (string path in resolvedPaths)
                {
                    FileAttributes attr = System.IO.File.GetAttributes(path);
                    FileSystemInfo source = ((attr & FileAttributes.Directory) == FileAttributes.Directory) ? new DirectoryInfo(path) : source = new FileInfo(path);

                    if (Strict && source is DirectoryInfo)
                    {
                        DeleteSharefileStrictRecursive(client, source as DirectoryInfo, target);
                    }

                    DeleteLocalItemRecursive(source, Recursive);
                }
            }
        }

        /// <summary>
        /// Upload contents recursively
        /// </summary>
        private void UploadRecursive(ShareFileClient client, int uploadId, FileSystemInfo source, Models.Item target, ActionType actionType)
        {
            if (source is DirectoryInfo)
            {
                var newFolder = new Models.Folder() { Name = source.Name };
                bool isExist = false;

                if (Synchronize)
                {
                    try
                    {
                        string path = String.Format("/{0}", source.Name);
                        Item item = null;
                        try
                        {
                            item = client.Items.ByPath(target.url, path).Execute();
                        }
                        catch (ODataException e)
                        {
                            if (e.Code != System.Net.HttpStatusCode.NotFound)
                            {
                                throw e;
                            }
                        }
                        isExist = item != null && item is Folder;
                        newFolder = (Folder)item;
                    }
                    catch { }
                }

                if (!isExist)
                {
                    newFolder = client.Items.CreateFolder(target.url, newFolder, OverWrite, false).Execute();
                }

                ActionManager actionManager = new ActionManager();

                foreach (var fsInfo in ((DirectoryInfo)source).EnumerateFileSystemInfos())
                {
                    if (fsInfo is DirectoryInfo && Recursive)
                    {
                        UploadRecursive(client, uploadId, fsInfo, newFolder, actionType);
                    }
                    else if (fsInfo is FileInfo)
                    {
                        IAction uploadAction = new UploadAction(FileSupport, client, fsInfo, newFolder, Details, actionType);
                        actionManager.AddAction(uploadAction);
                    }
                }

                actionManager.Execute();
            }
        }

        /// <summary>
        /// Delete sharefile contents on Strict flag to make exact copy of source
        /// </summary>
        private void DeleteSharefileStrictRecursive(ShareFileClient client, DirectoryInfo source, Item target)
        {
            var children = client.Items.GetChildren(target.url).Execute();
            var directories = source.GetDirectories();
            var files = source.GetFiles();

            foreach (var child in children.Feed)
            {
                if (child is Models.Folder)
                {
                    foreach (DirectoryInfo directory in directories)
                    {
                        if (directory.Name.Equals(child.Name))
                        {
                            DeleteSharefileStrictRecursive(client, directory, child);
                        }
                    }
                }
                else if (child is Models.File)
                {

                    bool found = false;

                    foreach (FileInfo file in files)
                    {
                        if (file.Name.Equals(child.Name))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        RemoveShareFileItem(client, child);
                    }
                }
            }

            foreach (DirectoryInfo directory in directories)
            {
                foreach (var child in children.Feed)
                {
                    if (child is Models.Folder && child.Name.Equals(directory.Name))
                    {
                        DeleteLocalStrictRecursive(client, child, directory);
                        break;
                    }
                }
            }



            foreach (FileInfo file in files)
            {
                bool found = false;
                foreach (var child in children.Feed)
                {
                    if (child is Models.File && child.Name.Equals(file.Name))
                    {
                        found = true;
                    }
                }

                if (!found)
                {
                    RemoveLocalItem(file);
                }
            }
        }

        /// <summary>
        /// Delete local items if Move flag is specified
        /// </summary>
        private void DeleteLocalItemRecursive(FileSystemInfo source, bool deleteFolder)
        {
            if (source is DirectoryInfo)
            {
                foreach (var child in (source as DirectoryInfo).EnumerateFileSystemInfos())
                {
                    if (child is FileInfo || Recursive)
                    {
                        DeleteLocalItemRecursive(child, !KeepFolders);
                    }
                }
            }

            if (source is FileInfo || deleteFolder)
            {
                RemoveLocalItem(source);
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Remove sharefile item
        /// </summary>
        private bool RemoveShareFileItem(ShareFileClient client, Item item)
        {
            Query<ODataObject> query = new Query<ODataObject>(client);

            query.HttpMethod = "DELETE";
            query.Id(item.Id);
            query.From("Items");

            try
            {
                client.Execute(query);
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Remove local item
        /// </summary>
        private bool RemoveLocalItem(FileSystemInfo item)
        {
            item.Delete();

            return true;
        }

        /// <summary>
        /// Delegate method (which will be used for Marking file status of completed files)
        /// </summary>
        private void TestMethod(String fileName)
        {

        }

        #endregion
    }
}
