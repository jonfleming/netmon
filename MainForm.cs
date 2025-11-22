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
        private Label statusLabel = null!;
        private LightIndicator statusLight = null!;
        private CheckBox alwaysOnTopCheck = null!;

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
            this.Text = "NetMon";
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.ClientSize = new Size(240, 90);
            this.TopMost = false;

            // Circular status light (green = connected, red = disconnected)
            statusLight = new LightIndicator()
            {
                Location = new Point(12, 12),
                Size = new Size(24, 24),
            };
            this.Controls.Add(statusLight);

            statusLabel = new Label()
            {
                Text = "Status: Unknown",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                AutoSize = false,
                Size = new Size(160, 48),
                Location = new Point(72, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                ForeColor = Color.Gray
            };
            this.Controls.Add(statusLabel);

            toggleButton = new Button()
            {
                Text = "Start",
                Location = new Point(12, 50),
                Size = new Size(90, 28),
            };
            toggleButton.Click += ToggleButton_Click;
            this.Controls.Add(toggleButton);

            alwaysOnTopCheck = new CheckBox()
            {
                Text = "Always on Top",
                Location = new Point(112, 54),
                AutoSize = true,
            };
            // When checked, set TopMost and make the window semi-transparent. When unchecked, restore opacity.
            alwaysOnTopCheck.CheckedChanged += (s, e) =>
            {
                this.TopMost = alwaysOnTopCheck.Checked;
                this.Opacity = this.TopMost ? 0.85 : 1.0;
            };
            this.Controls.Add(alwaysOnTopCheck);

             this.FormClosing += MainForm_FormClosing;
        }

        private void ToggleButton_Click(object? sender, EventArgs? e)
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

        private async Task StartMonitoring(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    bool connected = await IsInternetAvailableAsync();
                    UpdateStatus(connected);
                    if (!connected) StartAlarm(); else StopAlarm();

                    // Hard-coded interval: 5 seconds
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
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
            statusLight.On = connected;
            
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

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs? e)
        {
            _cts?.Cancel();
            StopAlarm();
            _httpClient?.Dispose();
        }

        // Simple circular indicator control used to show connection status
        private class LightIndicator : Control
        {
            private bool _on = false;
            public bool On
            {
                get => _on;
                set { _on = value; Invalidate(); }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                Color c = _on ? Color.LimeGreen : Color.DarkRed;
                using (var b = new SolidBrush(c))
                {
                    e.Graphics.FillEllipse(b, 0, 0, this.Width - 1, this.Height - 1);
                }
                using (var p = new Pen(Color.Black, 1))
                {
                    e.Graphics.DrawEllipse(p, 0, 0, this.Width - 1, this.Height - 1);
                }
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                Invalidate();
            }
        }
    }
}
