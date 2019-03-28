using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SmartPieChart {
    public partial class SmartPieChart : UserControl {
        const int MaxLevel = 8;
        const int MinInnerCircleRadius = 20;
        const int MinPieLevelSize = 20;
        const int MaxInnerCircleRadius = 80;
        const int MaxPieLevelSize = 80;

        double Diameter => Math.Min(RenderSize.Width, RenderSize.Height);
        double CenterCircleRadius => Math.Min(Math.Max(Diameter / 2 / MaxLevel, MinInnerCircleRadius), MaxInnerCircleRadius);
        TextBlock CenterTextBlock { get; set; }

        List<PiePiece> PiePieces { get; }

        public string ChartLabel { get => (string) GetValue(ChartLabelProperty); set => SetValue(ChartLabelProperty, value); }

        public static readonly DependencyProperty ChartLabelProperty =
            DependencyProperty.Register("ChartLabel", typeof(string), typeof(SmartPieChart),
                new PropertyMetadata("", ChartLabelPropertyChangedCallback));

        static void ChartLabelPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            SmartPieChart pieChart = d as SmartPieChart;
            pieChart?.UpdateTextBlock();
        }

        public List<PiePieceItem> ItemsSource { get => (List<PiePieceItem>) GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(List<PiePieceItem>), typeof(SmartPieChart),
                new PropertyMetadata(null, ItemsSourcePropertyChangedCallback));

        static void ItemsSourcePropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            SmartPieChart pieChart = d as SmartPieChart;
            pieChart?.UpdateItemsSource(e.OldValue as List<PiePieceItem>);
        }

        public SmartPieChart() {
            InitializeComponent();
            SizeChanged += SmartPieChart_SizeChanged;
            PiePieces = new List<PiePiece>();
        }

        void SmartPieChart_SizeChanged(object sender, SizeChangedEventArgs e) {
            GeneratePiePieces();
        }

        void UpdateItemsSource(List<PiePieceItem> previous) {
            if (previous != null) {
                //Unsubscribe(previous);
            }
            if (ItemsSource == null)
                return;
            //Subscribe(ItemsSource);

            GeneratePiePieces();
        }

        //void Unsubscribe(List<PiePieceItem> itemsSource) {
        //    //previous.CollectionChanged -= ItemsSourceCollectionChanged;
        //}

        //void Subscribe(List<PiePieceItem> itemsSource) {
        //    //itemsSource.CollectionChanged += ItemsSourceCollectionChanged;
        //    //ObserveBoundCollectionChanges(itemsSource);
        //}

        //void ItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
        //    GeneratePiePieces();
        //    ObserveBoundCollectionChanges();
        //}

        //void ObserveBoundCollectionChanges() {
        //    CollectionView myCollectionView = (CollectionView) CollectionViewSource.GetDefaultView(this.DataContext);

        //    if (myCollectionView == null)
        //        return;

        //    foreach (object item in myCollectionView) {
        //        if (item is INotifyPropertyChanged observable) {
        //            observable.PropertyChanged += ItemPropertyChanged;
        //        }
        //    }
        //}

        //void ItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
        //    GeneratePiePieces();
        //}

        void GeneratePiePieces() {
            Canvas.Children.Clear();
            PiePieces.Clear();
            CenterTextBlock = null;
            var items = ItemsSource;

            if (items == null)
                return;

            int maxDepth = CalcMaxDepth(items);
            double pieLevelSize = Math.Min(Math.Max((Diameter / 2 - CenterCircleRadius) / maxDepth, MinPieLevelSize), MaxPieLevelSize);

            AddCenterCircle();

            GeneratePiecesFromItems(items, CenterCircleRadius, pieLevelSize, 0d, 360d, new[] {Colors.DarkGreen, Colors.BlueViolet, Colors.Brown}, 0);
        }

        void AddCenterCircle() {
            Border centerCircle = new Border();
            centerCircle.Width = CenterCircleRadius * 2;
            centerCircle.Height = CenterCircleRadius * 2;
            centerCircle.BorderBrush = Brushes.Black;
            centerCircle.BorderThickness = new Thickness(0.5);
            centerCircle.Background = Brushes.AntiqueWhite;
            centerCircle.CornerRadius = new CornerRadius(CenterCircleRadius);
            Canvas.Children.Add(centerCircle);
            double centerY = RenderSize.Height / 2;
            Canvas.SetTop(centerCircle, centerY - CenterCircleRadius);
            double centerX = RenderSize.Width / 2;
            Canvas.SetLeft(centerCircle, centerX - CenterCircleRadius);
            UpdateTextBlock();
            centerCircle.Child = CenterTextBlock;
        }

        void UpdateTextBlock() {
            if (CenterTextBlock == null) {
                CenterTextBlock = new TextBlock();
                CenterTextBlock.FontSize = CenterCircleRadius * 0.5;
                CenterTextBlock.TextWrapping = TextWrapping.Wrap;
                CenterTextBlock.TextAlignment = TextAlignment.Center;
                CenterTextBlock.HorizontalAlignment = HorizontalAlignment.Center;
                CenterTextBlock.VerticalAlignment = VerticalAlignment.Center;
            }
            CenterTextBlock.Text = ChartLabel;
        }

        int CalcMaxDepth(List<PiePieceItem> items) {
            if (items.Count == 0)
                return 0;
            int maxInnerDepth = 0;
            foreach (PiePieceItem item in items) {
                maxInnerDepth = Math.Max(CalcMaxDepth(item.Items), maxInnerDepth);
            }
            return 1 + maxInnerDepth;
        }

        void GeneratePiecesFromItems(List<PiePieceItem> items, double innerRadius, double pieLevelSize, double startAngle, double wedgeAngle, Color[] colors, int level) {
            if (level == MaxLevel) {
                return;
            }
            double accumulativeAngle = 0;
            double outerRadius = innerRadius + pieLevelSize;
            double centerX = RenderSize.Width / 2;
            double centerY = RenderSize.Height / 2;
            int colorI = 0;
            foreach (var item in items) {
                if (!PiePiece.IsBeingVisible(innerRadius, item.Size * wedgeAngle))
                    continue;
                Color pieceColor = colors[colorI % colors.Length];
                PiePiece piePiece = new PiePiece(item) {
                    InnerRadius = innerRadius,
                    OuterRadius = outerRadius,
                    CenterX = centerX,
                    CenterY = centerY,
                    RotationAngle = accumulativeAngle + startAngle,
                    WedgeAngle = item.Size * wedgeAngle,
                    Fill = new SolidColorBrush(pieceColor),
                    Stroke = item.IsSpecial ? Brushes.White : Brushes.Black,
                    StrokeThickness = 0.5,
                };
                piePiece.MouseEnter += PiePiece_MouseEnter;
                piePiece.MouseLeave += PiePiece_MouseLeave;
                piePiece.MouseUp += PiePiece_MouseUp;
                PiePieces.Add(piePiece);
                Canvas.Children.Add(piePiece);
                GeneratePiecesFromItems(item.Items, outerRadius, pieLevelSize, accumulativeAngle + startAngle, piePiece.WedgeAngle, CalculateInnerColors(pieceColor), level + 1);
                accumulativeAngle += piePiece.WedgeAngle;
                colorI++;
            }
        }

        Color[] CalculateInnerColors(Color baseColor) {
            return new[] {
                Color.FromArgb((byte) (baseColor.A - 20), (byte) (baseColor.R + 5), baseColor.G, baseColor.B),
                Color.FromArgb((byte) (baseColor.A - 20), baseColor.R, (byte) (baseColor.G + 5), baseColor.B),
                Color.FromArgb((byte) (baseColor.A - 20), baseColor.R, baseColor.G, (byte) (baseColor.B + 5)),
            };
        }

        void PiePiece_MouseEnter(object sender, MouseEventArgs e) {
            PiePiece piePiece = e.Source as PiePiece;
            if (piePiece == null)
                return;
            RaiseOnPieceMouseEnter(e, piePiece.GetFullLabel());
        }

        EventHandler<SmartPieChartPieceMouseEventArgs> onPieceMouseEnter;
        public event EventHandler<SmartPieChartPieceMouseEventArgs> OnPieceMouseEnter { add => onPieceMouseEnter += value; remove => onPieceMouseEnter -= value; }

        protected virtual void RaiseOnPieceMouseEnter(MouseEventArgs e, string label) {
            var eventArgs = new SmartPieChartPieceMouseEventArgs(e, label);
            onPieceMouseEnter?.Invoke(this, eventArgs);
        }

        void PiePiece_MouseLeave(object sender, MouseEventArgs e) {
            PiePiece piePiece = e.Source as PiePiece;
            if(piePiece == null)
                return;
            RaiseOnPieceMouseLeave(e, piePiece.GetFullLabel());
        }

        EventHandler<SmartPieChartPieceMouseEventArgs> onPieceMouseLeave;
        public event EventHandler<SmartPieChartPieceMouseEventArgs> OnPieceMouseLeave { add => onPieceMouseLeave += value; remove => onPieceMouseLeave -= value; }

        protected virtual void RaiseOnPieceMouseLeave(MouseEventArgs e, string label) {
            var eventArgs = new SmartPieChartPieceMouseEventArgs(e, label);
            onPieceMouseLeave?.Invoke(this, eventArgs);
        }

        private void PiePiece_MouseUp(object sender, MouseButtonEventArgs e) {
            PiePiece piePiece = e.Source as PiePiece;
            if(piePiece == null)
                return;
            RaiseOnPieceMouseUp(e, piePiece.GetFullLabel());
        }

        EventHandler<SmartPieChartPieceMouseButtonEventArgs> onPieceMouseUp;
        public event EventHandler<SmartPieChartPieceMouseButtonEventArgs> OnPieceMouseUp { add => onPieceMouseUp += value; remove => onPieceMouseUp -= value; }

        protected virtual void RaiseOnPieceMouseUp(MouseButtonEventArgs e, string label) {
            var eventArgs = new SmartPieChartPieceMouseButtonEventArgs(e, label);
            onPieceMouseUp?.Invoke(this, eventArgs);
        }
    }

    public class SmartPieChartPieceMouseEventArgs : EventArgs {
        public MouseEventArgs MouseEventArgs { get; }
        public string Label { get; }

        public SmartPieChartPieceMouseEventArgs(MouseEventArgs mouseEventArgs, string label) {
            MouseEventArgs = mouseEventArgs;
            Label = label;
        }
    }
    public class SmartPieChartPieceMouseButtonEventArgs : EventArgs {
        public MouseEventArgs MouseButtonEventArgs { get; }
        public string Label { get; }

        public SmartPieChartPieceMouseButtonEventArgs(MouseButtonEventArgs mouseButtonEventArgs, string label) {
            MouseButtonEventArgs = mouseButtonEventArgs;
            Label = label;
        }
    }
}