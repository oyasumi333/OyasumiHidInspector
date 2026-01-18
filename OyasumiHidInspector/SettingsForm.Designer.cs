namespace OyasumiHidInspector
{
    partial class SettingsForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsForm));
            this.txtForceBlock = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.btnPenMode = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // txtForceBlock
            // 
            this.txtForceBlock.Font = new System.Drawing.Font("Microsoft Sans Serif", 15F);
            this.txtForceBlock.Location = new System.Drawing.Point(12, 53);
            this.txtForceBlock.Name = "txtForceBlock";
            this.txtForceBlock.ReadOnly = true;
            this.txtForceBlock.Size = new System.Drawing.Size(286, 30);
            this.txtForceBlock.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.BackColor = System.Drawing.Color.Transparent;
            this.label1.Font = new System.Drawing.Font("Verdana", 15F);
            this.label1.Location = new System.Drawing.Point(12, 25);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(308, 25);
            this.label1.TabIndex = 1;
            this.label1.Text = "Force block key combination:";
            // 
            // btnPenMode
            // 
            this.btnPenMode.Font = new System.Drawing.Font("Verdana", 20.25F);
            this.btnPenMode.Location = new System.Drawing.Point(588, 25);
            this.btnPenMode.Name = "btnPenMode";
            this.btnPenMode.Size = new System.Drawing.Size(184, 52);
            this.btnPenMode.TabIndex = 2;
            this.btnPenMode.Text = "Safe Mode";
            this.btnPenMode.UseVisualStyleBackColor = true;
            this.btnPenMode.Click += new System.EventHandler(this.btnPenMode_Click);
            // 
            // button1
            // 
            this.button1.Font = new System.Drawing.Font("Verdana", 15F);
            this.button1.Location = new System.Drawing.Point(12, 89);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(286, 52);
            this.button1.TabIndex = 3;
            this.button1.Text = "Change combination";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.BackColor = System.Drawing.Color.Transparent;
            this.lblStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 15F);
            this.lblStatus.Location = new System.Drawing.Point(583, 80);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(154, 25);
            this.lblStatus.TabIndex = 4;
            this.lblStatus.Text = "Status: waiting...";
            // 
            // SettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackgroundImage = global::OyasumiHidInspector.Properties.Resources.gladkii_elegantnyi_gradient_fioletovyi_fon_horoso_ispol_zovat_v_kacestve_dizaina;
            this.ClientSize = new System.Drawing.Size(784, 161);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.btnPenMode);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtForceBlock);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "SettingsForm";
            this.Text = "Settings";
            this.TopMost = true;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtForceBlock;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnPenMode;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Label lblStatus;
    }
}