using CPM;
using H8DUtility;
using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;


namespace H8DReader
{
    public partial class Form1 : Form
    {
        public static string label3str = "";

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct CPMDirEntry
        {
            public byte flag;
            public byte[] filename;
            public byte[] fileext;
            public byte extent;
            public byte[] unused;
            public byte sector_count;
            public byte[] alloc_map;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct HDOSDiskInfo
        {
            public byte serial_num;
            public ushort init_date;
            public long dir_sector;
            public long grt_sector;
            public byte sectors_per_group;
            public byte volume_type;
            public byte init_version;
            public long rgt_sector;
            public ushort volume_size;
            public ushort phys_sector_size;
            public byte flags;
            public byte[] label;
            public ushort reserved;
            public byte sectors_per_track;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct HDOSDirEntry
        {
            public byte[] filename;
            public byte[] fileext;
            public byte project;
            public byte version;
            public byte cluster_factor;
            public byte flags;
            public byte flags2;
            public byte first_group_num;
            public byte last_group_num;
            public byte last_sector_index;
            public ushort creation_date;
            public ushort alteration_date;
        }

        public struct DiskFileEntry
        {
            public int ListBox2Entry;
            public string DiskImageName;
            public string FileName;
            public HDOSDirEntry HDOSEntry;
        }

        public ArrayList DiskFileList;

        public struct DiskLabelEntry
        {
            public int ListBox2Entry;
            public string DiskImageName;
            public string DiskLabelName;
        }

        public ArrayList DiskLabelList;
        public DiskLabelEntry RelabelEntry;

        public byte[] HDOSGrtTable;
        public byte[] FILEGrtAllocTable;

        public bool bImageList = false;

        public int FileCount = 0;
        public int TotalSize = 0;


        public GroupBox FileViewerBorder;
        public RichTextBox FileViewerBox;

        public bool bSVDConnected;

        //
        //
        //

        public Form1()
        {
            InitializeComponent();
            CenterToScreen();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            label6.Text =
                "Version 2.2 CP/M extract/Add, IMD Read/Extract/Convert"; // version number update Darrell Pelan

            FileViewerBorder = new GroupBox();
            FileViewerBorder.Size = new Size(720, 580);
            FileViewerBorder.Location = new Point(90, 30);
            FileViewerBorder.Text = "File Viewer";
            FileViewerBorder.ForeColor = Color.Black;
            FileViewerBorder.BackColor = Color.DarkGray;
            FileViewerBorder.Visible = false;

            Controls.Add(FileViewerBorder);

            FileViewerBox = new RichTextBox();
            FileViewerBox.Size = new Size(700, 520);
            FileViewerBox.Location = new Point(10, 20);
            FileViewerBox.Font = new Font(FontFamily.GenericMonospace, 10);
            FileViewerBox.BorderStyle = BorderStyle.FixedSingle;
            FileViewerBox.BackColor = Color.LightGray;
            FileViewerBox.ReadOnly = true;

            FileViewerBorder.Controls.Add(FileViewerBox);

            var FileViewerButton = new Button();
            FileViewerButton.Name = "filebutton1";
            FileViewerButton.Text = "CLOSE";
            FileViewerButton.Location =
                new Point(FileViewerBorder.Size.Width / 2 - FileViewerButton.Size.Width / 2, 550);
            FileViewerButton.Click += new EventHandler(filebutton1_Click);
            FileViewerButton.BackColor = Color.LightGray;

            FileViewerBorder.Controls.Add(FileViewerButton);

            button2.Enabled = false;
            DisableButtons();
            /*
            string[] port_list = SerialPort.GetPortNames();
            if (port_list.Length > 0)
            {
                for (int i = 0; i < port_list.Length; i++)
                {
                    comboBox1.Items.Add(string.Format("{0}", port_list[i]));
                }
                comboBox1.SelectedIndex = 0;
            }

            serialPort1.Encoding = new UTF8Encoding();
            */
            folderBrowserDialog1.ShowNewFolderButton = false;
            DiskFileList = new ArrayList();
            DiskLabelList = new ArrayList();

            ReadData();
            FileCount = 0;
            // DCP
            if (folderBrowserDialog1.SelectedPath.Length > 0) button1_init();
        }

        // DCP
        private void button1_init()
        {
            button2.Enabled = false; // Catalog button disabled in case no files are found
            button1_initA();
            label3str = label3.Text; // used in CPMFile
        }

        private void
            button1_initA() // dcp modified code to read files store in last used directory. initA is used both on startup and when Folder Button is clicked.
        {
            listBox1.Items.Clear(); // clear file list
            label3.Text = folderBrowserDialog1.SelectedPath; // display current working directory
            // set file extension types to scan directory
            var file_list = new string[1];
            try
            {
                var h8d_list = Directory.GetFiles(label3.Text, "*.h8d");
                var svd_list = Directory.GetFiles(label3.Text, "*.svd");
                var imd_list = Directory.GetFiles(label3.Text, "*.imd");
                var h37_list = Directory.GetFiles(label3.Text, "*.h37");
                file_list = new string[h8d_list.Length + svd_list.Length + imd_list.Length +
                                       h37_list.Length]; // combine filename lists
                Array.Copy(h8d_list, file_list, h8d_list.Length);
                Array.Copy(svd_list, 0, file_list, h8d_list.Length, svd_list.Length);
                Array.Copy(imd_list, 0, file_list, h8d_list.Length + svd_list.Length, imd_list.Length);
                Array.Copy(h37_list, 0, file_list, h8d_list.Length + svd_list.Length + imd_list.Length,
                    h37_list.Length);
            }
            catch
            {
                // Directory not found, clear string
                file_list = null;
                label3.Text = "";
            }


            if (file_list.Length == 0)
            {
                listBox1.Items.Add("No image files found");
                label4.Text = "0 Files";
                bImageList = false;
            }
            else
            {
                foreach (var files in file_list) // add file names to listbox1
                {
                    string file_name;
                    file_name = files.Substring(files.LastIndexOf("\\") + 1).ToUpper();
                    listBox1.Items.Add(file_name);
                    var file_count = string.Format("{0} disk images", listBox1.Items.Count.ToString());
                    label4.Text = file_count;
                }

                button2.Enabled = true; // enable Catalog button
                bImageList = true;
            }
        }

        private void DisableButtons()
        {
            button3.Enabled = false;
            button4.Enabled = false;
            button5.Enabled = false;
            button7.Enabled = false;
        }

        private void ReadData()
        {
            if (File.Exists("H8DUtility.dat"))
            {
                var stream = File.OpenText("H8DUtility.dat");
                if (stream != null)
                {
                    folderBrowserDialog1.SelectedPath = stream.ReadLine();
                    stream.Close();
                }
            }
        }

        private void SaveData()
        {
            var stream = File.CreateText("H8DUtility.dat");
            if (stream != null)
            {
                stream.WriteLine(folderBrowserDialog1.SelectedPath);
                stream.Close();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            button2.Enabled = false;
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                SaveData();
                button1_initA();
            }
        }

        //********************* Add CP/M Blank Disk image ***********************
        private void buttonCreateCPM_click(object sender, EventArgs e)
        {
            var makeDisk = new Form5();
            makeDisk.ShowDialog();
            makeDisk.unload();
            Refresh();
        }

        //*************** Catalog ********************
        private void button2_Click(object sender, EventArgs e)
        {
            //  catalog selected image(s)
            FileCount = 0;
            TotalSize = 0;
            listBox2.Items.Clear();
            DiskFileList.Clear();
            DiskLabelList.Clear();

            if (listBox1.SelectedIndex != -1)
                // one or more files selected in listbox1
                foreach (var lb in listBox1.SelectedItems)
                {
                    var disk_name = label3.Text + "\\" + lb; // path + file name
                    listBox2.Items.Add(lb.ToString());
                    if (lb.ToString().Contains(".H8D"))
                        ProcessFile(disk_name);
                    else if (lb.ToString().Contains(".IMD")) ProcessFileImd(disk_name);
                    else if (lb.ToString().Contains(".H37")) ProcessFileH37(disk_name);
                }
            // dcp TODO add .imd capability
            else // no files selected, so process all of them in listbox1
                foreach (var lb in listBox1.Items)
                {
                    var disk_name = label3.Text + "\\" + lb;
                    listBox2.Items.Add(lb.ToString());

                    if (lb.ToString().Contains(".H8D"))
                        ProcessFile(disk_name);
                    else if (lb.ToString().Contains(".IMD")) ProcessFileImd(disk_name);
                    else if (lb.ToString().Contains(".H37")) ProcessFileH37(disk_name);
                }
            // dcp TODO add .imd capability for H-37 disks

            if (FileCount == 0)
            {
                DisableButtons();
            }
            else
            {
                button3.Enabled = true;
                button4.Enabled = true;
                button5.Enabled = true;
                button7.Enabled = true;
            }

            // dcp changed KB to bytes
            listBox2.Items.Add(string.Format("Total Files {0,5:N0}, Total File Size {1,5:N0} K", FileCount,
                TotalSize / 1024));
            listBox2.Items.Add("");
        }

        private ushort MakeBigEndian16(ushort data)
        {
            var h = (ushort) ((data & (ushort) 0x00FF) << 8);
            var l = (ushort) ((data & (ushort) 0xFF00) >> 8);
            var big_endian_16 = (ushort) (h | l);
            return big_endian_16;
        }

        //************************** Convert IMD  to H37 ******************************************
        private void Button13_click(object sender, EventArgs e)
        {
            var sectorSizeList = new int[] {128, 256, 512, 1024, 2048, 4096, 8192}; // IMD values
            int result = 0, diskType = 0;
            var encoding = new UTF8Encoding();
            if (listBox1.SelectedIndex != -1)
                foreach (var lb in listBox1.SelectedItems)
                {
                    if (lb.ToString().ToUpper().Contains(".IMD") || lb.ToString().ToUpper().Contains(".H37"))
                    {
                        var fileType = lb.ToString().ToUpper().Contains(".IMD");
                        // read entire file into memory
                        var diskFileName = label3.Text + "\\" + lb.ToString();
                        var file = File.OpenRead(diskFileName); // read entire file into an array of byte
                        var fileByte = new BinaryReader(file);
                        var fileLen = (int) file.Length;
                        var buf = new byte[fileLen+5*1024];
                        var wbuf = new byte[fileLen + 5 * 1024];        // add extra track as buffer
                        try
                            {
                            if (fileByte.Read(buf, 0, fileLen) != fileLen)
                                MessageBox.Show("File Read Error", "Error", MessageBoxButtons.OK);
                        }
                        catch
                        {
                            MessageBox.Show("File Access Error", "Error", MessageBoxButtons.OK);
                        }

                        fileByte.Close();
                        file.Close();

                        // create output file
                        int wBufPtr = 0, bufPtr = 0, firstSector = 0;
                        if (fileType)
                            diskFileName = diskFileName.Replace(".IMD", ".H37");
                        else
                            diskFileName = diskFileName.Replace(".H37", ".IMD");
                        if (File.Exists(diskFileName))
                            if (MessageBox.Show("File exists, Overwrite it?", "File Exists",
                                    MessageBoxButtons.YesNo) ==
                                DialogResult.No)
                                return;

                        var file_out = File.Create(diskFileName);
                        var bin_out = new BinaryWriter(file_out);

                        if (fileType)
                        {
                            while (buf[bufPtr] != 0x1a && bufPtr < fileLen)
                                bufPtr++; // look for end of text comment in IMD file
                            if (bufPtr < fileLen && buf[bufPtr + 1] < 6
                            ) // process as IMD file - found end of comment and next byte is valid
                            {
                                bufPtr += 4; // skip cylinder count, head value used for extra data flag
                                var spt = buf[bufPtr++]; // sectors per track
                                var sectorSize = sectorSizeList[buf[bufPtr++]];
                                var diskSkew = new int[spt];
                                for (var i = 0; i < spt; i++) diskSkew[i] = buf[bufPtr++]; // load skew table
                                int shift = 0, temp = 0;

                                while (diskSkew[0] != 1 && shift < spt)
                                {
                                    temp = diskSkew[spt - 1];
                                    for (var i = spt - 1; i > 0; i--) diskSkew[i] = diskSkew[i - 1];
                                    diskSkew[0] = temp;
                                    shift++; // count the number of times we had to shift the skew map
                                }

                                firstSector = bufPtr;
                                var numTrack = 0;

                                int sectorCnt = 0, totalSect = 0;
                                var sectStart = 0;
                                //var filePtr = firstSector;
                                while (bufPtr < fileLen)
                                {
                                    //int t1 = sectorCnt % spt;
                                    //int t2 = skewMap[sectorCnt % spt];
                                    //int t3 = (sectorCnt / spt) * spt;
                                    // int t4 = buf[bufPtr];
                                    totalSect++;
                                    var t0 = (sectorCnt + shift) % spt;
                                    switch (buf[bufPtr])
                                    {
                                        case 1:
                                        case 3:
                                        case 5:
                                        case 7:
                                            bufPtr++; // order sectors in starting with sector 1

                                            //var t00 = diskSkew[(sectorCnt + shift) % spt];
                                            //var t1 = t0 * sectorSize;
                                            wBufPtr = 0;
                                            for (var i = 0; i < sectorSize; i++)
                                                wbuf[(sectorCnt + shift) % spt * sectorSize + wBufPtr++] =
                                                    buf[bufPtr++];
                                           // Console.Write("Track {0} Sector {1} Offset {2:X4} Skew {3:g2} ",numTrack, sectorCnt, t0 * sectorSize + numTrack * 5120,diskSkew[sectorCnt]);
                                            //Console.WriteLine("{0:X2} {1:X2} {2:X2} {3:X2}", wbuf[t1 + 0],wbuf[t1 + 1], wbuf[t1 + 2], wbuf[t1 + 3]);
                                            break;
                                        case 2:
                                        case 4:
                                        case 6:
                                        case 8: // compressed sector
                                            bufPtr++;
                                            wBufPtr = 0;
                                            //t1 = t0 * sectorSize;

                                            for (var i = 0; i < sectorSize; i++)
                                                wbuf[(sectorCnt + shift) % spt * sectorSize + wBufPtr++] =
                                                    buf[bufPtr];
                                            //Console.Write("Track {0} Sector {1} Offset {2:X4} Skew {3:g2} ",numTrack, sectorCnt, t0 * sectorSize + numTrack * 5120,diskSkew[sectorCnt]);
                                            //Console.WriteLine("{0:X2} {1:X2} {2:X2} {3:X2}", wbuf[t1 + 0], wbuf[t1 + 1], wbuf[t1 + 2], wbuf[t1 + 3]);

                                            bufPtr++;
                                            break;
                                        case 0:
                                            bufPtr++;
                                            break;
                                        default:
                                            MessageBox.Show("Error - IMD sector marker out of scope", "Error",
                                                MessageBoxButtons.OK);
                                            break;
                                    }

                                    sectorCnt++;
                                    if (sectorCnt == spt)
                                    {
                                        //var t4 = sectorCnt * sectorSize;
                                        bin_out.Write(wbuf, 0, sectorCnt * sectorSize);
                                        sectorCnt = 0;
                                        wBufPtr = 0;
                                        bufPtr += 5 + spt; // skip track header and interleave info
                                        numTrack++;
                                        sectStart += sectorCnt * sectorSize;
                                    }
                                }

                                if (sectorCnt > 0) bin_out.Write(wbuf, 0, (sectorCnt - 1) * sectorSize);
                                bin_out.Close();
                                file_out.Close();
                            }
                        }
                        else // H37
                        {
                            var putCPM = new CPMFile();
                            diskType = buf[5];
                            int intLv = 0, spt = 0, sectorSize = 0, diskHeads = 1, ctr;
                            byte imdSectorIndex = 0;
                            byte imdMode = 5;
                            for (ctr = 0; ctr < putCPM.DiskType.GetLength(0); ctr++)
                                if (diskType == putCPM.DiskType[ctr, 0])
                                {
                                    intLv = putCPM.DiskType[ctr, 5];
                                    spt = putCPM.DiskType[ctr, 6];
                                    sectorSize = putCPM.DiskType[ctr, 7];
                                    diskHeads = putCPM.DiskType[ctr, 9];
                                    break;
                                }

                            for (ctr = 0; ctr < sectorSizeList.Length; ctr++)
                                if (sectorSizeList[ctr] == sectorSize)
                                {
                                    imdSectorIndex = (byte) ctr;
                                    break;
                                }

                            if (ctr == putCPM.DiskType.GetLength(0))
                            {
                                MessageBox.Show("Error - Invalid H37 disk type", "Error", MessageBoxButtons.OK);
                                break;
                            }

                            var skewMap = putCPM.BuildSkew(intLv, spt);
                            wBufPtr = bufPtr = 0;
                            byte cylinder = 0, head = 0;
                            var initString = "IMD 1.18 H37 format conversion to IMD by Darrell Pelan H8D Utility";
                            var tempbuf = initString.ToCharArray();
                            for (ctr = 0; ctr < tempbuf.Length; ctr++)
                                wbuf[wBufPtr++] = (byte) tempbuf[ctr];
                            wbuf[wBufPtr++] = 0x1A;
                            while (bufPtr < fileLen)
                            {
                                wbuf[wBufPtr++] = imdMode;
                                wbuf[wBufPtr++] = cylinder;
                                if(diskHeads > 1)
                                    wbuf[wBufPtr++] = head++;
                                wbuf[wBufPtr++] = (byte) spt;
                                wbuf[wBufPtr++] = imdSectorIndex;
                                for (ctr = 0; ctr < skewMap.Length; ctr++)
                                    wbuf[wBufPtr + skewMap[ctr]] = (byte) (ctr + 1);
                                wBufPtr += ctr;
                                for (var i = 0; i < spt; i++)
                                {
                                    wbuf[wBufPtr++] = 0x01;
                                    for (ctr = 0; ctr < sectorSize && bufPtr < fileLen; ctr++)
                                        wbuf[wBufPtr++] = buf[bufPtr++];
                                }

                                bin_out.Write(wbuf, 0, wBufPtr);
                                wBufPtr = 0;
                                if (head > 1)
                                {
                                    cylinder++;
                                    head = 0;
                                }
                            }

                            bin_out.Close();
                            file_out.Dispose();
                        }
                    }

                    result++;
                }

            var resultStr = string.Format("{0} Disks converted.", result);

            if (result == 1) resultStr = resultStr.Replace("ks", "k");
            MessageBox.Show(resultStr, "Disk Conversion", MessageBoxButtons.OK);
        }

        //******************************* Process File IMD ********************************
        private void ProcessFileImd(string DiskfileName) // for .IMD disks
        {
            var getCpmFile = new CPMFile(); // create instance of CPMFile, then call function
            //var fileNameList = new List<CPMFile.DirList>();
            long diskUsed = 0, diskTotal = 0;
            getCpmFile.ReadImdDir(DiskfileName, ref diskTotal);
            var diskFileCnt = 0;

            if (getCpmFile.fileNameList.Count > 0)
            {
                diskFileCnt = 0;
                diskUsed = 0;
                listBox2.Items.Add("======== === ==== =========");
                listBox2.Items.Add("  FILE   EXT SIZE   FLAGS  ");
                listBox2.Items.Add("======== === ==== =========");
                foreach (var f in getCpmFile.fileNameList)
                {
                    diskFileCnt++;
                    diskUsed += f.fsize;
                    var disk_file_entry = new DiskFileEntry();
                    disk_file_entry.DiskImageName = DiskfileName;
                    disk_file_entry.FileName = f.fname;
                    disk_file_entry.ListBox2Entry = listBox2.Items.Count;
                    DiskFileList.Add(disk_file_entry);
                    var tempStr = f.fname;
                    tempStr = tempStr.Insert(8, " ");
                    listBox2.Items.Add(string.Format("{0} {1,4} {2}", tempStr, f.fsize / 1024, f.flags));
                }
            }

            listBox2.Items.Add("======== === ==== =========");
            listBox2.Items.Add(string.Format("Files {0}, Total {1,3:N0} K, Free {2,5:N0} K, Disk Size {3,5:N0} k", diskFileCnt,
                diskUsed / 1024, diskTotal - (diskUsed / 1024), diskTotal));
            listBox2.Items.Add("");
            TotalSize += (int) diskUsed;
            FileCount += diskFileCnt;
        }
        //******************************* Process File H37 ********************************

        private void ProcessFileH37(string diskName) // for .H37 & H8D disks
        {
            var getCpmFile = new CPMFile(); // create instance of CPMFile, then call function
            int diskUsed = 0, diskTotal = 0;
            getCpmFile.ReadCpmDir(diskName, ref diskTotal);
            var diskFileCnt = 0;

            if (getCpmFile.fileNameList.Count > 0)
            {
                diskFileCnt = 0;
                diskUsed = 0;
                listBox2.Items.Add("======== === ==== =========");
                listBox2.Items.Add("  FILE   EXT SIZE   FLAGS  ");
                listBox2.Items.Add("======== === ==== =========");
                foreach (var f in getCpmFile.fileNameList)
                {
                    diskFileCnt++;
                    diskUsed += f.fsize;
                    var disk_file_entry = new DiskFileEntry();
                    disk_file_entry.DiskImageName = diskName;
                    disk_file_entry.FileName = f.fname;
                    disk_file_entry.ListBox2Entry = listBox2.Items.Count;
                    DiskFileList.Add(disk_file_entry);
                    var tempStr = f.fname.Insert(8, " ");
                    listBox2.Items.Add(string.Format("{0} {1,4} {2}", tempStr, f.fsize / 1024, f.flags));
                }
            }

            listBox2.Items.Add("======== === ==== =========");
                listBox2.Items.Add(string.Format("Files {0}, Total {1,3:N0} K, Free {2,5:N0} K, Disk Size {3,5:N0} k",
                    diskFileCnt, diskUsed / 1024, (diskTotal - diskUsed) / 1024, diskTotal / 1024));
                listBox2.Items.Add("");
                TotalSize += (int) diskUsed;
                FileCount += diskFileCnt;
         
        }

        //***************** Process File **********************
        private void ProcessFile(string file_name) // for .H8D disks
        {
            const int sector_size = 256;
            var buf = new byte[sector_size];
            var encoding = new UTF8Encoding();

            var file = File.OpenRead(file_name);
            var bin_file = new BinaryReader(file);
            buf = bin_file.ReadBytes(sector_size);
            //if ((buf[0] == 0xAF && buf[1] == 0xD3 && buf[2] == 0x7D && buf[3] == 0xCD) ||
            //    (buf[0] == 0xC3 && buf[1] == 0xA0 && buf[2] == 0x22 && buf[3] == 0x20) ||
            //    (buf[0] == 0xC3 && buf[1] == 0xA0 && buf[2] == 0x22 && buf[3] == 0x30))
            if (Form2.IsHDOSDisk(buf))
            {
                var disk_info = new HDOSDiskInfo();
                disk_info.label = new byte[60];
                file.Seek(0x0900, SeekOrigin.Begin);
                disk_info.serial_num = bin_file.ReadByte();
                disk_info.init_date = (ushort) bin_file.ReadUInt16(); //MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.dir_sector = (long) (bin_file.ReadUInt16() * 256); //MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.grt_sector = (long) (bin_file.ReadUInt16() * 256); //MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.sectors_per_group = bin_file.ReadByte();
                disk_info.volume_type = bin_file.ReadByte();
                disk_info.init_version = bin_file.ReadByte();
                disk_info.rgt_sector = (long) (bin_file.ReadUInt16() * 256); //MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.volume_size = (ushort) bin_file.ReadUInt16(); //MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.phys_sector_size = (ushort) bin_file.ReadUInt16(); //MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.flags = bin_file.ReadByte();
                disk_info.label = bin_file.ReadBytes(60);
                disk_info.reserved = bin_file.ReadUInt16(); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.sectors_per_track = bin_file.ReadByte();

                var disk_label = string.Format("{0}", encoding.GetString(disk_info.label, 0, 60));
                disk_label = disk_label.Trim();

                HDOSGrtTable = new byte[256];
                file.Seek(disk_info.grt_sector, SeekOrigin.Begin);
                HDOSGrtTable = bin_file.ReadBytes(256);

                if (CheckHDOSImageIntegrity() == false)
                {
                    //  if volume number is 0, try as CP/M image
                    if (disk_info.serial_num == 0)
                    {
                        listBox2.Items.Add("HDOS - CP/M DUAL FORMAT");
                        listBox2.Items.Add(disk_label);
                        listBox2.Items.Add(string.Format("Volume #{0}", disk_info.serial_num.ToString()));
                        ReadCPMImage(file_name, ref bin_file, ref encoding);
                    }
                    else
                    {
                        listBox2.Items.Add("");
                        listBox2.Items.Add("!! GRT TABLE IS CORRUPT !!");
                        listBox2.Items.Add("");
                    }

                    return;
                }

                var disk_label_entry = new DiskLabelEntry();
                disk_label_entry.DiskImageName = file_name;
                disk_label_entry.DiskLabelName = disk_label;
                disk_label_entry.ListBox2Entry = listBox2.Items.Count;
                DiskLabelList.Add(disk_label_entry);

                listBox2.Items.Add(disk_label);
                listBox2.Items.Add(string.Format("Volume #{0}", disk_info.serial_num.ToString()));

                listBox2.Items.Add("======== === ==== =========");
                listBox2.Items.Add("  FILE   EXT SIZE   DATE   ");
                listBox2.Items.Add("======== === ==== =========");

                file.Seek(disk_info.dir_sector, SeekOrigin.Begin);
                var entry = new HDOSDirEntry();
                entry.filename = new byte[8];
                entry.fileext = new byte[3];

                var fsize = 0;
                var entry_count = 0;
                var disk_file_count = 0;
                ushort total_size = 0;

                do
                {
                    if (ReadHDOSDirEntry(bin_file, ref entry, ref entry_count) == false) break;

                    fsize = ComputeHDOSFileSize(entry, disk_info.sectors_per_group);

                    if (fsize == -1)
                    {
                        listBox2.Items.Add("!! DIRECTORY IS CORRUPT !!");
                        listBox2.Items.Add("!!   FILESIZE FAILED    !!");
                        listBox2.Items.Add("");
                        return;
                    }

                    total_size += (ushort) fsize;
                    var day = (ushort) (entry.creation_date & 0x001F);
                    if (day == 0) day = 1;
                    var mon = (ushort) ((entry.creation_date & 0x01E0) >> 5);
                    var month = "Jan";
                    switch (mon)
                    {
                        case 1:
                            month = "Jan";
                            break;
                        case 2:
                            month = "Feb";
                            break;
                        case 3:
                            month = "Mar";
                            break;
                        case 4:
                            month = "Apr";
                            break;
                        case 5:
                            month = "May";
                            break;
                        case 6:
                            month = "Jun";
                            break;
                        case 7:
                            month = "Jul";
                            break;
                        case 8:
                            month = "Aug";
                            break;
                        case 9:
                            month = "Sep";
                            break;
                        case 10:
                            month = "Oct";
                            break;
                        case 11:
                            month = "Nov";
                            break;
                        case 12:
                            month = "Dec";
                            break;
                    }

                    var year = (ushort) ((entry.creation_date & 0x7E00) >> 9);
                    if (year == 0)
                        year = 9;
                    else if (year + 70 > 99) year = 99;
                    //if (month == "Inv" || year + 70 > 99)
                    //{
                    //    listBox2.Items.Add("!! DIRECTORY IS CORRUPT !!");
                    //    listBox2.Items.Add("!!     DATE FAILED      !!");
                    //    listBox2.Items.Add("");
                    //    return;
                    //}
                    var cre_date = string.Format("{0:D2}-{1}-{2}", day, month, year + 70);
                    var fname = encoding.GetString(entry.filename, 0, 8);
                    var f = fname.Replace('\0', ' ');
                    var fext = encoding.GetString(entry.fileext, 0, 3);
                    var e = fext.Replace('\0', ' ');
                    var item_name = string.Format("{0} {1} {2,4} {3}", f, e, fsize, cre_date);

                    var disk_file_entry = new DiskFileEntry();
                    disk_file_entry.DiskImageName = file_name;
                    disk_file_entry.FileName = string.Format("{0}{1}", f, e);
                    disk_file_entry.ListBox2Entry = listBox2.Items.Count;
                    disk_file_entry.HDOSEntry = entry;
                    DiskFileList.Add(disk_file_entry);

                    listBox2.Items.Add(item_name);

                    disk_file_count++;
                    FileCount++;
                } while (true);

                var total_free = ComputeHDOSFreeSize(disk_info.sectors_per_group);

                listBox2.Items.Add("======== === ==== =========");
                listBox2.Items.Add(string.Format("Files {0}, Total {1}, Free {2}", disk_file_count, total_size,
                    total_free));
                listBox2.Items.Add("");

                TotalSize += total_size * 256;
            }
            else
            {
                //  CP/M disk

                listBox2.Items.Add("CP/M DISK IMAGE");
                ProcessFileH37(file_name);
                //ReadCPMImage(file_name, ref bin_file, ref encoding);
            }

            file.Close();

            var file_count = string.Format("{0} files", FileCount);
            label5.Text = file_count;
        }

        private bool CheckHDOSImageIntegrity()
        {
            try
            {
                for (var i = 0; i < 256; i++)
                {
                    if (i > 0 && i < 5) continue;
                    if (i >= 200) continue;
                    var grp_count = 0;
                    var grp = HDOSGrtTable[i];
                    do
                    {
                        if (grp == 0) break;
                        if (HDOSGrtTable[grp] == grp) return false;
                        grp = HDOSGrtTable[grp];
                        grp_count++;
                    } while (grp_count < 256);

                    if (grp_count == 256) return false;
                }
            }
            catch (IndexOutOfRangeException e)
            {
                return false;
            }

            return true;
        }

        private int ComputeHDOSFileSize(HDOSDirEntry entry, ushort sectors_per_group)
        {
            var grp_count = 1;
            var grp = entry.first_group_num;
            if (grp < 4 || grp >= 200) return 0;
            while (HDOSGrtTable[grp] != 0 && grp_count < 256)
            {
                if (grp < 4 || grp >= 200) return -1;
                grp = HDOSGrtTable[grp];
                grp_count++;
            }

            if (grp_count == 256) return -1;

            var total_size = (grp_count - 1) * sectors_per_group + entry.last_sector_index;
            return total_size;
        }

        private ushort ComputeHDOSFreeSize(ushort sectors_per_group)
        {
            ushort grp_count = 0;
            ushort grp = 0;
            while (HDOSGrtTable[grp] != 0 && grp_count < 256)
            {
                grp = HDOSGrtTable[grp];
                grp_count++;
            }

            if (grp_count == 256) return 0;
            return (ushort) (grp_count * sectors_per_group);
        }

        private bool ReadHDOSDirEntry(BinaryReader bin_file, ref HDOSDirEntry entry, ref int entry_count)
        {
            do
            {
                entry.filename = bin_file.ReadBytes(8);
                entry.fileext = bin_file.ReadBytes(3);
                entry.project = bin_file.ReadByte();
                entry.version = bin_file.ReadByte();
                entry.cluster_factor = bin_file.ReadByte();
                entry.flags = bin_file.ReadByte();
                entry.flags2 = bin_file.ReadByte();
                entry.first_group_num = bin_file.ReadByte();
                entry.last_group_num = bin_file.ReadByte();
                entry.last_sector_index = bin_file.ReadByte();
                entry.creation_date = bin_file.ReadUInt16();
                entry.alteration_date = bin_file.ReadUInt16();

                entry_count++;
                if (entry_count == 22)
                {
                    var max_entries = bin_file.ReadUInt16();
                    long cur_dir_blk = bin_file.ReadUInt16() * 256; // MakeBigEndian16(bin_file.ReadUInt16());
                    long nxt_dir_blk = bin_file.ReadUInt16() * 256; //  MakeBigEndian16(bin_file.ReadUInt16());
                    bin_file.BaseStream.Seek(nxt_dir_blk, SeekOrigin.Begin);
                    entry_count = 0;
                }

                if (entry.filename[0] == 0xFE || entry.filename[0] == 0x7F) return false;
                if (entry.filename[0] == 0x00) continue;
                if (entry.filename[0] == 0xFF) continue;

                return true;
            } while (true);
        }

        //********************  dcp update original ReadCPMImage for .H8D format
        private void ReadCPMImage(string file_name, ref BinaryReader bin_file, ref UTF8Encoding encoding)
        {
            var fsize = 0;
            var entry_count = 0;
            var disk_file_count = 0;
            var total_free = 0;
            var total_size = 0;
            var disk_size = bin_file.BaseStream.Length / 1024 - 10; // dcp disk size on disk

            listBox2.Items.Add("======== === ==== =========");
            listBox2.Items.Add("  FILE   EXT SIZE   DATE   ");
            listBox2.Items.Add("======== === ==== =========");

            var entry = new CPMDirEntry();
            entry.filename = new byte[8];
            entry.fileext = new byte[3];
            entry.unused = new byte[2];
            entry.alloc_map = new byte[16];
            var offset = 0x1E00;
            bin_file.BaseStream.Seek(offset, SeekOrigin.Begin);

            do
            {
                if (ReadCPMDirEntry(bin_file, ref offset, ref entry, ref entry_count) == false) break;

                fsize += (ushort) (entry.sector_count * 128);
                // dcp assumes directory entries are sequential

                if (entry.sector_count < 0x80)
                {
                    if (fsize % 1024 != 0)
                        fsize = (ushort) (fsize / 1024 + 1);
                    else
                        fsize = (ushort) (fsize / 1024);
                    var fname = encoding.GetString(entry.filename, 0, 8);
                    var f = fname.Replace('\0', ' ');
                    var fext = encoding.GetString(entry.fileext, 0, 3);
                    var e = fext.Replace('\0', ' ');
                    var item_name = string.Format("{0} {1} {2,4} -- N/A --", f, e, fsize);

                    var disk_file_entry = new DiskFileEntry();
                    disk_file_entry.DiskImageName = file_name;
                    disk_file_entry.FileName = string.Format("{0}{1}", f, e);
                    disk_file_entry.ListBox2Entry = listBox2.Items.Count;
                    DiskFileList.Add(disk_file_entry);

                    listBox2.Items.Add(item_name);
                    total_size += fsize;

                    disk_file_count++;
                    FileCount++;
                    fsize = 0;
                }
            } while (true);

            total_free = (int) disk_size - total_size; // dcp

            listBox2.Items.Add("======== === ==== =========");
            listBox2.Items.Add(string.Format("Files {0}, Total {1}, Free {2}", disk_file_count, total_size,
                total_free));
            listBox2.Items.Add("");

            TotalSize += total_size * 1024;
        }

        private bool ReadCPMDirEntry(BinaryReader bin_file, ref int offset, ref CPMDirEntry entry, ref int entry_count)
        {
            var result = false; // dcp default return value
            do
            {
                try
                {
                    entry.flag = bin_file.ReadByte();
                    entry.filename = bin_file.ReadBytes(8);
                    entry.fileext = bin_file.ReadBytes(3);
                    for (var i = 0; i < 3; i++)
                        entry.fileext[i] =
                            (byte) (entry.fileext[i] & 0x7f); // mask bit 7 to account for funky ASCII conversion
                    entry.extent = bin_file.ReadByte();
                    entry.unused = bin_file.ReadBytes(2);
                    entry.sector_count = bin_file.ReadByte();
                    entry.alloc_map = bin_file.ReadBytes(16);
                    // check for erased sector - all 0xE5 and adjust directory start point if needed
                    if (entry.flag == 0xE5 && entry.filename[0] == 0xE5)
                    {
                        if (entry_count == 0) break;

                        if (offset == 0x1E00)
                        {
                            offset = 0x2200;
                            bin_file.BaseStream.Seek(offset, SeekOrigin.Begin);
                        }
                        else if (offset == 0x2200)
                        {
                            offset = 0x2600;
                            bin_file.BaseStream.Seek(offset, SeekOrigin.Begin);
                        }
                        else
                        {
                            break;
                        }

                        entry_count = 0;
                        continue;
                    }

                    if (entry.flag != 0xE5) // dcp not an erased directory entry
                    {
                        entry_count++;
                        result = true;
                    }
                    else
                    {
                        break;
                    }

                    /* dcp
                    if (entry.flag != 0)
                    {
                        continue;
                    }
                    */
                }
                catch
                {
                    result = false;
                    break;
                }
            } while (result == false);

            return result;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            //  search file list
            //string search_text = textBox1.Text;
            //if (search_text.Length > 0)
            //{
            //    if (listBox2.Items.Count > 0)
            //    {
            //        int i = listBox2.FindString(search_text);
            //        listBox2.SelectedItem = i;
            //    }
            //}
            //textBox1.Clear();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            var stream = File.CreateText(string.Format("{0}\\H8DCATALOG.TXT", label3.Text));
            if (stream != null)
            {
                stream.WriteLine("SEBHC DISK IMAGE CATALOG");
                stream.WriteLine("========================");
                stream.WriteLine(string.Format("{0} Disk Images", listBox1.Items.Count));
                stream.WriteLine(string.Format("{0} Total Files", FileCount));
                stream.WriteLine("");
                for (var i = 0; i < listBox2.Items.Count; i++)
                {
                    var str = listBox2.Items[i].ToString();
                    stream.WriteLine(str);
                }

                stream.Close();
            }

            MessageBox.Show(string.Format("Catalog text file saved to {0}\\H8DCATALOG.TXT", label3.Text));
        }

        private void button4_Click(object sender, EventArgs e)
        {
            //  dump to HTML document
            var stream = File.CreateText(string.Format("{0}\\H8DCATALOG.HTML", label3.Text));
            if (stream != null)
            {
                var line = 0;
                stream.WriteLine(
                    "<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 4.01//EN\" \"http://www.w3.org/TR/html4/strict.dtd\">");
                stream.WriteLine("<!-- Created by Les Bird's H8D Utility program. http://www.lesbird.com/sebhc -->");
                stream.WriteLine("<html>");
                stream.WriteLine("<head>");
                stream.WriteLine("<meta content=\"text/html; charset=ISO-8859-1\"");
                stream.WriteLine("http-equiv=\"content-type\">");
                stream.WriteLine("<title></title>");
                stream.WriteLine("</head>");
                stream.WriteLine("<body>");
                stream.WriteLine("<div style=\"text-align: center;\">");
                stream.WriteLine(
                    "<p><big><big style=\"font-weight: bold;\"><big><span style=\"font-family: monospace;\">SEBHC DISK IMAGE CATALOG</span></big></big></big></p>");
                stream.WriteLine("<br style=\"font-family: monospace;\">");
                stream.WriteLine(string.Format("<span style=\"font-family: monospace;\">Disk Images: {0}</span><br>",
                    listBox1.Items.Count));
                stream.WriteLine("<br style=\"font-family: monospace;\">");
                stream.WriteLine(string.Format("<span style=\"font-family: monospace;\">Total Files: {0}</span><br>",
                    FileCount));
                stream.WriteLine("<br>");
                stream.WriteLine("<br style=\"font-family: monospace;\">");
                for (var i = 0; i < listBox2.Items.Count; i++)
                {
                    var str = listBox2.Items[i].ToString();
                    if (str.Contains(".H8D") || str.Contains("Total Files"))
                    {
                        stream.WriteLine("<hr style=\"width: 100%; height: 2px; font-family: monospace;\">");
                        stream.WriteLine("<br>");
                        line = 0;
                    }

                    if (line > 0 && line < 5 || str.Contains("========"))
                        stream.WriteLine(HTMLFormat(listBox2.Items[i].ToString(), false));
                    else
                        stream.WriteLine(HTMLFormat(listBox2.Items[i].ToString(), true));
                    stream.WriteLine("<br>");
                    line++;
                }

                stream.WriteLine("<hr style=\"width: 100%; height: 2px; font-family: monospace;\">");
                stream.WriteLine("<br style=\"font-family: monospace;\">");
                stream.WriteLine("<span style=\"font-family: monospace;\">{0}</span><br>", DateTime.Now.ToString());
                stream.WriteLine("</div>");
                stream.WriteLine("</body>");
                stream.WriteLine("</html>");
                stream.Close();
            }

            MessageBox.Show(string.Format("Catalog HTML file saved to {0}\\H8DCATALOG.HTML", label3.Text));
        }

        private string HTMLFormat(string str, bool bold)
        {
            var html_formatted_str = str;
            for (var i = 0; i < html_formatted_str.Length; i++)
                if (html_formatted_str[i] == ' ')
                {
                    html_formatted_str = html_formatted_str.Remove(i, 1);
                    html_formatted_str = html_formatted_str.Insert(i, "&nbsp;");
                }

            if (bold)
                return string.Format("<span style=\"font-family: monospace; font-weight: bold;\">{0}</span>",
                    html_formatted_str);
            else
                return string.Format("<span style=\"font-family: monospace;\">{0}</span>", html_formatted_str);
        }

        private void button8_Click(object sender, EventArgs e)
        {
            //  rename files
        }

        private void button5_Click(object sender, EventArgs e)
        {
            //  view file
            var idx = listBox2.SelectedIndex;
            if (idx != -1)
                foreach (DiskFileEntry entry in DiskFileList)
                    if (entry.ListBox2Entry == idx)
                    {
                        ViewFile(entry.DiskImageName, entry);
                        return;
                    }
        }

        private void ViewFile(string disk_image_file, DiskFileEntry disk_file_entry)
        {
            //  view the selected file
            if (FileViewerBorder.Visible)
            {
                FileViewerBox.Clear();

                FileViewerBorder.Visible = false;

                listBox2.Enabled = true;
                listBox1.Enabled = true;
                button9.Enabled = true;
                button7.Enabled = true;
                button4.Enabled = true;
                button3.Enabled = true;
                if (bImageList) button2.Enabled = true;
                button1.Enabled = true;
                return;
            }

            var file = File.OpenRead(disk_image_file);
            var bin_file = new BinaryReader(file);
            var buf = bin_file.ReadBytes(256);
            //if ((buf[0] == 0xAF && buf[1] == 0xD3 && buf[2] == 0x7D && buf[3] == 0xCD) ||
            //    (buf[0] == 0xC3 && buf[1] == 0xA0 && buf[2] == 0x22 && buf[3] == 0x20) ||
            //    (buf[0] == 0xC3 && buf[1] == 0xA0 && buf[2] == 0x22 && buf[3] == 0x30))
            if (Form2.IsHDOSDisk(buf))
            {
                var disk_info = new HDOSDiskInfo();
                disk_info.label = new byte[60];
                file.Seek(0x0900, SeekOrigin.Begin);
                disk_info.serial_num = bin_file.ReadByte();
                disk_info.init_date = (ushort) bin_file.ReadUInt16(); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.dir_sector = (long) (bin_file.ReadUInt16() * 256); //  MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.grt_sector = (long) (bin_file.ReadUInt16() * 256); //  MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.sectors_per_group = bin_file.ReadByte();
                disk_info.volume_type = bin_file.ReadByte();
                disk_info.init_version = bin_file.ReadByte();
                disk_info.rgt_sector = (long) (bin_file.ReadUInt16() * 256); //  MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.volume_size = (ushort) bin_file.ReadUInt16(); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.phys_sector_size = (ushort) bin_file.ReadUInt16(); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.flags = bin_file.ReadByte();
                disk_info.label = bin_file.ReadBytes(60);
                disk_info.reserved = (ushort) bin_file.ReadUInt16(); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.sectors_per_track = bin_file.ReadByte();

                HDOSGrtTable = new byte[256];
                bin_file.BaseStream.Seek(disk_info.grt_sector, SeekOrigin.Begin);
                HDOSGrtTable = bin_file.ReadBytes(256);

                var bytes_to_read = 0;
                var fsize = ComputeHDOSFileSize(disk_file_entry.HDOSEntry, disk_info.sectors_per_group);
                int grp = disk_file_entry.HDOSEntry.first_group_num;

                var encoding = new UTF8Encoding();

                listBox1.Enabled = false;
                listBox2.Enabled = false;
                button1.Enabled = false;
                button2.Enabled = false;
                button3.Enabled = false;
                button4.Enabled = false;
                button7.Enabled = false;
                button9.Enabled = false;

                FileViewerBorder.Visible = true;

                var eof = false;

                do
                {
                    var sector_addr = grp * disk_info.sectors_per_group * 256;
                    bin_file.BaseStream.Seek(sector_addr, SeekOrigin.Begin);
                    bytes_to_read = disk_info.sectors_per_group * 256;
                    var buffer = bin_file.ReadBytes(bytes_to_read);
                    for (var i = 0; i < buffer.Length; i++)
                        if (eof)
                        {
                            buffer[i] = 0;
                        }
                        else
                        {
                            if (buffer[i] == 0) eof = true;
                        }

                    var t = encoding.GetString(buffer);
                    FileViewerBox.AppendText(t);
                    grp = HDOSGrtTable[grp];
                } while (grp != 0 && !eof);

                bytes_to_read = disk_file_entry.HDOSEntry.last_sector_index * 256;
                if (bytes_to_read != 0)
                {
                    var buffer = bin_file.ReadBytes(bytes_to_read);
                    for (var i = 0; i < buffer.Length; i++)
                        if (eof)
                        {
                            buffer[i] = 0;
                        }
                        else
                        {
                            if (buffer[i] == 0) eof = true;
                        }

                    var t = encoding.GetString(buffer);
                    FileViewerBox.AppendText(t);
                }

                FileViewerBorder.BringToFront();
                FileViewerBox.BringToFront();
            }
            else
            {
            }
        }

        private void filebutton1_Click(object sender, EventArgs e)
        {
            button5_Click(sender, e);
        }


        private void button6_Click(object sender, EventArgs e)
        {
            //  add file

            var file = File.OpenRead("EMPTY1S40T.h8d");
            var bin_file = new BinaryReader(file);
            var len = file.Length;
            var diskbuf = new byte[len];
            bin_file.Read(diskbuf, 0, (int) len);
            file.Close();
            var diskImageLen = len;
            // diskbuf contains contents of the empty disk image to be filled in by a selection of files

            // initialize the file GRT allocation table
            FILEGrtAllocTable = new byte[256];
            Array.Clear(FILEGrtAllocTable, 0, 256);

            var openFileDialog1 = new OpenFileDialog();

            openFileDialog1.InitialDirectory = "c:\\";
            openFileDialog1.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
            openFileDialog1.FilterIndex = 2;
            openFileDialog1.RestoreDirectory = true;
            openFileDialog1.Multiselect = true;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
                try
                {
                    foreach (var filename in openFileDialog1.FileNames)
                    {
                        file = File.OpenRead(filename);
                        bin_file = new BinaryReader(file);
                        len = file.Length;
                        var filebuf = new byte[len];
                        bin_file.Read(filebuf, 0, (int) len);
                        file.Close();
                        // write file data to disk image
                        InsertFile(ref diskbuf, ref filebuf, len, filename);
                    }

                    var saveDialog = new SaveFileDialog();
                    saveDialog.DefaultExt = "H8D";
                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        var saveFileName = saveDialog.FileName.ToUpper();
                        var writer = new BinaryWriter(File.Open(saveFileName, FileMode.Create));
                        var length = diskImageLen;
                        writer.Write(diskbuf, 0, (int) length);
                        writer.Close();

                        MessageBox.Show(string.Format("Disk image {0} saved.", saveDialog.FileName),
                            "DISK IMAGE SAVED");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not read file from disk. Original error: " + ex.Message);
                }
        }

        private void InsertFile(ref byte[] diskbuf, ref byte[] filebuf, long len, string filename)
        {
            long diski = 0x0900; // diskbuf index
            long filei = 0; // filebuf index

            var disk_info = new HDOSDiskInfo();
            disk_info.serial_num = diskbuf[diski]; // size 1
            disk_info.init_date = (ushort) diskbuf[diski + 1]; // MakeBigEndian16((ushort)diskbuf[diski + 1]); // size 2
            disk_info.dir_sector =
                (long) (diskbuf[diski + 3] * 256); // MakeBigEndian16((ushort)diskbuf[diski + 3]); // size 2
            disk_info.grt_sector =
                (long) (diskbuf[diski + 5] * 256); // MakeBigEndian16((ushort)diskbuf[diski + 5]); // size 2
            disk_info.sectors_per_group = (byte) diskbuf[diski + 7]; // size 1
            disk_info.volume_type = (byte) diskbuf[diski + 8]; // size 1
            disk_info.init_version = (byte) diskbuf[diski + 9]; // size 1
            disk_info.rgt_sector =
                (long) (diskbuf[diski + 10] * 256); // MakeBigEndian16((ushort)diskbuf[diski + 10]); // size 2
            disk_info.volume_size =
                (ushort) diskbuf[diski + 12]; // MakeBigEndian16((ushort)diskbuf[diski + 12]); // size 2
            disk_info.flags = (byte) diskbuf[diski + 14]; // size 1
            disk_info.phys_sector_size =
                (ushort) diskbuf[diski + 15]; // MakeBigEndian16((ushort)diskbuf[diski + 15]); // size 2
            // copy GRT table from disk buffer to working array
            HDOSGrtTable = new byte[256];
            Array.Copy(diskbuf, disk_info.grt_sector, HDOSGrtTable, 0, 256);

            // clear out the file GRT allocation table (do this for each file added to the image)
            Array.Clear(FILEGrtAllocTable, 0, 256);

            // write the file to the disk image
            var filename8 = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrEmpty(filename8)) return;
            filename8 = filename8.Substring(0, Math.Min(filename8.Length, 8));
            filename8 = filename8.PadRight(8, ' ');
            var ext3 = Path.GetExtension(filename);
            if (string.IsNullOrEmpty(ext3))
                ext3 = "   ";
            else
                ext3 = ext3.Substring(1, Math.Min(ext3.Length, 3));

            ext3 = ext3.PadRight(3, ' ');
            InsertHDOSDirEntry(ref diskbuf, ref disk_info, filename8, ext3, len);

            // copy file contents to diskbuf
            var n = 0;
            long k = 0;
            var group_size = disk_info.sectors_per_group * 256;
            var grp = 0;
            while (len > 0)
            {
                grp = FILEGrtAllocTable[n++];
                var bytes_to_copy = Math.Min(group_size, len);
                if (grp != 0)
                {
                    long addr = grp * group_size;
                    Array.Copy(filebuf, k, diskbuf, addr, bytes_to_copy);
                    k += bytes_to_copy;
                    HDOSGrtTable[grp] = FILEGrtAllocTable[n];
                }

                len -= bytes_to_copy;
            }

            // copy GRT table from working array back to disk buffer
            Array.Copy(HDOSGrtTable, 0, diskbuf, disk_info.grt_sector, 256);
        }

        private void InsertHDOSDirEntry(ref byte[] diskbuf, ref HDOSDiskInfo disk_info, string filename8, string ext3,
            long len)
        {
            var i = disk_info.dir_sector;
            long entry_len = 23;
            var max_entries = entry_len * 44;
            var more_entries = true;
            while (more_entries)
            {
                if (diskbuf[i] == 0xFF || diskbuf[i] == 0xFE)
                {
                    var c = filename8.ToCharArray();
                    var z = false;
                    for (var n = 0; n < 8; n++)
                        if (!z && (c[n] >= '0' && c[n] <= '9' || c[n] >= 'A' && c[n] <= 'Z' || c[n] == '-' ||
                                   c[n] == '_'))
                        {
                            diskbuf[i++] = (byte) c[n];
                        }
                        else
                        {
                            diskbuf[i++] = 0;
                            z = true;
                        }

                    c = ext3.ToCharArray();
                    z = false;
                    for (var n = 0; n < 3; n++)
                        if (!z && (c[n] >= '0' && c[n] <= '9' || c[n] >= 'A' && c[n] <= 'Z' || c[n] == '-' ||
                                   c[n] == '_'))
                        {
                            diskbuf[i++] = (byte) c[n];
                        }
                        else
                        {
                            diskbuf[i++] = 0;
                            z = true;
                        }

                    diskbuf[i++] = 0;
                    diskbuf[i++] = 0;
                    diskbuf[i++] = 3;
                    diskbuf[i++] = 0;
                    diskbuf[i++] = 0;
                    byte grtfirst = 0; // need to compute this from RGT table
                    byte grtlast = 0; // need to compute this from RGT table
                    byte secindex = 0; // need to compute this
                    GetGRTFirstLast(ref grtfirst, ref grtlast, ref secindex, disk_info.sectors_per_group, len);
                    diskbuf[i++] = grtfirst;
                    diskbuf[i++] = grtlast;
                    diskbuf[i++] = secindex;
                    diskbuf[i++] = 0x63;
                    diskbuf[i++] = 0x19;
                    diskbuf[i++] = 0x63;
                    diskbuf[i++] = 0x19;
                    return;
                }

                i += entry_len;
            }
        }

        private void GetGRTFirstLast(ref byte grtfirst, ref byte grtlast, ref byte secindex, byte sectors_per_group,
            long len)
        {
            grtfirst = HDOSGrtTable[0];
            grtlast = 0;
            secindex = 0;
            int i = grtfirst;
            var n = 0;
            while (true)
            {
                FILEGrtAllocTable[n++] = (byte) i;
                len -= sectors_per_group * 256;
                if (len <= 0)
                {
                    grtlast = (byte) i;
                    len = Math.Abs(len) / 256 + 1;
                    secindex = (byte) len;
                    HDOSGrtTable[0] = HDOSGrtTable[i];
                    return;
                }

                i = HDOSGrtTable[i];
            }
        }

        //************************* Extract a file *******************
        // process a file list from form 1
        private void button7_Click(object sender, EventArgs e)
        {
            //  extract file
            var files_extracted = 0;
            var getCpmFile = new CPMFile(); // create instance of CPMFile, then call function
            var diskTotal = 0;


            var idx = listBox2.SelectedIndex;
            if (idx != -1)
            {
                for (var i = 0; i < listBox2.SelectedItems.Count; i++)
                {
                    idx = listBox2.SelectedIndices[i];
                    foreach (DiskFileEntry entry in DiskFileList)
                        if (entry.ListBox2Entry == idx)
                        {
                            // dcp changed Extract file to return 1 if successful
                            if (entry.DiskImageName.Contains(".IMD"))
                                //var fileNameList = getCpmFile.ReadImdDir(entry.DiskImageName, ref diskTotal);
                                files_extracted += getCpmFile.ExtractFileCPMImd(entry);
                            else
                                files_extracted += ExtractFile(entry);
                            break;
                        }
                }

                listBox2.ClearSelected();
            }
            else
            {
                if (MessageBox.Show(
                    string.Format("There are a total of {0} files. Extract all files?", DiskFileList.Count),
                    "EXTRACT FILES", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    foreach (DiskFileEntry entry in DiskFileList)
                        files_extracted += ExtractFile(entry);
            }

            if (files_extracted > 0)
            {
                var message = string.Format("{0} file(s) extracted", files_extracted);
                MessageBox.Show(this, message, "H8D Utility");
            }
        }

        private int ExtractFile(DiskFileEntry disk_file_entry)
        {
            var result = 1; // dcp extracted file count to deal with CP/M file extract fail
            var disk_image_file = disk_file_entry.DiskImageName;
            var file = File.OpenRead(disk_image_file);
            var bin_file = new BinaryReader(file);
            var buf = bin_file.ReadBytes(256);
            //if ((buf[0] == 0xAF && buf[1] == 0xD3 && buf[2] == 0x7D && buf[3] == 0xCD) ||
            //    (buf[0] == 0xC3 && buf[1] == 0xA0 && buf[2] == 0x22 && buf[3] == 0x20) ||
            //    (buf[0] == 0xC3 && buf[1] == 0xA0 && buf[2] == 0x22 && buf[3] == 0x30))
            if (Form2.IsHDOSDisk(buf))
            {
                var disk_info = new HDOSDiskInfo();
                disk_info.label = new byte[60];
                file.Seek(0x0900, SeekOrigin.Begin);
                disk_info.serial_num = bin_file.ReadByte();
                disk_info.init_date = (ushort) bin_file.ReadUInt16(); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.dir_sector = (long) (bin_file.ReadUInt16() * 256); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.grt_sector = (long) (bin_file.ReadUInt16() * 256); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.sectors_per_group = bin_file.ReadByte();
                disk_info.volume_type = bin_file.ReadByte();
                disk_info.init_version = bin_file.ReadByte();
                disk_info.rgt_sector = (long) (bin_file.ReadUInt16() * 256); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.volume_size = (ushort) bin_file.ReadUInt16(); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.flags = bin_file.ReadByte();
                disk_info.phys_sector_size = (ushort) bin_file.ReadUInt16(); // MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.label = bin_file.ReadBytes(60);
                disk_info.reserved =
                    (ushort) bin_file.ReadUInt16(); // bin_file.ReadByte(); //MakeBigEndian16(bin_file.ReadUInt16());
                disk_info.sectors_per_track = bin_file.ReadByte();

                HDOSGrtTable = new byte[256];
                bin_file.BaseStream.Seek(disk_info.grt_sector, SeekOrigin.Begin);
                HDOSGrtTable = bin_file.ReadBytes(256);

                var encoding = new UTF8Encoding();

                var dir = string.Format("{0}_Files", disk_image_file);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var delims = " \0";
                var name = string.Format("{0}", encoding.GetString(disk_file_entry.HDOSEntry.filename))
                    .Trim(delims.ToCharArray());
                var ext = string.Format("{0}", encoding.GetString(disk_file_entry.HDOSEntry.fileext))
                    .Trim(delims.ToCharArray());
                var file_name = string.Format("{0}\\{1}.{2}", dir, name, ext);

                var file_out = File.Create(file_name);
                var bin_out = new BinaryWriter(file_out);

                var bytes_to_read = 0;
                var fsize = ComputeHDOSFileSize(disk_file_entry.HDOSEntry, disk_info.sectors_per_group);
                int grp = disk_file_entry.HDOSEntry.first_group_num;

                var eof = false;
                var is_text = false;
                var file_check = false;

                do
                {
                    var sector_addr = grp * disk_info.sectors_per_group * 256;
                    bin_file.BaseStream.Seek(sector_addr, SeekOrigin.Begin);
                    bytes_to_read = disk_info.sectors_per_group * 256;
                    var buffer = bin_file.ReadBytes(bytes_to_read);
                    if (!file_check)
                    {
                        if (buffer[0] == 0x09 || buffer[0] == 0x0A || buffer[0] == 0x0D ||
                            buffer[0] >= 0x20 && buffer[0] < 0x7F) is_text = true;
                        file_check = true;
                    }

                    if (is_text)
                        for (var i = 0; i < buffer.Length; i++)
                            if (!eof)
                            {
                                if (buffer[i] == 0) eof = true;
                            }
                            else
                            {
                                buffer[i] = 0;
                            }

                    bin_out.Write(buffer);
                    grp = HDOSGrtTable[grp];
                } while (grp != 0 && !eof);

                bytes_to_read = disk_file_entry.HDOSEntry.last_sector_index * 256;
                if (bytes_to_read != 0)
                {
                    var buffer = bin_file.ReadBytes(bytes_to_read);
                    if (is_text)
                        for (var i = 0; i < buffer.Length; i++)
                            if (!eof)
                            {
                                if (buffer[i] == 0) eof = true;
                            }
                            else
                            {
                                buffer[i] = 0;
                            }

                    bin_out.Write(buffer);
                }

                file_out.Close();
            }
            else
            {
                // dcp Add CPM Extract
                var getCpmFile = new CPMFile(); // create instance of CPMFile, then call function
                result = getCpmFile.ExtractFileCPM(disk_file_entry);
            }

            return result;
        }


        private void button9_Click(object sender, EventArgs e)
        {
            Form h89ldr = new Form2();
            h89ldr.ShowDialog();
            Refresh();
        }

        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (textBox2.ReadOnly) return;

            textBox2.Clear();
            textBox2.Enabled = false;
            button10.Enabled = false;
            RelabelEntry.ListBox2Entry = -1;
            RelabelEntry.DiskImageName = "";
            RelabelEntry.DiskLabelName = "";

            var idx = listBox2.SelectedIndex;
            if (idx != -1)
            {
                if (listBox2.SelectedItems.Count != 1) return;
                foreach (DiskLabelEntry entry in DiskLabelList)
                    if (entry.ListBox2Entry == idx)
                    {
                        textBox2.Text = entry.DiskLabelName;
                        textBox2.Enabled = true;
                        RelabelEntry = entry;
                        break;
                    }
            }
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            if (!button10.Enabled) button10.Enabled = true;
        }

        private void button10_Click(object sender, EventArgs e)
        {
            textBox2.ReadOnly = true;
            if (MessageBox.Show(
                string.Format(
                    "Change selected HDOS disk label to \"{0}\". This will change the label inside the disk file. Are you sure?",
                    textBox2.Text), "RELABEL DISK", MessageBoxButtons.YesNo) == DialogResult.Yes)
                if (RelabelEntry.ListBox2Entry != -1)
                {
                    listBox2.Items[RelabelEntry.ListBox2Entry] = textBox2.Text;
                    var s = textBox2.Text.PadRight(60, ' ');
                    RelabelEntry.DiskLabelName = s;
                    RelabelDisk();
                }

            textBox2.ReadOnly = false;
        }

        private void RelabelDisk()
        {
            var encoding = new UTF8Encoding();
            var write_stream = new BinaryWriter(File.OpenWrite(RelabelEntry.DiskImageName));

            write_stream.Seek(0x0911, SeekOrigin.Begin); //  seek to label position in disk image
            write_stream.Write(encoding.GetBytes(RelabelEntry.DiskLabelName), 0, RelabelEntry.DiskLabelName.Length);

            write_stream.Close();
        }

        private void button11_Click_1(object sender, EventArgs e)
        {
            Form svd_panel = new Form3();
            svd_panel.ShowDialog();
            Refresh();
        }

        private void button12_Click(object sender, EventArgs e)
        {
            //MessageBox.Show("This is a fairly stable release of Les Bird's H89 emulator. Click OK to continue");
            var emulator = new Form4();
            emulator.ShowDialog();
            emulator.Unload();
            Refresh();
        }
    }
}