using System;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace GenericTelemetryProvider
{
    // Burnout Revenge through Xerenge (telemetry = on in its xerenge.conf), or
    // DriveClub through DriveClub Recomp, or Star Fox 64 through
    // Starfox64Recomp (both on by default): the game sends its
    // telemetry over UDP to the port set here. One window per game, each with
    // its own saved port.
    public partial class BurnoutRevengeUI : Form
    {
        BurnoutRevengeTelemetryProvider provider;

        string saveFilename = "BurnoutRevenge\\BurnoutRevengeConfig.txt";
        string gameName = "Burnout Revenge";

        public BurnoutRevengeUI() : this("Burnout Revenge", "BurnoutRevenge")
        {
        }

        // gameName as shown; folder: where its port is saved (<folder>\<folder>Config.txt).
        public BurnoutRevengeUI(string gameName, string folder)
        {
            InitializeComponent();

            this.gameName = gameName;
            saveFilename = folder + "\\" + folder + "Config.txt";
            Text = gameName + (gameName == "DriveClub" ? " (DriveClub Recomp)" : gameName == "Star Fox 64" ? " (Starfox64Recomp)" : " (Xerenge)");

            statusLabel.Text = "Click Initialize!";

            provider = new BurnoutRevengeTelemetryProvider();
            provider.gameUI = provider.ui = this;
            provider.gameName = gameName;

            LoadConfig();

            FilterModuleCustom.Instance.InitFromConfig(MainConfig.Instance.configData.filterConfig);
        }

        void LoadConfig()
        {
            try
            {
                string path = MainConfig.installPath + saveFilename;
                if (File.Exists(path))
                {
                    BurnoutRevengeConfig config = JsonConvert.DeserializeObject<BurnoutRevengeConfig>(File.ReadAllText(path));
                    if (config != null && config.port > 0 && config.port <= 65535)
                        provider.port = config.port;
                }
            }
            catch (Exception) { }

            portTextBox.Text = provider.port.ToString();
        }

        void SaveConfig()
        {
            try
            {
                BurnoutRevengeConfig save = new BurnoutRevengeConfig();
                save.port = provider.port;

                string path = MainConfig.installPath + saveFilename;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonConvert.SerializeObject(save, Formatting.Indented));
            }
            catch (Exception) { }
        }

        public void StatusTextChanged(string text)
        {
            Utils.SetTextBoxThreadSafe(statusLabel, text);
        }

        public void DebugTextChanged(string text)
        {
            Utils.SetRichTextBoxThreadSafe(matrixBox, text);
        }

        private void initializeButton_Click(object sender, EventArgs e)
        {
            int port;
            if (!int.TryParse(portTextBox.Text.Trim(), out port) || port <= 0 || port > 65535)
            {
                statusLabel.Text = "The port must be a number from 1 to 65535";
                return;
            }

            provider.port = port;
            SaveConfig();

            initializeButton.Enabled = false;
            portTextBox.Enabled = false;
            statusLabel.Text = "Waiting for " + gameName + " on UDP port " + port;

            provider.Stop();
            provider.Run();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            provider.StopAllThreads();
            provider.Stop();
            if (!IsDisposed)
            {
                Dispose();
            }

            Application.ExitThread();
        }
    }

    public class BurnoutRevengeConfig
    {
        public int port = BurnoutRevengeAPI.DefaultPort;
    }
}
