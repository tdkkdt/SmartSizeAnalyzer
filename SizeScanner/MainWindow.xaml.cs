using System;
using System.Windows;
using System.Windows.Input;
using SizeScanner.Model;
using SmartPieChart;

namespace SizeScanner {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        ViewModel ViewModel { get; }

        public MainWindow() {
            InitializeComponent();
            DirectoryModel model = new DirectoryModel();
            ViewModel = new ViewModel(model);
            DataContext = ViewModel;
        }

        void OpenFolderExecuted(object sender, RoutedEventArgs e) {
            ViewModel.OpenFolder();
        }

        void RefreshFolderExecuted(object sender, ExecutedRoutedEventArgs e) {
            ViewModel.ReAnalyze();
        }

        void BrowseBackExecuted(object sender, ExecutedRoutedEventArgs e) {
            ViewModel.BrowseBack();
        }

        void BrowseHomeExecuted(object sender, ExecutedRoutedEventArgs e) {
            ViewModel.BrowseParent();
        }

        void SmartPieChart_OnOnPieceMouseEnter(object sender, SmartPieChartPieceMouseEventArgs e) {
            ViewModel.MouseEnterPiece(e.Label);
        }

        void SmartPieChart_OnOnPieceMouseLeave(object sender, SmartPieChartPieceMouseEventArgs e) {
            ViewModel.MouseLeavePiece();
        }

        void SmartPieChart_OnOnPieceMouseUp(object sender, SmartPieChartPieceMouseButtonEventArgs e) {
            ViewModel.MouseClickOnPiece(e.Label);
        }
    }
}