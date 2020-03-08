       /*
        Disk type: byte 5 in sector 0 on H-37 disks (starting from 0) to define disk parameters
        Allocation Block size: number of bytes in an the smallest block used by CP/M on the disk. must be a multiple of 128 (0x80)
                AB numbers start with 0. The directory starts in AB 0.
        Directory Stat: start of directory entries in bytes
        Allocation Block Number Size: number of bytes used in directory entry to reference an allocation block
        Dir Size: number of bytes used for the directory
        */
        // 0.Disk type, 1.Allocation block size, 2.Directory start, 3.Allocation block byte size, 4.dir size, 5.interleave, 6.Sectors per Track, 7.Sector Size, 8.# Tracks
        public int[,] DiskType =
        {   // 0    1       2     3     4    5  6   7       8
            {0x6f, 0x800, 0x2800, 2, 0x2000, 3, 5, 0x400, 160}, // H37 96tpi ED DS
            {0x62, 0x400, 0x2000, 1, 0x1000, 3, 16, 0x100, 80}, // H37 48tpi DD SS
            {0x63, 0x400, 0x2000, 1, 0x2000, 3, 9, 0x200, 80}, // H37 48tpi DD DS
            {0x67, 0x400, 0x2800, 2, 0x1000, 3, 5, 0x400, 160}, // H37 48tpi ED SS
            {0x6b, 0x800, 0x2000, 2, 0x2000, 3, 16, 0x100, 80}, // H37 48tpi ED DS
            {0xE5, 0x400, 0x1e00, 1, 0x800, 4, 10, 0x100, 40}, // Default H17 48tpi SD SS
            {0x00, 0x400, 0x1e00, 1, 0x800, 4, 10, 0x100, 40}, // Default H17 48tpi SD SS

        };
		
		     /*
           // ************** Insert File CP/M *********************
        /*
         * Directory entries are written sequentially
         * * cpmBuff = destination file image buffer
         * fileBuff = buffer of file to add to image
         * len = input file length
         * filename = CP/M filename
         * diskType = offset to DiskType Array
         */
 //2/27/2020
         // ************** Insert File CP/M *********************
        /*
         * Directory entries are written sequentially
         * cpmBuff = destination file image buffer
         * fileBuff = buffer of file to add to image
         * len = input file length
         * filename = CP/M filename
         * diskType = offset to DiskType Array
         * ref byte[] fileBuff
         */
        public int InsertFileCpm(ref byte[] cpmBuff, ref byte[] fileBuff, string filename, int diskType)
            {
            int result = 1;
            int allocBlock = DiskType[diskType, 1],
                dirStart = DiskType[diskType, 2],
                albDirSize = DiskType[diskType, 3],
                dirSize = DiskType[diskType, 4],
                interleave = DiskType[diskType, 5],
                spt = DiskType[diskType, 6],
                sectorSize = DiskType[diskType, 7], // disk parameter values
                totalTrack = DiskType[diskType, 8]; 
            int albNum = 2;       // default AB to store a file
            long diski = dirStart,   // CP/M disk index
                diskItemp,
                filei = 0;          // file buffer index
            byte extent = 0,
                extentNum = 0;
            int[] dirList = new int[dirSize / 32];
            int dirListi = 0;
            var len = fileBuff.Length;

            // write the file to the disk image
            string filename8 = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrEmpty(filename8))
                {
                return 0;
                }
            var skewMap = BuildSkew(interleave, spt);
            // build CP/M version of file name for directory
            var encoding = new ASCIIEncoding();
            filename8 = filename8.Substring(0, Math.Min(filename8.Length, 8));
            filename8 = filename8.PadRight(8, ' ');
            string ext3 = Path.GetExtension(filename);
            ext3 = string.IsNullOrEmpty(ext3) ? ext3 = "   " : ext3 = ext3.Substring(1, Math.Min(ext3.Length, 3));
            ext3 = ext3.PadRight(3, ' ');
            var filenameb = string.Format(filename8 + ext3).ToUpper();

            // Build allocation Block map and directory map
            var totalSectors = spt * totalTrack;
            //var tt0 = dirStart / allocBlock;
            int[] allocationBlockMap = new int[totalSectors /(allocBlock/sectorSize) +1];
            for (var i = 0; i < allocationBlockMap.Length; i++) allocationBlockMap[i] = 0;
            int[] dirMap = new int[dirSize/32];
            for(var i = 0; i < dirMap.Length; i ++) dirMap[i] =0;
            var dirCnt = 0;

            for(var numSect =0; numSect < dirSize/sectorSize; numSect++)        // number of sectors in directory
            {
                diski = diskItemp = dirStart + numSect / spt * sectorSize * spt + skewMap[numSect % spt] * sectorSize;      // buffer offset for sector start
                while (diski < diskItemp + sectorSize)          // process one sector
                {
                    if (cpmBuff[diski] < 15)
                    {
                        char[] fn = filenameb.ToCharArray();
                        int fcPtr = 1;
                        for (; fcPtr < 12; fcPtr++) if(cpmBuff[diski + fcPtr] != (byte)fn[fcPtr - 1]) break; // compare filename to filename in directory
                        if (fcPtr == 12)
                        {
                            MessageBox.Show("File already in Directory. Skipping", "File Exists", MessageBoxButtons.OK);
                            return 0;
                        }
                        var cnt = (cpmBuff[diski + 15] * 128) / allocBlock; // # of allocation blocks to get in directory record
                        for (var i = 0; i < cnt; i++)
                        {
                            if (albDirSize == 1)
                            {
                                var t04 = cpmBuff[diski + 16 + i];
                                allocationBlockMap[cpmBuff[diski + 16 + i]] = 1;
                            }
                            else
                            {
                                allocationBlockMap[cpmBuff[diski + 16 + i * 2] + cpmBuff[diski + 16 + i * 2 + 1] * 256]
                                    = 1;
                            }
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
            var sectorBlock = allocBlock / sectorSize;      // sectors per AB
            var trackSize = spt * sectorSize;                   // # bytes in a track
            float trackCnt = (float) trackSize / (float) allocBlock;      // # Allocation blocks in a track
            if (trackCnt % 2 != 0) trackCnt = 2;    
            else trackCnt = 1;                      // number of tracks for skew calculation
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
                            cpmBuff[dirList[dirListi--]] = 0xe5;
                    result = 0;
                    break;
                }

                // write up to 0x80 128 byte CP/M records
                dirList[dirListi++] = (int) diski; // list of disk entries to erase in case of failure
                cpmBuff[diski] = 0; // mark directory entry in use
                char[] fn = filenameb.ToCharArray();
                for (var i = 1; i < 12; i++) cpmBuff[diski + i] = (byte) fn[i - 1]; // copy file name to dir entry
                for (var i = 12; i < 32; i++)
                    cpmBuff[diski + i] = 0; // zero out extent list and remaining bytes in directory entry

                // update extent number and records in this extent

                var albCnt = dirSize / allocBlock;
                var albDirCnt = 0;
                var sectorCPMCnt = 0;

                while (filei < len && albDirCnt < 16 / albDirSize )// write up to 16 allocation blocks for this directory entry
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
                        var sectOffset = albCnt * allocBlock / sectorSize + i;
                        diskPtr = dirStart + (sectOffset / spt) * sectorSize * spt +
                                  skewMap[sectOffset % spt] * sectorSize;

                        for (var ctrIndex = 0; ctrIndex < sectorSize; ctrIndex++)
                            if (filei < len)
                            {
                                cpmBuff[diskPtr++] = fileBuff[filei++];
                                sectorCPMCnt++;
                            }

                        //else cpmBuff[diskPtr++] = 0; // pad with zero's to the end of the CP/M block
                    }

                    if (albDirSize == 1)
                        cpmBuff[diski + 16 + albDirCnt++] = (byte) albCnt;
                    else
                    {
                        cpmBuff[diski + 16 + albDirCnt++] = (byte) albCnt;
                        cpmBuff[diski + 16 + albDirCnt++] = (byte) (albCnt / 256);
                    }

                }

                cpmBuff[diski + 12] = extentNum++;
                var t2 = (byte) (albCnt * allocBlock / 128);
                cpmBuff[diski + 15] = (byte) (sectorCPMCnt/128);
            }


        

            return result;
            }