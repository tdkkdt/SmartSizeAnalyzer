using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SizeScanner.Annotations;
using SizeScanner.Model;

namespace SmartPieChart {
    public class PiePieceItem : INotifyPropertyChanged {
        string name;
        double size;

        public string Name {
            get => name;
            set {
                if (value == name)
                    return;
                name = value;
                OnPropertyChanged();
            }
        }

        public double Size {
            get => size;
            set {
                if (value.Equals(size))
                    return;
                size = value;
                OnPropertyChanged();
            }
        }

        public PiePieceItem Parent { get; private set; }

        public bool IsSpecial { get; set; }
        public List<PiePieceItem> Items { get; }

        public PiePieceItem(string name, double size, int capacity = 4) {
            Name = name;
            Size = size;
            Items = new List<PiePieceItem>(capacity);
        }

        public void Add(PiePieceItem item) {
            Items.Add(item);
            item.Parent = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    class PiePiece : Shape {
        public static bool IsBeingVisible(double innerRadius, double wedgeAngle) {
            return 2 * Math.PI * innerRadius * (wedgeAngle / 360) >= 2;
        }

        public double InnerRadius { get; set; }
        public double OuterRadius { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double RotationAngle { get; set; }
        public double WedgeAngle { get; set; }
        PiePieceItem PiePieceItem { get; set; }

        public PiePiece(PiePieceItem piePieceItem) {
            PiePieceItem = piePieceItem;
        }

        protected override Geometry DefiningGeometry {
            get {
                StreamGeometry geometry = new StreamGeometry();
                geometry.FillRule = FillRule.EvenOdd;
                using (StreamGeometryContext context = geometry.Open()) {
                    DrawGeometry(context);
                }
                geometry.Freeze();

                return geometry;
            }
        }

        private void DrawGeometry(StreamGeometryContext context) {
            Point innerArcStartPoint = PolarToCartesian(RotationAngle, InnerRadius);
            innerArcStartPoint.Offset(CenterX, CenterY);

            double wedgeAngle = WedgeAngle;
            if (wedgeAngle == 360) {
                wedgeAngle = 359.99;
            }
            Point innerArcEndPoint = PolarToCartesian(RotationAngle + wedgeAngle, InnerRadius);
            innerArcEndPoint.Offset(CenterX, CenterY);

            Point outerArcStartPoint = PolarToCartesian(RotationAngle, OuterRadius);
            outerArcStartPoint.Offset(CenterX, CenterY);

            Point outerArcEndPoint = PolarToCartesian(RotationAngle + wedgeAngle, OuterRadius);
            outerArcEndPoint.Offset(CenterX, CenterY);

            bool largeArc = wedgeAngle > 180.0;

            Size outerArcSize = new Size(OuterRadius, OuterRadius);
            Size innerArcSize = new Size(InnerRadius, InnerRadius);

            context.BeginFigure(innerArcStartPoint, true, true);
            context.LineTo(outerArcStartPoint, true, true);
            context.ArcTo(outerArcEndPoint, outerArcSize, 0, largeArc, SweepDirection.Clockwise, true, true);
            context.LineTo(innerArcEndPoint, true, true);
            context.ArcTo(innerArcStartPoint, innerArcSize, 0, largeArc, SweepDirection.Counterclockwise, true, true);
        }

        public static Point PolarToCartesian(double angle, double radius) {
            double angleRad = Math.PI / 180.0 * angle;

            double x = radius * Math.Cos(angleRad);
            double y = radius * Math.Sin(angleRad);

            return new Point(x, y);
        }

        static string separator = System.IO.Path.DirectorySeparatorChar.ToString();

        public string GetFullLabel() {
            Stack<string> labels = new Stack<string>();
            PiePieceItem current = PiePieceItem;
            while (current != null) {
                labels.Push(current.Name);
                current = current.Parent;
            }
            return string.Join(separator, labels);
        }
    }
}
