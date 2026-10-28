using System;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace GenericTelemetryProvider
{
    // Burnout Revenge through Xerenge: the game sends its telemetry over UDP
    // (telemetry = on in its xerenge.conf), to the port set here.
    public partial class BurnoutRevengeUI : Form
    {
        BurnoutRevengeTelemetryProvider provider;

        string saveFilename = "BurnoutRevenge\\BurnoutRevengeConfig.txt";

        public BurnoutRevengeUI()
        {
            InitializeComponent();

            statusLabel.Text = "Click Initialize!";

            provider = new BurnoutRevengeTelemetryProvider();
            provider.gameUI = provider.ui = this;

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
            statusLabel.Text = "Waiting for Burnout Revenge on UDP port " + port;

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
