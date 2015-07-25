using ResIL.Unmanaged;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UsefulThings;


namespace ResILWrapper
{
    // KFreon: Almost all of this stuff is from DDSImage.cs from this project, and the original at http://code.google.com/p/kprojects/
    // And also from SaltIsGood's contributions to the ME3Explorer project. http://me3explorer.sourceforge.com
    public class V8U8Image : ResILImageBase
    {
	    /// <summary>
        /// V8U8 mipmap
        /// </summary>
        public class MipMap
        {
            public int width;
            public int height;
            public byte[] data{get;set;}


            public MipMap(byte[] data, int w, int h)
            {
                long requiredSize = (long)(w * h * 2);
                if (data.Length != requiredSize)
                    throw new InvalidDataException("Data size is not valid for selected format.\nActual: " + data.Length + " bytes\nRequired: " + requiredSize + " bytes");

                this.data = data;
                width = w;
                height = h;
            }
        }
	
	    public string Path { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
	    public int Mips
	    {
		    get
		    {
			    return MipMaps == null ? -1 : MipMaps.Length;
		    }
	    }
	    MipMap[] MipMaps;
	


	    public V8U8Image(string FilePath)
	    {
		    Path = FilePath;
		    using (FileStream stream = new FileStream(Path, FileMode.Open))
			    LoadImage(stream);
	    }
	
	    public V8U8Image(Stream stream)
	    {
		    Path = null;
		    LoadImage(stream);
	    }
	
	    public V8U8Image(byte[] imgData)
	    {
		    Path = null;
		    using (MemoryTributary stream = new MemoryTributary(imgData))
			    LoadImage(stream);
	    }
	
	    private void LoadImage(Stream stream)
	    {		
		    stream.Seek(0, SeekOrigin.Begin);
            BinaryReader r = new BinaryReader(stream);
            int dwMagic = r.ReadInt32();
            if (dwMagic != 0x20534444)
                throw new Exception("This is not a DDS! Magic number wrong.");

            DDS_HEADER header = new DDS_HEADER();
            Read_DDS_HEADER(header, r);

            if (((header.ddspf.dwFlags & 0x00000004) != 0) && (header.ddspf.dwFourCC == 0x30315844 /*DX10*/))
            {
                throw new Exception("DX10 not supported yet!");
            }
		
            Debugger.Break(); // KFreon: Check that V8U8 fourcc is set - figure out what it is
		    if (header.ddspf.dwFourCC != 0)
			    throw new InvalidDataException("DDS not V8U8.");

            int mipMapCount = 1;
            if ((header.dwFlags & 0x00020000) != 0)
                mipMapCount = header.dwMipMapCount;

            int w = 0;
            int h = 0;

            double bytePerPixel = 2;
            MipMaps = new MipMap[mipMapCount];

            // KFreon: Get mips
            for (int i = 0; i < mipMapCount; i++)
            {
                w = (int)(header.dwWidth / Math.Pow(2, i));
                h = (int)(header.dwHeight / Math.Pow(2, i));

                int mipMapBytes = (int)(w * h * bytePerPixel);
                MipMaps[i] = new MipMap(r.ReadBytes(mipMapBytes), w, h);
            }
        
            Width = header.dwWidth;
            Height = header.dwHeight;
	    }

        public byte[] GetImageDataAs3Channel()
        {
            MemoryTributary bitmapStream = new MemoryTributary(Width * Height * 2);
            int ptr = 0;
            byte[] ImageData = MipMaps[0].data;
            using (BinaryWriter bitmapBW = new BinaryWriter(bitmapStream))
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        sbyte red = (sbyte)Buffer.GetByte(ImageData, ptr++);
                        sbyte green = (sbyte)Buffer.GetByte(ImageData, ptr++);
                        byte blue = 0xFF;

                        int fCol = blue | (0x7F + green) << 8 | (0x7F + red) << 16 | 0xFF << 24;
                        bitmapBW.Write(fCol);
                    }
                }

                ImageData = bitmapStream.ToArray();
            }

            return ImageData;
        }

	    public override BitmapImage ToImage(ImageType type = ImageType.Jpg, int quality = 80, int width = 0, int height = 0)
	    {            
            byte[] RawImageData = GetImageDataAs3Channel();

            int stride = (Width * 32 + 7) / 8;
                
            BitmapSource bmpsource = BitmapImage.Create(Width, Height, 96, 96, PixelFormats.Bgr32, BitmapPalettes.Halftone125, RawImageData, stride);
            return UsefulThings.WPF.Images.CreateWPFBitmap(bmpsource, width, height);
	    }


        private byte[] BuildMip(byte[] data, int width, int height, int scaleMultiplier)
        {
            List<byte> nextMip = new List<byte>();

            // KFreon: Build single mipmap of 
            for (int h = 0; h < height; h++)
            {
                if (h % scaleMultiplier != 0)
                    continue;

                for (int w = 0; w < width; w++)
                {
                    if (w % scaleMultiplier != 0)
                        continue;

                    int heightOffset = (width * scaleMultiplier * h) + (h == 0 ? 0 : 1);
                    int widthOffset = scaleMultiplier * w;
                    int r = heightOffset + widthOffset;
                    int b = heightOffset + widthOffset + 1;
                    nextMip.Add(data[r]);
                    nextMip.Add(data[b]);
                }
            }

            return nextMip.ToArray();
        }

        public override bool BuildMipMaps(bool rebuild = false)
        {
            bool success = false;

            MipMap LastMip = MipMaps.Last();

            int width = LastMip.width;
            int height = LastMip.height;

            Debug.WriteLine("Building V8U8 Mips with starting MIP size of {0} x {1}.", width, height);
            List<MipMap> newMips = new List<MipMap>();
            foreach (var mip in MipMaps)
                newMips.Add(mip);

            try
            {

                byte[] previousMipData = LastMip.data;
                while (width > 1 && height > 1)
                {
                    // KFreon: Need original height to resize. Original = previous, but initially it's the original height.
                    byte[] newMipData = BuildMip(previousMipData, width, height, 2);

                    // KFreon: Adjust to new dimensions for generating the new mipmap
                    height /= 2;
                    width /= 2;

                    newMips.Add(new MipMap(newMipData, width, height));

                    previousMipData = newMipData;
                }

                MipMaps = newMips.ToArray();
                success = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return success;
        }
	
	
	    #region Reading/Writing V8U8 File
	    /// <summary>
        /// Builds a V8U8 DDS header from certain details.
        /// </summary>
        /// <param name="Mips">Number of mipmaps.</param>
        /// <param name="height">Height of image.</param>
        /// <param name="width">Width of image.</param>
        /// <returns>DDS_HEADER struct from details.</returns>
        private static DDS_HEADER Build_V8U8_DDS_Header(int Mips, int height, int width)
        {
            DDS_HEADER header = new DDS_HEADER();
            header.dwSize = 124;
            header.dwFlags = 0x1 | 0x2 | 0x4 | 0x1000 | (Mips != 0 ? 0x20000 : 0x0);  // Flags to denote valid fields: DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PIXELFORMAT | DDSD_MIPMAPCOUNT
            header.dwWidth = width;
            header.dwHeight = height;
            header.dwCaps = 0x1000 | 0x8 | (Mips == 0 ? 0 : 0x400000);
            header.dwMipMapCount = Mips == 0 ? 1 : Mips;
            //header.dwPitchOrLinearSize = ((width + 1) >> 1)*4;

            DDS_PIXELFORMAT px = new DDS_PIXELFORMAT();
            px.dwSize = 32;
            //px.dwFlags = 0x200;
            px.dwFlags = 0x80000;
            px.dwRGBBitCount = 16;
            px.dwRBitMask = 255;
            px.dwGBitMask = 0x0000FF00;

            header.ddspf = px;
            return header;
        }
	
	    /// <summary>
        /// Writes V8U8 DDS header to binary stream.
        /// </summary>
        /// <param name="header">Header to write.</param>
        /// <param name="writer">BinaryWriter wrapped stream to write to.</param>
        private static void Write_V8U8_DDS_Header(DDS_HEADER header, BinaryWriter writer)
        {
            // KFreon: Write magic number ("DDS")
            writer.Write(0x20534444);

            // KFreon: Write all header fields regardless of filled or not
            writer.Write(header.dwSize);
            writer.Write(header.dwFlags);
            writer.Write(header.dwHeight);
            writer.Write(header.dwWidth);
            writer.Write(header.dwPitchOrLinearSize);
            writer.Write(header.dwDepth);
            writer.Write(header.dwMipMapCount);

            // KFreon: Write reserved1
            for (int i = 0; i < 11; i++)
                writer.Write(0);

            // KFreon: Write PIXELFORMAT
            DDS_PIXELFORMAT px = header.ddspf;
            writer.Write(px.dwSize);
            writer.Write(px.dwFlags);
            writer.Write(px.dwFourCC);
            writer.Write(px.dwRGBBitCount);
            writer.Write(px.dwRBitMask);
            writer.Write(px.dwGBitMask);
            writer.Write(px.dwBBitMask);
            writer.Write(px.dwABitMask);

            writer.Write(header.dwCaps);
            writer.Write(header.dwCaps2);
            writer.Write(header.dwCaps3);
            writer.Write(header.dwCaps4);
            writer.Write(header.dwReserved2);
        }


        /// <summary>
        /// Writes V8U8 image to file. Returns true if successful.
        /// </summary>
        /// <param name="mipmaps">V8U8 mips as List.</param>
        /// <param name="savepath">Path to save to.</param>
        /// <param name="height">Height of image.</param>
        /// <param name="width">Width of image.</param>
        /// <param name="Mips">Number of mips in image.</param>
        private static bool WriteV8U8(IEnumerable<MipMap> mipmaps, string savepath, int height, int width, int Mips)
        {
            FileStream fs = new FileStream(savepath, FileMode.CreateNew);
            return WriteV8U8ToStream(mipmaps, fs, width, height, Mips, true);
        }

        private static bool WriteV8U8ToStream(IEnumerable<MipMap> mipmaps, Stream destination, int width, int height, int Mips, bool DisposeStream)
        {
            DDS_HEADER header = Build_V8U8_DDS_Header(Mips, height, width);

            try
            {
                using (BinaryWriter writer = new BinaryWriter(destination, Encoding.Default, !DisposeStream))
                {
                    // KFreon: Get and write header
                    Write_V8U8_DDS_Header(header, writer);

                    foreach (MipMap mip in mipmaps)
                        foreach (sbyte byt in mip.data)
                            writer.Write(byt);
                }

                return true;
            }
            catch (IOException e)
            {
                Debug.WriteLine("Error writing to file: " + e.Message);
            }
            return false;
        }
	    #endregion Reading/Writing V8U8 File
	
	    public void Dispose()
	    {
		    // don't really need it here
	    }

        

        private const int FOURCC_DXT1 = 0x31545844;
        private const int FOURCC_DX10 = 0x30315844;
        private const int FOURCC_DXT5 = 0x35545844;
        private const int FOURCC_ATI2 = 0x32495441;
        //private const int FOURCC_V8U8 = 117;
        /*public enum DDSFormat
        {
            DXT1, DXT3, DXT5, V8U8, ATI2, G8, ARGB
        }*/

        public override byte[] ToArray()
        {
            if (MipMaps == null || MipMaps.Length == 0)
                return null;
            
            return MipMaps[0].data;
        }

        

        public override bool RemoveMipMaps(bool force = false)
        {
            if (MipMaps == null || MipMaps.Length == 0)
                return false;
            
            MipMaps = new MipMap[] { MipMaps[0] };
            return true;
        }

        public override bool ConvertAndSave(ImageType type, string savePath, ResILImageBase.MipMapMode MipsMode = MipMapMode.BuildAll, CompressedDataFormat surface = CompressedDataFormat.None, int quality = 80, bool SetJPGQuality = true)
        {
            using (FileStream fs = new FileStream(savePath, FileMode.CreateNew))
                return ConvertAndSave(type, fs, MipsMode, surface, quality, SetJPGQuality);
        }

        public override bool ConvertAndSave(ImageType type, Stream stream, ResILImageBase.MipMapMode MipsMode = MipMapMode.BuildAll, CompressedDataFormat surface = CompressedDataFormat.None, int quality = 80, bool SetJPGQuality = true)
        {
            // KFreon: If converting to something other than V8U8...
            if (surface != SurfaceFormat)
            {
                byte[] RawImageData = GetImageDataAs3Channel(); // KFreon: Get image data as raw rgb pixels
                using (ResILImage img = new ResILImage(RawImageData))
                    return img.ConvertAndSave(type, stream, MipsMode, surface, quality, SetJPGQuality);
            }
            else
            {
                // KFreon: Deal with mips first
                int expectedMips = EstimateNumMips(Width, Height);
                Debugger.Break(); // KFreon: Check that estimation is correct
                bool success = true;
                switch (MipsMode)
                {
                    case MipMapMode.BuildAll:
                        if (expectedMips != Mips)
                            success = BuildMipMaps();
                        break;
                    case MipMapMode.Rebuild:
                        // KFreon: Remove existing mips before building them again
                        if (!RemoveMipMaps())
                            success = false;
                        else
                            success = BuildMipMaps();
                        break;
                    case MipMapMode.ForceRemove:
                    case MipMapMode.RemoveAllButOne:
                        success = RemoveMipMaps();
                        break;
                }

                if (!success)
                {
                    Debug.WriteLine("Failed to fix mipmaps.");
                    return false;
                }

                // KFreon: Build formatting and write out to file
                return WriteV8U8ToStream(MipMaps, stream, Height, Width, Mips, false);
            }
        }

        public override bool Resize(int width, int height)
        {
            throw new NotImplementedException();
        }
    }
}
