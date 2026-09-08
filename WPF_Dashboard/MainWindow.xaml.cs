using SmartTrafficDashboard.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SmartTrafficDashboard
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        // 현재 ROI 드래그 시작점
        private Point _roiStartPoint;

        // 현재 그리고 있는 ROI
        private Rectangle _currentRoiRectangle;

        // ROI를 그리는 중인지
        private bool _isDrawingRoi = false;

        // 완성된 ROI 사각형 목록
        private readonly List<Rectangle> _roiRectangles
            = new List<Rectangle>();

        // 완성된 ROI 좌표 목록
        private readonly List<Rect> _selectedRois
            = new List<Rect>();


        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
            DataContext = _viewModel;
        }


        // ========================================
        // ROI 드래그 시작
        // ========================================

        private void RoiCanvas_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            // 이미 ROI가 2개 만들어져 있으면
            // 새로운 ROI를 그릴 때 모두 초기화
            if (_selectedRois.Count >= 2)
            {
                RoiCanvas.Children.Clear();

                _roiRectangles.Clear();
                _selectedRois.Clear();

                Console.WriteLine(
                    "기존 ROI 초기화 → ROI 1부터 다시 설정"
                );
            }


            _roiStartPoint =
                e.GetPosition(RoiCanvas);


            // 새 ROI 생성
            _currentRoiRectangle =
                new Rectangle
                {
                    // 얇은 흰색 테두리
                    Stroke = Brushes.White,

                    StrokeThickness = 1.5,

                    // 내부는 거의 투명
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


            // Canvas 밖으로 조금 나가더라도
            // 드래그가 끊기지 않도록 함
            RoiCanvas.CaptureMouse();
        }


        // ========================================
        // ROI 드래그 중
        // ========================================

        private void RoiCanvas_MouseMove(
            object sender,
            MouseEventArgs e)
        {
            if (!_isDrawingRoi)
                return;

            if (_currentRoiRectangle == null)
                return;


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


        // ========================================
        // ROI 드래그 완료
        // ========================================

        private void RoiCanvas_MouseLeftButtonUp(
            object sender,
            MouseButtonEventArgs e)
        {
            if (!_isDrawingRoi)
                return;

            if (_currentRoiRectangle == null)
                return;


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


            // 실수로 클릭만 한 경우는 ROI로 인정 안 함
            if (roiWidth < 10 ||
                roiHeight < 10)
            {
                RoiCanvas.Children.Remove(
                    _currentRoiRectangle
                );

                _currentRoiRectangle = null;

                return;
            }


            // 완성된 Rectangle 저장
            _roiRectangles.Add(
                _currentRoiRectangle
            );


            // 좌표 저장
            Rect roi =
                new Rect(
                    roiX,
                    roiY,
                    roiWidth,
                    roiHeight
                );


            _selectedRois.Add(
                roi
            );


            int roiNumber =
                _selectedRois.Count;


            Console.WriteLine(
                $"ROI {roiNumber} 선택 완료 " +
                $"X={roiX:F0}, " +
                $"Y={roiY:F0}, " +
                $"W={roiWidth:F0}, " +
                $"H={roiHeight:F0}"
            );


            if (_selectedRois.Count == 1)
            {
                Console.WriteLine(
                    "ROI 1 설정 완료 → ROI 2를 설정하세요."
                );
            }
            else if (_selectedRois.Count == 2)
            {
                Console.WriteLine(
                    "ROI 1 + ROI 2 설정 완료"
                );

                Console.WriteLine(
                    "두 ROI는 하나의 통합 감시구역으로 사용됩니다."
                );
            }


            _currentRoiRectangle = null;
        }


        // ========================================
        // 프로그램 종료
        // ========================================

        protected override void OnClosed(
            EventArgs e)
        {
            _viewModel?.StopCamera();

            base.OnClosed(e);
        }
    }
}