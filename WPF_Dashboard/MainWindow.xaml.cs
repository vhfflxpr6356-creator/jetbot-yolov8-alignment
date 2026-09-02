using SmartTrafficDashboard.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SmartTrafficDashboard
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        private Point _roiStartPoint;

        private Rectangle _currentRoiRectangle;

        private bool _isDrawingRoi = false;


        // 화면에 표시 중인 ROI 사각형
        private readonly List<Rectangle> _roiRectangles =
            new List<Rectangle>();


        // Canvas 기준 ROI
        private readonly List<System.Windows.Rect> _canvasRois =
            new List<System.Windows.Rect>();


        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();

            DataContext = _viewModel;
        }


        // ============================================
        // ROI 그리기 시작
        // ============================================
        private void RoiCanvas_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            // ROI가 이미 2개 있다면
            // 세 번째 클릭부터 전체 초기화
            if (_canvasRois.Count >= 2)
            {
                RoiCanvas.Children.Clear();

                _roiRectangles.Clear();

                _canvasRois.Clear();


                // ViewModel ROI도 초기화
                _viewModel.SetCanvasRois(
                 new List<System.Windows.Rect>(),
                 RoiCanvas.ActualWidth,
                 RoiCanvas.ActualHeight
                );


                Console.WriteLine(
                    "ROI 초기화 → ROI 1부터 다시 설정"
                );
            }


            _roiStartPoint =
                e.GetPosition(RoiCanvas);


            _currentRoiRectangle =
                new Rectangle
                {
                    Stroke = Brushes.White,

                    StrokeThickness = 1.5,

                    Fill = new SolidColorBrush(
                        Color.FromArgb(
                            6,
                            255,
                            255,
                            255
                        )
                    )
                };


            Canvas.SetLeft(
                _currentRoiRectangle,
                _roiStartPoint.X
            );


            Canvas.SetTop(
                _currentRoiRectangle,
                _roiStartPoint.Y
            );


            RoiCanvas.Children.Add(
                _currentRoiRectangle
            );


            _isDrawingRoi = true;

            RoiCanvas.CaptureMouse();
        }


        // ============================================
        // ROI 드래그 중
        // ============================================
        private void RoiCanvas_MouseMove(
            object sender,
            MouseEventArgs e)
        {
            if (!_isDrawingRoi)
            {
                return;
            }


            if (_currentRoiRectangle == null)
            {
                return;
            }


            Point currentPoint =
                e.GetPosition(RoiCanvas);


            double x =
                Math.Min(
                    currentPoint.X,
                    _roiStartPoint.X
                );


            double y =
                Math.Min(
                    currentPoint.Y,
                    _roiStartPoint.Y
                );


            double width =
                Math.Abs(
                    currentPoint.X -
                    _roiStartPoint.X
                );


            double height =
                Math.Abs(
                    currentPoint.Y -
                    _roiStartPoint.Y
                );


            Canvas.SetLeft(
                _currentRoiRectangle,
                x
            );


            Canvas.SetTop(
                _currentRoiRectangle,
                y
            );


            _currentRoiRectangle.Width =
                width;


            _currentRoiRectangle.Height =
                height;
        }


        // ============================================
        // ROI 그리기 완료
        // ============================================
        private void RoiCanvas_MouseLeftButtonUp(
            object sender,
            MouseButtonEventArgs e)
        {
            if (!_isDrawingRoi)
            {
                return;
            }


            if (_currentRoiRectangle == null)
            {
                return;
            }


            _isDrawingRoi = false;

            RoiCanvas.ReleaseMouseCapture();


            double roiX =
                Canvas.GetLeft(
                    _currentRoiRectangle
                );


            double roiY =
                Canvas.GetTop(
                    _currentRoiRectangle
                );


            double roiWidth =
                _currentRoiRectangle.Width;


            double roiHeight =
                _currentRoiRectangle.Height;


            // 실수로 클릭만 한 경우 제거
            if (roiWidth < 10 ||
                roiHeight < 10)
            {
                RoiCanvas.Children.Remove(
                    _currentRoiRectangle
                );


                _currentRoiRectangle = null;

                return;
            }


            _roiRectangles.Add(
                _currentRoiRectangle
            );


            System.Windows.Rect canvasRoi =
                new System.Windows.Rect(
                    roiX,
                    roiY,
                    roiWidth,
                    roiHeight
                );


            _canvasRois.Add(
                canvasRoi
            );


            Console.WriteLine(
                $"ROI {_canvasRois.Count} 화면 설정 완료"
            );


            // ============================================
            // ROI 두 개 모두 설정했을 때만 활성화
            // ============================================
            if (_canvasRois.Count == 2)
            {
                List<System.Windows.Rect>
                    normalizedRois =
                    new List<System.Windows.Rect>();


                foreach (
                    System.Windows.Rect canvasRect
                    in _canvasRois
                )
                {
                    System.Windows.Rect normalizedRect =
                        ConvertCanvasRectToNormalizedRect(
                            canvasRect
                        );


                    if (!normalizedRect.IsEmpty)
                    {
                        normalizedRois.Add(
                            normalizedRect
                        );
                    }
                }


                if (normalizedRois.Count == 2)
                {
                    _viewModel.SetCanvasRois(
                        _canvasRois,
                        RoiCanvas.ActualWidth,
                        RoiCanvas.ActualHeight
                );


                    Console.WriteLine(
                        "ROI 1 + ROI 2 활성화 완료"
                    );


                    for (
                        int i = 0;
                        i < normalizedRois.Count;
                        i++
                    )
                    {
                        Console.WriteLine(
                            $"ROI {i + 1} " +
                            $"NX={normalizedRois[i].X:F3}, " +
                            $"NY={normalizedRois[i].Y:F3}, " +
                            $"NW={normalizedRois[i].Width:F3}, " +
                            $"NH={normalizedRois[i].Height:F3}"
                        );
                    }
                }
                else
                {
                    Console.WriteLine(
                        "ROI 좌표 변환 실패"
                    );
                }
            }


            _currentRoiRectangle = null;
        }


        // ============================================
        // WPF Canvas 좌표
        // →
        // 실제 영상 기준 0~1 정규화 좌표
        //
        // Image Stretch="UniformToFill" 보정 포함
        // ============================================
        private System.Windows.Rect
            ConvertCanvasRectToNormalizedRect(
                System.Windows.Rect canvasRect)
        {
            BitmapSource source =
                CameraImage.Source
                as BitmapSource;


            if (source == null)
            {
                return System.Windows.Rect.Empty;
            }


            double canvasWidth =
                RoiCanvas.ActualWidth;


            double canvasHeight =
                RoiCanvas.ActualHeight;


            double imageWidth =
                source.PixelWidth;


            double imageHeight =
                source.PixelHeight;


            if (canvasWidth <= 0 ||
                canvasHeight <= 0 ||
                imageWidth <= 0 ||
                imageHeight <= 0)
            {
                return System.Windows.Rect.Empty;
            }


            // ============================================
            // UniformToFill
            //
            // 영상이 Canvas 전체를 채우도록
            // 더 큰 Scale 사용
            // ============================================
            double scale =
                Math.Max(
                    canvasWidth / imageWidth,
                    canvasHeight / imageHeight
                );


            double renderedWidth =
                imageWidth * scale;


            double renderedHeight =
                imageHeight * scale;


            // 중앙 정렬로 인한 크롭 위치
            double offsetX =
                (
                    canvasWidth -
                    renderedWidth
                ) / 2.0;


            double offsetY =
                (
                    canvasHeight -
                    renderedHeight
                ) / 2.0;


            // ============================================
            // Canvas ROI
            // →
            // 실제 원본 영상 Pixel 좌표
            // ============================================
            double imageLeft =
                (
                    canvasRect.Left -
                    offsetX
                ) / scale;


            double imageTop =
                (
                    canvasRect.Top -
                    offsetY
                ) / scale;


            double imageRight =
                (
                    canvasRect.Right -
                    offsetX
                ) / scale;


            double imageBottom =
                (
                    canvasRect.Bottom -
                    offsetY
                ) / scale;


            // 실제 이미지 범위 안으로 제한
            imageLeft =
                Clamp(
                    imageLeft,
                    0,
                    imageWidth
                );


            imageTop =
                Clamp(
                    imageTop,
                    0,
                    imageHeight
                );


            imageRight =
                Clamp(
                    imageRight,
                    0,
                    imageWidth
                );


            imageBottom =
                Clamp(
                    imageBottom,
                    0,
                    imageHeight
                );


            if (imageRight <= imageLeft ||
                imageBottom <= imageTop)
            {
                return System.Windows.Rect.Empty;
            }


            // ============================================
            // Pixel 좌표
            // →
            // 0 ~ 1 정규화
            // ============================================
            double normalizedLeft =
                imageLeft / imageWidth;


            double normalizedTop =
                imageTop / imageHeight;


            double normalizedRight =
                imageRight / imageWidth;


            double normalizedBottom =
                imageBottom / imageHeight;


            return new System.Windows.Rect(
                normalizedLeft,
                normalizedTop,
                normalizedRight -
                normalizedLeft,
                normalizedBottom -
                normalizedTop
            );
        }


        private double Clamp(
            double value,
            double min,
            double max)
        {
            if (value < min)
            {
                return min;
            }


            if (value > max)
            {
                return max;
            }


            return value;
        }


        // ============================================
        // 프로그램 종료
        // ============================================
        protected override void OnClosed(
            EventArgs e)
        {
            _viewModel?.StopCamera();

            base.OnClosed(e);
        }
    }
}