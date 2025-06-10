using ScottPlot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SIGGEN
{
    public partial class Form1 : Form
    {
        private TcpClient client;
        private NetworkStream stream;
        private Timer peakTimer;
        private List<double> frequencies = new List<double>();
        private List<double> peakValues = new List<double>();

        private double startFreqHz, stopFreqHz, stepFreqHz, centerFreqHz;

        public Form1()
        {
            InitializeComponent();

            cmbFrequency.Items.AddRange(new string[] { "Hz", "kHz", "MHz", "GHz" });
            cmbFrequency.SelectedIndex = 2; // Default to MHz

            peakTimer = new Timer();
            peakTimer.Interval = 1000;
            peakTimer.Tick += PeakTimer_Tick;

            formsPlot1.Plot.Axes.SetLimitsX(0, 100);
            formsPlot1.Plot.Axes.SetLimitsY(-100, 0); // dBm typically ranges negative
        }

        private double GetUnitMultiplier()
        {
            switch (cmbFrequency.SelectedItem?.ToString())
            {
                case "Hz": return 1;
                case "kHz": return 1e3;
                case "MHz": return 1e6;
                case "GHz": return 1e9;
                default: return 1e6;
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                if (client != null && client.Connected)
                {
                    peakTimer.Stop();
                    stream?.Close();
                    client?.Close();
                    client = null;

                    btnConnect.Text = "CONNECT";
                    lblStatus.Text = "Disconnected";
                    lblStatus.ForeColor = System.Drawing.Color.Red;
                }
                else
                {
                    string ip = txtIP.Text.Trim();
                    int port = 5025;

                    client = new TcpClient();
                    client.Connect(ip, port);
                    stream = client.GetStream();

                    SendScpi("*IDN?");
                    string idn = ReadScpi();
                    MessageBox.Show($"Connected to:\n{idn}");

                    btnConnect.Text = "DISCONNECT";
                    lblStatus.Text = "Connected";
                    lblStatus.ForeColor = System.Drawing.Color.Green;

                    peakTimer.Start();
                }
            }
            catch (SocketException sockEx)
            {
                MessageBox.Show($"Socket error:\n{sockEx.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed:\n{ex.Message}\n{ex.StackTrace}");
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            try
            {
                string command = txtCommand.Text.Trim();
                SendScpi(command);

                if (command.EndsWith("?"))
                {
                    string response = ReadScpi();
                    txtResponse.Text = response;
                }
                else
                {
                    txtResponse.Text = "Command sent.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SCPI command failed:\n{ex.Message}");
            }
        }

        private async void PeakTimer_Tick(object sender, EventArgs e)
        {
            peakTimer.Stop();

            if (client == null || !client.Connected)
                return;

            frequencies.Clear();
            peakValues.Clear();

            try
            {
                double multiplier = GetUnitMultiplier();

                for (double freq = startFreqHz; freq <= stopFreqHz; freq += stepFreqHz)
                {
                    SendScpi($":FREQ {freq}");
                    await Task.Delay(200);

                    SendScpi(":CALC:MARK1:MAX");
                    SendScpi(":CALC:MARK1:Y?");
                    string response = ReadScpi();

                    if (double.TryParse(response, out double peak))
                    {
                        frequencies.Add(freq / multiplier);
                        peakValues.Add(peak);
                        lblPeak.Text = $" Peak: {peak:F3} dBm";
                    }
                    else
                    {
                        lblPeak.Text = $"Invalid response at {freq / multiplier} {cmbFrequency.SelectedItem}: '{response}'";
                    }
                }

                Invoke((Action)(() =>
                {
                    PlotGraph();
                    UpdateMarkerFrequencyFromDevice(); // <-- add this
                }));

            }
            catch (Exception ex)
            {
                lblPeak.Text = $"Error: {ex.Message}";
            }
            finally
            {
                peakTimer.Start();
            }
        }
        private void UpdateMarkerFrequencyFromDevice()
        {
            try
            {
                SendScpi(":CALC:MARK1:X?");
                string response = ReadScpi();

                if (double.TryParse(response, NumberStyles.Float, CultureInfo.InvariantCulture, out double markerFreqHz))
                {
                    lblMarkFreq.Text = $"Marker Freq: {markerFreqHz / 1e9:F6} GHz";
                }
                else
                {
                    lblMarkFreq.Text = "Marker Freq: Invalid response";
                }
            }
            catch (Exception ex)
            {
                lblMarkFreq.Text = $"Marker Freq: Error";
            }
        }

        private void SendScpi(string command)
        {
            if (stream == null || !stream.CanWrite)
                throw new InvalidOperationException("Not connected.");

            byte[] data = Encoding.ASCII.GetBytes(command + "\n");
            stream.Write(data, 0, data.Length);
        }

        private string ReadScpi()
        {
            if (stream == null || !stream.CanRead)
                throw new InvalidOperationException("Not connected.");

            byte[] buffer = new byte[1024];
            var task = stream.ReadAsync(buffer, 0, buffer.Length);
            if (!task.Wait(1000)) // 1 sec timeout
                throw new TimeoutException("SCPI read timed out.");

            int bytesRead = task.Result;
            return Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
        }

        private void PlotGraph()
        {
            var plt = formsPlot1.Plot;
            plt.Clear();

            if (frequencies.Count == 0 || peakValues.Count == 0)
                return;

            plt.Add.Scatter(frequencies.ToArray(), peakValues.ToArray());
            plt.XLabel($"Frequency ({cmbFrequency.SelectedItem})");
            plt.YLabel("Peak Power (dBm)");
            plt.Axes.AutoScale();
            formsPlot1.Refresh();
        }

        private void btnClearGraph_Click(object sender, EventArgs e)
        {
            frequencies.Clear();
            peakValues.Clear();
            formsPlot1.Plot.Clear();
            formsPlot1.Refresh();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                peakTimer?.Stop();
                peakTimer?.Dispose();
                stream?.Close();
                stream?.Dispose();
                client?.Close();
                client?.Dispose();
            }
            catch { }
        }

        private void btnApply_Click_1(object sender, EventArgs e)
        {
            try
            {
                double multiplier = GetUnitMultiplier();

                if (!double.TryParse(txtStartFreq.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double start) ||
                    !double.TryParse(txtStopFreq.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double stop) ||
                    !double.TryParse(txtStepFreq.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double step) ||
                    !double.TryParse(txtCenterFreq.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double center))
                {
                    MessageBox.Show("Please enter valid numeric values.");
                    return;
                }

                startFreqHz = start * multiplier;
                stopFreqHz = stop * multiplier;
                stepFreqHz = step * multiplier;
                centerFreqHz = center * multiplier;

                if (startFreqHz >= stopFreqHz)
                {
                    MessageBox.Show("Start frequency must be less than Stop frequency.");
                    return;
                }

                if (stepFreqHz <= 0)
                {
                    MessageBox.Show("Step frequency must be positive.");
                    return;
                }
                double markerFreq = (startFreqHz + stopFreqHz) / 2;
                lblMarkFreq.Text = $"Marker Freq: {markerFreq / multiplier:F3} {cmbFrequency.SelectedItem}";
                MessageBox.Show("Frequency parameters applied successfully.", "Apply", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateMarkerFrequencyFromDevice();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying frequency settings:\n{ex.Message}");
            }
        }

        private async void btnAutoTune_Click(object sender, EventArgs e)
        {
            if (client == null || !client.Connected)
            {
                MessageBox.Show("Please connect to the device first.");
                return;
            }

            peakTimer.Stop();

            try
            {
                double multiplier = GetUnitMultiplier();

                double startFreq = double.Parse(txtStartFreq.Text, CultureInfo.InvariantCulture) * multiplier;
                double stopFreq = double.Parse(txtStopFreq.Text, CultureInfo.InvariantCulture) * multiplier;
                double stepFreq = double.Parse(txtStepFreq.Text, CultureInfo.InvariantCulture) * multiplier;

                frequencies.Clear();
                peakValues.Clear();

                double bestPeak = double.MinValue;
                double bestFreq = startFreq;

                for (double freq = startFreq; freq <= stopFreq; freq += stepFreq)
                {
                    SendScpi($":FREQ {freq}");
                    await Task.Delay(200);

                    SendScpi(":CALC:MARK1:MAX");
                    SendScpi(":CALC:MARK1:Y?");
                    string response = ReadScpi();

                    if (double.TryParse(response, out double peak))
                    {
                        frequencies.Add(freq / multiplier);
                        peakValues.Add(peak);

                        if (peak > bestPeak)
                        {
                            bestPeak = peak;
                            bestFreq = freq;
                        }

                        lblTunedFreq.Text = $"Scanning... Freq: {freq / multiplier:F3} {cmbFrequency.SelectedItem}, Peak: {peak:F3} dBm";
                        Application.DoEvents();
                    }
                    else
                    {
                        lblTunedFreq.Text = $"Invalid peak at {freq / multiplier:F3} {cmbFrequency.SelectedItem}";
                    }
                }

                SendScpi($":FREQ {bestFreq}");
                lblTunedFreq.Text = $"Auto Tune complete: Best Freq = {bestFreq / multiplier:F6} {cmbFrequency.SelectedItem}, Peak = {bestPeak:F3} dBm";
                PlotGraph();
                UpdateMarkerFrequencyFromDevice();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Auto Tune failed:\n{ex.Message}");
            }
            finally
            {
                peakTimer.Start();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Optional: Initialize things on form load
        }
    }
}
