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
        public static List<string> ValidFormats = null;

        #region Image Properties
        public string Path { get; private set; }
        public CompressedDataFormat SurfaceFormat { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int BitsPerPixel { get; private set; }
        public DataFormat MemoryFormat { get; private set; }  // KFreon: Format as loaded by ResIL (Usually RGB)
        public int Channels { get; private set; }
        public int DataSize { get; private set; }
        public DataType DataType { get; private set; }
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

        public ImageType imgType { get; set; }

        static ResILImage()
        {
            string[] types = Enum.GetNames(typeof(ResIL.Unmanaged.ImageType));
            ValidFormats = new List<string>() { "None", "DXT1", "DXT2", "DXT3", "DXT4", "DXT5", "3Dc/ATI2", "RXGB", "ATI1N/BC4", "DXT1A", "V8U8" }; // KFreon: DDS Surface formats
            ValidFormats.AddRange(types.Where(t => t != "Dds" && t != "Unknown").ToList()); // KFreon: Remove DDS from types list

        }

        public ResILImage(string FilePath)
        {
            Path = FilePath;
            Debugger.Break();
            imgType = DetermineType(ImageType.Unknown, UsefulThings.General.GetExternalData(FilePath));
            LoadImage(FilePath);
            PopulateInfo();
        }

        public ResILImage(MemoryTributary stream)
        {
            Path = null;
            imgType = DetermineType(ImageType.Unknown, stream.ToArray());
            LoadImage(stream);
            PopulateInfo();
        }

        public ResILImage(byte[] imgData, ImageType type = ImageType.Bmp)
        {
            Path = null;
            imgType = DetermineType(type, imgData);
            LoadImage(imgData, imgType);
            PopulateInfo();
        }


        private ImageType DetermineType(ImageType type, byte[] imgData)
        {
            // KFreon: Attempt to determine type unless provided
            ImageType GivenType = type;

            if (type == ImageType.Bmp || type == ImageType.Unknown)
                GivenType = IL2.DetermineImageType(imgData);

            if (GivenType == ImageType.Unknown)
                GivenType = type;

            return GivenType;
        }

        private ImageType DetermineType(ImageType type, string filename)
        {
            return DetermineType(type, UsefulThings.General.GetExternalData(filename));
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
        /// Reads ILImage struct information and populates relevent fields.
        /// Recursively (ew...) counts mips. Returns number of mips.
        /// </summary>
        /// <param name="BasePointer">Pointer to ILImage struct.</param>
        /// <param name="toplevel">True only when first called. All recursive stuff is False.</param>
        /// <returns>Number of mipmaps.</returns>
        private unsafe int CountMipsAndGetDetails(uint* BasePointer, bool toplevel = true)
        {
            int mipcount = 1;

            // KFreon: Read image dimensions
            int W = (int)*BasePointer++;
            int H = (int)*BasePointer++;
            int Depth = (int)*BasePointer++;


            // KFreon: Go to byte* land.
            byte* bytePtr = (byte*)BasePointer;
            uint BPP = *bytePtr++;
            int BitsPerChannel = *bytePtr++;
            bytePtr+=2;  // KFreon: Fix alignment

            // KFreon: Go back to uint* land.
            BasePointer = (uint*)bytePtr;
            uint BitsPerPScanLine = (uint)*BasePointer++;

            if (IntPtr.Size == 8)  // KFreon: x64 alignment?
                BasePointer++;

            // KFreon: Round and round the mulberry bush...
            uint* dataPtr = (uint*) (BasePointer += (IntPtr.Size/4)); 

            int datasize = (int)*BasePointer++;
            int PlaneSize = (int)*BasePointer++;
            DataFormat memform = (DataFormat)(*BasePointer++);
            DataType datatyp = (DataType)(*BasePointer++);
            OriginLocation origin = (OriginLocation)(*BasePointer++);

            if (IntPtr.Size == 8)  // KFreon: x64 alignment?
                BasePointer++;

            // KFreon: Skip palette
            BasePointer += 7; // x86 = 1C, x64 = 28

            if (IntPtr.Size == 8)  // KFreon: Alignment or no idea...
                BasePointer += 3;

            int duration = (int)*(BasePointer++);
            CubeMapFace cubeflags = (CubeMapFace)(*BasePointer++);


            #region CHECK MIPS
            long* th = (long*)BasePointer;
            th = (long*)*th;

            // KFreon: If there's a mip left, read it and increment mip count recursivly (Ugh...)
            if (th != (uint*)0)
                mipcount += CountMipsAndGetDetails((uint*)th, false);
            #endregion

            BasePointer++; // KFreon: Alignment?

            if (IntPtr.Size == 8)  // KFreon: x64 alignment?
                BasePointer++;

            int Next = (int)*BasePointer++;
            int Faces = (int)*BasePointer++;
            int Layers = (int)*BasePointer++;
            int animlist = (int)*BasePointer++;
            int animsize = (int)*BasePointer++;
            int profile = (int)*BasePointer++;
            int profilesize = (int)*BasePointer++;
            int offx = (int)*BasePointer++;
            int offy = (int)*BasePointer++;


            // KFreon: Imma byte this pointer
            bytePtr = (byte*)BasePointer;
            int dxtcdataPtr = (int)*bytePtr++;
            bytePtr += 3; // KFreon: Alignment


            // KFreon: Uint gonna byte me!
            BasePointer = (uint*)bytePtr;

            if (IntPtr.Size == 8)  // KFreon: uhh....
                BasePointer += 8;

            CompressedDataFormat surface = (CompressedDataFormat)(*BasePointer++);
            int dxtcsize = (int)*BasePointer++;

            // KFreon: Set original image properties
            if (toplevel)
            {
                Width = W;
                Height = H;
                BitsPerPixel = (int)BPP;
                Channels = BitsPerChannel;
                DataSize = datasize;
                MemoryFormat = memform;
                DataType = datatyp;

                // KFreon: Trump ResIL's format reading of V8U8 (gets it wrong). It's been set previously.
                if (SurfaceFormat != CompressedDataFormat.V8U8)
                    SurfaceFormat = surface;
            }

            return mipcount;
        }


        /// <summary>
        /// Load image from file. Returns true if successful.
        /// </summary>
        /// <param name="FilePath">Path to image file.</param>
        private bool LoadImage(string FilePath)
        {
            bool isNormalMap = false;

            // KFreon: If V8U8, use correct load function.
            if (CheckIfV8U8(FilePath) == true)
            {
                SurfaceFormat = CompressedDataFormat.V8U8;
                LoadV8U8(FilePath);
                isNormalMap = true;
            }
            else
            {
                IL2.Settings.KeepDXTC(true);
                if (!IL2.LoadImage(ref handle, FilePath))
                {
                    Debug.WriteLine("Loading from file failed for some reason.");
                    //Debug.WriteLine(GET ERROR FUNCTION);
                }
            }
            return isNormalMap;
        }

        /// <summary>
        /// Load image from byte[]. Returns true if successful.
        /// </summary>
        /// <param name="data">Data of image file, NOT raw pixel data.</param>
        /// <param name="type">Type of data (format etc jpg, dds, etc)</param>
        private bool LoadImage(byte[] data, ImageType type = ImageType.Bmp)
        {
            bool isNormalMap = false;
            
            // KFreon: If V8U8, use correct load function.
            MemoryTributary stream = new MemoryTributary(data);
            if (CheckIfV8U8(data: stream) == true)
            {
                SurfaceFormat = CompressedDataFormat.V8U8;
                LoadV8U8(stream);
                isNormalMap = true;
                stream.Dispose();
            }
            else
            {
                stream.Dispose(); // KFreon: Don't need stream anymore
                IL2.Settings.KeepDXTC(true);
                if (!IL2.LoadImageFromArray(ref handle, data, type))
                {
                    Debug.WriteLine("Loading image failed for some reason");
                    Debug.WriteLine(Enum.GetName(typeof(ErrorType), IL2.GetError()));
                }
            }
            return isNormalMap;
        }


        /// <summary>
        /// Load image from MemoryStream. Returns true if successful.
        /// </summary>
        /// <param name="data">Data of image file, NOT raw pixel data.</param>asf
        private bool LoadImage(MemoryTributary data)
        {
            bool isNormalMap = false;
            // KFreon: If V8U8, use correct load function.
            if (CheckIfV8U8(data: data) == true)
            {
                SurfaceFormat = CompressedDataFormat.V8U8;
                LoadV8U8(data);
                isNormalMap = true;
            }
            else
            {
                IL2.Settings.KeepDXTC(true);
                IL2.LoadImageFromStream(ref handle, data);
            }
            return isNormalMap;
        }

        /// <summary>
        /// Converts this image to WPF bitmap. Returns null on failure.
        /// </summary>
        /// <param name="type">Type of image to create.</param>
        /// <param name="quality">Quality of JPG image. Valid only if type is JPG. Range 0-100.</param>
        public BitmapImage ToImage(ImageType type = ImageType.Jpg, int quality = 80, int width = 0, int height = 0)
        {
            byte[] data = null;

            // KFreon: Set JPG quality if necessary
            if (type == ImageType.Jpg)
                ResIL.Settings.SetJPGQuality(quality);

            // KFreon: Save to array and build WPF bitmap.
            if (IL2.SaveToArray(handle, type, out data) != 0)
                return UsefulThings.WPF.Images.CreateWPFBitmap(data, width, height);
            else
            {
                Debug.WriteLine("Saving to array failed for some reason.");
                Debug.WriteLine(Enum.GetName(typeof(ErrorType), IL2.GetError()));
                return null;
            }
        }


        /// <summary>
        /// Converts instance to array of image data of a given image type.
        /// </summary>
        /// <param name="type">Type of image to get data of.</param>
        public byte[] ToArray(ImageType type)
        {
            byte[] data = null;
            if (IL2.SaveToArray(handle, type, out data) != 0)
                return data;
            else
            {
                Debug.WriteLine("To Array failed for some reason.");
                Debug.WriteLine(GetResILError());
            }
            return null;
        }


        public static string GetResILError()
        {
            return Enum.GetName(typeof(ErrorType), IL2.GetError());   check this to see if theres depth to errors array?
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
            if (isDDS(format))
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
        /// <param name="file">Path to image file. DEFAULT = null, data MUST NOT be null.</param>
        /// <returns>True if V8U8, False if not V8U8, null if invalid parameters provided.</returns>
        private static bool? CheckIfV8U8(string file)
        {
            if (file == null)
                return null;

            bool retval = false;
            try
            {
                // KFreon: Check format
                SaltisgoodDDSPreview dds = new SaltisgoodDDSPreview(new MemoryTributary(File.ReadAllBytes(file)), true);
                retval = dds.FormatString == "V8U8";
            }
            catch
            {
                // Ignore cos return is already false
            }
            return retval;
        }
        
        /// <summary>
        /// Checks if current working image is a V8U8 NormalMap image.
        /// </summary>
        /// <param name="data">Texture file data.</param>
        /// <returns>True if V8U8, False if not V8U8, null if invalid parameters provided.</returns>
        private static bool? CheckIfV8U8(MemoryTributary data)
        {
            if (data == null)
                return null;

            bool retval = false;
            try
            {
                // KFreon: Check format
                SaltisgoodDDSPreview dds = new SaltisgoodDDSPreview(data, true);
                retval = dds.FormatString == "V8U8";
            }
            catch (FormatException f)
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

                int mipMapBytes = (int)(w * h * bytePerPixel);
                V8U8Mips[i] = new MipMap(r.ReadBytes(mipMapBytes), DDSFormat.V8U8, w, h);
            }
            
            Width = header.dwWidth;
            Height = header.dwHeight;
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
        /// Read largest V8U8 mipmap into ResIL. Returns true if read successfully.
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
        /// Writes V8U8 image to file. Returns true if successful.
        /// </summary>
        /// <param name="bytes">V8U8 data as List.</param>
        /// <param name="savepath">Path to save to.</param>
        /// <param name="height">Height of image.</param>
        /// <param name="width">Width of image.</param>
        /// <param name="Mips">Number of mips in image.</param>
        private static bool WriteV8U8(List<sbyte> bytes, string savepath, int height, int width, int Mips)  check that this gets used - it needs to.
        {
            DDS_HEADER header = Get_V8U8_DDS_Header(0, height, width);
            
            try
            {
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
                return true;
            }
            catch(IOException e)
            {
                Debug.WriteLine("Error writing to file: " + e.Message);
            }
            return false;
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

        public bool BuildMipmaps(bool rebuild = false)
        {
            if (!rebuild && Mips > 1)
                return false;
            else if (Format == V8U8)
                {
                    BuildV8U8Mips();
                }
            else
                return ILU2.BuildMipmaps(handle);
        }

        public bool RemoveMipmaps(bool forceRemoval = false)
        {
            if (!forceRemoval && Mips == 1)
                return false;
            else if (Format == V8U8)
            {
                V8U8Mipmaps.Remove(1,);
            }
            else
                return ILU2.RemoveMips(handle);
        }
        
        private bool BuildV8U8Mips()
        {
            bool success = false;
            
            int width = V8U8Mips[0].Width / 2;
            int height = V8U8Mips[0].Height / 2;
            
            DebugOutput.WriteLine("Building V8U8 Mips with starting MIP size of {0} x {1}.", width, height);
            
            byte[] data = V8U8Mips[0].Data;
            try
            {
                int count = 1;
                while (width > 1 && height > 1)
                {
                    using (ResILImage mipmap = new ResILImage(data))
                    {
                        mipmap.Resize(widt, height);  // Note integer division - need to round somewhere
                        byte[] tempdata = mipmap.ToArray();
                        data = tempdata;
                        V8U8Mips[count++] = new MipMap(tempdata, DDSFormat.V8U8, width, height);
                    }
                    
                    height = (int)(height / 2);
                    width = (int)(width / 2);   
                }
                
                success = true;
            }
            catch(Exception e)
            {
                cw;
            }
            return success;
        }


        #region Manipulation
        /// <summary>
        /// Convert image to different types and save to path. Returns true if successful.
        /// </summary>
        /// <param name="type">Type of image to save as.</param>
        /// <param name="savePath">Path to save to.</param>
        /// <param name="surface">DDS Surface format to change to. Valid only if type is DDS.</param>
        /// <returns>True if success.</returns>
        public bool ConvertAndSave(ImageType type, string savePath, generateMipsenum, CompressedDataFormat surface = CompressedDataFormat.None, int quality = 80, bool SetJPGQuality = true)
        {
            if (SetJPGQuality && type == ImageType.Jpg)
                ResIL.Settings.SetJPGQuality(quality);

            BUildMips();  one of these needs to work on V8U8
            RemoveMIps();

            ChangeSurface(type, surface);

            return IL2.SaveImage(handle, savePath, type);
        }


        /// <summary>
        /// Converts image to different types and saves to stream. Returns true if successful.
        /// </summary>
        /// <param name="type">Desired image type.</param>
        /// <param name="stream">Data of image file, NOT raw pixel data.</param>
        /// <param name="surface">Surface format. ONLY valid when type is DDS.</param>
        /// <param name="quality">JPG quality. ONLY valid when tpye is JPG.</param>
        /// <param name="SetJPGQuality">Sets JPG output quality if true.</param>
        public bool ConvertAndSave(ImageType type, MemoryTributary stream, CompressedDataFormat surface = CompressedDataFormat.None, int quality = 80, bool SetJPGQuality = true)
        {
            if (SetJPGQuality && type == ImageType.Jpg)
                ResIL.Settings.SetJPGQuality(quality);

            ChangeSurface(type, surface);

            return IL2.SaveImageAsStream(handle, type, stream);
        }

        
        /// <summary>
        /// Changes DDS surface format to specified format.
        /// </summary>
        /// <param name="type">Type of image. Anything other than DDS will be ignored.</param>
        /// <param name="suface">Desired DDS surface format.</param>
        private void ChangeSurface(ImageType type, CompressedDataFormat surface)
        {
            // KFreon: Change surface format of DDS's
            if (type == ImageType.Dds && surface != CompressedDataFormat.None)
                IL2.Settings.SetDXTcFormat(surface);
        }


        /// <summary>
        /// Resizes image in ResIL. This is permenant in memory. On disk will not be altered.
        /// Returns true if successful.
        /// </summary>
        /// <param name="width">Width of image.</param>
        /// <param name="height">Height of image.</param>
        /// <returns>True if success.</returns>
        public bool Resize(int width, int height)
        {
            bool success = false;
            if (Format == V8U8)
            {
                if ((width / height == Width / Height))
                {
                    List<MipMap> newmips = new List<MipMap>();
                    foreach(MipMap mip in V8U8Mips)
                        if (MipMap.Width <= width)  // KFreon: Note that aspect is the same, so only need to check one dimension
                            newmips.Add(mip);
                    V8U8Mips = newmips.ToArray(newmips.Count);
                    success = true;
                }
                else
                {
                    success = ILU2.ResizeImage(handle, (uint)width, (uint)height, (byte)BitsPerPixel, (byte)Channels);
                    BuildMips();
                }
            }
            else
                success = ILU2.ResizeImage(handle, (uint)width, (uint)height, (byte)BitsPerPixel, (byte)Channels);
                
            return success;
        }
        #endregion


        private void Dispose(bool finalising)
        {
            if (handle == IntPtr.Zero)
                Debug.WriteLine("Image already deleted.");
            else
            {
                V8U8Mips = null;

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

                if (!finalising)
                    GC.SuppressFinalize(this);
            }
        }


        /// <summary> 
        /// KFreon: Destructor just calls dispose. Dispose shouldn't fail regardless of number of times called.
        /// DON'T RELY ON THIS. Use the Dispose pattern: using, and all that stuff.
        /// </summary>
        ~ResILImage()
        {
            Dispose(true);
        }

        public void Dispose()
        {
            Dispose(false);
        }
    }
}
