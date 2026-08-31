using System.Collections.ObjectModel;
using SmartTrafficDashboard.Models;

namespace SmartTrafficDashboard.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private string _systemStatus = "SYSTEM STANDBY";
        private string _currentTime = "--:--:--";
        private string _cameraStatus = "DISCONNECTED";
        private string _videoInputStatus = "대기";
        private string _vehicleCount = "--";
        private string _trafficStatus = "대기";
        private string _signalStatus = "대기";
        private string _emergencyStatus = "미감지";
        private string _signalChangeReason = "대기";

        public string SystemStatus { get => _systemStatus; set => SetProperty(ref _systemStatus, value); }
        public string CurrentTime { get => _currentTime; set => SetProperty(ref _currentTime, value); }
        public string CameraStatus { get => _cameraStatus; set => SetProperty(ref _cameraStatus, value); }
        public string VideoInputStatus { get => _videoInputStatus; set => SetProperty(ref _videoInputStatus, value); }
        public string VehicleCount { get => _vehicleCount; set => SetProperty(ref _vehicleCount, value); }
        public string TrafficStatus { get => _trafficStatus; set => SetProperty(ref _trafficStatus, value); }
        public string SignalStatus { get => _signalStatus; set => SetProperty(ref _signalStatus, value); }
        public string EmergencyStatus { get => _emergencyStatus; set => SetProperty(ref _emergencyStatus, value); }
        public string SignalChangeReason { get => _signalChangeReason; set => SetProperty(ref _signalChangeReason, value); }

        public ObservableCollection<EventLogItem> EventLogs { get; } =
            new ObservableCollection<EventLogItem>();
    }
}
