//MIT License

//Copyright(c) 2018 Daniele Picciaia

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.


using Microsoft.Synchronization;
using Microsoft.Synchronization.Files;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RMF
{
    /// <summary>
    /// Main class for RemoteMirroringFolder, that implements the BL of the application as a 'Manager' object.
    /// The RemoteMirroringFolder (RMF) uses Microsoft Sync Framework to implement and maintain  a complete-mirrored folder structure on two separated folders (local or remote)
    /// The application detects changes on one folder and replicates these changes and the other directory
    /// The App.config file contains all the parameter needed by the synchronization logic:
    /// 'Path1' and 'Path2' are the separated folder to be synced. Each one can be a local or remote (UNC) path
    /// 'CheckIntervalSec' indicates how frequently check for modification on the synced folders
    /// 'ExcludedFiles' reports filters (separated by a comma) used to exclude files from syncing
    /// 'ExcludedFolders' reports sub-folder names (separated by a comma) excluded by the syncing
    /// 'LoggerVerbosity' is the verbosity level (1..3)
    /// This object exposes method to Start/Stop the manager and detects file/folder changes
    /// </summary>
    public class RMFManager
    {
        #region Private variables
        private bool running = false;       
        private Task taskRun;
        #endregion Private variables

        /// <summary>
        /// This method starts the monitoring and sync operations
        /// </summary>
        public void Start()
        {


            running = true;
            taskRun = Task.Factory.StartNew(Run);
            Logger.Log("Main task started\nPath1 = {0}, Path2 = {1}, File filters = {2}, Folder filters = {3}, Check interval = {4} seconds", 
                LogInfo.Info, VerbosityInfoLevel.V2, 
                AppSettings.Default.Path1, AppSettings.Default.Path2, 
                AppSettings.Default.ExcludedFiles, AppSettings.Default.ExcludedFolders,
                AppSettings.Default.CheckIntervalSec);
        }
        /// <summary>
        /// This method stops the monitoring and sync operations
        /// </summary>
        public void Stop()
        {

            Logger.Log("Stopping main task...", LogInfo.Info, VerbosityInfoLevel.V2);
            running = false;
            taskRun.Wait();
            
            Logger.Log("Main task stopped", LogInfo.Info, VerbosityInfoLevel.V2);
        }
        /// <summary>
        /// Run method, executed by a dedicated Task to analyze file system changes and start syncing operations
        /// </summary>
        private void Run()
        {
            // Set options for the synchronization operation
            FileSyncOptions options =   FileSyncOptions.ExplicitDetectChanges |
                                        FileSyncOptions.RecycleDeletedFiles |
                                        FileSyncOptions.RecyclePreviousFileOnUpdates ;

            FileSyncScopeFilter filter = new FileSyncScopeFilter();
            if (AppSettings.Default.ExcludedFiles.Trim().Length > 0)
            {
                var excludedTypes = AppSettings.Default.ExcludedFiles.Split(',');
                foreach (var exType in excludedTypes)
                {
                    if (exType != null && exType.Trim().Length > 0)
                    {
                        filter.FileNameExcludes.Add(exType);
                    }
                }
            }
            else
            {
                Logger.Log("No file filter specified", LogInfo.Info, VerbosityInfoLevel.V2);
            }
            if (AppSettings.Default.ExcludedFolders.Trim().Length > 0)
            {
                var excludedFolders = AppSettings.Default.ExcludedFolders.Split(',');
                foreach (var exFolder in excludedFolders)
                {
                    if (exFolder != null && exFolder.Trim().Length > 0)
                    {
                        filter.SubdirectoryExcludes.Add(exFolder);
                    }
                }
            }
            else
            {
                Logger.Log("No folder filter specified", LogInfo.Info, VerbosityInfoLevel.V2);
            }

            while (running)
            {
                try
                {
                    DetectChangesOnFileSystemReplica(AppSettings.Default.Path1, filter, options);
                    DetectChangesOnFileSystemReplica(AppSettings.Default.Path2, filter, options);

                    SyncFileSystemReplicasOneWay(AppSettings.Default.Path1, AppSettings.Default.Path2, filter, options);
                    SyncFileSystemReplicasOneWay(AppSettings.Default.Path2, AppSettings.Default.Path1, filter, options);

                }
                catch (Exception e)
                {
                    Logger.Log("Error on sync provider execution:\n {0}", LogInfo.Error, VerbosityInfoLevel.V1, e.ToString());
                }
                Thread.Sleep(AppSettings.Default.CheckIntervalSec * 1000);
            }


        }
        /// <summary>
        /// Detects chenges on a specific path, according to MS Sync filter and options
        /// </summary>
        /// <param name="replicaRootPath">syncing path</param>
        /// <param name="filter">syncing filfters</param>
        /// <param name="options">syncing options</param>
        private void DetectChangesOnFileSystemReplica(
                string replicaRootPath,
                FileSyncScopeFilter filter, FileSyncOptions options)
        {
            FileSyncProvider provider = null;

            try
            {
                provider = new FileSyncProvider(replicaRootPath, filter, options);
                provider.DetectChanges();
            }
            finally
            {
                // Release resources
                if (provider != null)
                    provider.Dispose();
            }
        }
        /// <summary>
        /// Synchronize two folders (source and destination). The sync operation is ONE WAY only (From source to destination)
        /// </summary>
        /// <param name="sourceReplicaRootPath">source syncing path</param>
        /// <param name="destinationReplicaRootPath">destination syncing path</param>
        /// <param name="filter">syncing filter</param>
        /// <param name="options">syncing options</param>
        private void SyncFileSystemReplicasOneWay(
                string sourceReplicaRootPath, string destinationReplicaRootPath,
                FileSyncScopeFilter filter, FileSyncOptions options)
        {

            FileSyncProvider sourceProvider = null;
            FileSyncProvider destinationProvider = null;

            try
            {
                sourceProvider = new FileSyncProvider(
                    sourceReplicaRootPath, filter, options);
                destinationProvider = new FileSyncProvider(
                    destinationReplicaRootPath, filter, options);

                destinationProvider.AppliedChange += new EventHandler<AppliedChangeEventArgs>(OnAppliedChange);
                destinationProvider.SkippedChange += new EventHandler<SkippedChangeEventArgs>(OnSkippedChange);
                destinationProvider.ApplyingChange += DestinationProvider_ApplyingChange;

                SyncOrchestrator agent = new SyncOrchestrator();
                agent.LocalProvider = sourceProvider;
                agent.RemoteProvider = destinationProvider;
                agent.Direction = SyncDirectionOrder.Upload; // Sync source to destination


                var ret = agent.Synchronize();

                if (ret.UploadChangesTotal != 0)
                {
                    Logger.Log("Synchronizing '{0}', tot changes={1} ({2} done/{3} errors)", LogInfo.Info, VerbosityInfoLevel.V3, destinationProvider.RootDirectoryPath, ret.UploadChangesTotal, ret.UploadChangesApplied, ret.UploadChangesFailed);

                }

            }
            finally
            {
                // Release resources
                if (sourceProvider != null) sourceProvider.Dispose();
                if (destinationProvider != null) destinationProvider.Dispose();
            }
        }
 
        private void DestinationProvider_ApplyingChange(object sender, ApplyingChangeEventArgs args)
        {
            switch (args.ChangeType)
            {
                case ChangeType.Create:
                    Logger.Log("Detect 'ApplyingChange' CREATE on file: {0}", LogInfo.Info, VerbosityInfoLevel.V2, args.NewFileData.RelativePath);
                    break;
                case ChangeType.Delete:
                    Logger.Log("Detect 'ApplyingChange' DELETE on file: {0}", LogInfo.Info, VerbosityInfoLevel.V2, args.CurrentFileData.RelativePath);
                    break;
                case ChangeType.Rename:
                    Logger.Log("Detect 'ApplyingChange' RENAME on file: {0} to {1}", LogInfo.Info, VerbosityInfoLevel.V2, args.CurrentFileData.RelativePath, args.NewFileData.RelativePath);
                    break;
                case ChangeType.Update: 
                    Logger.Log("Detect 'ApplyingChange' UPDATE on file: {0} to {1}", LogInfo.Info, VerbosityInfoLevel.V2, args.CurrentFileData.RelativePath, args.NewFileData.RelativePath);
                    break;
            }
        }
        public void OnAppliedChange(object sender, AppliedChangeEventArgs args)
        {
            switch (args.ChangeType)
            {
                case ChangeType.Create:
                    Logger.Log("Detect 'OnAppliedChange' CREATE on file: {0}", LogInfo.Info, VerbosityInfoLevel.V2, args.NewFilePath);
                    break;
                case ChangeType.Delete:
                    Logger.Log("Detect 'OnAppliedChange' DELETE on file: {0}", LogInfo.Info, VerbosityInfoLevel.V2, args.OldFilePath);
                    break;
                case ChangeType.Rename:
                    Logger.Log("Detect 'OnAppliedChange' RENAME on file: {0} to {1}", LogInfo.Info, VerbosityInfoLevel.V2, args.OldFilePath, args.NewFilePath);
                    break;
                case ChangeType.Update:
                    Logger.Log("Detect 'OnAppliedChange' UPDATE on file: {0} to {1}", LogInfo.Info, VerbosityInfoLevel.V2, args.OldFilePath, args.NewFilePath);
                    break;
            }
        }
        public void OnSkippedChange(object sender, SkippedChangeEventArgs args)
        {
            Logger.Log("SKIP operation {0} on file {1} with error {2}", LogInfo.Info, VerbosityInfoLevel.V2,
                args.ChangeType.ToString().ToUpper(),
                (!string.IsNullOrEmpty(args.CurrentFilePath) ? args.CurrentFilePath : args.NewFilePath),
                (args.Exception == null) ? "no error msg" : args.Exception.Message );
        }
    }
}
