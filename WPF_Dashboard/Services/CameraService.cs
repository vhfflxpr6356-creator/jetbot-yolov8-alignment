using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SmartTrafficDashboard.Services
{
    public class CameraService : IDisposable
    {
        private VideoCapture _capture;
        private CancellationTokenSource _cancellationTokenSource;

        public event Action<BitmapSource> FrameReceived;

        public event Action<Mat> MatFrameReceived;

        public bool IsRunning { get; private set; }

        public int ActiveCameraIndex { get; private set; } = -1;

        public string ActiveBackend { get; private set; } = string.Empty;

        public bool Start(int cameraIndex = 0)
        {
            if (IsRunning)
                return true;

            // USB 연결 순서에 따라 카메라 번호가 달라지므로 자동 탐색한다.
            var cameraIndices = new List<int> { cameraIndex };
            for (int index = 0; index < 5; index++)
            {
                if (!cameraIndices.Contains(index))
                    cameraIndices.Add(index);
            }

            VideoCaptureAPIs[] backends =
            {
                VideoCaptureAPIs.DSHOW,
                VideoCaptureAPIs.MSMF,
                VideoCaptureAPIs.ANY
            };

            foreach (int index in cameraIndices)
            {
                foreach (VideoCaptureAPIs backend in backends)
                {
                    var candidate = new VideoCapture(index, backend);

                    bool frameAvailable = false;

                    if (candidate.IsOpened())
                    {
                        using (var probeFrame = new Mat())
                        {
                            // 일부 USB 카메라는 처음 몇 프레임이 비어 있을 수 있다.
                            for (int attempt = 0; attempt < 10; attempt++)
                            {
                                if (candidate.Read(probeFrame) && !probeFrame.Empty())
                                {
                                    frameAvailable = true;
                                    break;
                                }

                                Thread.Sleep(50);
                            }
                        }
                    }

                    if (frameAvailable)
                    {
                        _capture = candidate;
                        ActiveCameraIndex = index;
                        ActiveBackend = backend.ToString();
                        break;
                    }

                    candidate.Dispose();
                }

                if (_capture != null)
                    break;
            }

            if (_capture == null)
            {
                ActiveCameraIndex = -1;
                ActiveBackend = string.Empty;
                return false;
            }

            // 우선 안정적으로 1280 x 720 사용
            _capture.Set(VideoCaptureProperties.FrameWidth, 1280);
            _capture.Set(VideoCaptureProperties.FrameHeight, 720);
            _capture.Set(VideoCaptureProperties.Fps, 30);

            _cancellationTokenSource = new CancellationTokenSource();

            IsRunning = true;

            Task.Run(() => CaptureLoop(_cancellationTokenSource.Token));

            return true;
        }

        private void CaptureLoop(CancellationToken token)
        {
            using (Mat frame = new Mat())
            {
                while (!token.IsCancellationRequested)
                {
                    if (_capture == null || !_capture.IsOpened())
                        break;

                    bool success = _capture.Read(frame);

                    if (!success || frame.Empty())
                    {
                        Thread.Sleep(30);
                        continue;
                    }

                    BitmapSource bitmapSource = frame.ToBitmapSource();

                    // 다른 스레드에서도 안전하게 사용할 수 있도록 고정
                    bitmapSource.Freeze();

                    FrameReceived?.Invoke(bitmapSource);

                    Thread.Sleep(10);

                    MatFrameReceived?.Invoke(frame.Clone());
                }
            }
        }

        public void Stop()
        {
            if (!IsRunning)
                return;

            _cancellationTokenSource?.Cancel();

            _capture?.Release();
            _capture?.Dispose();

            _capture = null;

            ActiveCameraIndex = -1;
            ActiveBackend = string.Empty;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            IsRunning = false;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
