using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace ShareFile.Api.Powershell.Parallel
{
    /// <summary>
    /// ActionManager class to start parallel copying operations
    /// </summary>
    class ActionManager
    {
        public static int MaxParallelThreads { get; set; }
        private List<IAction> actionsList;
        
        /// <summary>
        /// Initialize list of copy actions
        /// </summary>
        public ActionManager()
        {
            actionsList = new List<IAction>();
        }

        /// <summary>
        /// Add action to list
        /// </summary>
        /// <param name="action">Action type: UploadAction or DownloadAction</param>
        internal void AddAction(IAction action)
        {
            actionsList.Add(action);
        }
        
        /// <summary>
        /// Execute all actions list in parallel (threads)
        /// </summary>
        internal void Execute()
        {
            if (actionsList.Count > 0)
            {
                try
                {
                    Action[] systemActionsArray = new Action[actionsList.Count];
                    int index = 0;
                    foreach (IAction action in actionsList)
                    {
                        Action systemAction = new Action(() => action.CopyFileItem());
                        systemActionsArray[index++] = systemAction;
                    }
                    
                    // if user didn't specify maximum threads to run in parallel then set default to available processors
                    if (MaxParallelThreads == 0)
                    {
                        MaxParallelThreads = System.Environment.ProcessorCount;
                    }

                    // setting maximum number of threads
                    ParallelOptions options = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelThreads };
                    System.Threading.Tasks.Parallel.Invoke(options, systemActionsArray);
                }
                catch (AggregateException e)
                {
                    if (e.InnerException != null)
                    {
                        throw e.InnerException;
                    }
                    else 
                    {
                        throw e;
                    }
                }
            }
        }

        /// <summary>
        /// Get total number of actions
        /// </summary>
        public int TotalActions {
            get 
            {
                return actionsList.Count;
            }
        }
    }
}
