using System;
using System.Collections.Generic;
using System.IO;
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

        FileSystemItem CalcSizePlain(DirectoryInfo root) {
            var stack = new Stack<FileSystemItem>();
            var queue = new Queue<(DirectoryInfo dirInfo, FileSystemItem fileSystem)>();
            var result = new FileSystemItem(root.Name);
            queue.Enqueue((root, result));
            while (queue.Count > 0) {
                (DirectoryInfo currentDirInfo, FileSystemItem currentFileSystemItem) = queue.Dequeue();
                stack.Push(currentFileSystemItem);
                try {
                    foreach (var childDirectoryInfo in currentDirInfo.EnumerateDirectories()) {
                        var childFileSystemItem = new FileSystemItem(childDirectoryInfo.Name);
                        currentFileSystemItem.InnerItems.Add(childFileSystemItem);
                        queue.Enqueue((childDirectoryInfo, childFileSystemItem));
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
                currentFileSystemItem.InnerItems.RemoveWhere(innerItem => !innerItem.IsValid);
                foreach (FileSystemItem childDirInfo in currentFileSystemItem.InnerItems) {
                    currentFileSystemItem.Size += childDirInfo.Size;
                }
            }
            return result;
        }
    }
}
