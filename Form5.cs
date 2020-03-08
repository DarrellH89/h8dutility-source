using CPM;
using H8DReader;
using System;
using System.IO;
using System.Windows.Forms;


namespace H8DUtility
    {
    public partial class Form5 : Form
        {
        public GroupBox FileViewerBorder;
        public RichTextBox FileViewerBox;
        public int FileCount = 0;

        public Form5()
            {
            InitializeComponent();
            CenterToParent();
            }

        private FolderBrowserDialog folderBrowserDialog1;

        /********** File buffer variables *********/
        // private const int bufferSize = 800 * 1024;
        //private byte[] buf = new byte[bufferSize];

        private void Form5_Load(object sender, EventArgs e)
            {
            textBox1.Text = Form1.label3str;
            FileCount = 0;
            buttonFolder_Init();
            }

        private void
            buttonFolder_Init() // dcp modified code to read files store in last used directory. initA is used both on startup and when Folder Button is clicked.
            {
            listBox1.Items.Clear(); // clear file list
            // set file extension types to scan directory
            string[] file_list = new string[1];
            try
                {
                string[] h8d_list = Directory.GetFiles(textBox1.Text, "*.h8d");
                string[] h37_list = Directory.GetFiles(textBox1.Text, "*.h37");
                file_list = new string[h8d_list.Length + h37_list.Length]; // combine filename lists
                Array.Copy(h8d_list, file_list, h8d_list.Length);
                Array.Copy(h37_list, 0, file_list, h8d_list.Length, h37_list.Length);
                }
            catch
                {
                // Directory not found, clear string
                file_list = null;
                textBox1.Text = "";
                }


            if (file_list.Length == 0)
                {
                listBox1.Items.Add("No image files found");

                }
            else
                {
                foreach (string files in file_list) // add file names to listbox1
                    {
                    string file_name;
                    file_name = files.Substring(files.LastIndexOf("\\") + 1).ToUpper();
                    listBox1.Items.Add(file_name);
                    string file_count = string.Format("{0} disk images", listBox1.Items.Count.ToString());
                    //label4.Text = file_count;
                    }
                }
            }

        public void unload()
            {

            }

        private void ButtonFolder_Click(object sender, EventArgs e)
            {
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            folderBrowserDialog1.SelectedPath = textBox1.Text;
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
                {
                textBox1.Text = folderBrowserDialog1.SelectedPath;
                buttonFolder_Init();
                }

            }

        private void ButtonH8d100_Click(object sender, EventArgs e)
            {
            fileCreate(6, "");
            }

        private void Buttonh37_806f_Click(object sender, EventArgs e)
            {
            fileCreate(0, "");
            }

        private void ButtonH37_806b_Click(object sender, EventArgs e)
            {
            fileCreate(1, "");
            }

        private void fileCreate(int diskType, string fileName)
            {
            var getCpm = new CPMFile(); // create instance of CPMFile, then call function
            // bool fileNew = false;
            string path = fileName;
            OpenFileDialog openFileDialog1 = new System.Windows.Forms.OpenFileDialog();

            if (path.Length == 0) // Create disk image if no path provided
                {
                openFileDialog1.InitialDirectory = textBox1.Text;
                if (diskType > 4)
                    openFileDialog1.Filter = "H8D Files (*.H8D)|*.H8D";
                else
                    openFileDialog1.Filter = "H37 files (*.H37)|*.H37";
                openFileDialog1.FilterIndex = 2;
                openFileDialog1.RestoreDirectory = true;
                openFileDialog1.CheckFileExists = false;
                openFileDialog1.ShowDialog();
                path = openFileDialog1.FileName;
                }

            //Console.Write("Path: ");
            //Console.WriteLine(path);

            //Console.WriteLine(File.Exists(path));
            var result = File.Exists(path);
            //Console.WriteLine("Result: {0:G}", result);
            int diskTotalBytes = 0;
            if (!result)
                {
                //Console.WriteLine("File DOES Not Exist");
                //Console.WriteLine("But it was Created: {0:G}", File.Exists(path));
                // calculate buffer size for disk image
                diskTotalBytes = getCpm.DiskType[diskType, 6] * getCpm.DiskType[diskType, 7] *
                                 getCpm.DiskType[diskType, 8];
                var cpmBuf = new byte[diskTotalBytes];
                for (var i = 0; i < diskTotalBytes; i++)
                    cpmBuf[i] = 0xE5;
                cpmBuf[getCpm.H37disktype] = (byte)getCpm.DiskType[diskType, 0]; // set disk type marker

                switch (cpmBuf[getCpm.H37disktype])

                    {
                    case 0x6f:
                        byte[] diskMark1 = { 0, 0x6f, 0xe5, 8, 0x10, 0xe5, 0xe5, 1, 0xe5, 0x28, 0, 4, 0xf, 0, 0x8a, 0x01, 0xff, 0, 0xf0, 0, 0x40, 0, 2, 0, 0xec };
                        for (var i = 0; i < diskMark1.Length; i++)
                            cpmBuf[getCpm.H37disktype - 1 + i] = diskMark1[i];
                        break;
                    case 0x6b:
                        byte[] diskMark2 = { 0, 0x6b, 0xe5, 2, 0x10, 0xe5, 0xe5, 1, 0xe5, 0x20, 0, 4, 0xf, 0, 0x3b, 0x01, 0xff, 0, 0xf0, 0, 0x40, 0, 2, 0, 0x4d };
                        for (var i = 0; i < diskMark2.Length; i++)
                            cpmBuf[getCpm.H37disktype - 1 + i] = diskMark2[i];

                        break;
                    default:
                        break;
                    }
                FileStream fso = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
                BinaryWriter fileOutByte = new BinaryWriter(fso);

                fileOutByte.Write(cpmBuf, 0, diskTotalBytes);
                fileOutByte.Close();
                fso.Dispose();

                }

            /*
             * Use ReadCmDir to read disk into CPMFiles buffer. After the file is in the buffer, add
             * each new file. Write CPMFiles buffer to disk if any files are successfully added
             */
            getCpm.ReadCpmDir(path, ref diskTotalBytes);




            // Get Files to add to image
            var startDir =
                textBox1.Text; // openFileDialog1.InitialDirectory; // check if a working folder is selected
            var fileCnt = 0;

            if (startDir == "") startDir = "c:\\";
            openFileDialog1.InitialDirectory = startDir;
            openFileDialog1.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
            openFileDialog1.FilterIndex = 2;
            openFileDialog1.RestoreDirectory = true;
            openFileDialog1.Multiselect = true;
            openFileDialog1.Title = "Select Files to Add to Image";
            string temp = "";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
                foreach (String filename in openFileDialog1.FileNames)
                    fileCnt += getCpm.InsertFileCpm(filename);
            if (fileCnt > 0) // Added a file or two
                {
                FileStream fsOut = new FileStream(path, FileMode.Open, FileAccess.Write);
                BinaryWriter fileOutBytes = new BinaryWriter(fsOut);

                fileOutBytes.Seek(0, SeekOrigin.Current);
                fileOutBytes.Write(getCpm.buf, 0, diskTotalBytes);
                fileOutBytes.Close();
                fsOut.Dispose();
                }
            }




        private void listBox1_DoubleClick(object sender, EventArgs e)
            {
            string path = "";
            foreach (var lb in listBox1.SelectedItems)
                {
                path = textBox1.Text + "\\" + lb.ToString();
                fileCreate(0, path);
                }
            }

        }
    }
