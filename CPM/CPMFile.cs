using H8DReader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace CPM
{
    public class CPMFile
    {
        // test bit 7 for the following filename chars
        private const byte FRead = 9; //  Read only
        private const byte FSys = 10; // 1 = true
        private const byte FChg = 11; // bit 7 means file changed
        private const byte FMask = 0x80; // bit mask
        //private const int BuffLen = 0x2000; // buffer size

        // Disk types
        public int H37disktype = 5; // location of H37 disk type
     
        /********** data values for reading .IMD disks       */
        private const int bufferSize = 850 * 1024;
        public byte[] buf = new byte[bufferSize];
        //public string addFilename = "";
        private const int sectorMax = 16 * 160; // max number of tracks

        private int[]
            diskMap = new int[sectorMax]; // an array of buffer pointers in buf[] for each sector on the disk starting with track 0, side 0

        private int albNumSize = 2; // size of ALB size in directory
        private int albSize = 512; // size of an alloction block
        private int dirSectStart = 15; // starting sector for disk directory counting from 0. Also first ALB.
        private string DiskImageImdActive = "";
        private long diskSize = 0;

        private int diskType = 0,
            bufPtr = 0,
            dirStart = 0,
            dirSize = 0,
            intLv = 0,
            spt = 0,
            sectorSize = 0,
            numTrack = 0;

        public List<DirList> fileNameList = new List<DirList>();

        /*
        Disk type: byte 5 in sector 0 on H-37 disks (starting from 0) to define disk parameters
        Allocation Block size: number of bytes in an the smallest block used by CP/M on the disk. must be a multiple of 128 (0x80)
                AB numbers start with 0. The directory starts in AB 0.
        Directory Stat: start of directory entries in bytes
        Allocation Block Number Size: number of bytes used in directory entry to reference an allocation block
        Dir Size: number of bytes used for the directory
         0000 =         DPENE	EQU	00000000B
         0040 =         DPEH17	EQU	01000000B
         0060 =         DPEH37	EQU	01100000B
         0008 =         DPE96T	EQU	00001000B
         0004 =         DPEED	EQU	00000100B
         0002 =         DPEDD	EQU	00000010B
         0001 =         DPE2S	EQU	00000001B

        */
        // 0.Disk type, 1.Allocation block size, 2.Directory start, 3.Allocation block byte size, 4.dir size, 5.interleave, 6.Sectors per Track, 7.Sector Size,
        // 8.# Tracks, 9. # heads
        public int[,] DiskType =
        {
            // 0    1       2     3     4    5  6   7       8  9
            {0x6f, 0x800, 0x2800, 2, 0x2000, 3, 5, 0x400, 160, 2}, // H37 96tpi ED DS
            {0x6b, 0x800, 0x2000, 2, 0x2000, 3, 16, 0x100, 160, 2}, // H37 96tpi DD DS
            {0x67, 0x800, 0x2800, 2, 0x1000, 3, 5, 0x400, 80, 2}, // H37 48tpi ED SS
            {0x62, 0x400, 0x2000, 1, 0x1000, 3, 16, 0x100, 40, 1}, // H37 48tpi DD SS
            {0x63, 0x400, 0x2000, 1, 0x2000, 3, 9, 0x200, 80, 2}, // H37 48tpi DD DS
            {0xE5, 0x400, 0x1e00, 1, 0x800, 4, 10, 0x100, 40, 1}, // Default H17 48tpi SD SS
            {0x00, 0x400, 0x1e00, 1, 0x800, 4, 10, 0x100, 40, 1}, // Default H17 48tpi SD SS
        };


        /*
        H37 disk identification at byte 6 on the first sector
        MSB = 6 for H37
        LSB
        Bit 4 1 = 48tpi in 96tpi drive
        Bit 3 0 = 48 tpi drive, 1 = 96 tpi
        Bit 2 1 = extended double density, used in conjunction with bit 1 (0110B)
        Bit 1 1 = double density, 0 = single density
        Bit 0 1 = double sided, 0 = single sided
        */
        private string fname;
        private byte[] fnameb;
        private bool readOnly; // Read only file
        private bool sys; // system file
        private bool chg; // disk changed - not used
        private uint fsize; // file size 
        private List<FCBlist> FCBfirst;

        public class DirList : IComparable<DirList>
        {
            public string fname; // filename plus extension in 8 + " " + 3 format
            public byte[] fnameB = new byte[11]; // byte array version of file name
            public int fsize; // file size in Kb
            public string flags; // flags for system and R/O
            public int fcbNumSize;
            public List<FCBlist> fcbList;

            public DirList()
            {
                fname = "";
                fsize = 0;
                fcbList = new List<FCBlist>();
                fcbNumSize = 1;
            }

            public DirList(string tFname, int tFsize, string tFlags)
            {
                fname = tFname;
                fsize = tFsize;
                flags = tFlags;
                fcbList = new List<FCBlist>();
                fcbNumSize = 1;
            }

            public int CompareTo(DirList other)
            {
                if (other == null) return 1;
                return string.Compare(fname, other.fname);
            }

            public bool Equals(DirList other)
            {
                if (other == null) return false;
                return fname.Equals(other.fname);
            }
        }


        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class FCBlist : IComparable<FCBlist>
        {
            public int[] fcb { get; set; } // 16 file control block numbers

            public int fcbnum { get; set; } // number of 128 byte records in this extant
            public int extantnum { get; set; } // extant number

            public FCBlist()
            {
                fcb = new int[16];
                fcb[0] = 0;
                fcbnum = 0;
                extantnum = 0;
            }

            public int Compare(FCBlist x, FCBlist other)
            {
                if (other == null) return 1;
                if (x.extantnum > other.extantnum) return 1;
                else if (x.extantnum == other.extantnum)
                    return 0;
                else return -1;
            }

            public int CompareTo(FCBlist other)
            {
                if (other == null) return 1;
                if (extantnum > other.extantnum) return 1;
                else if (extantnum == other.extantnum)
                    return 0;
                else return -1;
            }
        }


        public CPMFile()
        {
            fname = "";
            fnameb = new byte[11] {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0};
            readOnly = false;
            sys = false;
            chg = false;
            FCBfirst = new List<FCBlist>();
        }


        // destructor
        ~CPMFile()
        {
        }

        //*************** Read IMD Directory
        public void ReadImdDir(string diskFileName, ref long diskTotal)
        {
            // Check if file already in memory. If not, then process
            // open file: fileName
            // check H37 file type in byte 6
            // get disk parameters
            // Read directory gathering file names and sizes
            // update fcbList with fcb list for each file
            // add file names listBox2.Items
            // update file count and total file size
            var sectorSizeList = new int[] {128, 256, 512, 1024, 2048, 4096, 8192}; // IMD values
            var result = 0;
            var encoding = new UTF8Encoding();

            if (diskFileName != DiskImageImdActive) // check if data already in memory
            {
                var file = File.OpenRead(diskFileName); // read entire file into an array of byte
                var fileByte = new BinaryReader(file);
                var fileLen = (int) file.Length;
                //byte[] buf = new byte[bufferSize];
                try
                {
                    if (fileByte.Read(buf, 0, bufferSize) != fileLen)
                    {
                        MessageBox.Show("IMD file read error", "Error", MessageBoxButtons.OK);
                        return;
                    }
                }
                catch
                {
                    MessageBox.Show("File buffer too small", "Error", MessageBoxButtons.OK);
                    return;
                }

                DiskImageImdActive = diskFileName;
                diskSize = fileLen;
                fileNameList.Clear();
            }
            else
            {
                return; // list is current do nothing
            }

            var bufPtr = 0;
            while (buf[bufPtr] != 0x1a && bufPtr < bufferSize) bufPtr++; // look for end of text comment in IMD file
            if (bufPtr < bufferSize && buf[bufPtr + 1] < 6) // process as IMD file
            {
                bufPtr += 4;
                spt = buf[bufPtr++]; // sectors per track
                sectorSize = sectorSizeList[buf[bufPtr]];
                var skewMap = new int[spt];

                for (var i = 0; i < spt; i++) skewMap[i] = buf[++bufPtr]; // load skew map from IMD image
                bufPtr++; // point to first sector marker
                //firstSector = bufPtr;
                int ctr,
                    dirSizeD = 0,
                    sptD = 0,
                    sectorSizeD = 0;


                //
                // map sectors
                // bufPtr already points to first sector marker

                var sectorCnt = 0;
                while (sectorCnt < sectorMax)
                {
                    // debug
                    // int t1 = sectorCnt % spt;
                    //int t2 = skewMap[sectorCnt % spt];
                    //int t3 = (sectorCnt / spt) * spt;
                    diskMap[sectorCnt / spt * spt + skewMap[sectorCnt % spt] - 1] =
                        bufPtr; // bufPtr points to sector marker

                    int t4 = buf[bufPtr];
                    switch (buf[bufPtr])
                    {
                        case 1:
                        case 3:
                        case 5:
                        case 7:
                            bufPtr += sectorSize + 1;
                            break;
                        case 2:
                        case 4:
                        case 6:
                        case 8:
                            bufPtr += 2;
                            break;
                        case 0:
                            bufPtr++;
                            break;
                        default:
                            MessageBox.Show("Error - IMD sector marker out of scope", "Error",
                                MessageBoxButtons.OK);
                            break;
                    }

                    if ((sectorCnt + 1) % spt == 0 && sectorCnt > 0)
                        bufPtr += 5 + spt; // skip track header and interleave info
                    sectorCnt++;
                }
                //

                diskType = (int) buf[diskMap[0] + 6];


                for (ctr = 0; ctr < DiskType.GetLength(0); ctr++) // search DiskType array for values
                    if (diskType == DiskType[ctr, 0])
                    {
                        albSize = DiskType[ctr, 1]; // ALB Size
                        albNumSize = DiskType[ctr, 3]; // size of ALB size in directory
                        dirStart = DiskType[ctr, 2]; // physical start of directory
                        dirSizeD = DiskType[ctr, 4]; // size of the directory
                        sptD = DiskType[ctr, 6]; // sectors per track  
                        sectorSizeD = DiskType[ctr, 7]; // sector size
                        numTrack = DiskType[ctr, 8];
                        diskSize = diskTotal = numTrack * spt * sectorSize / 1024;
                        dirSectStart = dirStart / sectorSize;
                        break;
                    }

                // error if no match found
                if (ctr == DiskType.GetLength(0))
                    MessageBox.Show("Error - CP/M Disk Type not found in IMD File", "Error", MessageBoxButtons.OK);
                else
                    result = 1;


                if ((spt != sptD || sectorSize != sectorSizeD) && result == 1)
                {
                    MessageBox.Show("Error - sector/track or sector size mismatch", "Error", MessageBoxButtons.OK);
                    result = 0;
                }

                if (result == 1) // done error checking, read directory
                {
                    // Read Dir
                    var diskUsed = 0;
                    for (var i = 0; i < dirSizeD / sectorSize; i++)
                    {
                        bufPtr = diskMap[(int) (dirStart / sectorSize) + i];
                        if (buf[bufPtr++] % 2 > 0) // IMD sector marker is odd. data should contain sector size
                            for (var dirPtr = 0; dirPtr < sectorSize; dirPtr += 32)
                                if (buf[bufPtr + dirPtr] != 0xe5)
                                {
                                    var flagStr = "";
                                    if ((buf[bufPtr + dirPtr + 9] & 0x80) > 0) flagStr += "R/O";
                                    if ((buf[bufPtr + dirPtr + 10] & 0x80) > 0) flagStr += " S";
                                    if ((buf[bufPtr + dirPtr + 11] & 0x80) > 0) flagStr += " W";
                                    for (var k = 9; k < 12; k++)
                                        buf[bufPtr + dirPtr + k] &= 0x7f; // mask high bit for string conversion

                                    var fnameStr = encoding.GetString(buf, bufPtr + dirPtr + 1, 11);
                                    //fnameStr = fnameStr.Insert(8, " ");
                                    var fileDirSize = buf[bufPtr + dirPtr + 15] * 128;
                                    var temp = new DirList(fnameStr, fileDirSize, flagStr); // temp storage
                                    Array.Copy(buf, bufPtr + dirPtr + 1, temp.fnameB, 0, 11); // copy byte filename
                                    diskUsed += fileDirSize;
                                    temp.fcbNumSize = albNumSize;
                                    var tempFcb = new FCBlist
                                    {
                                        extantnum = buf[bufPtr + dirPtr + 12],
                                        fcbnum = buf[bufPtr + dirPtr + 15]
                                    };
                                    for (var k = 16; k < 32 - (albNumSize - 1) * 8; k++)
                                        tempFcb.fcb[k - 16] = (int) buf[bufPtr + dirPtr + 16 + (k - 16) * albNumSize];
                                    temp.fcbList.Add(tempFcb);
                                    var obj = fileNameList.FirstOrDefault(x => x.fname == fnameStr);
                                    if (obj != null) // directory entry exists
                                    {
                                        obj.fsize += fileDirSize; // update file size
                                        obj.fcbList.Add(tempFcb); // add file control block
                                    }
                                    else
                                    {
                                        fileNameList.Add(temp);
                                    }
                                }
                    }

                    fileNameList.Sort();
                    //debug
                    //foreach (var f in fileNameList)
                    //{
                    //    var testStr = f.fname + " ";
                    //    foreach (var t in f.fcbList)
                    //    {
                    //        for (var i = 0; i < 16/f.fcbNumSize; i++)
                    //            testStr = testStr + t.fcb[i].ToString() + " ";
                    //    }

                    //    Console.WriteLine(testStr);
                    //}
                }
            }

            if (result == 0) // clear instance data
            {
                diskSize = 0;
                DiskImageImdActive = "";
                fileNameList.Clear();
            }

            return;
        }


        //***************** Extract File CP/M IMD
        // Call ReadImdDisk to make sure image is in memory
        // Check to make sure file is in DirList
        public int ExtractFileCPMImd(Form1.DiskFileEntry diskFileEntry)
        {
            var diskImage = diskFileEntry.DiskImageName;
            //var fileNameListtemp = new List<CPMFile.DirList>();
            long diskUsed = 0, diskTotal = 0;
            var result = 0;


            ReadImdDir(diskImage, ref diskTotal);
            var obj = fileNameList.FirstOrDefault(x => x.fname == diskFileEntry.FileName);
            if (obj != null)
            {
                var encoding = new UTF8Encoding();
                var dir = string.Format("{0}_Files", diskImage); // create directory name and check if directory exists
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                fnameb = encoding.GetBytes(diskFileEntry.FileName);

                // Create output File
                var name = diskFileEntry.FileName.Substring(0, 8).Trim(' ');
                var ext = diskFileEntry.FileName.Substring(8, 3).Trim(' ');
                var file_name = string.Format("{0}\\{1}.{2}", dir, name, ext);

                if (File.Exists(file_name))
                    if (MessageBox.Show("File exists, Overwrite it?", "File Exists", MessageBoxButtons.YesNo) ==
                        DialogResult.No)
                    {
                        result = 0;
                        return result;
                    }


                var file_out = File.Create(file_name);
                var bin_out = new BinaryWriter(file_out);


                // Read file data from memory buffer
                var wBuff = new byte[obj.fsize * 1024 + 256]; // write buffer = file size plus a buffer
                var wBPtr = 0;
                var t0 = 0;
                foreach (var f in obj.fcbList)
                {
                    t0++;
                    var fcbNum = f.fcbnum; // number of 128 byte CP/M records in the FCB
                    for (var i = 0;
                        i < 16 / albNumSize && fcbNum > 0;
                        i++) // read each fcb block in record. may be 8 or 16 values
                        if (f.fcb[i] > 0) // only process valid allocation blocks
                        {
                            var sectPerAlb = albSize / sectorSize;
                            //var t2 = f.fcb[i] * sectPerAlb + dirSectStart; // debug
                            for (var albCnt = 0;
                                albCnt < sectPerAlb;
                                albCnt++) // number of sectors to read in this allocation block
                            {
                                //var t3 = f.fcb[i] * sectPerAlb + dirSectStart + albCnt;
                                var bufPtr =
                                    diskMap[
                                        f.fcb[i] * sectPerAlb + dirSectStart + albCnt]; // location of sector in buf[]
                                var bufData =
                                    buf[bufPtr]; // get IMD sector marker. If odd, a sector worth of data follows

                                if (bufData % 2 > 0) // IMD sector marker. odd number equals sector worth of data
                                {
                                    bufPtr++; // point to first data byte
                                    var k = 0; // declared outside for loop to preserve value
                                    for (;
                                        k < sectorSize / 128 && k < fcbNum;
                                        k++) // read only one sector or the number of fcb records left
                                    for (var j = 0; j < 128; j++)
                                        wBuff[wBPtr++] = buf[bufPtr++];
                                    fcbNum -= k; // decrement fcbnum counter by number of records read
                                }
                                else
                                    // IMD marker even, sector is compressed. next byte equals sector data
                                {
                                    bufPtr++;
                                    var k = 0;

                                    for (; k < sectorSize / 128 && k < fcbNum; k++)
                                    for (var j = 0; j < 128; j++)
                                        wBuff[wBPtr++] = buf[bufPtr];
                                    fcbNum -= k; // decrement fcbnum counter by number of records read
                                }
                            }
                        }
                }

                wBPtr--;
                bin_out.Write(wBuff, 0, wBPtr);
                bin_out.Close();
                file_out.Close();
                result = 1;
            }
            else
            {
                MessageBox.Show(diskFileEntry.FileName + " error. File not found in DirList", "Error",
                    MessageBoxButtons.OK);
            }

            return result;
        }


        // ************** Insert File CP/M *********************
        /*
         * Directory entries are written sequentially
         * buf = destination file image buffer
         * fileBuff = buffer of file to add to image
         * len = input file length
         * filename = CP/M filename
         * diskType = offset to DiskType Array
         * ref byte[] fileBuff
         */
        public int InsertFileCpm(string filename)
        {
            var result = 1;
            //int allocBlock = DiskType[diskType, 1],
            //    dirStart = DiskType[diskType, 2],
            //    albDirSize = DiskType[diskType, 3],
            //    dirSize = DiskType[diskType, 4],
            //    interleave = DiskType[diskType, 5],
            //    spt = DiskType[diskType, 6],
            //    sectorSize = DiskType[diskType, 7], // disk parameter values
            //    totalTrack = DiskType[diskType, 8]; 
            //int albNum = 2;       // default AB to store a file
            long diski = dirStart, // CP/M disk index
                diskItemp,
                filei = 0; // file buffer index
            byte extent = 0,
                extentNum = 0;
            var dirList = new int[dirSize / 32];
            var dirListi = 0;
            // string filename = addFilename;
            //if (filename.Length == 0||DiskImageImdActive.Length == 0)           // no filename to add
            //    return 0;
            var file = File.OpenRead(filename);
            var len = file.Length; // read entire file into buffer
            var filebuf = new byte[len];
            var bin_file = new BinaryReader(file);
            bin_file.Read(filebuf, 0, (int) len);
            bin_file.Close();
            file.Dispose();

            // write the file to the disk image
            var filename8 = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrEmpty(filename8)) return 0;
            var skewMap = BuildSkew(intLv, spt);
            // build CP/M version of file name for directory
            var encoding = new ASCIIEncoding();
            filename8 = filename8.Substring(0, Math.Min(filename8.Length, 8));
            filename8 = filename8.PadRight(8, ' ');
            var ext3 = Path.GetExtension(filename);
            ext3 = string.IsNullOrEmpty(ext3) ? ext3 = "   " : ext3 = ext3.Substring(1, Math.Min(ext3.Length, 3));
            ext3 = ext3.PadRight(3, ' ');
            var filenameb = string.Format(filename8 + ext3).ToUpper();

            // Build allocation Block map and directory map
            //var totalSectors = spt * totalTrack;
            //var tt0 = dirStart / allocBlock;
            var allocationBlockMap = new int[numTrack * spt / (albSize / sectorSize) + 1];
            for (var i = 0; i < allocationBlockMap.Length; i++) allocationBlockMap[i] = 0;
            var dirMap = new int[dirSize / 32];
            for (var i = 0; i < dirMap.Length; i++) dirMap[i] = 0;
            var dirCnt = 0;

            for (var numSect = 0; numSect < dirSize / sectorSize; numSect++) // number of sectors in directory
            {
                diski = diskItemp =
                    dirStart + numSect / spt * sectorSize * spt +
                    skewMap[numSect % spt] * sectorSize; // buffer offset for sector start
                var t0 = 0;
                while (diski < diskItemp + sectorSize) // process one sector
                {
                    if (buf[diski] < 15)            // check if user area is less than 15. greater indicates empty entry
                    {
                        var fn = filenameb.ToCharArray();   // check if file is in directory
                        var fcPtr = 1;
                        for (; fcPtr < 12; fcPtr++)
                            if (buf[diski + fcPtr] != (byte) fn[fcPtr - 1])
                                break; // compare filename to filename in directory
                        if (fcPtr == 12)
                        {
                            MessageBox.Show("File already in Directory. Skipping", "File Exists", MessageBoxButtons.OK);
                            return 0;
                        }

                        var cnt = buf[diski + 15] * 128 / albSize; // # of allocation blocks to get in directory record
                        
                        for (var i = 0; i < cnt; i++)       // build allocation block map
                            if (albNumSize == 1)
                            {
                                t0 = buf[diski + 16 + i];
                                allocationBlockMap[buf[diski + 16 + i]] = 1;
                            }
                            else
                            {
                                t0 = buf[diski + 16 + i * 2] + buf[diski + 16 + i * 2 + 1] * 256;
                                allocationBlockMap[buf[diski + 16 + i * 2] + buf[diski + 16 + i * 2 + 1] * 256]
                                    = 1;
                            }
                    }
                    else
                    {
                        dirMap[dirCnt] = (int) diski;
                        dirCnt++;
                    }

                    diski += 32;
                }
            }

            var sectorBlock = albSize / sectorSize; // sectors per AB
            var trackSize = spt * sectorSize; // # bytes in a track
            var trackCnt = (float) trackSize / (float) albSize; // # Allocation blocks in a track
            if (trackCnt % 2 != 0) trackCnt = 2;
            else trackCnt = 1; // number of tracks for skew calculation
            var minBlock = trackSize * (int) trackCnt; // minimum disk size to deal with due to skewing

            // copy data to correct place in disk buffer using skew information
            //var basePtr = dirStart; // albNum * allocBlock + dirStart - (albNum * allocBlock) % minBlock;
            var dirNext = 0;
            while (filei < len) // process file
            {
                // find empty directory entry
                if (dirNext < dirCnt)
                {
                    diski = dirMap[dirNext++];
                }
                else
                {
                    // not enough room on disk, erase directory entries used so far
                    while (dirListi >= 0)
                        if (dirList[dirListi] > 0)
                            buf[dirList[dirListi--]] = 0xe5;
                    result = 0;
                    break;
                }

                // write up to 0x80 128 byte CP/M records
                dirList[dirListi++] = (int) diski; // list of disk entries to erase in case of failure
                buf[diski] = 0; // mark directory entry in use
                var fn = filenameb.ToCharArray();
                for (var i = 1; i < 12; i++) buf[diski + i] = (byte) fn[i - 1]; // copy file name to dir entry
                for (var i = 12; i < 32; i++)
                    buf[diski + i] = 0; // zero out extent list and remaining bytes in directory entry

                // update extent number and records in this extent

                var albCnt = dirSize / albSize;
                var albDirCnt = 0;
                var sectorCPMCnt = 0;

                while (filei < len && albDirCnt < 16) // write up to 16 allocation blocks for this directory entry
                {
                    for (; albCnt < allocationBlockMap.Length; albCnt++)
                        if (allocationBlockMap[albCnt] == 0)
                        {
                            allocationBlockMap[albCnt] = 1;
                            break;
                        }

                    var diskPtr = 0;
                    // write # of sectors in allocation block
                    for (var i = 0; i < sectorBlock; i++)
                    {
                        var sectOffset = albCnt * albSize / sectorSize + i;
                        diskPtr = dirStart + sectOffset / spt * sectorSize * spt +
                                  skewMap[sectOffset % spt] * sectorSize;

                        for (var ctrIndex = 0; ctrIndex < sectorSize; ctrIndex++)
                            if (filei < len)
                            {
                                buf[diskPtr++] = filebuf[filei++];
                                sectorCPMCnt++;
                            }
                    }

                    if (albNumSize == 1)
                    {
                        buf[diski + 16 + albDirCnt++] = (byte) albCnt;
                    }
                    else
                    {
                        buf[diski + 16 + albDirCnt++] = (byte) albCnt;
                        buf[diski + 16 + albDirCnt++] = (byte) (albCnt / 256);
                    }
                }

                buf[diski + 12] = extentNum++;
                var t2 = (byte) (albCnt * albSize / 128);
                buf[diski + 15] = (byte) (sectorCPMCnt / 128);
            }

            return result;
        }
        //*********************ReadCPMdDir(fileName, ref diskTotal)
        //

        public void ReadCpmDir(string diskFileName, ref int diskTotal)
        {
            // Check if file already in memory. If not, then process
            // open file: fileName
            // check H37 file type in byte 6
            // get disk parameters
            // Read directory gathering file names and sizes
            // update fcbList with fcb list for each file
            // update file count and total file size

            int result = 0, fileLen = 0;
            var encoding = new UTF8Encoding();

            if (diskFileName != DiskImageImdActive) // check if data already in memory
            {
                var file = File.OpenRead(diskFileName); // read entire file into an array of byte
                var fileByte = new BinaryReader(file);
                fileLen = (int) file.Length;
                try
                {
                    if (fileByte.Read(buf, 0, bufferSize) != fileLen)
                    {
                        MessageBox.Show("File read error", "Error", MessageBoxButtons.OK);
                        return;
                    }
                }
                catch
                {
                    MessageBox.Show("File buffer too small", "Error", MessageBoxButtons.OK);
                    return;
                }

                DiskImageImdActive = diskFileName;
                diskTotal = fileLen;
                fileNameList.Clear();
                file.Close();
                file.Dispose();
            }
            else
            {
                return; // list is current do nothing
            }

            diskType = (int) buf[5];
            int ctr,
                bufPtr = 0;

            for (ctr = 0; ctr < DiskType.GetLength(0); ctr++) // search DiskType array for values
                if (diskType == DiskType[ctr, 0] || ctr == DiskType.GetLength(0) - 1)
                {
                    // if ctr equals last value, use as default
                    albSize = DiskType[ctr, 1]; // ALB Size
                    albNumSize = DiskType[ctr, 3]; // size of ALB size in directory
                    dirStart = DiskType[ctr, 2]; // physical start of directory
                    dirSize = DiskType[ctr, 4]; // size of the directory
                    intLv = DiskType[ctr, 5]; // interleave
                    spt = DiskType[ctr, 6]; // sectors per track  
                    sectorSize = DiskType[ctr, 7]; // sector size
                    numTrack = DiskType[ctr, 8]; // number of tracks on the disk
                    diskSize = numTrack * spt * sectorSize / 1024;
                    dirSectStart = dirStart / sectorSize;
                    break;
                }

            if (ctr == DiskType.GetLength(0) - 1 && fileLen > 120 * 1024) // using default, check file size
            {
                albSize = 0x800;
                dirSize = albSize * 2; // larger directory for 400k H17 disks
                diskSize = 160 * spt * sectorSize / 1024;
            }

            // error if no match found
            if (ctr == DiskType.GetLength(0))
                MessageBox.Show("Error - CP/M Disk Type not found in File", "Error", MessageBoxButtons.OK);
            else
                result = 1;
            if (result == 1) // done error checking, read directory
            {
                // Read Dir
                var diskUsed = 0;
                var skewMap = BuildSkew(intLv, spt);
                for (var i = 0; i < dirSize / sectorSize; i++) // loop through # sectors in directory
                {
                    bufPtr = dirStart + i / spt * sectorSize * spt + skewMap[i % spt] * sectorSize;
                    for (var dirPtr = 0; dirPtr < sectorSize; dirPtr += 32) // loop through sector checking DIR entries
                        if (buf[bufPtr + dirPtr] != 0xe5)
                        {
                            // process flag data
                            var flagStr = "";
                            if ((buf[bufPtr + dirPtr + 9] & 0x80) > 0) flagStr += "R/O";
                            if ((buf[bufPtr + dirPtr + 10] & 0x80) > 0) flagStr += " S";
                            if ((buf[bufPtr + dirPtr + 11] & 0x80) > 0) flagStr += " W";
                            for (var k = 9; k < 12; k++)
                                buf[bufPtr + dirPtr + k] &= 0x7f; // mask high bit for string conversion

                            // get file name in both string and byte format
                            var fnameStr = encoding.GetString(buf, bufPtr + dirPtr + 1, 11);
                            var fileDirSize = buf[bufPtr + dirPtr + 15] * 128;
                            var temp = new DirList(fnameStr, fileDirSize, flagStr); // temp storage
                            Array.Copy(buf, bufPtr + dirPtr + 1, temp.fnameB, 0, 11); // copy byte filename
                            diskUsed += fileDirSize;
                            temp.fcbNumSize = albNumSize;
                            var tempFcb = new FCBlist
                            {
                                extantnum = buf[bufPtr + dirPtr + 12],
                                fcbnum = buf[bufPtr + dirPtr + 15]
                            };
                            for (var k = 16; k < 32 - (albNumSize - 1) * 8; k++)
                            {
                                tempFcb.fcb[k - 16] = (int) buf[bufPtr + dirPtr + 16 + (k - 16) * albNumSize];
                                if (albNumSize > 1) // add in high order byte
                                    tempFcb.fcb[k - 16] +=
                                        (int) buf[bufPtr + dirPtr + 16 + (k - 16) * albNumSize + 1] * 256;
                            }

                            temp.fcbList.Add(tempFcb);
                            var obj = fileNameList.FirstOrDefault(x => x.fname == fnameStr);
                            if (obj != null) // directory entry exists
                            {
                                obj.fsize += fileDirSize; // update file size
                                obj.fcbList.Add(tempFcb); // add file control block
                            }
                            else
                            {
                                fileNameList.Add(temp);
                            }
                        }

                    fileNameList.Sort();
                }
            }
        }

        // ************** Extract File CP/M  *********************
        // inputs: path and filename, disk entry structure
        // output: requested file
        public int ExtractFileCPM(Form1.DiskFileEntry disk_file_entry)
        {
            var result = 1; // assume success
            var maxBuffSize = 0x2000; // largest allocation block size
            var diskTotal = 0;

            var disk_image_file = disk_file_entry.DiskImageName;
            if (disk_image_file != DiskImageImdActive) ReadCpmDir(disk_image_file, ref diskTotal);

            var encoding = new UTF8Encoding();
            var dir = string.Format("{0}_Files",
                disk_image_file); // create directory name and check if directory exists
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            fnameb = encoding.GetBytes(disk_file_entry.FileName);

            // Create output File
            var name = disk_file_entry.FileName.Substring(0, 8).Trim(' ');
            var ext = disk_file_entry.FileName.Substring(8, 3).Trim(' ');
            var file_name = string.Format("{0}\\{1}.{2}", dir, name, ext);

            if (File.Exists(file_name))
                if (MessageBox.Show("File exists, Overwrite it?", "File Exists", MessageBoxButtons.YesNo) ==
                    DialogResult.No)
                {
                    result = 0;
                    return result;
                }

            var file_out = File.Create(file_name);
            var bin_out = new BinaryWriter(file_out);

            var skewMap = BuildSkew(intLv, spt);

            // Find filename in DIRList
            var obj = fileNameList.FirstOrDefault(x => x.fname == disk_file_entry.FileName);
            if (obj != null)
            {
                var rBptr = 0; // read buffer ptr
                var wBptr = 0; // write buffer ptr
                var wBuff = new byte[obj.fsize + 256]; //   write buffer

                foreach (var f in obj.fcbList)
                {
                    var fcbNum = f.fcbnum;
                    for (var i = 0; i < 16; i++)
                        if (f.fcb[i] > 0) // allocation block to get
                            for (var k = 0; k < albSize / sectorSize; k++) // read the sectors in the allocation block
                            {
                                //GetALB(ref buff, 0, bin_file, f.fcb[i], dirStart, allocBlock, sectorSize, spt, skewMap);
                                var t0 = f.fcb[i];
                                var sectOffset = f.fcb[i] * albSize / sectorSize + k; // sector to get
                                var t1 = sectOffset % spt; // sector to get on the track
                                var t2 = sectOffset / spt; // # of tracks
                                rBptr = dirStart + sectOffset / spt * sectorSize * spt +
                                        skewMap[sectOffset % spt] * sectorSize;
                                var j = 0;
                                for (; j < sectorSize / 128 && j < fcbNum; j++)
                                for (var l = 0; l < 128; l++)
                                    wBuff[wBptr++] = buf[rBptr++];
                                fcbNum -= j;
                            }
                }

                bin_out.Write(wBuff, 0, wBptr);
            }

            bin_out.Close();
            file_out.Close();

            return result;
        }


        //******************** Build Skew *************************************
        // returns an integer array of size spt with the requested interleave intLv
        // array is in logical to physical format
        // logical sector is array index, value is physical order
        public int[] BuildSkew(int intLv, int spt)
        {
            var physicalS = 0;
            var logicalS = 0;
            var count = new int[spt];
            var skew = new int[spt];
            var t = 0;
            // initialize table
            for (var i = 0; i < spt; i++) // initialize skew table
            {
                skew[i] = 32;
                count[i] = i;
            }

            while (logicalS < spt) // build physical to logical skew table
            {
                if (skew[physicalS] > spt) // logical position not yet filled
                {
                    skew[physicalS] = (byte) logicalS++;
                    physicalS += intLv;
                }
                else
                {
                    physicalS++; // bump to next physical position
                }

                if (physicalS >= spt) physicalS = physicalS - spt;
            }

            Array.Sort(skew, count); // sort both arrays using skew values and return count array for offset
            return count;
        }
    }
}