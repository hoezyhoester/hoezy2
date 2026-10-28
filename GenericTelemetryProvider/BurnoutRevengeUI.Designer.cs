namespace GenericTelemetryProvider
{
    partial class BurnoutRevengeUI
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.statusLabel = new System.Windows.Forms.TextBox();
            this.matrixBox = new System.Windows.Forms.RichTextBox();
            this.initializeButton = new System.Windows.Forms.Button();
            this.portLabel = new System.Windows.Forms.Label();
            this.portTextBox = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            //
            // statusLabel
            //
            this.statusLabel.BackColor = System.Drawing.SystemColors.Control;
            this.statusLabel.Location = new System.Drawing.Point(12, 13);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.ReadOnly = true;
            this.statusLabel.Size = new System.Drawing.Size(459, 20);
            this.statusLabel.TabIndex = 5;
            this.statusLabel.Text = "Click Initialize!";
            this.statusLabel.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            //
            // portLabel
            //
            this.portLabel.AutoSize = true;
            this.portLabel.Location = new System.Drawing.Point(12, 46);
            this.portLabel.Name = "portLabel";
            this.portLabel.Size = new System.Drawing.Size(51, 13);
            this.portLabel.TabIndex = 10;
            this.portLabel.Text = "UDP port";
            //
            // portTextBox
            //
            this.portTextBox.Location = new System.Drawing.Point(12, 62);
            this.portTextBox.Name = "portTextBox";
            this.portTextBox.Size = new System.Drawing.Size(100, 20);
            this.portTextBox.TabIndex = 11;
            this.portTextBox.Text = "20790";
            //
            // matrixBox
            //
            this.matrixBox.Location = new System.Drawing.Point(12, 98);
            this.matrixBox.Name = "matrixBox";
            this.matrixBox.ReadOnly = true;
            this.matrixBox.Size = new System.Drawing.Size(459, 779);
            this.matrixBox.TabIndex = 8;
            this.matrixBox.Text = "";
            //
            // initializeButton
            //
            this.initializeButton.Location = new System.Drawing.Point(232, 39);
            this.initializeButton.Name = "initializeButton";
            this.initializeButton.Size = new System.Drawing.Size(239, 53);
            this.initializeButton.TabIndex = 9;
            this.initializeButton.Text = "Initialize!";
            this.initializeButton.UseVisualStyleBackColor = true;
            this.initializeButton.Click += new System.EventHandler(this.initializeButton_Click);
            //
            // BurnoutRevengeUI
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(483, 889);
            this.Controls.Add(this.portTextBox);
            this.Controls.Add(this.portLabel);
            this.Controls.Add(this.initializeButton);
            this.Controls.Add(this.matrixBox);
            this.Controls.Add(this.statusLabel);
            this.Icon = GenericTelemetryProvider.Properties.Resources.GTPIcon;
            this.Name = "BurnoutRevengeUI";
            this.Text = "Burnout Revenge (Xerenge)";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.OnFormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.TextBox statusLabel;
        private System.Windows.Forms.RichTextBox matrixBox;
        private System.Windows.Forms.Button initializeButton;
        private System.Windows.Forms.Label portLabel;
        private System.Windows.Forms.TextBox portTextBox;
    }
}
