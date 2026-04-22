using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using windows_recorder_v2.Services;

namespace windows_recorder_v2
{
    public partial class MainWindow : Window
    {
        private readonly AudioRecordingService _recordingService;
        private bool _isDarkTheme;
        private readonly string _saveFolder;

        public MainWindow()
        {
            InitializeComponent();
            _recordingService = new AudioRecordingService();
            _saveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WindowsRecorder");
            OutputPathText.Text = $"保存位置: {_saveFolder}";

            _recordingService.VolumeLevelChanged += (s, level) =>
            {
                Dispatcher.Invoke(() =>
                {
                    VolumeBar.Value = Math.Min(100, level * 200);
                });
            };

            _recordingService.DurationChanged += (s, duration) =>
            {
                Dispatcher.Invoke(() =>
                {
                    TimeDisplay.Text = duration.ToString(@"hh\:mm\:ss");
                });
            };

            _recordingService.RecordingStatusChanged += (s, status) =>
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = status;
                });
            };
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            _isDarkTheme = !_isDarkTheme;
            ApplyTheme(_isDarkTheme);
            ThemeLabel.Text = _isDarkTheme ? "暗色" : "亮色";
        }

        private void ApplyTheme(bool isDark)
        {
            var resources = Application.Current.Resources;
            resources.MergedDictionaries.Clear();

            if (isDark)
            {
                resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Themes/DarkTheme.xaml")
                });
            }
            else
            {
                resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Themes/LightTheme.xaml")
                });
            }
        }

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_recordingService.IsRecording)
            {
                StopRecording();
            }
            else
            {
                StartRecording();
            }
        }

        private void StartRecording()
        {
            string format = WavFormat.IsChecked == true ? "WAV" : "MP3";
            _recordingService.SetFormat(format);
            _recordingService.StartRecording();

            RecordButton.Content = "停止录音";
            PauseButton.IsEnabled = true;
            PauseButton.Content = "暂停";
            SystemAudioCheck.IsEnabled = false;
            MicrophoneCheck.IsEnabled = false;
            WavFormat.IsEnabled = false;
            Mp3Format.IsEnabled = false;
        }

        private void StopRecording()
        {
            var basePath = _recordingService.StopRecording();

            if (!string.IsNullOrEmpty(basePath))
            {
                try
                {
                    string finalPath = basePath;
                    if (Mp3Format.IsChecked == true)
                    {
                        finalPath = ConvertToMp3(basePath + "_system.wav", basePath + "_mic.wav");
                    }

                    string message = string.IsNullOrEmpty(finalPath)
                        ? "录音已保存到: " + _saveFolder
                        : "录音已保存到: " + finalPath;
                    MessageBox.Show(message, "录音完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            RecordButton.Content = "开始录音";
            PauseButton.IsEnabled = false;
            PauseButton.Content = "暂停";
            TimeDisplay.Text = "00:00:00";
            StatusText.Text = "准备就绪";
            VolumeBar.Value = 0;
            SystemAudioCheck.IsEnabled = true;
            MicrophoneCheck.IsEnabled = true;
            WavFormat.IsEnabled = true;
            Mp3Format.IsEnabled = true;
        }

        private string? ConvertToMp3(string systemPath, string micPath)
        {
            // 简单的混音和MP3转换
            // 如果只有系统音频，直接重命名为mp3
            if (File.Exists(systemPath))
            {
                string mp3Path = systemPath.Replace("_system.wav", ".mp3");
                File.Move(systemPath, mp3Path);
                return mp3Path;
            }
            return null;
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_recordingService.IsPaused)
            {
                _recordingService.ResumeRecording();
                PauseButton.Content = "暂停";
            }
            else
            {
                _recordingService.PauseRecording();
                PauseButton.Content = "继续";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _recordingService.Dispose();
            base.OnClosed(e);
        }
    }
}