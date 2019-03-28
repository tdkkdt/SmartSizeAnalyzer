using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace SizeScanner.Model {
    class DirectoryModel : IServiceContainer, IDisposable {
        static readonly char[] DirSeperator = {Path.DirectorySeparatorChar};
        ServiceContainer ServiceContainer { get; set; }
        FileSystemItem Root { get; set; }
        FileSystemWatcher Watcher { get; set; }
        ModelUpdater ModelUpdater { get; set; }
        ReaderWriterLockSlim RWLock { get; set; }

        public DirectoryModel() {
            ServiceContainer = new ServiceContainer();
            Watcher = new FileSystemWatcher();
            RWLock = new ReaderWriterLockSlim();
        }

        public async Task ReadInfoFromDisk(string rootDirName, CancellationToken cancellationToken) {
            Watcher.EnableRaisingEvents = false;
            if (string.IsNullOrEmpty(rootDirName)) {
                throw new ArgumentException(nameof(rootDirName));
            }
            if (!Directory.Exists(rootDirName)) {
                Root = FileSystemItem.CreateEmptyInvalid();
                return;
            }
            if (rootDirName.EndsWith("\\") && rootDirName.Length > 3) {
                rootDirName = rootDirName.Substring(0, rootDirName.Length - 1);
            }
            ModelCalculator calculator = new ModelCalculator(this);
            FileSystemItem newRoot = await calculator.CalcSize(rootDirName, cancellationToken);
            RWLock.EnterWriteLock();
            Root?.MakeInvalid();
            Root = newRoot;
            RWLock.ExitWriteLock();
            if (!Root.IsValid)
                return;
            CreateWatcher(rootDirName);
        }

        public void DoTaskUnderRWLockForReading(Action action) {
            try {
                RWLock.EnterReadLock();
                action();
            }
            finally {
                RWLock.ExitReadLock();
            }
        }

        public string GetRootName() {
            RWLock.EnterReadLock();
            string result = Root == null || !Root.IsValid ? "" : Root.Name;
            RWLock.ExitReadLock();
            return result;
        }

        public bool IsRootInvalidOrNull() {
            RWLock.EnterReadLock();
            bool result = Root == null || !Root.IsValid;
            RWLock.ExitReadLock();
            return result;
        }

        void CreateWatcher(string rootDirName) {
            ModelUpdater?.Dispose();
            ModelUpdater = new ModelUpdater(this);

            Watcher.Path = rootDirName;
            Watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size;

            Watcher.Filter = "*.*";

            Watcher.Changed += OnChanged;
            Watcher.Created += OnCreated;
            Watcher.Deleted += OnDeleted;
            Watcher.Renamed += OnRenamed;

            Watcher.IncludeSubdirectories = true;

            Watcher.EnableRaisingEvents = true;
        }

        void OnRenamed(object sender, RenamedEventArgs e) {
            ModelUpdater.EnqueueTask(new RenamedTask(e.OldFullPath, e.FullPath, e.Name, e.OldName));
        }

        void OnDeleted(object sender, FileSystemEventArgs e) {
            ModelUpdater.EnqueueTask(new DeletedTask(e.FullPath, e.Name));
        }

        void OnCreated(object sender, FileSystemEventArgs e) {
            ModelUpdater.EnqueueTask(new CreatedTask(e.FullPath, e.Name));
        }

        void OnChanged(object sender, FileSystemEventArgs e) {
            ModelUpdater.EnqueueTask(new ChangedTask(e.FullPath, e.Name));
        }

        #region IServiceContainer

        public object GetService(Type serviceType) {
            return ServiceContainer.GetService(serviceType);
        }

        public void AddService(Type serviceType, object serviceInstance) {
            ServiceContainer.AddService(serviceType, serviceInstance);
        }

        public void AddService(Type serviceType, object serviceInstance, bool promote) {
            ServiceContainer.AddService(serviceType, serviceInstance, promote);
        }

        public void AddService(Type serviceType, ServiceCreatorCallback callback) {
            ServiceContainer.AddService(serviceType, callback);
        }

        public void AddService(Type serviceType, ServiceCreatorCallback callback, bool promote) {
            ServiceContainer.AddService(serviceType, callback, promote);
        }

        public void RemoveService(Type serviceType) {
            ServiceContainer.RemoveService(serviceType);
        }

        public void RemoveService(Type serviceType, bool promote) {
            ServiceContainer.RemoveService(serviceType, promote);
        }

        #endregion

        public FileSystemItem GetFileSystemItemInfo(string dirName) {
            FileSystemItem result;
            RWLock.EnterReadLock();
            try {
                result = GetFileSystemItemInfoCore(dirName);
            }
            finally {
                RWLock.ExitReadLock();
            }
            return result;
        }

        FileSystemItem GetFileSystemItemInfoCore(string dirName) {
            if (string.IsNullOrEmpty(dirName))
                return null;
            if (Root == null)
                return null;
            if (!dirName.StartsWith(Root.Name)) {
                return null;
            }
            if (dirName == Root.Name) {
                return Root;
            }
            return GetFileSystemItemByRelativePathCore(dirName.Substring(Root.Name.Length));
        }

        FileSystemItem GetFileSystemItemByRelativePathCore(string relativePath) {
            FileSystemItem current = Root;
            string[] pathItems = relativePath.Split(DirSeperator, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in pathItems) {
                if (!current.InnerItems.TryGetValue(new FileSystemItem(s), out FileSystemItem next)) {
                    return null;
                }
                current = next;
            }
            return current;
        }

        List<FileSystemItem> GetFileSystemItemPathCore(string dirName) {
            if (string.IsNullOrEmpty(dirName))
                return null;
            if (Root == null)
                return null;
            if (!dirName.StartsWith(Root.Name)) {
                return null;
            }
            if (dirName == Root.Name) {
                return new List<FileSystemItem> {Root};
            }
            return GetFileSystemItemPathByRelativePathCore(dirName.Substring(Root.Name.Length));
        }

        List<FileSystemItem> GetFileSystemItemPathByRelativePathCore(string relativePath) {
            List<FileSystemItem> result = new List<FileSystemItem>();
            FileSystemItem current = Root;
            string[] pathItems = relativePath.Split(DirSeperator, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in pathItems) {
                result.Add(current);
                if (!current.InnerItems.TryGetValue(new FileSystemItem(s), out FileSystemItem next)) {
                    return null;
                }
                current = next;
            }
            result.Add(current);
            return result;
        }

        public void Dispose() {
            ServiceContainer?.Dispose();
            Watcher?.Dispose();
            ModelUpdater?.Dispose();
        }

        public void UpdateRename(RenamedTask renamedTask) {
            List<FileSystemItem> path = null;
            RWLock.EnterWriteLock();
            try {
                path = GetFileSystemItemPathCore(renamedTask.OldFullName);
                if (path != null) {
                    int position = renamedTask.Name.LastIndexOf(Path.DirectorySeparatorChar);
                    var newName = renamedTask.Name.Substring(position + 1);
                    if (path.Count == 1) {
                        path[0].RenameAsRoot(newName);
                    }
                    else {
                        path[path.Count - 2].RenameInner(path[path.Count - 1], newName);
                    }
                }
            }
            finally {
                RWLock.ExitWriteLock();
            }
            if (path != null)
                RaiseOnRenamedFileSystemItem(renamedTask.OldFullName, renamedTask.FullName);
        }

        EventHandler<FileSystemInfoRenamedEventArgs> onRenamedFileSystemItem;
        public event EventHandler<FileSystemInfoRenamedEventArgs> OnRenamedFileSystemItem { add => onRenamedFileSystemItem += value; remove => onRenamedFileSystemItem -= value; }

        protected virtual void RaiseOnRenamedFileSystemItem(string oldFullName, string fullName) {
            var eventArgs = new FileSystemInfoRenamedEventArgs(oldFullName, fullName);
            onRenamedFileSystemItem?.Invoke(this, eventArgs);
        }

        public void UpdateDelete(DeletedTask deletedTask) {
            bool handled = false;
            RWLock.EnterWriteLock();
            try {
                handled = UpdateDeleteCore(deletedTask);
            }
            finally {
                RWLock.ExitWriteLock();
            }
            if (handled) {
                RaiseOnDeletedFileSystemItem(deletedTask.FullPath);
            }
        }

        bool UpdateDeleteCore(DeletedTask deletedTask) {
            string fullPath = deletedTask.FullPath;
            var path = GetFileSystemItemPathCore(fullPath);
            if (path == null)
                return false;
            if (path.Count == 1) {
                path[0].InnerItems.Clear();
                path[0].MakeInvalid();
            }
            else {
                var target = path[path.Count - 1];
                var size = target.Size;
                target.InnerItems.Clear();
                target.MakeInvalid();
                for (int i = path.Count - 2; i >= 0; i--) {
                    var parent = path[i];
                    parent.Size -= size;
                    if (i == path.Count - 2) {
                        parent.InnerItems.Remove(target);
                    }
                }
            }
            return true;
        }

        EventHandler<FileSystemInfoPathEventArgs> onDeleteFileSystemItem;
        public event EventHandler<FileSystemInfoPathEventArgs> OnDeletedFileSystemItem { add => onDeleteFileSystemItem += value; remove => onDeleteFileSystemItem -= value; }

        protected virtual void RaiseOnDeletedFileSystemItem(string fullName) {
            var eventArgs = new FileSystemInfoPathEventArgs(fullName);
            onDeleteFileSystemItem?.Invoke(this, eventArgs);
        }

        public void UpdateCreated(CreatedTask createdTask) {
            bool handled;
            RWLock.EnterWriteLock();
            try {
                handled = UpdateCreatedCore(createdTask);
            }
            finally {
                RWLock.ExitWriteLock();
            }
            if (handled) {
                RaiseOnCreatedFileSystemItem(createdTask.FullPath);
            }
        }

        bool UpdateCreatedCore(CreatedTask createdTask) {
            var fullName = createdTask.FullPath;
            int position = fullName.LastIndexOf(Path.DirectorySeparatorChar);
            var parentPath = fullName.Remove(position);
            if (parentPath.Length == 2) {
                parentPath += "\\";
            }
            var path = GetFileSystemItemPathCore(parentPath);
            if (path == null)
                return false;
            FileSystemItem newItem;
            if (Directory.Exists(fullName)) {
                try {
                    DirectoryInfo di = new DirectoryInfo(fullName);
                    newItem = new FileSystemItem(di.Name);
                }
                catch (SecurityException) {
                    return false;
                }
            }
            else if (File.Exists(fullName)) {
                try {
                    FileInfo fi = new FileInfo(fullName);
                    newItem = new FileSystemItem(fi);
                }
                catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException) {
                    return false;
                }
            }
            else {
                return false;
            }
            path[path.Count - 1].InnerItems.Add(newItem);
            foreach (var pathItem in path) {
                pathItem.Size += newItem.Size;
            }
            return true;
        }

        EventHandler<FileSystemInfoPathEventArgs> onCreatedFileSystemItem;
        public event EventHandler<FileSystemInfoPathEventArgs> OnCreatedFileSystemItem { add => onCreatedFileSystemItem += value; remove => onCreatedFileSystemItem -= value; }

        protected virtual void RaiseOnCreatedFileSystemItem(string fullName) {
            var eventArgs = new FileSystemInfoPathEventArgs(fullName);
            onCreatedFileSystemItem?.Invoke(this, eventArgs);
        }

        public void UpdateSizeChanged(ChangedTask changedTask) {
            bool handled = false;
            RWLock.EnterWriteLock();
            try {
                handled = UpdateSizeChangedCore(changedTask);
            }
            finally {
                RWLock.ExitWriteLock();
            }
            if (handled) {
                RaiseOnSizeChangedFileSystemItem(changedTask.FullPath);
            }
        }

        bool UpdateSizeChangedCore(ChangedTask changedTask) {
            string fullPath = changedTask.FullPath;
            var path = GetFileSystemItemPathCore(fullPath);
            if (path == null)
                return false;
            long newSize;
            try {
                FileInfo fi = new FileInfo(changedTask.FullPath);
                newSize = !fi.Exists ? 0 : fi.Length;
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException) {
                return false;
            }
            long deltaSize = newSize - path[path.Count - 1].Size;
            foreach (FileSystemItem item in path) {
                item.Size += deltaSize;
            }
            return true;
        }

        EventHandler<FileSystemInfoPathEventArgs> onSizeChangedFileSystemItem;
        public event EventHandler<FileSystemInfoPathEventArgs> OnSizeChangedFileSystemItem { add => onSizeChangedFileSystemItem += value; remove => onSizeChangedFileSystemItem -= value; }

        protected virtual void RaiseOnSizeChangedFileSystemItem(string fullName) {
            var eventArgs = new FileSystemInfoPathEventArgs(fullName);
            onSizeChangedFileSystemItem?.Invoke(this, eventArgs);
        }
    }

    class FileSystemInfoRenamedEventArgs : EventArgs {
        public string OldFullName { get; set; }
        public string FullName { get; set; }

        public FileSystemInfoRenamedEventArgs(string oldFullName, string fullName) {
            OldFullName = oldFullName;
            FullName = fullName;
        }
    }

    class FileSystemInfoPathEventArgs : EventArgs {
        public string FullName { get; set; }

        public FileSystemInfoPathEventArgs(string fullName) {
            FullName = fullName;
        }
    }
}
