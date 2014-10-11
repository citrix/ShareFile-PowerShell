using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ShareFile.Api.Powershell.Log;

namespace ShareFile.Api.Powershell.Parallel
{
    /// <summary>
    /// ProgressBar class to keep track of file progress
    /// </summary>
    class ProgressBar
    {
        private string CurrentFile { get; set; }

        private int CurrentPct { get; set; }

        private bool Finished { get; set; }

        private FileSupport CopySfItemObj { get; set; }

        public ProgressBar(FileSupport sfItem)
        {
            Finished = false;
            CurrentFile = null;
            CurrentPct = 0;
            CopySfItemObj = sfItem;
        }

        public void SetProgress(string filename, int percentage)
        {
            CurrentFile = filename;
            CurrentPct = percentage;
            Logger.Instance.Info(String.Format("Starting copying file '{0}'.", CurrentFile));
        }

        private void WriteProgress()
        {
            if (CurrentFile != null)
            {
                Console.WriteLine(string.Format("{0} - {1}%", CurrentFile, CurrentPct));
            }
        }

        public void Finish()
        {
            CurrentPct = 100;
            Finished = true;
            WriteProgress();
            CopySfItemObj(CurrentFile);
            Logger.Instance.Info(String.Format("File '{0}' copied successfully.", CurrentFile));
        }

        public void UpdateLoop(Object sf)
        {
            while (!Finished)
            {
                WriteProgress();
                Thread.Sleep(2000);
            }
        }
    }
}
