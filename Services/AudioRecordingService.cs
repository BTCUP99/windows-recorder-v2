using System;
using System.IO;
using NAudio.Wave;

namespace windows_recorder_v2.Services
{
    public class AudioRecordingService : IDisposable
    {
        private WasapiLoopbackCapture? _systemAudioCapture;
        private WaveInEvent? _microphoneCapture;
        private WaveFileWriter? _systemWriter;
        private WaveFileWriter? _microphoneWriter;
        private readonly string _outputFolder;
        private bool _isPaused;
        private string _currentBasePath = string.Empty;
        private DateTime _recordingStartTime;
        private TimeSpan _pausedDuration;
        private DateTime _pauseStartTime;
        private bool _isRecording;
        private string _format = "WAV";

        public event EventHandler<string>? RecordingStatusChanged;
        public event EventHandler<float>? VolumeLevelChanged;
        public event EventHandler<TimeSpan>? DurationChanged;

        public bool IsRecording => _isRecording;
        public bool IsPaused => _isPaused;
        public TimeSpan CurrentDuration => _isPaused
            ? _pauseStartTime - _recordingStartTime - _pausedDuration
            : (DateTime.Now - _recordingStartTime - _pausedDuration);

        public AudioRecordingService()
        {
            _outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WindowsRecorder");
            Directory.CreateDirectory(_outputFolder);
        }

        public void SetFormat(string format)
        {
            _format = format;
        }

        public void StartRecording()
        {
            if (_isRecording) return;

            _recordingStartTime = DateTime.Now;
            _pausedDuration = TimeSpan.Zero;
            _isPaused = false;
            _isRecording = true;

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _currentBasePath = Path.Combine(_outputFolder, $"recording_{timestamp}");

            StartSystemAudioCapture();
            StartMicrophoneCapture();

            RecordingStatusChanged?.Invoke(this, "正在录音");
            StartDurationTimer();
        }

        public void PauseRecording()
        {
            if (!_isRecording || _isPaused) return;

            _isPaused = true;
            _pauseStartTime = DateTime.Now;
            RecordingStatusChanged?.Invoke(this, "已暂停");
        }

        public void ResumeRecording()
        {
            if (!_isRecording || !_isPaused) return;

            _pausedDuration += DateTime.Now - _pauseStartTime;
            _isPaused = false;
            RecordingStatusChanged?.Invoke(this, "正在录音");
        }

        public string? StopRecording()
        {
            if (!_isRecording) return null;

            StopSystemAudioCapture();
            StopMicrophoneCapture();

            _isRecording = false;
            _isPaused = false;

            string? finalPath = _currentBasePath;
            RecordingStatusChanged?.Invoke(this, "录音已停止");

            return finalPath;
        }

        private void StartSystemAudioCapture()
        {
            try
            {
                _systemAudioCapture = new WasapiLoopbackCapture();
                _systemWriter = new WaveFileWriter(_currentBasePath + "_system.wav", _systemAudioCapture.WaveFormat);

                _systemAudioCapture.DataAvailable += (s, e) =>
                {
                    if (_systemWriter != null && !_isPaused)
                    {
                        _systemWriter.Write(e.Buffer, 0, e.BytesRecorded);
                        float max = 0;
                        for (int i = 0; i < e.BytesRecorded; i += 4)
                        {
                            float sample = Math.Abs(BitConverter.ToSingle(e.Buffer, i));
                            if (sample > max) max = sample;
                        }
                        VolumeLevelChanged?.Invoke(this, max);
                    }
                };

                _systemAudioCapture.RecordingStopped += (s, e) =>
                {
                    _systemWriter?.Dispose();
                    _systemWriter = null;
                };

                _systemAudioCapture.StartRecording();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"系统音频捕获失败: {ex.Message}");
            }
        }

        private void StartMicrophoneCapture()
        {
            try
            {
                int deviceIndex = GetMicrophoneDeviceIndex();
                if (deviceIndex < 0) return;

                _microphoneCapture = new WaveInEvent
                {
                    DeviceNumber = deviceIndex,
                    WaveFormat = new WaveFormat(44100, 16, 1)
                };

                _microphoneWriter = new WaveFileWriter(_currentBasePath + "_mic.wav", _microphoneCapture.WaveFormat);

                _microphoneCapture.DataAvailable += (s, e) =>
                {
                    if (_microphoneWriter != null && !_isPaused)
                    {
                        _microphoneWriter.Write(e.Buffer, 0, e.BytesRecorded);
                    }
                };

                _microphoneCapture.RecordingStopped += (s, e) =>
                {
                    _microphoneWriter?.Dispose();
                    _microphoneWriter = null;
                };

                _microphoneCapture.StartRecording();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"麦克风捕获失败: {ex.Message}");
            }
        }

        private int GetMicrophoneDeviceIndex()
        {
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var caps = WaveInEvent.GetCapabilities(i);
                if (caps.ProductName.ToLower().Contains("microphone") ||
                    caps.ProductName.ToLower().Contains("mic") ||
                    caps.ProductName.ToLower().Contains("输入"))
                {
                    return i;
                }
            }
            return WaveInEvent.DeviceCount > 0 ? 0 : -1;
        }

        private void StopSystemAudioCapture()
        {
            try
            {
                _systemAudioCapture?.StopRecording();
            }
            catch { }
            _systemAudioCapture?.Dispose();
            _systemAudioCapture = null;
            _systemWriter?.Dispose();
            _systemWriter = null;
        }

        private void StopMicrophoneCapture()
        {
            try
            {
                _microphoneCapture?.StopRecording();
            }
            catch { }
            _microphoneCapture?.Dispose();
            _microphoneCapture = null;
            _microphoneWriter?.Dispose();
            _microphoneWriter = null;
        }

        private System.Windows.Threading.DispatcherTimer? _durationTimer;

        private void StartDurationTimer()
        {
            _durationTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _durationTimer.Tick += (s, e) =>
            {
                if (_isRecording && !_isPaused)
                {
                    DurationChanged?.Invoke(this, CurrentDuration);
                }
            };
            _durationTimer.Start();
        }

        public void Dispose()
        {
            _durationTimer?.Stop();
            StopSystemAudioCapture();
            StopMicrophoneCapture();
        }
    }
}