using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using SizeScanner.Annotations;
using SizeScanner.Model;
using SmartPieChart;

namespace SizeScanner {
    sealed class ViewModel : INotifyPropertyChanged, IProgressIndicatorService, IDisposable {
        string currentDirName;
        bool ready;
        long currentDirSize;
        double progress;
        long anchorDirSize;
        List<PiePieceItem> pieItems;
        CancellationTokenSource ModelCalculatorCancelationTokenSource { get; set; }
        CancellationTokenSource ConverterCancelationTokenSource { get; set; }

        #region  Properties
        DirectoryModel DirectoryModel { get; }

        public string CurrentDirName {
            get => currentDirName;
            private set {
                if (value == currentDirName)
                    return;
                currentDirName = value;
                OnPropertyChanged();
            }
        }

        public long CurrentDirSize {
            get => currentDirSize;
            private set {
                if (value == currentDirSize)
                    return;
                currentDirSize = value;
                OnPropertyChanged();
            }
        }

        public bool Ready {
            get => ready;
            private set {
                if (value == ready)
                    return;
                ready = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public double Progress {
            get => progress;
            private set {
                if (value.Equals(progress))
                    return;
                progress = value;
                OnPropertyChanged();
            }
        }

        public long AnchorDirSize {
            get => anchorDirSize;
            set {
                if (value == anchorDirSize)
                    return;
                anchorDirSize = value;
                OnPropertyChanged();
            }
        }

        public List<PiePieceItem> PieItems {
            get => pieItems;
            set {
                if (Equals(value, pieItems))
                    return;
                pieItems = value;
                OnPropertyChanged();
            }
        }

        Stack<string> AnchorPaths { get; }
        string AnchorPath => AnchorPaths.Count == 0 ? DirectoryModel.GetRootName() : AnchorPaths.Peek();
        #endregion

        public ViewModel(DirectoryModel directoryModel) {
            DirectoryModel = directoryModel;
            directoryModel.AddService(typeof(IProgressIndicatorService), this);
            Ready = true;
            Progress = 0;
            PieItems = new List<PiePieceItem>();
            AnchorPaths = new Stack<string>();
            directoryModel.OnRenamedFileSystemItem += DirectoryModel_OnRenamedFileSystemItem;
            directoryModel.OnDeletedFileSystemItem += DirectoryModel_OnDeletedFileSystemItem;
            directoryModel.OnCreatedFileSystemItem += DirectoryModel_OnCreatedFileSystemItem;
            directoryModel.OnSizeChangedFileSystemItem += DirectoryModel_OnSizeChangedFileSystemItem;
        }

        public async void AnalyzeDirectory(string rootDirName) {
            AnchorPaths.Clear();
            ModelCalculatorCancelationTokenSource?.Cancel();
            ModelCalculatorCancelationTokenSource?.Dispose();
            ModelCalculatorCancelationTokenSource = new CancellationTokenSource();
            try {
                await DirectoryModel.ReadInfoFromDisk(rootDirName, ModelCalculatorCancelationTokenSource.Token);
            }
            catch (OperationCanceledException) {
                return;
            }
            if (DirectoryModel.IsRootInvalidOrNull()) {
                return;
            }
            ShowAnchorDirectory(true, true);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region IProgressIndicatorService Members

        public void Begin() {
            Ready = false;
            Progress = 0;
        }

        public void End() {
            Progress = 100;
            Ready = true;
        }

        public void SetProgress(double value) {
            Progress = value * 100;
        }

        #endregion

        async void ShowAnchorDirectory(bool shouldPopOnCancel, bool showProgress) {
            string anchorPath = AnchorPath;
            var newAnchor = DirectoryModel.GetFileSystemItemInfo(anchorPath);
            if (newAnchor == null) {
                CurrentDirName = $"Не найдена папка {anchorPath}. Советую перечитать папки с диска.";
                CurrentDirSize = 0;
                PieItems.Clear();
                if (shouldPopOnCancel && AnchorPaths.Count > 0) {
                    AnchorPaths.Pop();
                }
                return;
            }
            ConverterCancelationTokenSource?.Cancel();
            ConverterCancelationTokenSource?.Dispose();
            ConverterCancelationTokenSource = new CancellationTokenSource();
            List<PiePieceItem> newPieItems;
            try {
                newPieItems = await ConvertFileSystemItemToPiePieceItem(newAnchor, showProgress);
            }
            catch (OperationCanceledException) {
                if (shouldPopOnCancel && AnchorPaths.Count > 0) {
                    AnchorPaths.Pop();
                }
                return;
            }
            PieItems = newPieItems;
            CurrentDirName = anchorPath;
            CurrentDirSize = newAnchor.Size;
            AnchorDirSize = newAnchor.Size;
        }

        ConfiguredTaskAwaitable<List<PiePieceItem>> ConvertFileSystemItemToPiePieceItem(FileSystemItem fileSystemItem, bool showProgress) {
            return Task.Run(() => {
                if (showProgress)
                    Begin();
                List<PiePieceItem> result = new List<PiePieceItem>(fileSystemItem.InnerItems.Count);
                int index = 0;
                DirectoryModel.DoTaskUnderRWLockForReading(() => {
                    double progressChange = 1d / fileSystemItem.InnerItems.Count;
                    foreach (var innerItem in fileSystemItem.InnerItems) {
                        PiePieceItem piePieceItem = CreatePiePieceItemFromFileSystemItem(fileSystemItem, innerItem);
                        ConvertFileSystemItemToPiePieceItemCore(innerItem, piePieceItem, 1);
                        result.Add(piePieceItem);
                        index++;
                        if (showProgress)
                            SetProgress(progressChange * index);
                    }
                });
                if (showProgress)
                    End();
                return result;
            }).ConfigureAwait(false);
        }

        static PiePieceItem CreatePiePieceItemFromFileSystemItem(FileSystemItem parent, FileSystemItem innerItem) {
            double innerItemSize = innerItem.Size / (double) parent.Size;
            PiePieceItem piePieceItem = new PiePieceItem(innerItem.Name, innerItemSize, innerItem.InnerItems.Count);
            piePieceItem.IsSpecial = !innerItem.IsValid;
            return piePieceItem;
        }

        void ConvertFileSystemItemToPiePieceItemCore(FileSystemItem fileSystemItem, PiePieceItem parent, int level) {
            const int maxLevel = 8;
            if (level == maxLevel) {
                return;
            }
            foreach (var innerItem in fileSystemItem.InnerItems) {
                PiePieceItem piePieceItem = CreatePiePieceItemFromFileSystemItem(fileSystemItem, innerItem);
                ConvertFileSystemItemToPiePieceItemCore(innerItem, piePieceItem, level + 1);
                parent.Add(piePieceItem);
            }
        }

        public void OpenFolder() {
            using (var fbd = new FolderBrowserDialog()) {
                ModelCalculatorCancelationTokenSource?.Cancel();
                DialogResult result = fbd.ShowDialog();
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath)) {
                    AnalyzeDirectory(fbd.SelectedPath);
                }
            }
        }

        public void MouseEnterPiece(string pathPart) {
            string dirName = Path.Combine(AnchorPath, pathPart);
            var fileSystemItem = DirectoryModel.GetFileSystemItemInfo(dirName);
            if (fileSystemItem == null)
                return;
            CurrentDirSize = fileSystemItem.Size;
            CurrentDirName = dirName;
        }

        public void MouseLeavePiece() {
            CurrentDirSize = AnchorDirSize;
            CurrentDirName = AnchorPath;
        }

        public void MouseClickOnPiece(string pathPart) {
            AnchorPaths.Push(Path.Combine(AnchorPath, pathPart));
            ShowAnchorDirectory(true, true);
        }

        public void ReAnalyze() {
            if (DirectoryModel.IsRootInvalidOrNull()) {
                return;
            }
            AnalyzeDirectory(DirectoryModel.GetRootName());
        }

        public void BrowseBack() {
            if (AnchorPaths.Count <= 0)
                return;
            AnchorPaths.Pop();
            ShowAnchorDirectory(true, true);
        }

        public void BrowseParent() {
            if (DirectoryModel.GetRootName() == AnchorPath) {
                return;
            }
            int separator = AnchorPath.LastIndexOf(Path.DirectorySeparatorChar);
            string newAnchorPath = AnchorPath.Remove(separator);
            if (newAnchorPath.Length == 2) {
                //буква диска
                newAnchorPath += "\\";
            }
            AnchorPaths.Push(newAnchorPath);
            ShowAnchorDirectory(true, true);
        }

        private void DirectoryModel_OnRenamedFileSystemItem(object sender, FileSystemInfoRenamedEventArgs e) {
            UpdateNameOnPiePiece(e);
            List<string> temp = new List<string>(AnchorPaths.Count);
            while (AnchorPaths.Count > 0) {
                temp.Add(AnchorPaths.Pop());
            }
            for (int i = temp.Count - 1; i >= 0; i--) {
                string t = temp[i];
                AnchorPaths.Push(t.Replace(e.OldFullName, e.FullName));
            }
            if (CurrentDirName == e.OldFullName) {
                CurrentDirName = e.FullName;
            }
        }

        void UpdateNameOnPiePiece(FileSystemInfoRenamedEventArgs e) {
            if (pieItems == null || pieItems.Count == 0)
                return;
            var currentPieItems = pieItems;
            PiePieceItem currentItem = null;
            string[] pathItems = e.OldFullName.Substring(DirectoryModel.GetRootName().Length).Split(new[] {Path.DirectorySeparatorChar}, StringSplitOptions.RemoveEmptyEntries);
            foreach (string pathItem in pathItems) {
                if (currentPieItems.Count > 0) {
                    int l = 0;
                    int r = currentPieItems.Count - 1;
                    while (l <= r) {
                        int m = l + (r - l) / 2;
                        PiePieceItem item = currentPieItems[m];
                        if (item.Name == pathItem) {
                            l = m;
                            break;
                        }
                        if (String.Compare(pathItem, item.Name, StringComparison.Ordinal) < 0) {
                            r = m - 1;
                        }
                        else {
                            l = m + 1;
                        }
                    }
                    currentItem = currentPieItems[l];
                }
                if (currentItem ==null || currentItem.Name != pathItem) {
                    break;
                }
                currentPieItems = currentItem.Items;
            }
            if (currentItem == null || currentItem.Name != pathItems[pathItems.Length - 1]) {
                return;
            }
            var fileSystemItem = DirectoryModel.GetFileSystemItemInfo(e.FullName);
            if (fileSystemItem == null) {
                return;
            }
            currentItem.Name = fileSystemItem.Name;
        }

        static bool ContainsPath(string a, string b) {
            return a.StartsWith(b) && (a.Length == b.Length || a[b.Length] == Path.DirectorySeparatorChar);
        }

        bool IsChangedVisible(string changedPath) {
            return ContainsPath(AnchorPath, changedPath) || ContainsPath(changedPath, AnchorPath);
        }

        private void DirectoryModel_OnDeletedFileSystemItem(object sender, FileSystemInfoPathEventArgs e) {
            List<string> temp = new List<string>(AnchorPaths.Count);
            while (AnchorPaths.Count > 0) {
                temp.Add(AnchorPaths.Pop());
            }
            for (int i = temp.Count - 1; i >= 0; i--) {
                string t = temp[i];
                if (t.Contains(e.FullName)) {
                    continue;
                }
                AnchorPaths.Push(t);
            }
            if (IsChangedVisible(e.FullName)) {
                ShowAnchorDirectory(false, false);
            }
        }

        private void DirectoryModel_OnCreatedFileSystemItem(object sender, FileSystemInfoPathEventArgs e) {
            if(IsChangedVisible(e.FullName)) {
                ShowAnchorDirectory(false, false);
            }
        }

        private void DirectoryModel_OnSizeChangedFileSystemItem(object sender, FileSystemInfoPathEventArgs e) {
            if (IsChangedVisible(e.FullName)) {
                ShowAnchorDirectory(false, false);
            }
        }

        public void Dispose() {
            ModelCalculatorCancelationTokenSource?.Dispose();
            ConverterCancelationTokenSource?.Dispose();
        }
    }
}