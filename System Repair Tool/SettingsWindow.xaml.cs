using System.IO;
using System.Windows;
using Microsoft.Win32;
using System_Repair_Tool.Properties;

namespace SystemRepairTool
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            IncludeOsDetailsCheck.IsChecked = Settings.Default.IncludeOsDetails;
            IncludeHardwareDetailsCheck.IsChecked = Settings.Default.IncludeHardwareDetails;
            IncludeStorageDetailsCheck.IsChecked = Settings.Default.IncludeStorageDetails;
            IncludeSecurityDetailsCheck.IsChecked = Settings.Default.IncludeSecurityDetails;
            IncludeNetworkDetailsCheck.IsChecked = Settings.Default.IncludeNetworkDetails;
            IncludeEventLogDetailsCheck.IsChecked = Settings.Default.IncludeEventLogDetails;

            IncludeCheckHealthCheck.IsChecked = Settings.Default.IncludeCheckHealth;
            IncludeScanHealthCheck.IsChecked = Settings.Default.IncludeScanHealth;
            IncludeRestoreHealthCheck.IsChecked = Settings.Default.IncludeRestoreHealth;
            IncludeSfcCheck.IsChecked = Settings.Default.IncludeSfc;
            IncludeChkdskScanCheck.IsChecked = Settings.Default.IncludeChkdskScan;
            IncludeAnalyzeComponentStoreCheck.IsChecked = Settings.Default.IncludeAnalyzeComponentStore;
            IncludeReagentInfoCheck.IsChecked = Settings.Default.IncludeReagentInfo;

            IncludeFlushDnsCheck.IsChecked = Settings.Default.IncludeFlushDns;
            IncludeWinsockResetCheck.IsChecked = Settings.Default.IncludeWinsockReset;
            IncludeIpResetCheck.IsChecked = Settings.Default.IncludeIpReset;

            IncludeStartComponentCleanupCheck.IsChecked = Settings.Default.IncludeStartComponentCleanup;
            IncludeWindowsUpdateResetCheck.IsChecked = Settings.Default.IncludeWindowsUpdateReset;

            MinerSearchPathBox.Text = Settings.Default.MinerSearchPath;
            RunMinerSearchAfterRepairCheck.IsChecked = Settings.Default.RunMinerSearchAfterRepair;
            PromptToSaveLogCheck.IsChecked = Settings.Default.PromptToSaveLog;
        }

        private void BrowseMinerSearchButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Выберите MinerSearch",
                Filter = "Executable (*.exe)|*.exe"
            };

            if (dlg.ShowDialog() == true)
                MinerSearchPathBox.Text = dlg.FileName;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var path = (MinerSearchPathBox.Text ?? string.Empty).Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(path) && !File.Exists(path))
            {
                MessageBox.Show("Указанный файл MinerSearch не найден.");
                return;
            }

            Settings.Default.IncludeOsDetails = IncludeOsDetailsCheck.IsChecked == true;
            Settings.Default.IncludeHardwareDetails = IncludeHardwareDetailsCheck.IsChecked == true;
            Settings.Default.IncludeStorageDetails = IncludeStorageDetailsCheck.IsChecked == true;
            Settings.Default.IncludeSecurityDetails = IncludeSecurityDetailsCheck.IsChecked == true;
            Settings.Default.IncludeNetworkDetails = IncludeNetworkDetailsCheck.IsChecked == true;
            Settings.Default.IncludeEventLogDetails = IncludeEventLogDetailsCheck.IsChecked == true;

            Settings.Default.IncludeCheckHealth = IncludeCheckHealthCheck.IsChecked == true;
            Settings.Default.IncludeScanHealth = IncludeScanHealthCheck.IsChecked == true;
            Settings.Default.IncludeRestoreHealth = IncludeRestoreHealthCheck.IsChecked == true;
            Settings.Default.IncludeSfc = IncludeSfcCheck.IsChecked == true;
            Settings.Default.IncludeChkdskScan = IncludeChkdskScanCheck.IsChecked == true;
            Settings.Default.IncludeAnalyzeComponentStore = IncludeAnalyzeComponentStoreCheck.IsChecked == true;
            Settings.Default.IncludeReagentInfo = IncludeReagentInfoCheck.IsChecked == true;

            Settings.Default.IncludeFlushDns = IncludeFlushDnsCheck.IsChecked == true;
            Settings.Default.IncludeWinsockReset = IncludeWinsockResetCheck.IsChecked == true;
            Settings.Default.IncludeIpReset = IncludeIpResetCheck.IsChecked == true;

            Settings.Default.IncludeStartComponentCleanup = IncludeStartComponentCleanupCheck.IsChecked == true;
            Settings.Default.IncludeWindowsUpdateReset = IncludeWindowsUpdateResetCheck.IsChecked == true;

            Settings.Default.MinerSearchPath = path;
            Settings.Default.RunMinerSearchAfterRepair = RunMinerSearchAfterRepairCheck.IsChecked == true;
            Settings.Default.PromptToSaveLog = PromptToSaveLogCheck.IsChecked == true;
            Settings.Default.Save();

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
