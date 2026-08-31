#include <WiFi.h>
#include <WebServer.h>
#include <Adafruit_NeoPixel.h>
#include <string.h>

// =========================
// Wi-Fi 설정
// =========================

// Static IP. Python / Flutter에서 이 IP로 요청하면 됨.
IPAddress local_IP(192, 168, 0, 162);
IPAddress gateway(192, 168, 0, 253);
IPAddress subnet(255, 255, 255, 0);
IPAddress primaryDNS(8, 8, 8, 8);
IPAddress secondaryDNS(8, 8, 4, 4);

// Wi-Fi 이름 / 비밀번호
const char* WIFI_SSID = "DigiDep 2F1";
const char* WIFI_PASSWORD = "@polytech";

// =========================
// NeoPixel LED 설정
// =========================

// 너희 신호등은 GPIO 12번으로 확인됨
#define LED_DATA_PIN 12
#define LED_COUNT 72
#define BRIGHTNESS 40

Adafruit_NeoPixel strip(LED_COUNT, LED_DATA_PIN, NEO_GRB + NEO_KHZ800);

// LED 번호 범위
#define RED_START     0
#define RED_END       20

#define YELLOW_START  21
#define YELLOW_END    41

#define LEFT_START    42
#define LEFT_END      50

#define GREEN_START   51
#define GREEN_END     71

// =========================
// 자동 루틴 시간
// =========================
const unsigned long GREEN_TIME  = 10000;  // 초록불 10초
const unsigned long YELLOW_TIME = 2000;   // 노란불 2초
const unsigned long RED_TIME    = 10000;  // 빨간불 10초
const unsigned long LEFT_TIME   = 5000;   // 빨간불 + 좌회전 5초

// =========================
// 서버 / 상태 변수
// =========================
WebServer server(80);

String currentSignal = "OFF";
bool autoMode = false;

int autoStep = 0;
unsigned long lastAutoChangeTime = 0;

// autoStep
// 0 = GREEN
// 1 = YELLOW
// 2 = RED
// 3 = RED_LEFT
// 4 = YELLOW

// =========================
// CORS
// =========================
void addCorsHeader() {
  server.sendHeader("Access-Control-Allow-Origin", "*");
  server.sendHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
  server.sendHeader("Access-Control-Allow-Headers", "Content-Type");
}

// =========================
// LED 기본 함수
// =========================
void clearStrip() {
  strip.clear();
}

void fillRange(int startIndex, int endIndex, uint32_t color) {
  for (int i = startIndex; i <= endIndex; i++) {
    if (i >= 0 && i < LED_COUNT) {
      strip.setPixelColor(i, color);
    }
  }
}

void showOff() {
  clearStrip();
  strip.show();

  currentSignal = "OFF";
  Serial.println("Signal: OFF");
}

void showRed() {
  clearStrip();
  fillRange(RED_START, RED_END, strip.Color(255, 0, 0));
  strip.show();

  currentSignal = "RED";
  Serial.println("Signal: RED");
}

void showYellow() {
  clearStrip();
  fillRange(YELLOW_START, YELLOW_END, strip.Color(255, 150, 0));
  strip.show();

  currentSignal = "YELLOW";
  Serial.println("Signal: YELLOW");
}

void showGreen() {
  clearStrip();
  fillRange(GREEN_START, GREEN_END, strip.Color(0, 255, 0));
  strip.show();

  currentSignal = "GREEN";
  Serial.println("Signal: GREEN");
}

void showLeft() {
  clearStrip();
  fillRange(LEFT_START, LEFT_END, strip.Color(0, 255, 0));
  strip.show();

  currentSignal = "LEFT";
  Serial.println("Signal: LEFT");
}

void showRedAndLeft() {
  clearStrip();
  fillRange(RED_START, RED_END, strip.Color(255, 0, 0));
  fillRange(LEFT_START, LEFT_END, strip.Color(0, 255, 0));
  strip.show();

  currentSignal = "RED_LEFT";
  Serial.println("Signal: RED_LEFT");
}

// =========================
// 신호 상태 변경 함수
// =========================
bool setSignal(String state) {
  state.trim();
  state.toUpperCase();

  // AUTO가 아닌 수동 명령이 들어오면 자동 모드 해제
  if (state != "AUTO") {
    autoMode = false;
  }

  if (state == "RED") {
    showRed();
    return true;
  }
  else if (state == "YELLOW") {
    showYellow();
    return true;
  }
  else if (state == "GREEN") {
    showGreen();
    return true;
  }
  else if (state == "LEFT") {
    showLeft();
    return true;
  }
  else if (state == "RED_LEFT") {
    showRedAndLeft();
    return true;
  }
  else if (state == "OFF") {
    showOff();
    return true;
  }
  else if (state == "AUTO") {
    autoMode = true;
    autoStep = 0;
    lastAutoChangeTime = millis();
    showGreen();

    Serial.println("Auto mode started");
    return true;
  }
  else {
    Serial.print("Invalid state: ");
    Serial.println(state);
    return false;
  }
}

// =========================
// 자동 루틴
// =========================
void updateAutoRoutine() {
  if (!autoMode) return;

  unsigned long now = millis();
  unsigned long duration = 0;

  if (autoStep == 0) {
    duration = GREEN_TIME;
  }
  else if (autoStep == 1) {
    duration = YELLOW_TIME;
  }
  else if (autoStep == 2) {
    duration = RED_TIME;
  }
  else if (autoStep == 3) {
    duration = LEFT_TIME;
  }
  else if (autoStep == 4) {
    duration = YELLOW_TIME;
  }

  if (now - lastAutoChangeTime >= duration) {
    autoStep++;

    if (autoStep > 4) {
      autoStep = 0;
    }

    lastAutoChangeTime = now;

    if (autoStep == 0) {
      showGreen();
    }
    else if (autoStep == 1) {
      showYellow();
    }
    else if (autoStep == 2) {
      showRed();
    }
    else if (autoStep == 3) {
      showRedAndLeft();
    }
    else if (autoStep == 4) {
      showYellow();
    }
  }
}

// =========================
// HTTP 핸들러
// =========================
void handleRoot() {
  addCorsHeader();

  String msg = "";
  msg += "ESP32 Traffic Light Ready\n";
  msg += "Current Signal: " + currentSignal + "\n";
  msg += "Auto Mode: ";
  msg += autoMode ? "ON\n" : "OFF\n";
  msg += "\n";
  msg += "Use:\n";
  msg += "/signal?state=RED\n";
  msg += "/signal?state=YELLOW\n";
  msg += "/signal?state=GREEN\n";
  msg += "/signal?state=LEFT\n";
  msg += "/signal?state=RED_LEFT\n";
  msg += "/signal?state=OFF\n";
  msg += "/signal?state=AUTO\n";
  msg += "/status\n";
  msg += "/test\n";

  server.send(200, "text/plain", msg);
}

void handlePing() {
  addCorsHeader();
  server.send(200, "text/plain", "pong");
}

void handleSignal() {
  addCorsHeader();

  if (!server.hasArg("state")) {
    server.send(400, "application/json", "{\"success\":false,\"message\":\"missing state\"}");
    return;
  }

  String state = server.arg("state");
  state.trim();
  state.toUpperCase();

  Serial.print("HTTP request state: ");
  Serial.println(state);

  bool success = setSignal(state);

  if (!success) {
    String response = "{";
    response += "\"success\":false,";
    response += "\"message\":\"invalid state\",";
    response += "\"state\":\"" + state + "\"";
    response += "}";

    server.send(400, "application/json", response);
    return;
  }

  String response = "{";
  response += "\"success\":true,";
  response += "\"state\":\"" + state + "\",";
  response += "\"currentSignal\":\"" + currentSignal + "\",";
  response += "\"autoMode\":" + String(autoMode ? "true" : "false");
  response += "}";

  server.send(200, "application/json", response);
}

void handleStatus() {
  addCorsHeader();

  String response = "{";
  response += "\"success\":true,";
  response += "\"ip\":\"" + WiFi.localIP().toString() + "\",";
  response += "\"currentSignal\":\"" + currentSignal + "\",";
  response += "\"autoMode\":" + String(autoMode ? "true" : "false") + ",";
  response += "\"rssi\":" + String(WiFi.RSSI());
  response += "}";

  server.send(200, "application/json", response);
}

void runLightTest() {
  showRed();
  delay(500);

  showYellow();
  delay(500);

  showGreen();
  delay(500);

  showLeft();
  delay(500);

  showRedAndLeft();
  delay(500);

  showOff();
  delay(300);

  showRed();
}

void handleTest() {
  addCorsHeader();

  Serial.println("HTTP request: /test");
  autoMode = false;
  runLightTest();

  server.send(200, "application/json", "{\"success\":true,\"message\":\"test complete\"}");
}

void handleNotFound() {
  addCorsHeader();
  server.send(404, "application/json", "{\"success\":false,\"message\":\"not found\"}");
}

// =========================
// Wi-Fi 연결 함수
// =========================
void connectWiFi() {
  WiFi.mode(WIFI_STA);

  if (!WiFi.config(local_IP, gateway, subnet, primaryDNS, secondaryDNS)) {
    Serial.println("Static IP configuration failed");
  }

  if (strlen(WIFI_PASSWORD) == 0) {
    WiFi.begin(WIFI_SSID);
  } else {
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
  }

  Serial.print("Connecting to Wi-Fi");

  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }

  Serial.println();
  Serial.println("Wi-Fi connected");
  Serial.print("ESP32 IP address: ");
  Serial.println(WiFi.localIP());
}

// =========================
// setup / loop
// =========================
void setup() {
  Serial.begin(115200);
  delay(1000);

  // NeoPixel 시작
  strip.begin();
  strip.setBrightness(BRIGHTNESS);
  strip.show();

  showOff();

  // 부팅 테스트
  runLightTest();

  // Wi-Fi 연결
  connectWiFi();

  // HTTP 라우팅
  server.on("/", HTTP_GET, handleRoot);
  server.on("/ping", HTTP_GET, handlePing);
  server.on("/signal", HTTP_GET, handleSignal);
  server.on("/status", HTTP_GET, handleStatus);
  server.on("/test", HTTP_GET, handleTest);
  server.onNotFound(handleNotFound);

  server.begin();
  Serial.println("HTTP server started");

  // 초기 상태
  setSignal("RED");
}

void loop() {
  server.handleClient();
  updateAutoRoutine();
}