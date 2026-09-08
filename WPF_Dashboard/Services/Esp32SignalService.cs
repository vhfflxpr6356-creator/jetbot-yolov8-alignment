using SmartTrafficDashboard.Models;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SmartTrafficDashboard.Services
{
    public sealed class Esp32CommandResult
    {
        public bool Success { get; private set; }
        public bool WasSent { get; private set; }
        public string Message { get; private set; }

        public static Esp32CommandResult Ok(string message, bool wasSent = true)
        {
            return new Esp32CommandResult { Success = true, WasSent = wasSent, Message = message };
        }

        public static Esp32CommandResult Fail(string message)
        {
            return new Esp32CommandResult { Success = false, WasSent = false, Message = message };
        }
    }

    public sealed class Esp32SignalService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _requestLock = new SemaphoreSlim(1, 1);
        private SignalState? _lastSignal;
        private bool _disposed;

        public Esp32SignalService(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("ESP32 주소가 비어 있습니다.", nameof(baseUrl));

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(3)
            };
        }

        public async Task<Esp32CommandResult> CheckConnectionAsync()
        {
            return await SendRequestAsync("status").ConfigureAwait(false);
        }

        public async Task<Esp32CommandResult> SendSignalAsync(SignalState state)
        {
            await _requestLock.WaitAsync().ConfigureAwait(false);

            try
            {
                if (_lastSignal == state)
                    return Esp32CommandResult.Ok("이미 같은 신호 상태입니다.", false);

                string command = ToCommand(state);
                Esp32CommandResult result = await SendRequestAsync(
                    "signal?state=" + Uri.EscapeDataString(command)).ConfigureAwait(false);

                if (result.Success)
                    _lastSignal = state;

                return result;
            }
            finally
            {
                _requestLock.Release();
            }
        }

        private async Task<Esp32CommandResult> SendRequestAsync(string relativeUrl)
        {
            if (_disposed)
                return Esp32CommandResult.Fail("ESP32 서비스가 종료되었습니다.");

            try
            {
                using (HttpResponseMessage response = await _httpClient.GetAsync(relativeUrl).ConfigureAwait(false))
                {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                        return Esp32CommandResult.Fail(
                            "ESP32 오류 " + (int)response.StatusCode + ": " + body);

                    return Esp32CommandResult.Ok(body);
                }
            }
            catch (TaskCanceledException)
            {
                return Esp32CommandResult.Fail("ESP32 응답 시간이 초과되었습니다.");
            }
            catch (HttpRequestException ex)
            {
                return Esp32CommandResult.Fail("ESP32 연결 실패: " + ex.Message);
            }
            catch (Exception ex)
            {
                return Esp32CommandResult.Fail("ESP32 통신 오류: " + ex.Message);
            }
        }

        private static string ToCommand(SignalState state)
        {
            switch (state)
            {
                case SignalState.Red:
                    return "RED";
                case SignalState.Yellow:
                    return "YELLOW";
                case SignalState.Green:
                    return "GREEN";
                case SignalState.Off:
                    return "OFF";
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _httpClient.Dispose();
            _requestLock.Dispose();
        }
    }
}
