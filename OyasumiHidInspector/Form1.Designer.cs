using System;

namespace OyasumiHidInspector
{
    partial class Form1
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
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }

                UninstallHooks();

                if (timer != null)
                {
                    timer.Stop();
                    timer.Dispose();
                }

                _keyboardHookProc = null;
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.startBtn = new System.Windows.Forms.Button();
            this.listBoxLog = new System.Windows.Forms.ListBox();
            this.lblInfo = new System.Windows.Forms.Label();
            this.lstHid = new System.Windows.Forms.ListBox();
            this.btnSettings = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // startBtn
            // 
            this.startBtn.BackColor = System.Drawing.Color.LavenderBlush;
            this.startBtn.Font = new System.Drawing.Font("Verdana", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.startBtn.Location = new System.Drawing.Point(3, 4);
            this.startBtn.Name = "startBtn";
            this.startBtn.Size = new System.Drawing.Size(235, 123);
            this.startBtn.TabIndex = 0;
            this.startBtn.TabStop = false;
            this.startBtn.Text = "Start Listening";
            this.startBtn.UseVisualStyleBackColor = false;
            this.startBtn.Click += new System.EventHandler(this.startBtn_Click);
            // 
            // listBoxLog
            // 
            this.listBoxLog.AllowDrop = true;
            this.listBoxLog.BackColor = System.Drawing.Color.White;
            this.listBoxLog.FormattingEnabled = true;
            this.listBoxLog.Items.AddRange(new object[] {
            "    "});
            this.listBoxLog.Location = new System.Drawing.Point(244, 4);
            this.listBoxLog.Name = "listBoxLog";
            this.listBoxLog.Size = new System.Drawing.Size(537, 745);
            this.listBoxLog.TabIndex = 3;
            this.listBoxLog.TabStop = false;
            this.listBoxLog.UseTabStops = false;
            // 
            // lblInfo
            // 
            this.lblInfo.BackColor = System.Drawing.Color.Transparent;
            this.lblInfo.Location = new System.Drawing.Point(0, 400);
            this.lblInfo.Name = "lblInfo";
            this.lblInfo.Size = new System.Drawing.Size(230, 349);
            this.lblInfo.TabIndex = 6;
            this.lblInfo.Text = "--Select device to view info--";
            // 
            // lstHid
            // 
            this.lstHid.FormattingEnabled = true;
            this.lstHid.Location = new System.Drawing.Point(3, 133);
            this.lstHid.Name = "lstHid";
            this.lstHid.Size = new System.Drawing.Size(235, 264);
            this.lstHid.TabIndex = 7;
            this.lstHid.SelectedIndexChanged += new System.EventHandler(this.lstHid_SelectedIndexChanged);
            // 
            // btnSettings
            // 
            this.btnSettings.BackColor = System.Drawing.Color.LavenderBlush;
            this.btnSettings.Font = new System.Drawing.Font("Verdana", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.btnSettings.Location = new System.Drawing.Point(3, 626);
            this.btnSettings.Name = "btnSettings";
            this.btnSettings.Size = new System.Drawing.Size(235, 123);
            this.btnSettings.TabIndex = 8;
            this.btnSettings.TabStop = false;
            this.btnSettings.Text = "Settings";
            this.btnSettings.UseVisualStyleBackColor = false;
            this.btnSettings.Click += new System.EventHandler(this.btnSettings_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.WhiteSmoke;
            this.BackgroundImage = global::OyasumiHidInspector.Properties.Resources.gladkii_elegantnyi_gradient_fioletovyi_fon_horoso_ispol_zovat_v_kacestve_dizaina;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.ClientSize = new System.Drawing.Size(784, 761);
            this.Controls.Add(this.btnSettings);
            this.Controls.Add(this.lstHid);
            this.Controls.Add(this.lblInfo);
            this.Controls.Add(this.listBoxLog);
            this.Controls.Add(this.startBtn);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "Form1";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "HID - Inspector";
            this.TopMost = true;
            this.ResumeLayout(false);

        }


        #endregion

        private System.Windows.Forms.Button startBtn;
        public System.Windows.Forms.ListBox listBoxLog;
        private System.Windows.Forms.Label lblInfo;
        private System.Windows.Forms.ListBox lstHid;
        private System.Windows.Forms.Button btnSettings;
    }
}

