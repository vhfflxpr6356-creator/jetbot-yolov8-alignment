# WPF Smart Traffic Dashboard

AI 스마트 교통 신호 시스템의 WPF 관제 대시보드입니다.

## 현재 구현 상태

- WPF UI 배치 완료
- MVVM 기본 구조 적용
- Data Binding 적용
- Event Log용 ObservableCollection 구조 적용
- 실제 CCTV / Python / ESP32 / 네트워크 기능은 아직 연결하지 않음

## 구조

- Models/EventLogItem.cs
- ViewModels/BaseViewModel.cs
- ViewModels/MainViewModel.cs
- MainWindow.xaml
- MainWindow.xaml.cs

## 바인딩 대상

- 시스템 상태
- 현재 시간
- 카메라 연결 상태
- 영상 입력 상태
- 차량 수
- 혼잡 상태
- 현재 신호
- 긴급차량 상태
- 신호 전환 사유
- 이벤트 로그

실제 기능 연결 단계에서는 Python/ESP32 등의 실시간 데이터가 MainViewModel 속성을 갱신하도록 연결할 예정입니다.
