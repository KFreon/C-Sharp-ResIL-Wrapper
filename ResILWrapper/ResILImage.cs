using ResIL.Unmanaged;
using ResILWrapper.V8U8Stuff;
using ResILWrapper.V8U8Stuff.AmaroK86Stuff;
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
    /// <summary>
    /// Provides object-oriented/C# way of interacting with ResIL images.
    /// </summary>
    public class ResILImage : IDisposable
    {
        public readonly static List<string> ValidFormats = new List<string> { "DXT1", "DXT3", "DXT5", "3DC", "ATI2N", "V8U8", "JPG", "PNG", "BMP", "GIF" };

        #region Image Properties
        public string Path { get; private set; }
        public CompressedDataFormat SurfaceFormat { get; private set; }
        public string SurfaceFormatString
        {
            get
            {
                string form = Enum.GetName(typeof(CompressedDataFormat), SurfaceFormat);
                return form == "None" ? System.IO.Path.GetExtension(Path) : form;
            }
        }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int BitsPerPixel { get; private set; }
        public DataFormat MemoryFormat { get; private set; }  // KFreon: Format as loaded by ResIL
        public string MemoryFormatString
        {
            get
            {
                return Enum.GetName(typeof(DataFormat), MemoryFormat);
            }
        }
        public int Channels { get; private set; }
        public int DataSize { get; private set; }
        public DataType DataType { get; private set; }
        public string DataTypeString
        {
            get
            {
                return Enum.GetName(typeof(DataType), DataType);
            }
        }
        private bool isV8U8
        {
            get
            {
                return V8U8Mips != null && V8U8Mips.Count() < 0;
            }
        }

        int mips = -1;
        public int Mips
        {
            get
            {
                if (V8U8Mips != null)
                    return V8U8Mips.Count();
                else
                    return mips;
            }
            private set
            {
                mips = value;
            }
        }

        public MipMap[] V8U8Mips;
        IntPtr handle = IL2.GenerateImage();
        public bool ValidHandle
        {
            get
            {
                return handle != IntPtr.Zero;
            }
        }
        #endregion


        public ResILImage(string FilePath)
        {
            Path = FilePath;
            LoadImage(FilePath);
            PopulateInfo();
        }

        public ResILImage(MemoryTributary stream)
        {
            Path = null;
            LoadImage(stream);
            PopulateInfo();
        }

        public ResILImage(byte[] imgData, ImageType type = ImageType.Bmp)
        {
            Path = null;
            LoadImage(imgData, type);
            PopulateInfo();
        }
        

        /// <summary>
        /// Populates image information from the ILImage*.
        /// </summary>
        public unsafe void PopulateInfo()
        {
            // KFreon: Pointer to ILImage struct
            uint* BasePointer = (uint*)handle.ToPointer();

            // KFreon: Get information and count mipmaps (recursive rubbish)
            Mips = CountMipsAndGetDetails(BasePointer);
        }


        /// <summary>
        /// Reads ILImage struct and is used recursively to count mipmaps.
        /// </summary>
        /// <param name="BasePointer">Pointer to ILImage struct.</param>
        /// <param name="toplevel">OPTIONAL: True only when first called. All recursive stuff is False.</param>
        /// <returns>Number of mipmaps.</returns>
        private unsafe int CountMipsAndGetDetails(uint* BasePointer, bool toplevel = true)
        {
            int mipcount = 1;
            byte* BytePtr = (byte*)BasePointer; 

            // KFreon: Read image dimensions
            int W = (int)*(BasePointer++);
            int H = (int)*(BasePointer++);
            BasePointer++;  // KFreon: Ignore depth

            // KFreon: Move to byte*
            BytePtr = (byte*)BasePointer;

            // KFreon: Read Bits per pixel and number of channels
            int BPP = *(BytePtr++);
            int Chan = *(BytePtr++);

            BytePtr += 2; // KFreon: Skip data 

            // KFreon: I am aware I could stay in byte* land to skip the following, but I wanted to keep some structure so I know what's going on.

            BasePointer = (uint*)BytePtr;   // KFreon: Back to uint*
            BasePointer++;   // KFreon: Skip scanline stuff

            BytePtr = (byte*)BasePointer;  // KFreon: To byte*
            BytePtr += 12;  // KFreon: Skip bunches of stuff

            BasePointer = (uint*)BytePtr;  // KFreon: Back to uint*

            // KFreon: Read Datasize, format in memory, and type of data.
            int datasiz = (int)*(BasePointer++);
            BasePointer++;  // KFreon: Skip planesize
            DataFormat memform = (DataFormat)(*(BasePointer++));
            DataType datatyp = (DataType)(*(BasePointer++));

            // KFreon: Address before checking mips with offset for skipping everything in between
            uint* before = BasePointer + 29;
            BytePtr = (byte*)before;
            BytePtr--;
            before = (uint*)BytePtr;



            #region CHECK MIPS
            BasePointer++;  // KFreon: Skip origin
            BasePointer += 13; // KFreon: Skip palette


            byte** ptr = (byte**)BasePointer;  // KFreon: Dunno if all this is necessary. I don't do pointer stuff much.
            var th = (long*)(*ptr);

            // KFreon: If there's a mip left, read it and increment mip count recursivly (Ugh...)
            if (th != (uint*)0)
                mipcount += CountMipsAndGetDetails((uint*)th, false);
            #endregion



            // KFreon: Reset pointer position
            BasePointer = before;

            BasePointer += 2;  // KFreon: Ignore image offsets
            BasePointer += 3; // KFreon: Ignore data

            byte* t = (byte*)BasePointer;
            t++;
            BasePointer = (uint*)t;

            CompressedDataFormat surface = (CompressedDataFormat)(*(BasePointer++));


            // KFreon: Set original image properties
            if (toplevel)
            {
                Width = W;
                Height = H;
                BitsPerPixel = BPP;
                Channels = Chan;
                DataSize = datasiz;
                MemoryFormat = memform;
                DataType = datatyp;
                SurfaceFormat = surface;
            }

            return mipcount;
        }


        /// <summary>
        /// Load image from file.
        /// </summary>
        /// <param name="FilePath">Path to image file.</param>
        private bool LoadImage(string FilePath)
        {
            bool isNormal = false;

            // KFreon: If V8U8, use correct load function.
            if (CheckIfV8U8(FilePath) == true)
            {
                SurfaceFormat = CompressedDataFormat.V8U8;
                LoadV8U8(FilePath);
                isNormal = true;
            }
            else
            {
                IL2.Settings.KeepDXTC(true);
                IL2.LoadImage(ref handle, FilePath);
            }
            return isNormal;
        }

        private bool LoadImage(byte[] data, ImageType type = ImageType.Bmp)
        {
            bool isNormal = false;
            // KFreon: If V8U8, use correct load function.
            MemoryTributary stream = new MemoryTributary(data);
            if (CheckIfV8U8(data: stream) == true)
            {
                SurfaceFormat = CompressedDataFormat.V8U8;
                LoadV8U8(stream);
                isNormal = true;
                stream.Dispose();
            }
            else
            {
                stream.Dispose();
                IL2.Settings.KeepDXTC(true);
                if (!IL2.LoadImageFromArray(ref handle, data, type))
                {
                    Debug.WriteLine("Loading image failed for some reason");
                    Debug.WriteLine(Enum.GetName(typeof(ErrorType), IL2.GetError()));
                }
            }
            return isNormal;
        }

        private bool LoadImage(MemoryTributary data)
        {
            bool isNormal = false;
            // KFreon: If V8U8, use correct load function.
            if (CheckIfV8U8(data: data) == true)
            {
                SurfaceFormat = CompressedDataFormat.V8U8;
                LoadV8U8(data);
                isNormal = true;
            }
            else
            {
                IL2.Settings.KeepDXTC(true);
                IL2.LoadImageFromStream(ref handle, data);
            }
            return isNormal;
        }

        /// <summary>
        /// Converts this image to WPF bitmap.
        /// </summary>
        /// <param name="type">OPTIONAL: Type of image to create. Default is JPG.</param>
        /// <param name="quality">OPTIONAL: Quality of JPG image. Valid only if type is JPG. Range 0-100. Default is 80.</param>
        /// <returns></returns>
        public BitmapImage ToImage(ImageType type = ImageType.Jpg, int quality = 80, int width = 0)
        {
            byte[] data = null;

            // KFreon: Set JPG quality if necessary
            if (type == ImageType.Jpg)
                ResIL.Settings.SetJPGQuality(quality);

            // KFreon: Save to array and build WPF bitmap.
            if (IL2.SaveToArray(handle, type, out data) != 0)
                return UsefulThings.WPF.Images.CreateWPFBitmap(data, width);
            else
            {
                Debug.WriteLine("Saving to array failed for some reason.");
                Debug.WriteLine(Enum.GetName(typeof(ErrorType), IL2.GetError()));
                return null;
            }
        }


        public byte[] ToArray(ImageType type)
        {
            byte[] data = null;
            Debugger.Break();
            if (IL2.SaveToArray(handle, type, out data) != 0)
                return data;
            else
            {
                Debug.WriteLine("To Array failed for some reason.");
                Debug.WriteLine(Enum.GetName(typeof(ErrorType), IL2.GetError()));
            }
            return null;
        }


        #region Static methods
        /// <summary>
        /// Decides on an extension based on a format string. Returns extension with a dot, or null if invalid format.
        /// </summary>
        /// <param name="format">Format string to decide on.</param>
        /// <returns>Returns extension with a dot, or null if invalid format.</returns>
        public static string GetExtensionFromFormat(string format)
        {
            string ext = null;

            // KFreon: Check if format is valid
            if (!isValidFormat(format))
                ext = null;

            // KFreon: Get a format
            if (format.Contains("DXT") || format == "3Dc" || format == "ATI2N" || format == "V8U8")
                ext = ".DDS";
            else
            {
                if (!format.Contains('.'))
                    ext = '.' + format;
                else
                    ext = format;
            }
            return ext;
        }

        /// <summary>
        /// Checks if given format is valid.
        /// </summary>
        /// <param name="format">Format to check.</param>
        /// <returns>True if valid.</returns>
        public static bool isValidFormat(string format)
        {
            return ValidFormats.Contains(format.ToUpperInvariant());
        }


        /// <summary>
        /// Gets a filter string from extension for use in Open and Save dialogues
        /// </summary>
        /// <param name="ext"></param>
        /// <returns></returns>
        public static string GetFilterString(string ext)
        {
            string retval = "";

            switch (ext.ToLowerInvariant())
            {
                case ".dds":
                    retval = "DirectX Images|*.dds";
                    break;
                case ".jpg":
                    retval = "JPEG Images|*.jpg";
                    break;
                case ".bmp":
                    retval = "Bitmap Images|*.bmp";
                    break;
                case ".png":
                    retval = "Portable Network Graphics|*.png";
                    break;
                case ".gif":
                    retval = "Graphics Interchange Format|*.gif";
                    break;
            }

            return retval;
        }
        #endregion

        // KFreon: Most of this stuff is from DDSImage.cs from this project, and the original at http://code.google.com/p/kprojects/
        #region V8U8 Stuff
        /// <summary>
        /// Checks if current working image is a V8U8 NormalMap image.
        /// </summary>
        /// <param name="file">OPTIONAL: Path to image file. DEFAULT = null, data MUST NOT be null.</param>
        /// <param name="data">OPTIONAL: Raw image data. DEFAULT = null, file MUST NOT be null.</param>
        /// <returns>True if V8U8, False if not V8U8, null if invalid parameters provided.</returns>
        private static bool? CheckIfV8U8(string file = null, MemoryTributary data = null)
        {
            // KFreon: Check if everything is all good. One parameter MUST NOT be null.
            if (data == null && file == null)
                return null;

            bool retval = false;
            try
            {
                // KFreon: Check format
                SaltisgoodDDSPreview dds = new SaltisgoodDDSPreview(data == null ? new MemoryTributary(File.ReadAllBytes(file)) : data);
                retval = dds.FormatString == "V8U8";
            }
            catch
            {
                // Ignore cos return is already false
            }
            return retval;
        }

        /// <summary>
        /// Reads DDS header from file.
        /// </summary>
        /// <param name="h">Header struct.</param>
        /// <param name="r">File reader.</param>
        private static void Read_DDS_HEADER(DDS_HEADER h, BinaryReader r)
        {
            h.dwSize = r.ReadInt32();
            h.dwFlags = r.ReadInt32();
            h.dwHeight = r.ReadInt32();
            h.dwWidth = r.ReadInt32();
            h.dwPitchOrLinearSize = r.ReadInt32();
            h.dwDepth = r.ReadInt32();
            h.dwMipMapCount = r.ReadInt32();
            for (int i = 0; i < 11; ++i)
            {
                h.dwReserved1[i] = r.ReadInt32();
            }
            Read_DDS_PIXELFORMAT(h.ddspf, r);
            h.dwCaps = r.ReadInt32();
            h.dwCaps2 = r.ReadInt32();
            h.dwCaps3 = r.ReadInt32();
            h.dwCaps4 = r.ReadInt32();
            h.dwReserved2 = r.ReadInt32();
        }


        /// <summary>
        /// Reads DDS pixel format.
        /// </summary>
        /// <param name="p">Pixel format struct.</param>
        /// <param name="r">File reader.</param>
        private static void Read_DDS_PIXELFORMAT(DDS_PIXELFORMAT p, BinaryReader r)
        {
            p.dwSize = r.ReadInt32();
            p.dwFlags = r.ReadInt32();
            p.dwFourCC = r.ReadInt32();
            p.dwRGBBitCount = r.ReadInt32();
            p.dwRBitMask = r.ReadInt32();
            p.dwGBitMask = r.ReadInt32();
            p.dwBBitMask = r.ReadInt32();
            p.dwABitMask = r.ReadInt32();
        }


        /// <summary>
        /// Setup V8U8 properties and headers from filename.
        /// </summary>
        /// <param name="ddsFileName">Filename of V8U8 image.</param>
        private void SetupV8U8(string ddsFileName)
        {
            using (FileStream ddsStream = File.OpenRead(ddsFileName))
                SetupV8U8(ddsStream);
        }


        /// <summary>
        /// Setup V8U8 properties and headers from stream. This is modified from AmaroK86's DDS code.
        /// </summary>
        /// <param name="ddsStream">Stream containing V8U8 data.</param>
        private void SetupV8U8(Stream ddsStream)
        {
            ddsStream.Seek(0, SeekOrigin.Begin);
            BinaryReader r = new BinaryReader(ddsStream);
            int dwMagic = r.ReadInt32();
            if (dwMagic != 0x20534444)
                throw new Exception("This is not a DDS!");

            DDS_HEADER header = new DDS_HEADER();
            Read_DDS_HEADER(header, r);

            if (((header.ddspf.dwFlags & 0x00000004) != 0) && (header.ddspf.dwFourCC == 0x30315844 /*DX10*/))
            {
                throw new Exception("DX10 not supported yet!");
            }

            int mipMapCount = 1;
            if ((header.dwFlags & 0x00020000) != 0)
                mipMapCount = header.dwMipMapCount;

            int w = 0;
            int h = 0;

            double bytePerPixel = 2;
            V8U8Mips = new MipMap[mipMapCount];

            // KFreon: Get mips
            for (int i = 0; i < mipMapCount; i++)
            {
                w = (int)(header.dwWidth / Math.Pow(2, i));
                h = (int)(header.dwHeight / Math.Pow(2, i));

                // KFreon: Set max image size
                if (i == 0)
                {
                    Width = w;
                    Height = h;
                }

                int mipMapBytes = (int)(w * h * bytePerPixel);
                V8U8Mips[i] = new MipMap(r.ReadBytes(mipMapBytes), DDSFormat.V8U8, w, h);
            }
        }


        /// <summary>
        /// V8U8 mipmap
        /// </summary>
        public class MipMap
        {
            public int width;
            public int height;
            DDSFormat ddsFormat;
            private byte[] _data;
            public byte[] data
            {
                get
                {
                    return _data;
                }
                set
                {
                    _data = value;
                }
            }


            public MipMap(byte[] data, DDSFormat format, int w, int h)
            {
                long requiredSize = (long)(w * h * 2);
                if (data.Length != requiredSize)
                    throw new InvalidDataException("Data size is not valid for selected format.\nActual: " + data.Length + " bytes\nRequired: " + requiredSize + " bytes");

                this.data = data;
                ddsFormat = format;
                width = w;
                height = h;
            }
        }


        /// <summary>
        /// Builds a V8U8 DDS header from certain details.
        /// </summary>
        /// <param name="Mips">Number of mipmaps.</param>
        /// <param name="height">Height of image.</param>
        /// <param name="width">Width of image.</param>
        /// <returns>DDS_HEADER struct from details.</returns>
        private static DDS_HEADER Get_V8U8_DDS_Header(int Mips, int height, int width)
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
        /// Read largest V8U8 mipmap into ResIL.
        /// </summary>
        /// <returns>True if success.</returns>
        private bool ReadV8U8()
        {
            MemoryTributary bitmapStream = new MemoryTributary(Width * Height * 2);
            BitmapSource bmp = null;
            using (BinaryWriter bitmapBW = new BinaryWriter(bitmapStream))
            {
                int ptr = 0;
                byte[] ImageData = V8U8Mips[0].data;
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

                int stride = (Width * 32 + 7) / 8;

                bmp = BitmapImage.Create(Width, Height, 96, 96, PixelFormats.Bgr32, BitmapPalettes.Halftone125, bitmapStream.ToArray(), stride);
            }


            // KFreon: Format stuff into bmp format, then load it into ResIL
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            MemoryTributary ms = new MemoryTributary();
            encoder.Save(ms);

            return IL2.LoadImageFromStream(ref handle, ms);
        }


        /// <summary>
        /// Writes V8U8 image to file.
        /// </summary>
        /// <param name="bytes">V8U8 data as List.</param>
        /// <param name="savepath">Path to save to.</param>
        /// <param name="height">Height of image.</param>
        /// <param name="width">Width of image.</param>
        /// <param name="Mips">Number of mips in image.</param>
        private static void WriteV8U8(List<sbyte> bytes, string savepath, int height, int width, int Mips)
        {
            DDS_HEADER header = Get_V8U8_DDS_Header(0, height, width);
            using (FileStream fs = new FileStream(savepath, FileMode.CreateNew))
            {
                using (BinaryWriter writer = new BinaryWriter(fs))
                {
                    // KFreon: Get and write header
                    Write_V8U8_DDS_Header(header, writer);

                    foreach (sbyte byt in bytes)
                        writer.Write(byt);
                }
            }
        }


        /// <summary>
        /// Load V8U8 image from file.
        /// </summary>
        /// <param name="filename">File to load from.</param>
        private void LoadV8U8(string filename)
        {
            SetupV8U8(filename);
            ReadV8U8();
        }


        /// <summary>
        /// Load V8U8 image from stream.
        /// </summary>
        /// <param name="stream">Stream to load from.</param>
        private void LoadV8U8(Stream stream)
        {
            SetupV8U8(stream);
            ReadV8U8();
        }
        #endregion


        #region Manipulation
        /// <summary>
        /// Convert image to different types and save to path.
        /// </summary>
        /// <param name="type">Type of image to save as.</param>
        /// <param name="savePath">Path to save to.</param>
        /// <param name="surface">OPTIONAL: DDS Surface format to change to. Valid only if type is DDS.</param>
        /// <returns>True if success.</returns>
        public bool ConvertAndSave(ImageType type, string savePath, CompressedDataFormat surface = CompressedDataFormat.None, int quality = 80, bool SetJPGQuality = true)
        {
            // KFreon: Set JPG quality if necessary
            if (SetJPGQuality && type == ImageType.Jpg)
                ResIL.Settings.SetJPGQuality(quality);


            ChangeSurface(type, surface);

            // KFreon: Save image
            return IL2.SaveImage(handle, savePath, type);
        }


        public bool ConvertAndSave(ImageType type, MemoryTributary stream, CompressedDataFormat surface = CompressedDataFormat.None, int quality = 80, bool SetJPGQuality = true)
        {
            // KFreon: Set JPG quality if necessary
            if (SetJPGQuality && type == ImageType.Jpg)
                ResIL.Settings.SetJPGQuality(quality);

            ChangeSurface(type, surface);

            // KFreon: Save image
            return IL2.SaveImageAsStream(handle, type, stream);
        }

        private void ChangeSurface(ImageType type, CompressedDataFormat surface)
        {
            // KFreon: Change surface format of DDS's
            if (type == ImageType.Dds && surface != CompressedDataFormat.None)
                IL2.Settings.SetDXTcFormat(surface);
        }


        /// <summary>
        /// Resizes image in ResIL. This is permenant in memory. On disk will not be altered.
        /// </summary>
        /// <param name="width">Width of image.</param>
        /// <param name="height">Height of image.</param>
        /// <returns>True if success.</returns>
        public bool Resize(int width, int height)
        {
            return ILU2.ResizeImage(handle, (uint)width, (uint)height, (byte)BitsPerPixel, (byte)Channels);
        }
        #endregion

        public void Dispose()
        {
            if (handle == IntPtr.Zero)
                Debug.WriteLine("Image already deleted.");
            else
            {
                try
                {
                    if (V8U8Mips != null)
                        V8U8Mips = null;
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Unable to clear V8U8 Mips array: " + e.Message);
                }

                try
                {
                    // KFreon: Delete image from ResIL
                    if (!IL2.DeleteImage(handle))
                        Debug.WriteLine("Image already deleted.");
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Failed to delete image from ResIL: " + e.Message);
                }

                // KFreon: Reset file handle
                handle = IntPtr.Zero;

                GC.SuppressFinalize(this);
            }
        }


        /// <summary> 
        /// KFreon: Destructor just calls dispose. Dispose shouldn't fail regardless of number of times called.
        /// DON'T RELY ON THIS. Use the Dispose pattern, using and all that stuff.
        /// </summary>
        ~ResILImage()
        {
            Dispose();
        }
    }
}
