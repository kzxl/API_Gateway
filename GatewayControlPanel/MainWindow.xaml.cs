using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace GatewayControlPanel
{
    public partial class MainWindow : Window
    {
        private Process gatewayProcess;
        private string currentGatewayPath;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var port = PortTextBox.Text;
                var gatewayType = GatewayTypeComboBox.SelectedIndex;

                // Validate port
                if (!int.TryParse(port, out int portNumber) || portNumber < 1024 || portNumber > 65535)
                {
                    MessageBox.Show("Please enter a valid port number (1024-65535)", "Invalid Port",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Determine gateway path
                string workingDir = "";
                string command = "";
                string args = "";

                switch (gatewayType)
                {
                    case 0: // Node.js
                        workingDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "gateway-node");
                        command = "cmd.exe";
                        args = $"/c set PORT={port} && node server-uarch.js";
                        break;

                    case 1: // Go
                        workingDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "gateway-go");
                        command = "cmd.exe";
                        args = $"/c set PORT={port} && go run main.go context.go";
                        break;

                    case 2: // .NET 8
                        workingDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "APIGateway", "APIGateway");
                        command = "cmd.exe";
                        args = $"/c set ASPNETCORE_URLS=http://0.0.0.0:{port} && dotnet run";
                        break;
                }

                workingDir = Path.GetFullPath(workingDir);

                if (!Directory.Exists(workingDir))
                {
                    MessageBox.Show($"Gateway directory not found: {workingDir}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Start gateway process
                gatewayProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = args,
                        WorkingDirectory = workingDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                gatewayProcess.OutputDataReceived += (s, ev) =>
                {
                    if (!string.IsNullOrEmpty(ev.Data))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            StatusTextBlock.Text += ev.Data + "\n";
                        });
                    }
                };

                gatewayProcess.ErrorDataReceived += (s, ev) =>
                {
                    if (!string.IsNullOrEmpty(ev.Data))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            StatusTextBlock.Text += "[ERROR] " + ev.Data + "\n";
                        });
                    }
                };

                gatewayProcess.Start();
                gatewayProcess.BeginOutputReadLine();
                gatewayProcess.BeginErrorReadLine();

                currentGatewayPath = workingDir;

                // Update UI
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                OpenAdminButton.IsEnabled = true;
                PortTextBox.IsEnabled = false;
                GatewayTypeComboBox.IsEnabled = false;
                StatusLabel.Text = "● Running";
                StatusLabel.Foreground = System.Windows.Media.Brushes.Green;

                StatusTextBlock.Text = $"Gateway started on port {port}\n";
                StatusTextBlock.Text += $"Type: {GatewayTypeComboBox.Text}\n";
                StatusTextBlock.Text += $"Working Directory: {workingDir}\n\n";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start gateway: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (gatewayProcess != null && !gatewayProcess.HasExited)
                {
                    gatewayProcess.Kill();
                    gatewayProcess.WaitForExit(5000);
                    gatewayProcess.Dispose();
                    gatewayProcess = null;
                }

                // Update UI
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                OpenAdminButton.IsEnabled = false;
                PortTextBox.IsEnabled = true;
                GatewayTypeComboBox.IsEnabled = true;
                StatusLabel.Text = "● Stopped";
                StatusLabel.Foreground = System.Windows.Media.Brushes.Red;

                StatusTextBlock.Text += "\nGateway stopped.\n";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to stop gateway: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenAdminButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var port = PortTextBox.Text;
                var url = $"http://localhost:8888"; // Admin UI port

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open Admin UI: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Stop gateway on close
            if (gatewayProcess != null && !gatewayProcess.HasExited)
            {
                var result = MessageBox.Show("Gateway is still running. Stop it before closing?",
                    "Confirm", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    gatewayProcess.Kill();
                    gatewayProcess.WaitForExit(5000);
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }

            base.OnClosing(e);
        }
    }
}
