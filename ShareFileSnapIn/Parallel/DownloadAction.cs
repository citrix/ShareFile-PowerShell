using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ShareFile.Api.Client.Transfers.Downloaders;

namespace ShareFile.Api.Powershell.Parallel
{
    /// <summary>
    /// DownloadAction class to download files from ShareFile server
    /// </summary>
    class DownloadAction : IAction
    {
        private Models.File child;
        private Client.ShareFileClient client;
        private int downloadId;
        private FileSystemInfo target;
        private ActionType actionType;
        private FileSupport copySfItem;

        public DownloadAction(FileSupport cmdLet, Client.ShareFileClient client, int downloadId, Models.File child, FileSystemInfo target, ActionType type)
        {
            this.child = child;
            this.client = client;
            this.downloadId = downloadId;
            this.target = target;
            this.actionType = type;
            this.copySfItem = cmdLet;
        }

        void IAction.CopyFileItem()
        {
            string fileName = System.IO.Path.Combine(target.FullName, child.FileName);
            bool duplicateFile = File.Exists(fileName);
            bool hashcodeMatches = duplicateFile ? Utility.GetMD5HashFromFile(fileName).Equals(child.Hash) : false;

            if (duplicateFile && actionType == ActionType.None)
            {
                throw new IOException("File already exist");
            }
            else if (!duplicateFile || actionType == ActionType.Force || (actionType == ActionType.Sync && !hashcodeMatches))
            {
                using (var fileStream = new FileStream(fileName, actionType == ActionType.Force ? FileMode.Create : FileMode.CreateNew))
                {
                    var progressBar = new ProgressBar(copySfItem);
                    progressBar.SetProgress(child.FileName, 0);
                    var downloader = client.GetAsyncFileDownloader(child);
                    downloader.OnTransferProgress =
                        (sender, args) =>
                        {
                            if (args.Progress.TotalBytes > 0)
                            {
                                var pct = (int)(((double)args.Progress.BytesTransferred / (double)args.Progress.TotalBytes) * 100);
                                progressBar.SetProgress(child.FileName, pct);
                            }
                        };

                    ThreadPool.QueueUserWorkItem(new WaitCallback(progressBar.UpdateLoop));

                    Task task = downloader.DownloadToAsync(fileStream);
                    task.ContinueWith(t => progressBar.Finish());
                    task.Wait();

                    fileStream.Close();
                }
            }
        }

        
    }
}
