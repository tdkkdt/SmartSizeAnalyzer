using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace SizeScanner.Model {
    class ModelCalculator {
        IServiceProvider ServiceProvider { get; }

        IProgressIndicatorService ProgressIndicator { get => ServiceProvider.GetService(typeof(IProgressIndicatorService)) as IProgressIndicatorService; }

        public ModelCalculator(IServiceProvider serviceProvider) {
            ServiceProvider = serviceProvider;
        }

        public Task<FileSystemItem> CalcSize(string rootDir, CancellationToken cancellationToken) {
            return Task.Run(() => {
                try {
                    ProgressIndicator?.Begin();
                    return CalcSizeCore(rootDir, cancellationToken);
                }
                finally {
                    ProgressIndicator?.End();
                }
            });
        }

        FileSystemItem CalcSizeCore(string rootDir, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            var directoryInfo = new DirectoryInfo(rootDir);
            DirectoryInfo[] childDirs;
            try {
                childDirs = directoryInfo.GetDirectories();
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException || e is DirectoryNotFoundException) {
                return FileSystemItem.CreateEmptyInvalid();
            }
            var tasks = new Task[childDirs.Length];
            var childFileSystemItems = new FileSystemItem[childDirs.Length];
            long rootDirSize = 0;
            double progressChange = 1d / (childDirs.Length + 1);
            double progress = 0;
            for (int i = 0; i < childDirs.Length; i++) {
                cancellationToken.ThrowIfCancellationRequested();
                var childDir = childDirs[i];
                int childDirIndex = i;
                tasks[i] = Task.Factory.StartNew(() => {
                    childFileSystemItems[childDirIndex] = CalcSizePlain(childDir);
                    Interlocked.Add(ref rootDirSize, childFileSystemItems[childDirIndex].Size);
                    double currentProgress, newProgress;
                    do {
                        currentProgress = progress;
                        newProgress = progress + progressChange;
                    } while (!currentProgress.Equals(Interlocked.CompareExchange(ref progress, newProgress, currentProgress)));
                    if (!cancellationToken.IsCancellationRequested)
                        ProgressIndicator?.SetProgress(newProgress);
                }, cancellationToken);
            }
            var fsRoot = new FileSystemItem(rootDir);
            foreach (var childFileInfo in directoryInfo.EnumerateFiles()) {
                cancellationToken.ThrowIfCancellationRequested();
                fsRoot.InnerItems.Add(new FileSystemItem(childFileInfo));
                fsRoot.Size += childFileInfo.Length;
            }
            cancellationToken.ThrowIfCancellationRequested();
            ProgressIndicator?.SetProgress(progressChange);
            Task.WaitAll(tasks, cancellationToken);
            fsRoot.Size += rootDirSize;
            foreach (var childFileSystemItem in childFileSystemItems) {
                if (childFileSystemItem.IsValid) {
                    fsRoot.InnerItems.Add(childFileSystemItem);
                }
            }
            return fsRoot;
        }

//        FileSystemItem CalcSizePlain(DirectoryInfo root) {
//            var stack = new Stack<FileSystemItem>();
//            var queue = new Queue<(string dirPath, FileSystemItem fileSystem)>();
//            var result = new FileSystemItem(root.Name);
//            queue.Enqueue((root.FullName, result));
//            while (queue.Count > 0) {
//                (string dirPath, FileSystemItem currentFileSystemItem) = queue.Dequeue();
//                stack.Push(currentFileSystemItem);
//                ProcessDirectory(dirPath, currentFileSystemItem, queue);
//            }
//            while (stack.Count > 0) {
//                var currentFileSystemItem = stack.Pop();
//                if(!currentFileSystemItem.IsValid)
//                    continue;
//                List<FileSystemItem> forRemoving = new List<FileSystemItem>();
//                foreach (FileSystemItem childDirInfo in currentFileSystemItem.InnerItems) {
//                    if(childDirInfo.IsValid) {
//                        currentFileSystemItem.Size += childDirInfo.Size;
//                    }
//                    else {
//                        forRemoving.Add(childDirInfo);
//                    }
//                }
//                foreach (var fileSystemItem in forRemoving) {
//                    currentFileSystemItem.InnerItems.Remove(fileSystemItem);
//                }
//            }
//            return result;
//        }
//        static void ProcessDirectory(string dirPath, FileSystemItem currentFileSystemItem, Queue<(string dirPath, FileSystemItem fileSystem)> queue) {
//            IntPtr findHandle = WinAPI.INVALID_HANDLE_VALUE;
//            try {
//                findHandle = WinAPI.FindFirstFileW(Path.Combine(dirPath, @"*"), out WinAPI.WIN32_FIND_DATAW findData);
//                if(findHandle == WinAPI.INVALID_HANDLE_VALUE)
//                    return;
//                do {
//                    if(findData.cFileName == "." || findData.cFileName == "..") continue;
//                    if(findData.dwFileAttributes.HasFlag(FileAttributes.Directory) && !findData.dwFileAttributes.HasFlag(FileAttributes.ReparsePoint)) {
//                        var childFileSystemItem = new FileSystemItem(findData.cFileName);
//                        currentFileSystemItem.InnerItems.Add(childFileSystemItem);
//                        queue.Enqueue((Path.Combine(dirPath, findData.cFileName), childFileSystemItem));
//                    }
//                    else if(!findData.dwFileAttributes.HasFlag(FileAttributes.Directory)) {
//                        long length = ((long) findData.nFileSizeHigh << 32) + findData.nFileSizeLow;
//                        currentFileSystemItem.InnerItems.Add(new FileSystemItem(findData.cFileName, length));
//                    }
//                } while (WinAPI.FindNextFile(findHandle, out findData));
//            }
//            catch {
//                currentFileSystemItem.MakeInvalid();
//            }
//            finally {
//                if(findHandle != WinAPI.INVALID_HANDLE_VALUE) {
//                    WinAPI.FindClose(findHandle);
//                }
//            }
//        }

        FileSystemItem CalcSizePlain(DirectoryInfo root) {
            //TODO: Try to use 
            var stack = new Stack<FileSystemItem>();
            var queue = new Queue<(DirectoryInfo dirInfo, FileSystemItem fileSystem)>();
            var result = new FileSystemItem(root.Name);
            queue.Enqueue((root, result));
            while (queue.Count > 0) {
                (DirectoryInfo currentDirInfo, FileSystemItem currentFileSystemItem) = queue.Dequeue();
                stack.Push(currentFileSystemItem);
                try {
                    var innerDirectories = currentDirInfo.GetDirectories();
                    if(innerDirectories.Length > 40) {
                        //dirty hack
                        ProcessInnerDirectoriesParallel(innerDirectories, currentFileSystemItem);
                    }
                    else {
                        foreach (var childDirectoryInfo in innerDirectories) {
                            var childFileSystemItem = new FileSystemItem(childDirectoryInfo.Name);
                            currentFileSystemItem.InnerItems.Add(childFileSystemItem);
                            queue.Enqueue((childDirectoryInfo, childFileSystemItem));
                        }
                    }
                    foreach (var childFileInfo in currentDirInfo.EnumerateFiles()) {
                        currentFileSystemItem.InnerItems.Add(new FileSystemItem(childFileInfo));
                    }
                }
                catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException || e is DirectoryNotFoundException) {
                    currentFileSystemItem.MakeInvalid();
                }
            }
            while (stack.Count > 0) {
                var currentFileSystemItem = stack.Pop();
                if (!currentFileSystemItem.IsValid)
                    continue;
                List<FileSystemItem> forRemoving = new List<FileSystemItem>();
                foreach (FileSystemItem childDirInfo in currentFileSystemItem.InnerItems) {
                    if(childDirInfo.IsValid) {
                        currentFileSystemItem.Size += childDirInfo.Size;
                    }
                    else {
                        forRemoving.Add(childDirInfo);
                    }
                }
                foreach (var fileSystemItem in forRemoving) {
                    currentFileSystemItem.InnerItems.Remove(fileSystemItem);
                }
            }
            return result;
        }
        void ProcessInnerDirectoriesParallel(DirectoryInfo[] innerDirectories, FileSystemItem currentFileSystemItem) {
            SpinLock spinLock = new SpinLock();
            const int tasksGroupSize = 4;
            var tasks = new Task[tasksGroupSize];
            int i = 0;
            for (int j = 0; j < tasksGroupSize; j++) {
                tasks[j] = Task.Factory.StartNew(() => {
                    while (i < innerDirectories.Length) {
                        int index = Interlocked.Increment(ref i);
                        if(index == innerDirectories.Length) {
                            return;
                        }
                        var innerItem = CalcSizePlain(innerDirectories[index]);
                        bool lockTaken = false;
                        try {
                            spinLock.Enter(ref lockTaken);
                            currentFileSystemItem.InnerItems.Add(innerItem);
                        }
                        finally {
                            if(lockTaken) {
                                spinLock.Exit();
                            }
                        }
                    }
                });
            }
            Task.WaitAll(tasks);
        }
    }
}
