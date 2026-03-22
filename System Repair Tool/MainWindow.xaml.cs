using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;
using Microsoft.Win32;
using System_Repair_Tool.Properties;

namespace SystemRepairTool
{
    public partial class MainWindow : Window
    {
        private bool hasErrors;
        private DateTime startTime;
        private int totalSteps;
        private readonly List<StepExecutionResult> stepResults = new List<StepExecutionResult>();

        public MainWindow()
        {
            InitializeComponent();

            if (!IsAdmin())
            {
                MessageBox.Show("Требуются права администратора");
                RestartAsAdmin();
                return;
            }

            ApplySavedSettings();

            ComputerInfoSuiteCheck.Checked += SuiteCheckChanged;
            ComputerInfoSuiteCheck.Unchecked += SuiteCheckChanged;
            RepairSuiteCheck.Checked += SuiteCheckChanged;
            RepairSuiteCheck.Unchecked += SuiteCheckChanged;
            NetworkSuiteCheck.Checked += SuiteCheckChanged;
            NetworkSuiteCheck.Unchecked += SuiteCheckChanged;
            MaintenanceSuiteCheck.Checked += SuiteCheckChanged;
            MaintenanceSuiteCheck.Unchecked += SuiteCheckChanged;
            RunMinerSearchAfterCompletionCheck.Checked += SuiteCheckChanged;
            RunMinerSearchAfterCompletionCheck.Unchecked += SuiteCheckChanged;
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentSelectionAsDefaults();

            var steps = BuildSteps();
            if (steps.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы один крупный режим.");
                return;
            }

            StartButton.IsEnabled = false;
            OutputBox.Clear();
            ProgressBar.Value = 0;
            hasErrors = false;
            stepResults.Clear();
            startTime = DateTime.Now;
            totalSteps = steps.Count;

            Status("Выполнение...", "#4CC2FF");
            Log("===== START =====");
            Log($"Запуск: {startTime:yyyy-MM-dd HH:mm:ss}");

            if (RepairSuiteCheck.IsChecked == true)
                await EnsureTrustedInstaller();

            for (var i = 0; i < steps.Count; i++)
                await RunStepAsync(steps[i], i + 1);

            var endTime = DateTime.Now;
            Log("\n===== РЕЗУЛЬТАТ =====");
            Log(BuildResultSummary());
            Log($"Время: {startTime:yyyy-MM-dd HH:mm:ss} - {endTime:yyyy-MM-dd HH:mm:ss}");

            Status(hasErrors ? "Завершено с замечаниями" : "Готово без ошибок", hasErrors ? "#FF7A7A" : "#63E6A5");

            if (RunMinerSearchAfterCompletionCheck.IsChecked == true)
                LaunchMinerSearchInternal(true);

            if (Settings.Default.PromptToSaveLog)
            {
                var result = MessageBox.Show("Сохранить лог?", "System Repair Tool", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                    SaveLogToFile();
            }

            StartButton.IsEnabled = true;
        }

        private string BuildResultSummary()
        {
            var okCount = stepResults.Count(x => x.IsSuccess);
            var failed = stepResults.Where(x => !x.IsSuccess).ToList();

            var builder = new StringBuilder();
            builder.AppendLine($"Шагов успешно: {okCount}/{stepResults.Count}");
            builder.AppendLine($"Ошибок: {failed.Count}");

            if (failed.Count > 0)
            {
                builder.AppendLine("Требуют внимания:");
                foreach (var item in failed)
                    builder.AppendLine($"- {item.Name}: {item.Message}");
            }

            return builder.ToString().TrimEnd();
        }

        private List<RepairStep> BuildSteps()
        {
            var steps = new List<RepairStep>();

            if (ComputerInfoSuiteCheck.IsChecked == true)
                AddComputerInfoSteps(steps);

            if (RepairSuiteCheck.IsChecked == true)
                AddRepairSteps(steps);

            if (NetworkSuiteCheck.IsChecked == true)
                AddNetworkSteps(steps);

            if (MaintenanceSuiteCheck.IsChecked == true)
                AddMaintenanceSteps(steps);

            return steps;
        }

        private void AddComputerInfoSteps(List<RepairStep> steps)
        {
            AddPowerShellStepIfEnabled(steps, Settings.Default.IncludeOsDetails, "Computer Info: OS", @"
$os = Get-CimInstance Win32_OperatingSystem
$cs = Get-CimInstance Win32_ComputerSystem
$reg = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
'[OS]'
'Computer Name: ' + $env:COMPUTERNAME
'User: ' + $env:USERNAME
'Caption: ' + $os.Caption
'Version: ' + $os.Version
'Build: ' + $os.BuildNumber
'UBR: ' + $reg.UBR
'Install Date: ' + $os.InstallDate
'Last Boot: ' + $os.LastBootUpTime
'Uptime Hours: ' + [math]::Round(((Get-Date) - $os.LastBootUpTime).TotalHours, 1)
'Manufacturer: ' + $cs.Manufacturer
'Model: ' + $cs.Model
");

            AddPowerShellStepIfEnabled(steps, Settings.Default.IncludeHardwareDetails, "Computer Info: Hardware", @"
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$board = Get-CimInstance Win32_BaseBoard | Select-Object -First 1
$bios = Get-CimInstance Win32_BIOS | Select-Object -First 1
$ram = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 2)
'[HARDWARE]'
'CPU: ' + $cpu.Name
'Cores: ' + $cpu.NumberOfCores
'Logical Processors: ' + $cpu.NumberOfLogicalProcessors
'RAM (GB): ' + $ram
'Motherboard: ' + $board.Manufacturer + ' ' + $board.Product
'BIOS: ' + $bios.Manufacturer + ' ' + $bios.SMBIOSBIOSVersion
'BIOS Date: ' + $bios.ReleaseDate
'GPU:'
Get-CimInstance Win32_VideoController | ForEach-Object { '- ' + $_.Name + ' | VRAM MB: ' + [math]::Round($_.AdapterRAM / 1MB, 0) }
");

            AddPowerShellStepIfEnabled(steps, Settings.Default.IncludeStorageDetails, "Computer Info: Storage", @"
'[STORAGE]'
Get-CimInstance Win32_LogicalDisk -Filter ""DriveType=3"" | ForEach-Object {
    'Drive ' + $_.DeviceID + ' | FS: ' + $_.FileSystem + ' | Size GB: ' + [math]::Round($_.Size / 1GB, 2) + ' | Free GB: ' + [math]::Round($_.FreeSpace / 1GB, 2)
}
'Physical Disks:'
Get-PhysicalDisk -ErrorAction SilentlyContinue | ForEach-Object {
    '- ' + $_.FriendlyName + ' | Media: ' + $_.MediaType + ' | Health: ' + $_.HealthStatus + ' | Size GB: ' + [math]::Round($_.Size / 1GB, 2)
}
");

            AddPowerShellStepIfEnabled(steps, Settings.Default.IncludeSecurityDetails, "Computer Info: Security", @"
'[SECURITY]'
try { 'Secure Boot: ' + (Confirm-SecureBootUEFI) } catch { 'Secure Boot: not available' }
try { $tpm = Get-Tpm; 'TPM Present: ' + $tpm.TpmPresent; 'TPM Ready: ' + $tpm.TpmReady } catch { 'TPM: not available' }
try {
    Get-BitLockerVolume | ForEach-Object {
        'BitLocker ' + $_.MountPoint + ' | Status: ' + $_.VolumeStatus + ' | Protection: ' + $_.ProtectionStatus
    }
} catch { 'BitLocker: not available' }
try {
    $mp = Get-MpComputerStatus
    'Defender Enabled: ' + $mp.AntivirusEnabled
    'RealTime Protection: ' + $mp.RealTimeProtectionEnabled
    'Defender Version: ' + $mp.AMProductVersion
} catch { 'Defender: not available' }
");

            AddPowerShellStepIfEnabled(steps, Settings.Default.IncludeNetworkDetails, "Computer Info: Network", @"
'[NETWORK]'
Get-NetAdapter | Where-Object Status -ne 'Disabled' | ForEach-Object {
    'Adapter: ' + $_.Name + ' | Status: ' + $_.Status + ' | Link: ' + $_.LinkSpeed
}
'IPv4:'
Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -notlike '169.254*' } | ForEach-Object {
    '- ' + $_.InterfaceAlias + ': ' + $_.IPAddress
}
'DNS:'
Get-DnsClientServerAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | ForEach-Object {
    '- ' + $_.InterfaceAlias + ': ' + ($_.ServerAddresses -join ', ')
}
try { 'WinHTTP Proxy: ' + ((netsh winhttp show proxy) | Out-String).Trim() } catch { 'WinHTTP Proxy: n/a' }
");

            AddPowerShellStepIfEnabled(steps, Settings.Default.IncludeEventLogDetails, "Computer Info: Event Logs", @"
'[EVENTS]'
Get-WinEvent -FilterHashtable @{LogName='System'; Level=1,2; StartTime=(Get-Date).AddDays(-7)} -MaxEvents 200 |
ForEach-Object {
    [PSCustomObject]@{
        Key = $_.ProviderName + '|' + $_.Id + '|' + $_.Message.Split([Environment]::NewLine)[0]
        Time = $_.TimeCreated
        Provider = $_.ProviderName
        Id = $_.Id
        Level = $_.LevelDisplayName
        Message = $_.Message.Split([Environment]::NewLine)[0]
    }
} |
Group-Object Key |
Select-Object -First 15 |
ForEach-Object {
    $item = $_.Group | Sort-Object Time -Descending | Select-Object -First 1
    '[' + $item.Time + '] ' + $item.Provider + ' | ID ' + $item.Id + ' | ' + $item.Level + ' | x' + $_.Count + ' | ' + $item.Message
}
");
        }

        private void AddRepairSteps(List<RepairStep> steps)
        {
            AddStepIfEnabled(steps, Settings.Default.IncludeCheckHealth, "DISM CheckHealth", null, null, "checkhealth");
            AddStepIfEnabled(steps, Settings.Default.IncludeScanHealth, "DISM ScanHealth", null, null, "scanhealth");
            AddStepIfEnabled(steps, Settings.Default.IncludeRestoreHealth, "DISM RestoreHealth", null, null, "restorehealth");
            AddStepIfEnabled(steps, Settings.Default.IncludeSfc, "SFC", "sfc", Encoding.Unicode);
            AddPowerShellStepIfEnabled(steps, Settings.Default.IncludeChkdskScan, "CHKDSK Scan", @"
'[CHKDSK]'
try {
    Repair-Volume -DriveLetter C -Scan
    'Проверка тома C: завершена.'
} catch {
    'Не удалось выполнить Repair-Volume: ' + $_.Exception.Message
    exit 1
}
");
            AddStepIfEnabled(steps, Settings.Default.IncludeAnalyzeComponentStore, "AnalyzeComponentStore", "dism /online /cleanup-image /analyzecomponentstore", Encoding.GetEncoding(866));
            AddStepIfEnabled(steps, Settings.Default.IncludeReagentInfo, "WinRE Info", "reagentc /info", Encoding.GetEncoding(866), allowNonZeroExitCode: true);
        }

        private void AddNetworkSteps(List<RepairStep> steps)
        {
            AddStepIfEnabled(steps, Settings.Default.IncludeFlushDns, "FlushDNS", "ipconfig /flushdns", Encoding.GetEncoding(866));
            AddStepIfEnabled(steps, Settings.Default.IncludeWinsockReset, "WinsockReset", "netsh winsock reset", Encoding.GetEncoding(866));
            AddStepIfEnabled(steps, Settings.Default.IncludeIpReset, "IpReset", "netsh int ip reset", Encoding.GetEncoding(866));
        }

        private void AddMaintenanceSteps(List<RepairStep> steps)
        {
            AddStepIfEnabled(steps, Settings.Default.IncludeStartComponentCleanup, "StartComponentCleanup", "dism /online /cleanup-image /startcomponentcleanup", Encoding.GetEncoding(866));
            AddStepIfEnabled(steps, Settings.Default.IncludeWindowsUpdateReset, "WindowsUpdateReset", "\"net stop wuauserv && net stop bits && net stop cryptsvc && ren %systemroot%\\SoftwareDistribution SoftwareDistribution.old && ren %systemroot%\\System32\\catroot2 catroot2.old && net start cryptsvc && net start bits && net start wuauserv\"", Encoding.GetEncoding(866));
        }

        private static void AddStepIfEnabled(List<RepairStep> steps, bool enabled, string name, string command, Encoding encoding, string dismVerb = null, bool allowNonZeroExitCode = false, bool markErrorOutputAsFailure = true)
        {
            if (!enabled)
                return;

            steps.Add(new RepairStep
            {
                Name = name,
                Command = command,
                Encoding = encoding,
                DismVerb = dismVerb,
                AllowNonZeroExitCode = allowNonZeroExitCode,
                MarkErrorOutputAsFailure = markErrorOutputAsFailure
            });
        }

        private static void AddPowerShellStepIfEnabled(List<RepairStep> steps, bool enabled, string name, string script)
        {
            if (!enabled)
                return;

            var preparedScript =
                "[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false);" + Environment.NewLine +
                "$OutputEncoding = New-Object System.Text.UTF8Encoding($false);" + Environment.NewLine +
                "$ProgressPreference='SilentlyContinue';" + Environment.NewLine +
                "$InformationPreference='SilentlyContinue';" + Environment.NewLine +
                script;

            var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(preparedScript));

            steps.Add(new RepairStep
            {
                Name = name,
                Command = "powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -InputFormat None -OutputFormat Text -EncodedCommand " + encodedScript,
                Encoding = Encoding.UTF8,
                AllowNonZeroExitCode = true,
                MarkErrorOutputAsFailure = false
            });
        }

        private async Task RunStepAsync(RepairStep step, int stepIndex)
        {
            var result = new StepExecutionResult { Name = step.Name, IsSuccess = true, Message = "OK" };

            StepText.Text = $"Этап {stepIndex}/{totalSteps}: {step.Name}";
            Log($"\n=== {step.Name} ===");

            var psi = CreateProcessStartInfo(step);
            using (var process = new Process { StartInfo = psi })
            {
                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        Dispatcher.Invoke(() => HandleLine(e.Data, stepIndex, step, result));
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (ShouldIgnorePowerShellNoise(e.Data))
                                return;

                            Log(e.Data);
                            if (step.MarkErrorOutputAsFailure)
                            {
                                result.IsSuccess = false;
                                result.Message = e.Data.Trim();
                                hasErrors = true;
                            }
                        });
                    }
                };

                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    await Task.Run(() => process.WaitForExit());

                    if (process.ExitCode != 0 && !step.AllowNonZeroExitCode)
                    {
                        result.IsSuccess = false;
                        result.Message = "Код завершения: " + process.ExitCode;
                        hasErrors = true;
                    }
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                    hasErrors = true;
                    Log("Ошибка запуска процесса: " + ex.Message);
                }
            }

            if (step.Name == "CHKDSK Scan" && result.IsSuccess == false)
                result.Message = "Проверь запуск от администратора или состояние тома C:";

            stepResults.Add(result);
            ProgressBar.Value = (stepIndex * 100.0) / totalSteps;
        }

        private ProcessStartInfo CreateProcessStartInfo(RepairStep step)
        {
            if (step.Command == "sfc")
            {
                return new ProcessStartInfo(GetSfcPath(), "/scannow")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.Unicode,
                    StandardErrorEncoding = Encoding.Unicode
                };
            }

            if (step.DismVerb != null)
            {
                return new ProcessStartInfo("dism.exe", "/online /cleanup-image /" + step.DismVerb)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.GetEncoding(866),
                    StandardErrorEncoding = Encoding.GetEncoding(866)
                };
            }

            return new ProcessStartInfo("cmd.exe", "/c " + step.Command)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = step.Encoding ?? Encoding.GetEncoding(866),
                StandardErrorEncoding = step.Encoding ?? Encoding.GetEncoding(866)
            };
        }

        private void HandleLine(string line, int stepIndex, RepairStep step, StepExecutionResult result)
        {
            line = line.Trim();
            if (string.IsNullOrWhiteSpace(line))
                return;

            if (ShouldIgnorePowerShellNoise(line))
                return;

            if (Regex.IsMatch(line, @"^\[[=\s\d\.%]+\]$"))
                return;

            var sfc = Regex.Match(line, @"Проверка\s+(\d{1,3})%");
            if (sfc.Success)
            {
                UpdateProgress(stepIndex, int.Parse(sfc.Groups[1].Value));
                return;
            }

            var dism = Regex.Match(line, @"(\d{1,3}\.\d+)%");
            if (dism.Success)
            {
                UpdateProgress(stepIndex, (int)float.Parse(dism.Groups[1].Value, CultureInfo.InvariantCulture));
                return;
            }

            Log(line);

            if (!step.MarkErrorOutputAsFailure)
                return;

            if (Regex.IsMatch(line, "error|ошибка|failed|corrupt|отказано в доступе", RegexOptions.IgnoreCase))
            {
                result.IsSuccess = false;
                result.Message = line;
                hasErrors = true;
            }
        }

        private static bool ShouldIgnorePowerShellNoise(string line)
        {
            var trimmed = (line ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return true;
            if (trimmed.StartsWith("#< CLIXML", StringComparison.OrdinalIgnoreCase))
                return true;
            if (trimmed.StartsWith("<Objs Version=", StringComparison.OrdinalIgnoreCase))
                return true;
            if (trimmed.Contains("http://schemas.microsoft.com/powershell/2004/04"))
                return true;
            if (trimmed.Contains("<PR N=\"Record\">"))
                return true;
            if (trimmed.Contains("<TN RefId=") || trimmed.Contains("<TNRef RefId=") || trimmed.Contains("<MS><I64 N="))
                return true;
            return false;
        }

        private void UpdateProgress(int stepIndex, int stepPercent)
        {
            ProgressBar.Value = ((stepIndex - 1) * 100.0 + stepPercent) / totalSteps;
        }

        private async Task EnsureTrustedInstaller()
        {
            Log("Проверка TrustedInstaller...");
            await RunHidden("sc config TrustedInstaller start= demand");
            await RunHidden("net start TrustedInstaller");
            await Task.Delay(800);
        }

        private async Task RunHidden(string cmd)
        {
            using (var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c " + cmd)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            }))
            {
                if (process != null)
                    await Task.Run(() => process.WaitForExit());
            }
        }

        private void ApplySavedSettings()
        {
            ComputerInfoSuiteCheck.IsChecked = Settings.Default.RunComputerInfoSuite;
            RepairSuiteCheck.IsChecked = Settings.Default.RunRepairSuite;
            NetworkSuiteCheck.IsChecked = Settings.Default.RunNetworkSuite;
            MaintenanceSuiteCheck.IsChecked = Settings.Default.RunMaintenanceSuite;
            RunMinerSearchAfterCompletionCheck.IsChecked = Settings.Default.RunMinerSearchAfterRepair;
            RefreshSummary();
        }

        private void SaveCurrentSelectionAsDefaults()
        {
            Settings.Default.RunComputerInfoSuite = ComputerInfoSuiteCheck.IsChecked == true;
            Settings.Default.RunRepairSuite = RepairSuiteCheck.IsChecked == true;
            Settings.Default.RunNetworkSuite = NetworkSuiteCheck.IsChecked == true;
            Settings.Default.RunMaintenanceSuite = MaintenanceSuiteCheck.IsChecked == true;
            Settings.Default.RunMinerSearchAfterRepair = RunMinerSearchAfterCompletionCheck.IsChecked == true;
            Settings.Default.Save();
            RefreshSummary();
        }

        private void QuickPresetButton_Click(object sender, RoutedEventArgs e)
        {
            ComputerInfoSuiteCheck.IsChecked = true;
            RepairSuiteCheck.IsChecked = true;
            NetworkSuiteCheck.IsChecked = false;
            MaintenanceSuiteCheck.IsChecked = false;
            SaveCurrentSelectionAsDefaults();
        }

        private void FullPresetButton_Click(object sender, RoutedEventArgs e)
        {
            ComputerInfoSuiteCheck.IsChecked = true;
            RepairSuiteCheck.IsChecked = true;
            NetworkSuiteCheck.IsChecked = true;
            MaintenanceSuiteCheck.IsChecked = true;
            SaveCurrentSelectionAsDefaults();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentSelectionAsDefaults();
            var window = new SettingsWindow { Owner = this };
            if (window.ShowDialog() == true)
            {
                ApplySavedSettings();
                Status("Настройки сохранены", "#4CC2FF");
            }
        }

        private void RefreshSummary()
        {
            var summary = new StringBuilder();
            summary.AppendLine("Информация о компьютере:");
            summary.AppendLine(ComputerInfoSuiteCheck.IsChecked == true ? GetEnabledInfoDetailsSummary() : "раздел отключён");
            summary.AppendLine();
            summary.AppendLine("Восстановление:");
            summary.AppendLine(RepairSuiteCheck.IsChecked == true ? GetEnabledRepairDetailsSummary() : "раздел отключён");
            summary.AppendLine();
            summary.AppendLine("Сеть:");
            summary.AppendLine(NetworkSuiteCheck.IsChecked == true ? GetEnabledNetworkDetailsSummary() : "раздел отключён");
            summary.AppendLine();
            summary.AppendLine("Обслуживание:");
            summary.AppendLine(MaintenanceSuiteCheck.IsChecked == true ? GetEnabledMaintenanceDetailsSummary() : "раздел отключён");
            summary.AppendLine();
            summary.AppendLine("MinerSearch:");
            summary.AppendLine(RunMinerSearchAfterCompletionCheck.IsChecked == true ? "будет запущен после завершения" : "автозапуск выключен");
            ConfigurationSummaryText.Text = summary.ToString().Trim();
        }

        private void SuiteCheckChanged(object sender, RoutedEventArgs e)
        {
            RefreshSummary();
        }

        private static string GetEnabledInfoDetailsSummary()
        {
            var parts = new List<string>();
            if (Settings.Default.IncludeOsDetails) parts.Add("ОС");
            if (Settings.Default.IncludeHardwareDetails) parts.Add("железо");
            if (Settings.Default.IncludeStorageDetails) parts.Add("диски");
            if (Settings.Default.IncludeSecurityDetails) parts.Add("безопасность");
            if (Settings.Default.IncludeNetworkDetails) parts.Add("сеть");
            if (Settings.Default.IncludeEventLogDetails) parts.Add("журналы");
            return parts.Count == 0 ? "ничего не выбрано" : string.Join(", ", parts);
        }

        private static string GetEnabledRepairDetailsSummary()
        {
            var parts = new List<string>();
            if (Settings.Default.IncludeCheckHealth) parts.Add("CheckHealth");
            if (Settings.Default.IncludeScanHealth) parts.Add("ScanHealth");
            if (Settings.Default.IncludeRestoreHealth) parts.Add("RestoreHealth");
            if (Settings.Default.IncludeSfc) parts.Add("SFC");
            if (Settings.Default.IncludeChkdskScan) parts.Add("CHKDSK");
            if (Settings.Default.IncludeAnalyzeComponentStore) parts.Add("AnalyzeComponentStore");
            if (Settings.Default.IncludeReagentInfo) parts.Add("WinRE");
            return parts.Count == 0 ? "ничего не выбрано" : string.Join(", ", parts);
        }

        private static string GetEnabledNetworkDetailsSummary()
        {
            var parts = new List<string>();
            if (Settings.Default.IncludeFlushDns) parts.Add("FlushDNS");
            if (Settings.Default.IncludeWinsockReset) parts.Add("Winsock");
            if (Settings.Default.IncludeIpReset) parts.Add("IP reset");
            return parts.Count == 0 ? "ничего не выбрано" : string.Join(", ", parts);
        }

        private static string GetEnabledMaintenanceDetailsSummary()
        {
            var parts = new List<string>();
            if (Settings.Default.IncludeStartComponentCleanup) parts.Add("ComponentCleanup");
            if (Settings.Default.IncludeWindowsUpdateReset) parts.Add("Windows Update reset");
            return parts.Count == 0 ? "ничего не выбрано" : string.Join(", ", parts);
        }

        private void LaunchMinerSearchButton_Click(object sender, RoutedEventArgs e)
        {
            LaunchMinerSearchInternal(false);
        }

        private void LaunchMinerSearchInternal(bool logOnly)
        {
            var exePath = ResolveMinerSearchPath();
            if (string.IsNullOrWhiteSpace(exePath))
            {
                if (!logOnly)
                    MessageBox.Show("Путь к MinerSearch не настроен.");
                return;
            }

            if (!File.Exists(exePath))
            {
                if (!logOnly)
                    MessageBox.Show("Файл MinerSearch не найден.");
                Log("MinerSearch не найден: " + exePath);
                return;
            }

            var workingDirectory = Path.GetDirectoryName(exePath);
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                if (!logOnly)
                    MessageBox.Show("Не удалось определить рабочую папку MinerSearch.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = true
                });
                Log("Запущен MinerSearch: " + exePath);
            }
            catch (Exception ex)
            {
                Log("Ошибка запуска MinerSearch: " + ex.Message);
                if (!logOnly)
                    MessageBox.Show("Не удалось запустить MinerSearch:\n" + ex.Message);
            }
        }

        private static string ResolveMinerSearchPath()
        {
            if (!string.IsNullOrWhiteSpace(Settings.Default.MinerSearchPath))
                return Settings.Default.MinerSearchPath.Trim().Trim('"');

            var bundledPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "MinerSearch", "MinerSearch_v1.4.8.4.exe");
            if (File.Exists(bundledPath))
                return bundledPath;

            var defaultPath = @"D:\Программы\MinerSearch_v1.4.8.4\MinerSearch_v1.4.8.4.exe";
            return File.Exists(defaultPath) ? defaultPath : string.Empty;
        }

        private static string GetSfcPath()
        {
            var sysnativePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Sysnative\sfc.exe");
            if (File.Exists(sysnativePath))
                return sysnativePath;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "sfc.exe");
        }

        private void Log(string text)
        {
            OutputBox.AppendText(text + Environment.NewLine);
            OutputBox.ScrollToEnd();
        }

        private void Status(string text, string color)
        {
            StatusText.Text = text;
            StatusText.Foreground = (Brush)new BrushConverter().ConvertFromString(color);
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

        private static bool IsAdmin()
        {
            var id = WindowsIdentity.GetCurrent();
            var p = new WindowsPrincipal(id);
            return p.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void RestartAsAdmin()
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

        private sealed class StepExecutionResult
        {
            public string Name { get; set; }
            public bool IsSuccess { get; set; }
            public string Message { get; set; }
        }
    }
}
