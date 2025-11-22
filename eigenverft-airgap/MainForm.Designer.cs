using Eigenverft.AirGap.Controls;

namespace Eigenverft.AirGap
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.MainPanel = new System.Windows.Forms.Panel();
            this.transparentLabelClose = new Eigenverft.AirGap.Controls.TransparentLabelEx3();
            this.transparentLabelExText = new Eigenverft.AirGap.Controls.TransparentLabelEx3();
            this.progressBarEx31 = new Eigenverft.AirGap.Controls.ProgressBarEx3();
            this.MainPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // MainPanel
            // 
            this.MainPanel.BackgroundImage = global::Eigenverft.AirGap.Properties.Resources.eigenverft_background_part_logo;
            this.MainPanel.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.MainPanel.Controls.Add(this.transparentLabelClose);
            this.MainPanel.Controls.Add(this.transparentLabelExText);
            this.MainPanel.Controls.Add(this.progressBarEx31);
            this.MainPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.MainPanel.Location = new System.Drawing.Point(0, 0);
            this.MainPanel.Margin = new System.Windows.Forms.Padding(0);
            this.MainPanel.MaximumSize = new System.Drawing.Size(500, 320);
            this.MainPanel.MinimumSize = new System.Drawing.Size(500, 320);
            this.MainPanel.Name = "MainPanel";
            this.MainPanel.Size = new System.Drawing.Size(500, 320);
            this.MainPanel.TabIndex = 0;
            // 
            // transparentLabelClose
            // 
            this.transparentLabelClose.BackColor = System.Drawing.Color.Transparent;
            this.transparentLabelClose.EndColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(122)))), ((int)(((byte)(26)))));
            this.transparentLabelClose.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.transparentLabelClose.ForeColor = System.Drawing.Color.Black;
            this.transparentLabelClose.FullShadowColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.transparentLabelClose.FullShadowDepth = 2;
            this.transparentLabelClose.FullShadowEnabled = false;
            this.transparentLabelClose.GradientAngle = 45F;
            this.transparentLabelClose.Location = new System.Drawing.Point(470, 9);
            this.transparentLabelClose.Name = "transparentLabelClose";
            this.transparentLabelClose.ShadowColor = System.Drawing.Color.Gray;
            this.transparentLabelClose.ShadowEnabled = true;
            this.transparentLabelClose.ShadowOffset = new System.Drawing.Point(1, 0);
            this.transparentLabelClose.Size = new System.Drawing.Size(18, 19);
            this.transparentLabelClose.StartColor = System.Drawing.Color.FromArgb(((int)(((byte)(10)))), ((int)(((byte)(27)))), ((int)(((byte)(48)))));
            this.transparentLabelClose.TabIndex = 4;
            this.transparentLabelClose.Text = "X";
            this.transparentLabelClose.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.transparentLabelClose.Click += new System.EventHandler(this.TransparentLabelClose_Click);
            // 
            // transparentLabelExText
            // 
            this.transparentLabelExText.BackColor = System.Drawing.Color.Transparent;
            this.transparentLabelExText.EndColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(122)))), ((int)(((byte)(26)))));
            this.transparentLabelExText.Font = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold);
            this.transparentLabelExText.ForeColor = System.Drawing.Color.Black;
            this.transparentLabelExText.FullShadowColor = System.Drawing.Color.FromArgb(((int)(((byte)(128)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.transparentLabelExText.FullShadowDepth = 2;
            this.transparentLabelExText.FullShadowEnabled = false;
            this.transparentLabelExText.GradientAngle = 45F;
            this.transparentLabelExText.Location = new System.Drawing.Point(52, 45);
            this.transparentLabelExText.Name = "transparentLabelExText";
            this.transparentLabelExText.ShadowColor = System.Drawing.Color.Gray;
            this.transparentLabelExText.ShadowEnabled = true;
            this.transparentLabelExText.ShadowOffset = new System.Drawing.Point(1, 0);
            this.transparentLabelExText.Size = new System.Drawing.Size(217, 17);
            this.transparentLabelExText.StartColor = System.Drawing.Color.FromArgb(((int)(((byte)(10)))), ((int)(((byte)(27)))), ((int)(((byte)(48)))));
            this.transparentLabelExText.TabIndex = 3;
            this.transparentLabelExText.Text = "Steer your own ship - locally.";
            // 
            // progressBarEx31
            // 
            this.progressBarEx31.BackgroundTransparencyPercent = 50;
            this.progressBarEx31.BarBackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(122)))), ((int)(((byte)(26)))));
            this.progressBarEx31.BorderStyle = Eigenverft.AirGap.Controls.ProgressBarEx3.BorderStyleEnum.None;
            this.progressBarEx31.CustomText = "Starting...";
            this.progressBarEx31.CustomTextColor = System.Drawing.Color.FromArgb(((int)(((byte)(10)))), ((int)(((byte)(27)))), ((int)(((byte)(48)))));
            this.progressBarEx31.CustomTextFont = new System.Drawing.Font("Segoe UI Semibold", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.progressBarEx31.GradientEndColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(122)))), ((int)(((byte)(26)))));
            this.progressBarEx31.GradientStartColor = System.Drawing.Color.FromArgb(((int)(((byte)(10)))), ((int)(((byte)(27)))), ((int)(((byte)(48)))));
            this.progressBarEx31.Location = new System.Drawing.Point(8, 286);
            this.progressBarEx31.Maximum = 1000;
            this.progressBarEx31.Name = "progressBarEx31";
            this.progressBarEx31.Size = new System.Drawing.Size(485, 28);
            this.progressBarEx31.TabIndex = 2;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(500, 320);
            this.Controls.Add(this.MainPanel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(500, 320);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(500, 320);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Form1";
            this.Shown += new System.EventHandler(this.MainForm_Shown);
            this.MainPanel.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel MainPanel;
        private ProgressBarEx3 progressBarEx31;
        private TransparentLabelEx3 transparentLabelExText;
        private TransparentLabelEx3 transparentLabelClose;
    }
}
