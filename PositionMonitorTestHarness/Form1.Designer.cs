namespace PositionMonitorTestHarness
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
            this.label1 = new System.Windows.Forms.Label();
            this.buttonStartAccount = new System.Windows.Forms.Button();
            this.listBoxLog = new System.Windows.Forms.ListBox();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.buttonStop = new System.Windows.Forms.Button();
            this.comboBoxAccount = new System.Windows.Forms.ComboBox();
            this.buttonStart = new System.Windows.Forms.Button();
            this.buttonStopAccount = new System.Windows.Forms.Button();
            this.buttonStartAll = new System.Windows.Forms.Button();
            this.buttonStopAll = new System.Windows.Forms.Button();
            this.checkBoxAutoRefresh = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(22, 22);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(50, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Account:";
            // 
            // buttonStartAccount
            // 
            this.buttonStartAccount.Enabled = false;
            this.buttonStartAccount.Location = new System.Drawing.Point(292, 17);
            this.buttonStartAccount.Name = "buttonStartAccount";
            this.buttonStartAccount.Size = new System.Drawing.Size(75, 23);
            this.buttonStartAccount.TabIndex = 1;
            this.buttonStartAccount.Text = "Start";
            this.buttonStartAccount.UseVisualStyleBackColor = true;
            this.buttonStartAccount.Click += new System.EventHandler(this.buttonStartAccount_Click);
            // 
            // listBoxLog
            // 
            this.listBoxLog.FormattingEnabled = true;
            this.listBoxLog.Location = new System.Drawing.Point(25, 389);
            this.listBoxLog.Name = "listBoxLog";
            this.listBoxLog.Size = new System.Drawing.Size(1461, 251);
            this.listBoxLog.TabIndex = 2;
            // 
            // dataGridView1
            // 
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Location = new System.Drawing.Point(25, 56);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.Size = new System.Drawing.Size(1461, 327);
            this.dataGridView1.TabIndex = 4;
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Location = new System.Drawing.Point(1092, 17);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(75, 23);
            this.buttonRefresh.TabIndex = 5;
            this.buttonRefresh.Text = "Refresh";
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            // 
            // buttonStop
            // 
            this.buttonStop.Enabled = false;
            this.buttonStop.Location = new System.Drawing.Point(1361, 17);
            this.buttonStop.Name = "buttonStop";
            this.buttonStop.Size = new System.Drawing.Size(113, 23);
            this.buttonStop.TabIndex = 6;
            this.buttonStop.Text = "Stop Monitor";
            this.buttonStop.UseVisualStyleBackColor = true;
            this.buttonStop.Click += new System.EventHandler(this.buttonStop_Click);
            // 
            // comboBoxAccount
            // 
            this.comboBoxAccount.FormattingEnabled = true;
            this.comboBoxAccount.Location = new System.Drawing.Point(78, 18);
            this.comboBoxAccount.Name = "comboBoxAccount";
            this.comboBoxAccount.Size = new System.Drawing.Size(208, 21);
            this.comboBoxAccount.TabIndex = 7;
            this.comboBoxAccount.SelectedIndexChanged += new System.EventHandler(this.comboBoxAccount_SelectedIndexChanged);
            // 
            // buttonStart
            // 
            this.buttonStart.Location = new System.Drawing.Point(1233, 17);
            this.buttonStart.Name = "buttonStart";
            this.buttonStart.Size = new System.Drawing.Size(113, 23);
            this.buttonStart.TabIndex = 8;
            this.buttonStart.Text = "Start Monitor";
            this.buttonStart.UseVisualStyleBackColor = true;
            this.buttonStart.Click += new System.EventHandler(this.buttonStart_Click);
            // 
            // buttonStopAccount
            // 
            this.buttonStopAccount.Enabled = false;
            this.buttonStopAccount.Location = new System.Drawing.Point(373, 17);
            this.buttonStopAccount.Name = "buttonStopAccount";
            this.buttonStopAccount.Size = new System.Drawing.Size(75, 23);
            this.buttonStopAccount.TabIndex = 9;
            this.buttonStopAccount.Text = "Stop";
            this.buttonStopAccount.UseVisualStyleBackColor = true;
            this.buttonStopAccount.Click += new System.EventHandler(this.buttonStopAccount_Click);
            // 
            // buttonStartAll
            // 
            this.buttonStartAll.Enabled = false;
            this.buttonStartAll.Location = new System.Drawing.Point(489, 17);
            this.buttonStartAll.Name = "buttonStartAll";
            this.buttonStartAll.Size = new System.Drawing.Size(75, 23);
            this.buttonStartAll.TabIndex = 10;
            this.buttonStartAll.Text = "Start All";
            this.buttonStartAll.UseVisualStyleBackColor = true;
            this.buttonStartAll.Click += new System.EventHandler(this.buttonStartAll_Click);
            // 
            // buttonStopAll
            // 
            this.buttonStopAll.Enabled = false;
            this.buttonStopAll.Location = new System.Drawing.Point(570, 17);
            this.buttonStopAll.Name = "buttonStopAll";
            this.buttonStopAll.Size = new System.Drawing.Size(75, 23);
            this.buttonStopAll.TabIndex = 11;
            this.buttonStopAll.Text = "Stop All";
            this.buttonStopAll.UseVisualStyleBackColor = true;
            this.buttonStopAll.Click += new System.EventHandler(this.buttonStopAll_Click);
            // 
            // checkBoxAutoRefresh
            // 
            this.checkBoxAutoRefresh.AutoSize = true;
            this.checkBoxAutoRefresh.Checked = true;
            this.checkBoxAutoRefresh.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxAutoRefresh.Location = new System.Drawing.Point(1006, 20);
            this.checkBoxAutoRefresh.Name = "checkBoxAutoRefresh";
            this.checkBoxAutoRefresh.Size = new System.Drawing.Size(83, 17);
            this.checkBoxAutoRefresh.TabIndex = 12;
            this.checkBoxAutoRefresh.Text = "Auto refresh";
            this.checkBoxAutoRefresh.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1498, 658);
            this.Controls.Add(this.checkBoxAutoRefresh);
            this.Controls.Add(this.buttonStopAll);
            this.Controls.Add(this.buttonStartAll);
            this.Controls.Add(this.buttonStopAccount);
            this.Controls.Add(this.buttonStart);
            this.Controls.Add(this.comboBoxAccount);
            this.Controls.Add(this.buttonStop);
            this.Controls.Add(this.buttonRefresh);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.listBoxLog);
            this.Controls.Add(this.buttonStartAccount);
            this.Controls.Add(this.label1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button buttonStartAccount;
        private System.Windows.Forms.ListBox listBoxLog;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Button buttonRefresh;
        private System.Windows.Forms.Button buttonStop;
        private System.Windows.Forms.ComboBox comboBoxAccount;
        private System.Windows.Forms.Button buttonStart;
        private System.Windows.Forms.Button buttonStopAccount;
        private System.Windows.Forms.Button buttonStartAll;
        private System.Windows.Forms.Button buttonStopAll;
        private System.Windows.Forms.CheckBox checkBoxAutoRefresh;
    }
}