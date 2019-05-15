using System;
using System.Collections.Generic;
using SystemIO = System.IO;
namespace SizeScanner.Model {
    class FileInfo : IFileInfo {
        public long Length => SystemFileInfo.Length;
        public string Name => SystemFileInfo.Name;

        SystemIO.FileInfo SystemFileInfo { get; }

        public FileInfo(SystemIO.FileInfo systemFileInfo) {
            SystemFileInfo = systemFileInfo ?? throw new ArgumentException(nameof(systemFileInfo));
        }
    }

    class DirectoryInfo : IDirectoryInfo {
        public string Name => SystemDirectoryInfo.Name;

        SystemIO.DirectoryInfo SystemDirectoryInfo {get;}
        
        public DirectoryInfo(SystemIO.DirectoryInfo systemDirectoryInfo) {
            SystemDirectoryInfo = systemDirectoryInfo ?? throw new ArgumentException(nameof(systemDirectoryInfo));
        }

        public IDirectoryInfo[] GetDirectories() {
            var systemDirectoryInfos = SystemDirectoryInfo.GetDirectories();
            IDirectoryInfo[] result = new IDirectoryInfo[systemDirectoryInfos.Length];
            for (int i = 0; i < result.Length; i++) {
                result[i] = new DirectoryInfo(systemDirectoryInfos[i]);
            }
            return result;
        }

        public IEnumerable<IFileInfo> EnumerateFiles() {
            foreach (var systemFileInfo in SystemDirectoryInfo.EnumerateFiles()) {
                yield return new FileInfo(systemFileInfo);
            }
        }

        public IEnumerable<IDirectoryInfo> EnumerateDirectories() {
            foreach (var systemDirectoryInfo in SystemDirectoryInfo.EnumerateDirectories()) {
                yield return new DirectoryInfo(systemDirectoryInfo);
            }
        }
    }
    
    class FileSystemService:IFileSystemService {
        public IDirectoryInfo GetDirectoryInfo(string folderName) {
            return new DirectoryInfo(new SystemIO.DirectoryInfo(folderName));
        }
    }
}