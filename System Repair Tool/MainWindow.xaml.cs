using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Globalization;
using System.Text;

namespace SystemRepairTool
{
    public partial class MainWindow : Window
    {
        private bool hasErrors = false;
        private DateTime startTime;
        private int totalSteps = 4;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartButton.IsEnabled = false;
            OutputBox.Clear();
            ProgressBar.Value = 0;

            hasErrors = false;
            startTime = DateTime.Now;

            Status("Выполнение...", "#00BFFF");

            await RunStep("CheckHealth", "dism /online /cleanup-image /checkhealth", 1, Encoding.GetEncoding(866));
            await RunStep("ScanHealth", "dism /online /cleanup-image /scanhealth", 2, Encoding.GetEncoding(866));
            await RunStep("RestoreHealth", "dism /online /cleanup-image /restorehealth", 3, Encoding.GetEncoding(866));
            await RunStep("SFC", "sfc /scannow", 4, Encoding.Unicode);

            var endTime = DateTime.Now;

            Log("\n===== РЕЗУЛЬТАТ =====");

            if (hasErrors)
            {
                Status("Обнаружены ошибки", "Red");
                Log("Обнаружены возможные ошибки");
            }
            else
            {
                Status("Готово без ошибок", "#00FF88");
                Log("Ошибки не обнаружены");
            }

            Log($"Время: {startTime} - {endTime}");

            StartButton.IsEnabled = true;
        }

        private async Task RunStep(string name, string command, int step, Encoding encoding)
        {
            StepText.Text = $"Этап {step}/{totalSteps}: {name}";
            Log($"\n=== {name} ===");
            Log($"Запуск: {command}");

            var psi = new ProcessStartInfo
            {
                FileName = command.StartsWith("sfc") ? "sfc.exe" : "dism.exe",
                Arguments = command.Contains(" ") ? command.Substring(command.IndexOf(' ') + 1) : "",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = encoding,
                StandardErrorEncoding = encoding
            };

            var process = new Process { StartInfo = psi };

            process.OutputDataReceived += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;

                Dispatcher.Invoke(() =>
                {
                    HandleLine(e.Data, step);
                });
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;

                Dispatcher.Invoke(() =>
                {
                    Log(e.Data);
                    hasErrors = true;
                });
            };

            if (!process.Start())
            {
                Log("Ошибка запуска процесса");
                return;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await Task.Run(() => process.WaitForExit());

            Dispatcher.Invoke(() =>
            {
                int finalProgress = (step * 100) / totalSteps;
                ProgressBar.Value = finalProgress;
            });
        }

        private void HandleLine(string line, int step)
        {
            // Проценты
            var match = Regex.Match(line, @"(\d{1,3}\.\d+)%");

            if (match.Success)
            {
                if (float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    int percent = (int)value;
                    int global = ((step - 1) * 100 + percent) / totalSteps;
                    ProgressBar.Value = Math.Min(global, 100);
                }
            }

            // Фильтр прогресс-строк DISM
            if (Regex.IsMatch(line.Trim(), @"^\[[=\s\d\.%]+\]$"))
                return;

            Log(line);

            if (Regex.IsMatch(line, "error|ошибка|failed|corrupt", RegexOptions.IgnoreCase))
                hasErrors = true;
        }

        private void Log(string text)
        {
            OutputBox.AppendText(text + Environment.NewLine);
            OutputBox.ScrollToEnd();
        }

        private void Status(string text, string color)
        {
            StatusText.Text = text;
            StatusText.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(color);
        }
    }
}