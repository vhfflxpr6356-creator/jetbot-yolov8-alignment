using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
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

        public bool Start(int cameraIndex = 0)
        {
            if (IsRunning)
                return true;

            _capture = new VideoCapture(cameraIndex);

            if (!_capture.IsOpened())
            {
                _capture.Dispose();
                _capture = null;
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