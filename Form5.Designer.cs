namespace H8DUtility
    {
    partial class Form5
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
            this.buttonFolder = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.buttonH8d100 = new System.Windows.Forms.Button();
            this.buttonh3780tk = new System.Windows.Forms.Button();
            this.buttonH3740tk = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // buttonFolder
            // 
            this.buttonFolder.Location = new System.Drawing.Point(30, 71);
            this.buttonFolder.Name = "buttonFolder";
            this.buttonFolder.Size = new System.Drawing.Size(84, 23);
            this.buttonFolder.TabIndex = 0;
            this.buttonFolder.Text = "Folder";
            this.buttonFolder.UseVisualStyleBackColor = true;
            this.buttonFolder.Click += new System.EventHandler(this.ButtonFolder_Click);
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(131, 74);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(439, 20);
            this.textBox1.TabIndex = 1;
            // 
            // listBox1
            // 
            this.listBox1.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBox1.FormattingEnabled = true;
            this.listBox1.ItemHeight = 14;
            this.listBox1.Location = new System.Drawing.Point(30, 113);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(313, 340);
            this.listBox1.TabIndex = 2;
            this.listBox1.DoubleClick += new System.EventHandler(this.listBox1_DoubleClick);
            // 
            // buttonH8d100
            // 
            this.buttonH8d100.Location = new System.Drawing.Point(440, 146);
            this.buttonH8d100.Name = "buttonH8d100";
            this.buttonH8d100.Size = new System.Drawing.Size(130, 23);
            this.buttonH8d100.TabIndex = 3;
            this.buttonH8d100.Text = "H8D 100k";
            this.buttonH8d100.UseVisualStyleBackColor = true;
            this.buttonH8d100.Click += new System.EventHandler(this.ButtonH8d100_Click);
            // 
            // buttonh3780tk
            // 
            this.buttonh3780tk.Location = new System.Drawing.Point(440, 175);
            this.buttonh3780tk.Name = "buttonh3780tk";
            this.buttonh3780tk.Size = new System.Drawing.Size(130, 23);
            this.buttonh3780tk.TabIndex = 5;
            this.buttonh3780tk.Text = "H37 80t DS ED 800k";
            this.buttonh3780tk.UseVisualStyleBackColor = true;
            this.buttonh3780tk.Click += new System.EventHandler(this.Buttonh37_806f_Click);
            // 
            // buttonH3740tk
            // 
            this.buttonH3740tk.Location = new System.Drawing.Point(440, 204);
            this.buttonH3740tk.Name = "buttonH3740tk";
            this.buttonH3740tk.Size = new System.Drawing.Size(130, 23);
            this.buttonH3740tk.TabIndex = 6;
            this.buttonH3740tk.Text = "H37 80t DS DD 640k";
            this.buttonH3740tk.UseVisualStyleBackColor = true;
            this.buttonH3740tk.Click += new System.EventHandler(this.ButtonH37_806b_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(27, 29);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(489, 20);
            this.label1.TabIndex = 7;
            this.label1.Text = "Click Folder to change directories.  Double click file name to add files";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(437, 113);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(232, 18);
            this.label2.TabIndex = 8;
            this.label2.Text = "Click below to create an empty file";
            // 
            // Form5
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(758, 557);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.buttonH3740tk);
            this.Controls.Add(this.buttonh3780tk);
            this.Controls.Add(this.buttonH8d100);
            this.Controls.Add(this.listBox1);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.buttonFolder);
            this.Name = "Form5";
            this.Text = "Add Files to CP/M Image";
            this.Load += new System.EventHandler(this.Form5_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

            }

        #endregion
        private System.Windows.Forms.Button buttonFolder;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.Button buttonH8d100;
        private System.Windows.Forms.Button buttonh3780tk;
        private System.Windows.Forms.Button buttonH3740tk;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        }
    }