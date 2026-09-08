using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using SmartTrafficDashboard.Models;
using SmartTrafficDashboard.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Media;

namespace SmartTrafficDashboard.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private Brush _systemStatusColor = Brushes.Gray;
        private Brush _cameraStatusColor = Brushes.Gray;
        private readonly CameraService _cameraService;
        private readonly YoloService _yoloService;

        private readonly DispatcherTimer _clockTimer;

        private bool _isInferencing = false;
        private bool _hasReceivedFrame = false;

        private BitmapSource _cameraFrame;

        private string _systemStatus = "SYSTEM STANDBY";
        private string _currentTime = "--:--:--";
        private string _cameraStatus = "DISCONNECTED";
        private string _videoInputStatus = "카메라 연결 대기";

        private string _vehicleCount = "--";
        private string _trafficStatus = "대기";
        private string _signalStatus = "대기";
        private string _emergencyStatus = "미감지";
        private string _signalChangeReason = "대기";

        public Brush SystemStatusColor
        {
            get => _systemStatusColor;
            set => SetProperty(
                ref _systemStatusColor,
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
        public MainViewModel()
        {
            _cameraService = new CameraService();
            _yoloService = new YoloService();

            // YOLO Bounding Box가 적용된 Mat 사용
            _cameraService.MatFrameReceived += OnMatFrameReceived;

            // ================================
            // 현재 시간 표시
            // ================================
            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _clockTimer.Tick += (s, e) =>
            {
                CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            };

            _clockTimer.Start();

            // 실행하자마자 시간 표시
            CurrentTime = DateTime.Now.ToString("HH:mm:ss");

            // ================================
            // 카메라 시작
            // ================================
            StartCamera();

            // ================================
            // YOLO 모델 로드
            // ================================
            LoadYoloModel();
        }


        // ========================================
        // 카메라 시작
        // ========================================
        private void StartCamera()
        {
            SystemStatus = "SYSTEM STANDBY";
            SystemStatusColor = Brushes.Gray;

            CameraStatus = "CONNECTING...";
            CameraStatusColor = Brushes.Orange;

            VideoInputStatus = "카메라 연결 중";

            bool started = _cameraService.Start(0);

            if (!started)
            {
                SystemStatus = "SYSTEM STANDBY";
                SystemStatusColor = Brushes.Gray;

                CameraStatus = "DISCONNECTED";
                CameraStatusColor = Brushes.Red;

                VideoInputStatus = "카메라 연결 실패";
            }
        }


        // ========================================
        // 카메라 종료
        // ========================================
        public void StopCamera()
        {
            _clockTimer?.Stop();

            _cameraService?.Stop();
            _yoloService?.Dispose();

            SystemStatus = "SYSTEM STANDBY";
            SystemStatusColor = Brushes.Gray;

            CameraStatus = "DISCONNECTED";
            CameraStatusColor = Brushes.Red;

            VideoInputStatus = "카메라 연결 종료";
        }


        // ========================================
        // YOLO 모델 로드
        // ========================================
        private void LoadYoloModel()
        {
            string loadResult = _yoloService.LoadModel();

            if (_yoloService.IsLoaded)
            {
                string modelInfo = _yoloService.GetModelInfo();

                MessageBox.Show(
                    loadResult + "\n\n" + modelInfo,
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


        // ========================================
        // 카메라 Mat 프레임 수신
        // ========================================
        private void OnMatFrameReceived(Mat frame)
        {
            // 실제 프레임이 처음 들어온 순간
            // 카메라 연결 성공으로 확정
            if (!_hasReceivedFrame)
            {
                _hasReceivedFrame = true;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    SystemStatus = "SYSTEM ONLINE";
                    SystemStatusColor = Brushes.Green;

                    CameraStatus = "CONNECTED";
                    CameraStatusColor = Brushes.Green;

                    VideoInputStatus = "영상 입력 정상";
                });
            }


            // 이미 추론 중이면 현재 프레임은 버림
            if (_isInferencing)
            {
                frame.Dispose();
                return;
            }


            _isInferencing = true;


            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    // ================================
                    // YOLO 추론
                    //
                    // Class 0 = Ambulance
                    // Class 1 = JetBot
                    // ================================
                    var detections =
                        _yoloService.Detect(
                            frame,
                            0.50f
                        );


                    // ================================
                    // Bounding Box 출력
                    // ================================
                    foreach (var detection in detections)
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
                            (int)detection.Width;


                        int height =
                            (int)detection.Height;


                        // 프레임 밖으로 나가지 않도록 보정
                        left = Math.Max(0, left);
                        top = Math.Max(0, top);


                        width = Math.Min(
                            width,
                            frame.Width - left
                        );


                        height = Math.Min(
                            height,
                            frame.Height - top
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
                            frame,
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
                            detection.Confidence.ToString("0.00");


                        Cv2.PutText(
                            frame,
                            label,
                            new OpenCvSharp.Point(
                                left,
                                Math.Max(
                                    25,
                                    top - 8
                                )
                            ),
                            HersheyFonts.HersheySimplex,
                            0.7,
                            boxColor,
                            2
                        );
                    }


                    // ================================
                    // Mat → WPF BitmapSource
                    // ================================
                    BitmapSource bitmap =
                        frame.ToBitmapSource();

                    bitmap.Freeze();


                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CameraFrame = bitmap;
                    });


                    // ================================
                    // 디버그 출력
                    // ================================
                    if (detections.Count > 0)
                    {
                        var bestDetection =
                            detections
                                .OrderByDescending(
                                    d => d.Confidence
                                )
                                .First();


                        string bestClassName =
                            bestDetection.ClassId == 0
                            ? "Ambulance"
                            : "JetBot";


                        System.Diagnostics.Debug.WriteLine(
                            "DETECTION COUNT = " +
                            detections.Count +
                            " / CLASS = " +
                            bestClassName +
                            " / BEST CONFIDENCE = " +
                            bestDetection.Confidence.ToString("0.00")
                        );
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "NO DETECTION"
                        );
                    }
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
                    frame.Dispose();
                    _isInferencing = false;
                }
            });
        }


        // ========================================
        // Binding Properties
        // ========================================

        public string SystemStatus
        {
            get => _systemStatus;
            set => SetProperty(
                ref _systemStatus,
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


        public string EmergencyStatus
        {
            get => _emergencyStatus;
            set => SetProperty(
                ref _emergencyStatus,
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


        public ObservableCollection<EventLogItem> EventLogs
        {
            get;
        }
        =
        new ObservableCollection<EventLogItem>();
    }
}