#include <WiFi.h>
#include <WebServer.h>
#include <Adafruit_NeoPixel.h>

// =====================================================
// Wi-Fi 설정
// =====================================================

// 실제 Wi-Fi 정보로 변경
const char* WIFI_SSID = "DigiDep 2F1";
const char* WIFI_PASSWORD = "@polytech";

// WPF app.config의 Esp32BaseUrl과 동일한 IP 사용
IPAddress localIp(192, 168, 0, 162);
IPAddress gateway(192, 168, 0, 253);
IPAddress subnet(255, 255, 255, 0);
IPAddress primaryDns(8, 8, 8, 8);

// =====================================================
// NeoPixel 설정
// =====================================================

constexpr uint8_t LED_DATA_PIN = 12;
constexpr uint16_t LED_COUNT = 72;
constexpr uint8_t BRIGHTNESS = 40;

// =====================================================
// 신호등 LED 영역
// =====================================================

// 빨간불
constexpr int RED_START = 0;
constexpr int RED_END = 20;

// 노란불
constexpr int YELLOW_START = 21;
constexpr int YELLOW_END = 41;

// 좌회전 화살표
constexpr int LEFT_START = 42;
constexpr int LEFT_END = 50;

// 초록불
constexpr int GREEN_START = 51;
constexpr int GREEN_END = 71;

// =====================================================
// 객체 생성
// =====================================================

Adafruit_NeoPixel strip(
  LED_COUNT,
  LED_DATA_PIN,
  NEO_GRB + NEO_KHZ800
);

WebServer server(80);

// 현재 신호 상태
String currentSignal = "OFF";

// =====================================================
// 지정한 LED 범위 켜기
// =====================================================

void fillRange(int first, int last, uint32_t color) {

  for (int i = first; i <= last && i < LED_COUNT; ++i) {

    if (i >= 0) {
      strip.setPixelColor(i, color);
    }
  }
}

// =====================================================
// 신호등 상태 변경
// =====================================================

void applySignal(const String& state) {

  // 먼저 전체 LED OFF
  strip.clear();

  // 빨간불
  if (state == "RED") {

    fillRange(
      RED_START,
      RED_END,
      strip.Color(255, 0, 0)
    );
  }

  // 노란불
  else if (state == "YELLOW") {

    fillRange(
      YELLOW_START,
      YELLOW_END,
      strip.Color(255, 255, 0)
    );
  }

  // 초록불
  else if (state == "GREEN") {

    fillRange(
      GREEN_START,
      GREEN_END,
      strip.Color(0, 255, 0)
    );
  }

  // OFF이면 strip.clear() 상태 그대로

  strip.show();

  currentSignal = state;

  Serial.print("Signal: ");
  Serial.println(currentSignal);
}

// =====================================================
// 올바른 신호인지 검사
// =====================================================

bool isValidSignal(const String& state) {

  return state == "RED" ||
         state == "YELLOW" ||
         state == "GREEN" ||
         state == "OFF";
}

// =====================================================
// JSON 응답
// =====================================================

void sendJson(int statusCode, const String& json) {

  // 다른 프로그램에서 HTTP 요청 허용
  server.sendHeader(
    "Access-Control-Allow-Origin",
    "*"
  );

  server.send(
    statusCode,
    "application/json",
    json
  );
}

// =====================================================
// /status
// 현재 ESP32 상태 반환
// =====================================================

void handleStatus() {

  String json = "{";

  json += "\"success\":true,";
  json += "\"ip\":\"";
  json += WiFi.localIP().toString();
  json += "\",";
  json += "\"currentSignal\":\"";
  json += currentSignal;
  json += "\",";
  json += "\"rssi\":";
  json += String(WiFi.RSSI());

  json += "}";

  sendJson(200, json);
}

// =====================================================
// /signal?state=RED
// 신호등 제어
// =====================================================

void handleSignal() {

  // state 파라미터가 없으면 오류
  if (!server.hasArg("state")) {

    sendJson(
      400,
      "{\"success\":false,\"message\":\"missing state\"}"
    );

    return;
  }

  String state = server.arg("state");

  state.trim();
  state.toUpperCase();

  // 잘못된 상태값
  if (!isValidSignal(state)) {

    sendJson(
      400,
      "{\"success\":false,\"message\":\"invalid state\"}"
    );

    return;
  }

  // 신호 변경
  applySignal(state);

  String json = "{";

  json += "\"success\":true,";
  json += "\"currentSignal\":\"";
  json += currentSignal;
  json += "\"}";

  sendJson(200, json);
}

// =====================================================
// Wi-Fi 연결
// =====================================================

void connectWifi() {

  WiFi.mode(WIFI_STA);

  // 고정 IP
  WiFi.config(
    localIp,
    gateway,
    subnet,
    primaryDns
  );

  WiFi.begin(
    WIFI_SSID,
    WIFI_PASSWORD
  );

  Serial.print("Connecting to Wi-Fi");

  while (WiFi.status() != WL_CONNECTED) {

    delay(500);

    Serial.print(".");
  }

  Serial.println();

  Serial.println("Wi-Fi Connected!");

  Serial.print("IP Address: ");
  Serial.println(WiFi.localIP());

  Serial.print("RSSI: ");
  Serial.println(WiFi.RSSI());
}

// =====================================================
// SETUP
// =====================================================

void setup() {

  Serial.begin(115200);

  delay(500);

  Serial.println();
  Serial.println("==============================");
  Serial.println("ESP32 Traffic Light Starting");
  Serial.println("==============================");

  // NeoPixel 초기화
  strip.begin();

  strip.setBrightness(BRIGHTNESS);

  // 처음에는 전체 OFF
  applySignal("OFF");

  // Wi-Fi 연결
  connectWifi();

  // -----------------------------
  // HTTP API 등록
  // -----------------------------

  // 상태 확인
  server.on(
    "/status",
    HTTP_GET,
    handleStatus
  );

  // 신호 변경
  server.on(
    "/signal",
    HTTP_GET,
    handleSignal
  );

  // 존재하지 않는 주소
  server.onNotFound([]() {

    sendJson(
      404,
      "{\"success\":false,\"message\":\"not found\"}"
    );
  });

  // HTTP 서버 시작
  server.begin();

  Serial.println("HTTP Server Started");

  // 정상 부팅 확인용
  // 시작 시 빨간불
  applySignal("RED");

  Serial.println("==============================");
  Serial.println("Traffic Light Ready");
  Serial.println("==============================");
}

// =====================================================
// LOOP
// =====================================================

void loop() {

  // HTTP 요청 처리
  server.handleClient();
}
