using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using Microsoft.Win32;

namespace SystemRepairTool
{
    public partial class MainWindow : Window
    {
        private bool hasErrors = false;
        private DateTime startTime;
        private int totalSteps = 0;
        private int lastPercent = -1;

        public MainWindow()
        {
            InitializeComponent();

            if (!IsAdmin())
            {
                MessageBox.Show("Требуются права администратора");
                RestartAsAdmin();
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartButton.IsEnabled = false;
            OutputBox.Clear();
            ProgressBar.Value = 0;

            hasErrors = false;
            lastPercent = -1;
            startTime = DateTime.Now;

            // подтверждение сети
            if (NetworkCheck.IsChecked == true)
            {
                var confirm = MessageBox.Show(
                    "Сброс сети может повлиять на VPN и настройки.\nПродолжить?",
                    "Внимание",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                    NetworkCheck.IsChecked = false;
            }

            Status("Выполнение...", "#00BFFF");

            await EnsureTrustedInstaller();

            var steps = BuildSteps();
            totalSteps = steps.Count;

            for (int i = 0; i < steps.Count; i++)
            {
                await RunStepFlexible(steps[i], i + 1);
            }

            var endTime = DateTime.Now;

            Log("\n===== РЕЗУЛЬТАТ =====");

            if (hasErrors)
                Status("Обнаружены ошибки", "Red");
            else
                Status("Готово без ошибок", "#00FF88");

            Log($"Время: {startTime} - {endTime}");

            var result = MessageBox.Show("Сохранить лог?", "System Repair Tool",
                MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
                SaveLogToFile();

            StartButton.IsEnabled = true;
        }

        private List<RepairStep> BuildSteps()
        {
            var steps = new List<RepairStep>
            {
                new RepairStep { Name="SystemInfo", Command="systeminfo", Encoding=Encoding.GetEncoding(866) },
                new RepairStep { Name="CheckHealth" },
                new RepairStep { Name="ScanHealth" },
                new RepairStep { Name="RestoreHealth" },
                new RepairStep { Name="SFC", Command="sfc", Encoding=Encoding.Unicode },
                new RepairStep { Name="Cleanup", Command="dism /online /cleanup-image /startcomponentcleanup", Encoding=Encoding.GetEncoding(866) }
            };

            if (NetworkCheck.IsChecked == true)
            {
                steps.Add(new RepairStep { Name = "FlushDNS", Command = "ipconfig /flushdns" });
                steps.Add(new RepairStep { Name = "WinsockReset", Command = "netsh winsock reset" });
            }

            return steps;
        }

        private async Task RunStepFlexible(RepairStep step, int stepIndex)
        {
            StepText.Text = $"Этап {stepIndex}/{totalSteps}: {step.Name}";
            Log($"\n=== {step.Name} ===");

            lastPercent = -1;

            ProcessStartInfo psi;

            if (step.Command == "sfc")
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    @"Sysnative\sfc.exe");

                psi = new ProcessStartInfo(path, "/scannow")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.Unicode,
                    StandardErrorEncoding = Encoding.Unicode
                };
            }
            else if (step.Command == null)
            {
                psi = new ProcessStartInfo("dism.exe",
                    "/online /cleanup-image /" + step.Name.ToLower())
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.GetEncoding(866),
                    StandardErrorEncoding = Encoding.GetEncoding(866)
                };
            }
            else
            {
                psi = new ProcessStartInfo("cmd.exe", "/c " + step.Command)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = step.Encoding ?? Encoding.GetEncoding(866),
                    StandardErrorEncoding = step.Encoding ?? Encoding.GetEncoding(866)
                };
            }

            var process = new Process { StartInfo = psi };

            process.OutputDataReceived += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;
                Dispatcher.Invoke(() => HandleLine(e.Data, stepIndex));
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

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await Task.Run(() => process.WaitForExit());

            ProgressBar.Value = (stepIndex * 100) / totalSteps;
        }

        private void HandleLine(string line, int step)
        {
            line = line.Trim();

            if (Regex.IsMatch(line, @"^\[[=\s\d\.%]+\]$"))
                return;

            var sfc = Regex.Match(line, @"Проверка\s+(\d{1,3})%");
            if (sfc.Success)
            {
                int p = int.Parse(sfc.Groups[1].Value);
                ProgressBar.Value = ((step - 1) * 100 + p) / totalSteps;
                return;
            }

            var dism = Regex.Match(line, @"(\d{1,3}\.\d+)%");
            if (dism.Success)
            {
                int p = (int)float.Parse(dism.Groups[1].Value, CultureInfo.InvariantCulture);
                ProgressBar.Value = ((step - 1) * 100 + p) / totalSteps;
                return;
            }

            Log(line);

            if (Regex.IsMatch(line, "error|ошибка|failed|corrupt", RegexOptions.IgnoreCase))
                hasErrors = true;
        }

        private async Task EnsureTrustedInstaller()
        {
            Log("Настройка и запуск TrustedInstaller...");

            await RunHidden("sc config TrustedInstaller start= demand");
            await RunHidden("sc config TrustedInstaller start= auto");
            await RunHidden("net stop TrustedInstaller >nul 2>&1");
            await RunHidden("net start TrustedInstaller");

            await Task.Delay(1500);
        }

        private async Task RunHidden(string cmd)
        {
            var psi = new ProcessStartInfo("cmd.exe", "/c " + cmd)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = Process.Start(psi);
            await Task.Run(() => process.WaitForExit());
        }

        private void Log(string text)
        {
            OutputBox.AppendText(text + Environment.NewLine);
            OutputBox.ScrollToEnd();
        }

        private void Status(string text, string color)
        {
            StatusText.Text = text;
            StatusText.Foreground =
                (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(color);
        }

        private void SaveLogToFile()
        {
            var dlg = new SaveFileDialog
            {
                FileName = $"SystemRepair_{DateTime.Now:yyyy-MM-dd_HH-mm}.txt",
                Filter = "Text (*.txt)|*.txt"
            };

            if (dlg.ShowDialog() == true)
                File.WriteAllText(dlg.FileName, OutputBox.Text, Encoding.UTF8);
        }

        private bool IsAdmin()
        {
            var id = WindowsIdentity.GetCurrent();
            var p = new WindowsPrincipal(id);
            return p.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void RestartAsAdmin()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                UseShellExecute = true,
                Verb = "runas"
            });

            Application.Current.Shutdown();
        }

        private void Link_Click(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
    }

    class RepairStep
    {
        public string Name { get; set; }
        public string Command { get; set; }
        public Encoding Encoding { get; set; }
    }
}