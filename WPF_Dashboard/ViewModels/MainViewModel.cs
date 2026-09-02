using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using SmartTrafficDashboard.Models;
using SmartTrafficDashboard.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SmartTrafficDashboard.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private int _fpsFrameCount = 0;

        private DateTime _fpsStartTime =
            DateTime.Now;

        private string _fpsText =
            "FPS --";
        private readonly CameraService _cameraService;
        private readonly YoloService _yoloService;
        private readonly DispatcherTimer _clockTimer;

        private bool _isInferencing = false;
        private bool _hasReceivedFrame = false;

        // 이전 Ambulance 감지 상태
        private bool _wasAmbulanceDetected = false;


        // =========================================================
        // ROI
        // =========================================================
        private readonly object _roiLock =
            new object();

        // WPF 화면에 그린 ROI 좌표
        private readonly List<System.Windows.Rect> _canvasRois =
            new List<System.Windows.Rect>();

        private double _roiCanvasWidth = 0;
        private double _roiCanvasHeight = 0;


        // =========================================================
        // UI 상태
        // =========================================================
        private BitmapSource _cameraFrame;

        private string _systemStatus =
            "SYSTEM STANDBY";

        private string _currentTime =
            "--:--:--";

        private string _cameraStatus =
            "DISCONNECTED";

        private string _videoInputStatus =
            "카메라 연결 대기";

        private string _vehicleCount =
            "--";

        private string _trafficStatus =
            "대기";

        private string _signalStatus =
            "RED";

        // TRAFFIC STATUS용 응급 상태
        private string _emergencyStatus =
            "비응급";

        // SIGNAL / EMERGENCY용 긴급차량 감지 상태
        private string _emergencyDetectionStatus =
            "미감지";

        private string _signalChangeReason =
            "정상 운행";

        private Brush _systemStatusColor =
            Brushes.Gray;

        private Brush _cameraStatusColor =
            Brushes.Gray;

        // 현재 신호 색상
        private Brush _signalStatusColor =
            Brushes.Red;

        // TRAFFIC STATUS 응급 상태 표시 색상
        // 비응급 = 초록 / 응급 = 빨강
        private Brush _emergencyStatusColor =
            Brushes.Green;

        public string FpsText
        {
            get => _fpsText;

            set => SetProperty(
                ref _fpsText,
                value
            );
        }


        // =========================================================
        // 생성자
        // =========================================================
        public MainViewModel()
        {
            _cameraService =
                new CameraService();

            _yoloService =
                new YoloService();


            _cameraService.MatFrameReceived +=
                OnMatFrameReceived;


            // 현재 시간
            _clockTimer =
                new DispatcherTimer
                {
                    Interval =
                        TimeSpan.FromSeconds(1)
                };


            _clockTimer.Tick +=
                (s, e) =>
                {
                    CurrentTime =
                        DateTime.Now.ToString(
                            "HH:mm:ss"
                        );
                };


            _clockTimer.Start();


            CurrentTime =
                DateTime.Now.ToString(
                    "HH:mm:ss"
                );


            StartCamera();

            LoadYoloModel();
        }


        // =========================================================
        // MainWindow에서 ROI 2개 전달
        // =========================================================
        public void SetCanvasRois(
            IEnumerable<System.Windows.Rect> rois,
            double canvasWidth,
            double canvasHeight)
        {
            lock (_roiLock)
            {
                _canvasRois.Clear();


                if (rois != null)
                {
                    foreach (
                        System.Windows.Rect roi
                        in rois
                    )
                    {
                        if (!roi.IsEmpty)
                        {
                            _canvasRois.Add(
                                roi
                            );
                        }
                    }
                }


                _roiCanvasWidth =
                    canvasWidth;

                _roiCanvasHeight =
                    canvasHeight;
            }


            System.Diagnostics.Debug.WriteLine(
                "================================"
            );

            System.Diagnostics.Debug.WriteLine(
                $"ROI COUNT = {_canvasRois.Count}"
            );

            System.Diagnostics.Debug.WriteLine(
                $"CANVAS = " +
                $"{_roiCanvasWidth:F0} x " +
                $"{_roiCanvasHeight:F0}"
            );


            for (
                int i = 0;
                i < _canvasRois.Count;
                i++
            )
            {
                System.Diagnostics.Debug.WriteLine(
                    $"ROI {i + 1} = " +
                    $"X:{_canvasRois[i].X:F0}, " +
                    $"Y:{_canvasRois[i].Y:F0}, " +
                    $"W:{_canvasRois[i].Width:F0}, " +
                    $"H:{_canvasRois[i].Height:F0}"
                );
            }


            System.Diagnostics.Debug.WriteLine(
                "================================"
            );
        }


        // =========================================================
        // Canvas ROI
        // →
        // 실제 카메라 Frame ROI
        //
        // Image Stretch="UniformToFill" 보정
        // =========================================================
        private List<OpenCvSharp.Rect> GetFrameRois(
            int frameWidth,
            int frameHeight)
        {
            List<OpenCvSharp.Rect> result =
                new List<OpenCvSharp.Rect>();


            lock (_roiLock)
            {
                // ROI 두 개가 모두 설정되어야 활성화
                if (_canvasRois.Count != 2)
                {
                    return result;
                }


                if (_roiCanvasWidth <= 0 ||
                    _roiCanvasHeight <= 0)
                {
                    return result;
                }


                // UniformToFill과 같은 Scale 계산
                double scale =
                    Math.Max(
                        _roiCanvasWidth /
                        frameWidth,

                        _roiCanvasHeight /
                        frameHeight
                    );


                double renderedWidth =
                    frameWidth *
                    scale;


                double renderedHeight =
                    frameHeight *
                    scale;


                // 중앙 정렬로 인해 잘리는 영역
                double offsetX =
                    (
                        _roiCanvasWidth -
                        renderedWidth
                    ) / 2.0;


                double offsetY =
                    (
                        _roiCanvasHeight -
                        renderedHeight
                    ) / 2.0;


                foreach (
                    System.Windows.Rect canvasRoi
                    in _canvasRois
                )
                {
                    // Canvas 좌표
                    // →
                    // 실제 Frame 좌표
                    double frameLeft =
                        (
                            canvasRoi.Left -
                            offsetX
                        ) / scale;


                    double frameTop =
                        (
                            canvasRoi.Top -
                            offsetY
                        ) / scale;


                    double frameRight =
                        (
                            canvasRoi.Right -
                            offsetX
                        ) / scale;


                    double frameBottom =
                        (
                            canvasRoi.Bottom -
                            offsetY
                        ) / scale;


                    // Frame 범위 안으로 제한
                    frameLeft =
                        Clamp(
                            frameLeft,
                            0,
                            frameWidth
                        );


                    frameTop =
                        Clamp(
                            frameTop,
                            0,
                            frameHeight
                        );


                    frameRight =
                        Clamp(
                            frameRight,
                            0,
                            frameWidth
                        );


                    frameBottom =
                        Clamp(
                            frameBottom,
                            0,
                            frameHeight
                        );


                    int x =
                        (int)Math.Round(
                            frameLeft
                        );


                    int y =
                        (int)Math.Round(
                            frameTop
                        );


                    int right =
                        (int)Math.Round(
                            frameRight
                        );


                    int bottom =
                        (int)Math.Round(
                            frameBottom
                        );


                    int width =
                        right - x;


                    int height =
                        bottom - y;


                    if (width <= 0 ||
                        height <= 0)
                    {
                        continue;
                    }


                    // OpenCV 범위 초과 방지
                    if (x + width > frameWidth)
                    {
                        width =
                            frameWidth - x;
                    }


                    if (y + height > frameHeight)
                    {
                        height =
                            frameHeight - y;
                    }


                    if (width > 0 &&
                        height > 0)
                    {
                        result.Add(
                            new OpenCvSharp.Rect(
                                x,
                                y,
                                width,
                                height
                            )
                        );
                    }
                }
            }


            return result;
        }


        // =========================================================
        // ROI 안의 영상만 YOLO가 볼 수 있도록 생성
        //
        // 중요:
        // 이것은 화면 표시용이 아님.
        //
        // 실제 WPF 화면은 원본 카메라 그대로 표시됨.
        // =========================================================
        private Mat CreateRoiDetectionFrame(
            Mat source,
            List<OpenCvSharp.Rect> frameRois)
        {
            // YOLO 분석용 Frame
            Mat roiFrame =
                Mat.Zeros(
                    source.Rows,
                    source.Cols,
                    source.Type()
                ).ToMat();


            foreach (
                OpenCvSharp.Rect roi
                in frameRois
            )
            {
                using (
                    Mat sourceRegion =
                        new Mat(
                            source,
                            roi
                        )
                )
                {
                    using (
                        Mat targetRegion =
                            new Mat(
                                roiFrame,
                                roi
                            )
                    )
                    {
                        sourceRegion.CopyTo(
                            targetRegion
                        );
                    }
                }
            }


            return roiFrame;
        }


        // =========================================================
        // 검출 객체 중심점이 ROI 1 또는 ROI 2 안인지
        //
        // 마지막 안전장치
        // =========================================================
        private bool IsDetectionInsideRoi(
            Detection detection,
            List<OpenCvSharp.Rect> frameRois)
        {
            double centerX =
                detection.X;

            double centerY =
                detection.Y;


            foreach (
                OpenCvSharp.Rect roi
                in frameRois
            )
            {
                if (
                    centerX >= roi.X &&
                    centerX <= roi.X + roi.Width &&
                    centerY >= roi.Y &&
                    centerY <= roi.Y + roi.Height
                )
                {
                    return true;
                }
            }


            return false;
        }


        // =========================================================
        // Clamp
        // =========================================================
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


        // =========================================================
        // 카메라 시작
        // =========================================================
        private void StartCamera()
        {
            SystemStatus =
                "SYSTEM STANDBY";


            SystemStatusColor =
                Brushes.Gray;


            CameraStatus =
                "CONNECTING...";


            CameraStatusColor =
                Brushes.Orange;


            VideoInputStatus =
                "카메라 연결 중";


            bool started =
                _cameraService.Start(1);


            if (!started)
            {
                SystemStatus =
                    "SYSTEM STANDBY";


                SystemStatusColor =
                    Brushes.Gray;


                CameraStatus =
                    "DISCONNECTED";


                CameraStatusColor =
                    Brushes.Red;


                VideoInputStatus =
                    "카메라 연결 실패";
            }
        }


        // =========================================================
        // 카메라 종료
        // =========================================================
        public void StopCamera()
        {
            _clockTimer?.Stop();

            _cameraService?.Stop();

            _yoloService?.Dispose();


            SystemStatus =
                "SYSTEM STANDBY";


            SystemStatusColor =
                Brushes.Gray;


            CameraStatus =
                "DISCONNECTED";


            CameraStatusColor =
                Brushes.Red;


            VideoInputStatus =
                "카메라 연결 종료";
        }


        // =========================================================
        // YOLO 모델 로드
        // =========================================================
        private void LoadYoloModel()
        {
            string loadResult =
                _yoloService.LoadModel();


            if (_yoloService.IsLoaded)
            {
                string modelInfo =
                    _yoloService.GetModelInfo();


                MessageBox.Show(
                    loadResult +
                    "\n\n" +
                    modelInfo,
                    "YOLO 모델 확인"
                );
            }
            else
            {
                MessageBox.Show(
                    loadResult,
                    "YOLO 모델 로드 실패"
                );
            }
        }


        // =========================================================
        // 카메라 Frame 수신
        // =========================================================
        private void OnMatFrameReceived(

            Mat frame)
        {
            // 첫 프레임 수신
            if (!_hasReceivedFrame)
            {
                _hasReceivedFrame =
                    true;


                Application.Current.Dispatcher.Invoke(
                    () =>
                    {
                        SystemStatus =
                            "SYSTEM ONLINE";


                        SystemStatusColor =
                            Brushes.Green;


                        CameraStatus =
                            "CONNECTED";


                        CameraStatusColor =
                            Brushes.Green;


                        VideoInputStatus =
                            "영상 입력 정상";
                    }
                );
            }


            // 추론 중이면 Frame 버림
            if (_isInferencing)
            {
                frame.Dispose();

                return;
            }


            _isInferencing =
                true;


            System.Threading.Tasks.Task.Run(
                () =>
                {
                    Mat displayFrame =
                        null;

                    Mat roiDetectionFrame =
                        null;


                    try
                    {
                        int frameWidth =
                            frame.Width;


                        int frameHeight =
                            frame.Height;


                        // =================================================
                        // 화면에 보여줄 Frame
                        //
                        // 원본 카메라 그대로 복사
                        // =================================================
                        displayFrame =
                            frame.Clone();


                        // =================================================
                        // 현재 ROI 1 + ROI 2
                        // =================================================
                        List<OpenCvSharp.Rect> frameRois =
                            GetFrameRois(
                                frameWidth,
                                frameHeight
                            );


                        // ROI 두 개가 아직 설정되지 않은 경우
                        if (frameRois.Count != 2)
                        {
                            BitmapSource normalBitmap =
                                displayFrame.ToBitmapSource();


                            normalBitmap.Freeze();


                            Application.Current.Dispatcher.Invoke(
                                () =>
                                {
                                    CameraFrame =
                                        normalBitmap;


                                    VehicleCount =
                                        "0";


                                    UpdateAmbulanceStatus(
                                        false
                                    );
                                }
                            );


                            return;
                        }


                        // =================================================
                        // ROI 영역만 YOLO가 검사할 Frame 생성
                        // =================================================
                        roiDetectionFrame =
                            CreateRoiDetectionFrame(
                                frame,
                                frameRois
                            );


                        // =================================================
                        // YOLO 실행
                        //
                        // Class 0 = Ambulance
                        // Class 1 = JetBot
                        //
                        // ROI 밖 영상은 YOLO 입력에 들어가지 않음
                        // =================================================
                        var allDetections =
                            _yoloService.Detect(
                                roiDetectionFrame,
                                0.50f
                            );


                        // =================================================
                        // 혹시 경계에서 검출된 경우까지 확실하게 제거
                        //
                        // 중심점이 ROI 안인 객체만 최종 인정
                        // =================================================
                        var detections =
                            allDetections
                                .Where(
                                    detection =>
                                        IsDetectionInsideRoi(
                                            detection,
                                            frameRois
                                        )
                                )
                                .ToList();


                        // =================================================
                        // ROI 안 Ambulance
                        // =================================================
                        bool ambulanceDetected =
                            detections.Any(
                                detection =>
                                    detection.ClassId == 0
                            );


                        // =================================================
                        // ROI 안 차량 수
                        //
                        // Class 0 = Ambulance
                        // Class 1 = JetBot
                        // =================================================
                        int vehicleCount =
                            detections.Count(
                                detection =>
                                    detection.ClassId == 0 ||
                                    detection.ClassId == 1
                            );


                        // =================================================
                        // ROI 안 객체만 Bounding Box 표시
                        // =================================================
                        foreach (
                            Detection detection
                            in detections
                        )
                        {
                            int left =
                                (int)(
                                    detection.X -
                                    detection.Width / 2f
                                );


                            int top =
                                (int)(
                                    detection.Y -
                                    detection.Height / 2f
                                );


                            int width =
                                (int)
                                detection.Width;


                            int height =
                                (int)
                                detection.Height;


                            left =
                                Math.Max(
                                    0,
                                    left
                                );


                            top =
                                Math.Max(
                                    0,
                                    top
                                );


                            width =
                                Math.Min(
                                    width,
                                    frameWidth - left
                                );


                            height =
                                Math.Min(
                                    height,
                                    frameHeight - top
                                );


                            if (width <= 0 ||
                                height <= 0)
                            {
                                continue;
                            }


                            string className =
                                detection.ClassId == 0
                                ? "Ambulance"
                                : "JetBot";


                            Scalar boxColor =
                                detection.ClassId == 0
                                ? Scalar.Red
                                : Scalar.LimeGreen;


                            Cv2.Rectangle(
                                displayFrame,
                                new OpenCvSharp.Rect(
                                    left,
                                    top,
                                    width,
                                    height
                                ),
                                boxColor,
                                3
                            );


                            string label =
                                className +
                                " " +
                                detection
                                    .Confidence
                                    .ToString(
                                        "0.00"
                                    );


                            Cv2.PutText(
                                displayFrame,
                                label,
                                new OpenCvSharp.Point(
                                    left,
                                    Math.Max(
                                        25,
                                        top - 8
                                    )
                                ),
                                HersheyFonts
                                    .HersheySimplex,
                                0.7,
                                boxColor,
                                2
                            );
                        }


                        // =================================================
                        // 화면 표시
                        // =================================================
                        BitmapSource bitmap =
                            displayFrame
                                .ToBitmapSource();


                        bitmap.Freeze();


                        Application.Current.Dispatcher.Invoke(
    () =>
    {
        CameraFrame =
            bitmap;

        VehicleCount =
            vehicleCount.ToString();

        UpdateAmbulanceStatus(
            ambulanceDetected
        );


        // FPS 계산
        _fpsFrameCount++;

        double elapsedSeconds =
            (DateTime.Now - _fpsStartTime)
                .TotalSeconds;

        if (elapsedSeconds >= 1.0)
        {
            double fps =
                _fpsFrameCount /
                elapsedSeconds;

            FpsText =
                $"FPS {fps:0.0}";

            _fpsFrameCount =
                0;

            _fpsStartTime =
                DateTime.Now;
        }
    }
);


                        // Debug
                        System.Diagnostics.Debug.WriteLine(
                            $"ROI YOLO = {allDetections.Count} / " +
                            $"FINAL = {detections.Count} / " +
                            $"VEHICLE = {vehicleCount} / " +
                            $"AMBULANCE = {ambulanceDetected}"
                        );
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "YOLO ERROR = " +
                            ex.Message
                        );
                    }
                    finally
                    {
                        roiDetectionFrame?.Dispose();

                        displayFrame?.Dispose();

                        frame.Dispose();


                        _isInferencing =
                            false;
                    }
                }
            );
        }


        // =========================================================
        // Ambulance 상태
        // =========================================================
        private void UpdateAmbulanceStatus(
            bool detected)
        {
            if (detected)
            {
                // TRAFFIC STATUS
                EmergencyStatus =
                    "응급";

                EmergencyStatusColor =
                    Brushes.Red;


                // SIGNAL / EMERGENCY
                EmergencyDetectionStatus =
                    "감지";

                SignalChangeReason =
                    "긴급차량 우선";


                // ROI에 처음 진입한 순간만 로그
                if (!_wasAmbulanceDetected)
                {
                    EventLogs.Insert(
                        0,
                        new EventLogItem
                        {
                            Time =
                                DateTime.Now.ToString(
                                    "HH:mm:ss"
                                ),

                            Type =
                                "긴급차량",

                            Message =
                                "Ambulance ROI 진입 감지"
                        }
                    );
                }
            }
            else
            {
                // TRAFFIC STATUS
                EmergencyStatus =
                    "비응급";

                EmergencyStatusColor =
                    Brushes.Green;


                // SIGNAL / EMERGENCY
                EmergencyDetectionStatus =
                    "미감지";

                SignalChangeReason =
                    "정상 운행";


                // ROI에서 이탈한 순간만 로그
                if (_wasAmbulanceDetected)
                {
                    EventLogs.Insert(
                        0,
                        new EventLogItem
                        {
                            Time =
                                DateTime.Now.ToString(
                                    "HH:mm:ss"
                                ),

                            Type =
                                "긴급차량",

                            Message =
                                "Ambulance ROI 이탈"
                        }
                    );
                }
            }


            _wasAmbulanceDetected =
                detected;
        }


        // =========================================================
        // Binding
        // =========================================================
        public string SystemStatus
        {
            get => _systemStatus;

            set => SetProperty(
                ref _systemStatus,
                value
            );
        }


        public Brush SystemStatusColor
        {
            get => _systemStatusColor;

            set => SetProperty(
                ref _systemStatusColor,
                value
            );
        }


        public string CurrentTime
        {
            get => _currentTime;

            set => SetProperty(
                ref _currentTime,
                value
            );
        }


        public string CameraStatus
        {
            get => _cameraStatus;

            set => SetProperty(
                ref _cameraStatus,
                value
            );
        }


        public Brush CameraStatusColor
        {
            get => _cameraStatusColor;

            set => SetProperty(
                ref _cameraStatusColor,
                value
            );
        }


        public string VideoInputStatus
        {
            get => _videoInputStatus;

            set => SetProperty(
                ref _videoInputStatus,
                value
            );
        }


        public string VehicleCount
        {
            get => _vehicleCount;

            set => SetProperty(
                ref _vehicleCount,
                value
            );
        }


        public string TrafficStatus
        {
            get => _trafficStatus;

            set => SetProperty(
                ref _trafficStatus,
                value
            );
        }


        public string SignalStatus
        {
            get => _signalStatus;

            set => SetProperty(
                ref _signalStatus,
                value
            );
        }


        public Brush SignalStatusColor
        {
            get => _signalStatusColor;

            set => SetProperty(
                ref _signalStatusColor,
                value
            );
        }


        public string EmergencyStatus
        {
            get => _emergencyStatus;

            set => SetProperty(
                ref _emergencyStatus,
                value
            );
        }


        public string EmergencyDetectionStatus
        {
            get => _emergencyDetectionStatus;

            set => SetProperty(
                ref _emergencyDetectionStatus,
                value
            );
        }


        public Brush EmergencyStatusColor
        {
            get => _emergencyStatusColor;

            set => SetProperty(
                ref _emergencyStatusColor,
                value
            );
        }


        public string SignalChangeReason
        {
            get => _signalChangeReason;

            set => SetProperty(
                ref _signalChangeReason,
                value
            );
        }


        public BitmapSource CameraFrame
        {
            get => _cameraFrame;

            set => SetProperty(
                ref _cameraFrame,
                value
            );
        }


        public ObservableCollection<EventLogItem>
            EventLogs
        {
            get;
        }
        =
        new ObservableCollection<EventLogItem>();
    }
}