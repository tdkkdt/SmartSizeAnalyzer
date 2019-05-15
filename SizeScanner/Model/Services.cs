using System.Collections.Generic;

namespace SizeScanner.Model {
    #region ProgressIndicator

    public interface IProgressIndicatorService {
        void Begin();
        void End();
        void SetProgress(double value);
    }

    #endregion

    #region FileSystems

    interface IFileSystemService {
        IDirectoryInfo GetDirectoryInfo(string folderName);
    }

    interface IFileInfo {
        long Length { get; }
        string Name { get; }
    }

    interface IDirectoryInfo {
        string Name { get; }
        IDirectoryInfo[] GetDirectories();
        IEnumerable<IFileInfo> EnumerateFiles();
        IEnumerable<IDirectoryInfo> EnumerateDirectories();
    }

    #endregion
}