---

## 스마트 교통 신호 제어 통합 작업

이 섹션은 JetBot 교통 시연 프로젝트에서 최종적으로 통합한 CCTV 기반 교통 혼잡 감지 및 긴급차량 우선 신호 제어 기능을 정리한 내용입니다.

웹캠 영상에서 일반 JetBot과 긴급차량 JetBot을 인식하고, 방향별 ROI 영역의 혼잡도를 계산한 뒤 Wi-Fi를 통해 ESP32 신호등을 제어합니다. 또한 같은 Python 서버에서 MJPEG 영상 송출, WebSocket 이벤트 송출, Supabase 교통 상태 저장을 함께 처리하여 Flutter Web Dashboard와 연동했습니다.

### 개요

본 통합 작업은 단순 객체 인식 결과를 실제 신호 제어 흐름으로 연결하는 것을 목표로 했습니다.

Python 실행 파일 하나에서 다음 기능을 함께 처리합니다.

- 웹캠 영상 입력
- YOLOv8 / OpenVINO 기반 객체 인식
- ByteTrack 기반 차량 ID 추적
- 방향별 ROI 기반 혼잡도 계산
- 긴급차량 우선 신호 제어
- ESP32 신호등 Wi-Fi 제어
- MJPEG 영상 스트리밍
- WebSocket 이벤트 송출
- Supabase 교통 상태 저장
- Flutter Web Dashboard 연동

최종 시연은 Windows 노트북, USB 웹캠, ESP32 NeoPixel 신호등 환경에서 진행했습니다.

### 주요 기능

- 일반 JetBot 및 긴급차량 JetBot 인식
- Intel CPU 환경에서 OpenVINO 모델을 활용한 추론 속도 개선
- ByteTrack 기반 차량 ID 추적
- ROI 안 차량 수, 평균 정지시간, 속도저하 계산
- 혼잡도 점수 기반 신호 제어
- 긴급차량 감지 시 우선 신호 제어
- ESP32 신호등 HTTP 제어
- MJPEG 방식 실시간 영상 송출
- WebSocket 기반 감지 이벤트 송출
- Supabase를 통한 교통 상태 데이터 저장
- Flutter Web Dashboard 교통 상태 표시 연동

### 시스템 흐름

```text
USB 웹캠
  ↓
YOLOv8 객체 인식
  ↓
ByteTrack ID 추적
  ↓
ROI 기반 교통 상태 분석
  ├─ 차량 수 계산
  ├─ 평균 정지시간 계산
  └─ 속도저하 계산
  ↓
혼잡도 판단 / 긴급차량 우선 판단
  ↓
Python Server
  ├─ MJPEG 영상 송출
  ├─ WebSocket 이벤트 송출
  ├─ Supabase 교통 상태 저장
  └─ ESP32 신호등 제어
  ↓
Flutter Web Dashboard / ESP32 신호등
```

### 혼잡도 계산 방식

혼잡도는 ROI 안의 차량 수, 차량 ID별 평균 정지시간, 속도저하를 점수화하여 계산했습니다.

```text
혼잡도 점수 = V/C 점수 × 0.40 + 정지시간 점수 × 0.40 + 속도저하 점수 × 0.20
```

```text
V/C 점수     = ROI 안 차량 수 / ROI 최대 수용 차량 수
정지시간 점수 = 차량 ID별 평균 정지시간 / 기준 정지시간
속도저하 점수 = 자유주행속도 대비 현재 평균속도 감소율
```

기본 기준값은 다음과 같습니다.

```text
ROI 최대 수용 차량 수: 3대
기준 정지시간: 10초
자유주행속도: 80 px/s
신호 변경 기준: 혼잡도 60점 이상
안정 감지 시간: 3초
```

혼잡도 점수가 기준 이상으로 3초 이상 유지되면 다음 순서로 신호를 변경합니다.

```text
YELLOW 3초 → GREEN
```

### 긴급차량 우선 신호 제어

긴급차량은 일반 혼잡도 판단보다 우선 적용됩니다.

```text
긴급차량 confidence 기준: 0.50
안정 감지 프레임: 3프레임
최소 우선 신호 유지 시간: 5초
미감지 복귀 시간: 2초
최대 GREEN 유지 시간: 10초
```

긴급차량이 감지되면 해당 상황을 우선 처리하고, 긴급차량이 통과했거나 일정 시간 감지되지 않으면 일반 혼잡도 기반 신호 제어로 복귀합니다.

### ESP32 신호등 제어

신호등은 ESP32와 NeoPixel LED 배열을 이용해 제어했습니다.

```text
LED 데이터 핀: GPIO 12

RED    = 0 ~ 20
YELLOW = 21 ~ 41
LEFT   = 42 ~ 50
GREEN  = 51 ~ 71
```

Python 서버는 ESP32 HTTP 서버로 요청을 보내 신호 상태를 변경합니다.

```text
http://<esp32-ip>/signal?state=GREEN
```

### 영상 송출 및 대시보드 연동

```text
영상 스트림: http://localhost:8080/stream
이벤트 스트림: ws://localhost:8765
```

오버레이 제어:

```text
http://localhost:8080/overlay?enabled=0
http://localhost:8080/overlay?enabled=1
```

### Supabase 연동

방향별 교통 상태 데이터를 Supabase `traffic_status` 테이블로 전송하도록 구성했습니다.

저장 데이터는 다음과 같습니다.

```text
direction
vehicle_count
ambulance_count
jetbot_count
avg_stop_time
traffic_volume
congestion_level
signal_state
emergency
updated_at
```

`traffic_status` 테이블에서는 `direction` 값을 기준으로 기존 행을 갱신하는 upsert 방식을 사용했습니다.

예시 `.env` 파일:

```text
SUPABASE_URL=your_supabase_project_url
SUPABASE_KEY=your_supabase_key
SUPABASE_TABLE=traffic_status
```

주의: `.env` 파일에는 Supabase Key가 들어가므로 GitHub에 업로드하지 않습니다.

### 실행 방법

```powershell
cd hyojun_streamer
pip install -r requirements.txt
python detect_stream.py --camera 1 --esp32-ip 192.168.0.162
```

오버레이 없이 실행:

```powershell
python detect_stream.py --camera 1 --esp32-ip 192.168.0.162 --no-overlay
```

Supabase 저장 없이 실행:

```powershell
python detect_stream.py --camera 1 --esp32-ip 192.168.0.162 --no-supabase
```

### 관련 파일

```text
hyojun_streamer/detect_stream.py
hyojun_streamer/JetBot_Last_openvino_model/
hyojun_streamer/JetBot_Last.onnx
hyojun_streamer/classes.txt
hyojun_streamer/data.yaml
hyojun_streamer/roi_config.json
hyojun_streamer/requirements.txt
esp32_signal_light_server/esp32_signal_light_server.ino
```

### 담당 작업

- ESP32 신호등 Wi-Fi 제어 구현
- NeoPixel LED 데이터 핀 및 LED 번호 범위 확인
- 객체 인식 결과와 신호등 제어 로직 통합
- ROI 기반 혼잡도 판단 기준 구성
- 긴급차량 우선 신호 제어 로직 적용
- 웹캠 입력, YOLO 추론, ROI 분석, 영상 송출, ESP32 제어를 하나의 Python 실행 흐름으로 통합
- 대시보드 데이터 흐름에 맞춰 교통 상태 데이터 연동
- Supabase 교통 상태 저장 기능 추가
- OpenVINO 모델 적용을 통한 Intel 노트북 환경 추론 속도 개선

### 문제 해결 기록

- ESP32 신호등의 제조사 펌웨어와 회로 자료가 없어 NeoPixel 제어 방식과 LED 번호 범위를 직접 확인했습니다.
- 노트북 내장 카메라가 선택되는 문제는 `--camera` 옵션으로 USB 웹캠 인덱스를 지정하여 해결했습니다.
- CPU 환경에서 ONNX 모델 실행 시 프레임이 크게 떨어지는 문제는 OpenVINO 모델을 적용하여 개선했습니다.
- ESP32 연결 문제는 노트북과 ESP32가 같은 Wi-Fi에 있는지 확인하고 HTTP 직접 요청으로 신호등 동작을 검증했습니다.
- Supabase 중복 키 문제는 `traffic_status` 테이블에서 `direction` 기준 upsert 방식으로 기존 데이터를 갱신하도록 처리했습니다.
