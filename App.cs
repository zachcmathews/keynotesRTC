using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Serilog;

namespace KeynotesRTC
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class App : IExternalApplication
    {
        private Dictionary<int, TrackedDocument> trackedDocuments = new Dictionary<int, TrackedDocument>();
        
        // Interface
        public Result OnStartup(UIControlledApplication uiapp)
        {
            // Setup logging
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File("log.txt", shared: true, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                .CreateLogger();

            // Register event handlers
            uiapp.ControlledApplication.DocumentOpened += new EventHandler<DocumentOpenedEventArgs>(OnDocumentOpen);
            uiapp.ControlledApplication.DocumentClosing += new EventHandler<DocumentClosingEventArgs>(OnDocumentClosing);
            uiapp.Idling += new EventHandler<Autodesk.Revit.UI.Events.IdlingEventArgs>(OnIdling);
            uiapp.ControlledApplication.DocumentChanged += new EventHandler<DocumentChangedEventArgs>(OnDocumentChanged);

            return Result.Succeeded;
        }
        public Result OnShutdown(UIControlledApplication uiapp)
        {
            // Unregister event handlers
            uiapp.ControlledApplication.DocumentOpened -= OnDocumentOpen;
            uiapp.ControlledApplication.DocumentClosing -= OnDocumentClosing;
            uiapp.Idling -= OnIdling;
            uiapp.ControlledApplication.DocumentChanged -= OnDocumentChanged;

            return Result.Succeeded;
        }

        // Event handlers
        public void OnDocumentOpen(object sender, DocumentOpenedEventArgs e)
        {
            // Track opened document
            TrackedDocument trackedDocument = new TrackedDocument(this, e.Document);
            trackedDocuments.Add(trackedDocument.Hash, trackedDocument);
        }
        public void OnDocumentClosing(object sender, DocumentClosingEventArgs e)
        {
            // Quit tracking closed document
            int hash = e.Document.GetHashCode();
            trackedDocuments.Remove(hash);
        }
        public void OnIdling(object sender, IdlingEventArgs e)
        {
            foreach (KeyValuePair<int, TrackedDocument> kvp in trackedDocuments)
            {
                TrackedDocument trackedDocument = kvp.Value;
                trackedDocument.SyncKeynotes();
            }
        }
        public void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            foreach (string name in e.GetTransactionNames())
            {
                if (name == "Keynoting Settings")
                {
                    int hash = e.GetDocument().GetHashCode();

                    if (trackedDocuments.ContainsKey(hash))
                    {
                        trackedDocuments[hash].RestartTracking();
                    }
                }
            }
        }
    }

    public class TrackedDocument
    {
        // Document and add-in properties
        private App app;
        private Document document;
        private string documentPath;
        public int Hash { get; }

        // Keynote properties
        private KeynoteTable keynoteTable;
        private string keynotesPath;
        private string keynotesDir;
        private string keynotesFile;
        private bool keynotesFileChanged;

        // Tracker properties
        private bool stopTracking;
        private Thread tracker;

        // Construct & destroy
        public TrackedDocument(App app, Document document)
        {
            this.app = app;
            this.document = document;
            documentPath = document.PathName;
            Hash = document.GetHashCode();
            keynoteTable = KeynoteTable.GetKeynoteTable(this.document);

            StartTracking();
        }
        ~TrackedDocument()
        {
            StopTracking();
        }

        // Keynote table methods
        public void SyncKeynotes()
        {
            if (keynotesFileChanged)
            {
                using (Transaction transaction = new Transaction(document))
                {
                    if (TransactionStatus.Started == transaction.Start("Reload keynote table"))
                    {
                        KeyBasedTreeEntriesLoadResults reloadResults = new KeyBasedTreeEntriesLoadResults();
                        ExternalResourceLoadStatus reloadStatus = keynoteTable.Reload(reloadResults);               // reload the keynotes table

                        if (ExternalResourceLoadStatus.Success == reloadStatus)
                        {
                            if (TransactionStatus.Committed == transaction.Commit())                                // commit the transaction
                            {
                                keynotesFileChanged = false;                                                        // reset the flag
                            }
                            else
                            {
                                Log.Error($"Could not commit transaction for document {documentPath}");
                            }
                        }
                        else
                        {
                            if (TransactionStatus.RolledBack != transaction.RollBack())                             // rollback the transaction
                            {
                                Log.Error($"Could not rollback transaction for document {documentPath}");
                            }
                        }
                    }
                }
            }
        }

        // Tracking methods
        private void StartTracking()
        {
            // Clear the flags
            keynotesFileChanged = false;
            stopTracking = false;

            // Get the keynotes path, directory, and file name 
            if (keynoteTable.IsExternalFileReference())
            {
                ModelPath keynotesModelPath = keynoteTable.GetExternalFileReference().GetAbsolutePath();
                if (!keynotesModelPath.Empty)
                {
                    keynotesPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(keynotesModelPath);
                    keynotesDir = keynotesPath.Substring(0, keynotesPath.LastIndexOf('\\'));
                    keynotesFile = keynotesPath.Substring(keynotesPath.LastIndexOf('\\') + 1);
                }
                else
                {
                    Log.Error($"No keynotes file for the document {documentPath}");
                    stopTracking = true;
                }
            }
            else
            {
                Log.Error($"Keynote table not an external file reference for document {documentPath}");
                stopTracking = true;
            }

            // Start tracking if flag was not set in the logic above
            if (!stopTracking)
            {
                tracker = new Thread(Track);
                tracker.Start();
            }
        }
        private void StopTracking()
        {
            // Stop the tracker
            stopTracking = true;
            keynotesFileChanged = false;
            if (tracker.IsAlive)
            {
                tracker.Join();
            }
        }
        public void RestartTracking()
        {
            StopTracking();
            StartTracking();
        }
        private void Track()
        {
            using (FileSystemWatcher fsWatcher = new FileSystemWatcher())
            {
                fsWatcher.Path = keynotesDir;                                   // directory to watch
                fsWatcher.Filter = keynotesFile;                                // file to watch

                fsWatcher.NotifyFilter = NotifyFilters.LastWrite;               // listen for writes
                fsWatcher.Changed += delegate
                {
                    keynotesFileChanged = true;                                 // set the flag
                };

                try
                {
                    fsWatcher.EnableRaisingEvents = true;                       // start watching
                }
                catch (FileNotFoundException e)
                {
                    Log.Error($"Could not find file at path {keynotesPath} for document at {documentPath}");
                    Log.Error(e.StackTrace);
                    stopTracking = true;
                }
                catch (Exception e)
                {
                    Log.Error($"Could not start tracking file {keynotesPath} for document at {documentPath}");
                    Log.Error(e.StackTrace);
                    stopTracking = true;
                }

                while (!stopTracking) ;
            }
        }
    }
}