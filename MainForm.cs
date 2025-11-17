using System;
using System.Drawing;
using System.IO;
using System.Media;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetMon
{
    public class MainForm : Form
    {
        private Button toggleButton = null!;
        private Button checkNowButton = null!;
        private Label statusLabel = null!;
        private Label lastCheckedLabel = null!;
        private NumericUpDown intervalNumeric = null!;

        private CancellationTokenSource? _cts;
        private HttpClient _httpClient;

        private SoundPlayer? _player;
        private MemoryStream? _alarmStream;
        private bool _isAlarmPlaying = false;
        private bool _isMonitoring = false;

        public MainForm()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(6);

            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Text = "NetMon - Internet Monitor";
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.ClientSize = new Size(380, 170);

            statusLabel = new Label()
            {
                Text = "Status: Unknown",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                AutoSize = false,
                Size = new Size(340, 48),
                Location = new Point(20, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                ForeColor = Color.Gray
            };
            this.Controls.Add(statusLabel);

            toggleButton = new Button()
            {
                Text = "Start",
                Location = new Point(20, 80),
                Size = new Size(120, 40),
            };
            toggleButton.Click += ToggleButton_Click;
            this.Controls.Add(toggleButton);

            checkNowButton = new Button()
            {
                Text = "Check Now",
                Location = new Point(150, 80),
                Size = new Size(100, 40),
            };
            checkNowButton.Click += CheckNowButton_Click;
            this.Controls.Add(checkNowButton);

            var intervalLabel = new Label()
            {
                Text = "Interval (s):",
                Location = new Point(20, 130),
                Size = new Size(80, 20),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            this.Controls.Add(intervalLabel);

            intervalNumeric = new NumericUpDown()
            {
                Minimum = 1,
                Maximum = 3600,
                Value = 5,
                Location = new Point(110, 128),
                Size = new Size(60, 24),
            };
            this.Controls.Add(intervalNumeric);

            lastCheckedLabel = new Label()
            {
                Text = "Last check: never",
                Location = new Point(180, 128),
                Size = new Size(180, 24),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            this.Controls.Add(lastCheckedLabel);

            this.FormClosing += MainForm_FormClosing;
        }

        private async void ToggleButton_Click(object sender, EventArgs e)
        {
            if (!_isMonitoring)
            {
                // Start monitoring
                _cts = new CancellationTokenSource();
                _isMonitoring = true;
                toggleButton.Text = "Stop";
                _ = StartMonitoring(_cts.Token);
            }
            else
            {
                // Stop monitoring
                _cts?.Cancel();
                _isMonitoring = false;
                toggleButton.Text = "Start";
                StopAlarm();
                UpdateStatusUnknown();
            }
        }

        private async void CheckNowButton_Click(object sender, EventArgs e)
        {
            bool connected = await IsInternetAvailableAsync();
            UpdateStatus(connected);
            if (!connected) StartAlarm(); else StopAlarm();
        }

        private async Task StartMonitoring(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    bool connected = await IsInternetAvailableAsync();
                    UpdateStatus(connected);
                    if (!connected) StartAlarm(); else StopAlarm();

                    int intervalSeconds = (int)intervalNumeric.Value;
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), token);
                }
            }
            catch (TaskCanceledException)
            {
                // Expected when cancellation requested; ignore.
            }
            catch (Exception ex)
            {
                // Log or show a brief message
                MessageBox.Show(this, "Monitoring stopped due to an error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isMonitoring = false;
                this.Invoke((Action)(() => toggleButton.Text = "Start"));
            }
        }

        private async Task<bool> IsInternetAvailableAsync()
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://clients3.google.com/generate_204");
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent;
            }
            catch
            {
                return false;
            }
        }

        private void UpdateStatus(bool connected)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateStatus(connected)));
                return;
            }
            statusLabel.Text = connected ? "Status: Connected" : "Status: Disconnected";
            statusLabel.ForeColor = connected ? Color.Green : Color.Red;
            lastCheckedLabel.Text = "Last check: " + DateTime.Now.ToString("HH:mm:ss");
        }

        private void UpdateStatusUnknown()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(UpdateStatusUnknown));
                return;
            }
            statusLabel.Text = "Status: Unknown";
            statusLabel.ForeColor = Color.Gray;
            lastCheckedLabel.Text = "Last check: never";
        }

        private void StartAlarm()
        {
            if (_isAlarmPlaying) return;
            try
            {
                _alarmStream = GenerateSineWaveWav(800, 1000); // 800Hz, 1s
                _player = new SoundPlayer(_alarmStream);
                _player.PlayLooping();
                _isAlarmPlaying = true;
            }
            catch (Exception ex)
            {
                // If we can't play sound, show a message once and stop.
                MessageBox.Show(this, "Could not play alarm sound: " + ex.Message, "Sound Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _isAlarmPlaying = false;
            }
        }

        private void StopAlarm()
        {
            if (!_isAlarmPlaying) return;
            try
            {
                _player?.Stop();
                _player?.Dispose();
                _player = null;
                _alarmStream?.Dispose();
                _alarmStream = null;
                _isAlarmPlaying = false;
            }
            catch
            {
                // ignore
            }
        }

        private MemoryStream GenerateSineWaveWav(double frequencyHz = 880.0, int durationMs = 500, int sampleRate = 44100, short amplitude = 10000)
        {
            int channels = 1;
            short bitsPerSample = 16;
            int bytesPerSample = bitsPerSample / 8;
            int samples = (int)((long)sampleRate * durationMs / 1000);
            int dataSize = samples * channels * bytesPerSample;

            MemoryStream ms = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(ms);

            // RIFF header
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            // fmt subchunk
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); // Subchunk1Size for PCM
            
            writer.Write((short)1); // AudioFormat = PCM
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * bytesPerSample); // ByteRate
            writer.Write((short)(channels * bytesPerSample)); // BlockAlign
            writer.Write(bitsPerSample);

            // data subchunk
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            double theta = 2.0 * Math.PI * frequencyHz / sampleRate;
            for (int n = 0; n < samples; n++)
            {
                short sample = (short)(amplitude * Math.Sin(theta * n));
                writer.Write(sample);
            }

            writer.Flush();
            ms.Position = 0;
            return ms;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _cts?.Cancel();
            StopAlarm();
            _httpClient?.Dispose();
        }
    }
}
