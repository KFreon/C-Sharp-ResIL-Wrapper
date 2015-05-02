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

namespace ResIL.Unmanaged
{
    /// <summary>
    /// Main ResIL image functions
    /// </summary>
    public static class IL2
    {
        const string IL2DLL = "ResIL.dll";
        public static bool isInitialised { get; private set; }

        #region Constructor stuff
        [DllImport("kernel32.dll")]
        internal static extern IntPtr LoadLibrary(string pathName);

        static IL2()
        {
            // KFreon: Setup pathing to DLL
            string appPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string mainpath = null;
            
            if (Environment.Is64BitProcess)
                mainpath = Path.Combine(appPath, "x64");
            else
                mainpath = Path.Combine(appPath, "x86");

            string resil = Path.Combine(mainpath, "ResIL.dll");
            string ilu = Path.Combine(mainpath, "ILU.dll");
            string ilut = Path.Combine(mainpath, "ILUT.dll");
            string zlib = Path.Combine(mainpath, "zlib1.dll");

            // KFreon: Load library for correct architecture
            IntPtr zl = LoadLibrary(zlib);
            IntPtr res = LoadLibrary(resil);
            IntPtr ILU = LoadLibrary(ilu);
            IntPtr ILUT = LoadLibrary(ilut);

            Init();
        }
        #endregion


        #region IL2 Managed Methods
        #region Loading
        /// <summary>
        /// Load image from byte[].
        /// </summary>
        /// <param name="img">Ref to current image pointer.</param>
        /// <param name="imageData">Image Data array.</param>
        /// <returns>True if loading succeeds.</returns>
        public static bool LoadImageFromArray(ref IntPtr img, byte[] imageData, ImageType type = ImageType.Bmp)
        {
            // KFreon: Initialised if necessary
            if (!isInitialised)
                Init();
            return il2LoadL(img, type, imageData, (uint)imageData.Length);
        }


        /// <summary>
        /// Load image from stream.
        /// </summary>
        /// <param name="img">Ref to current image pointer.</param>
        /// <param name="stream">Stream containing image data.</param>
        /// <returns>True if succeeds.</returns>
        public static bool LoadImageFromStream(ref IntPtr img, Stream stream)
        {
            // KFreon: Initialise if necessary
            if (!isInitialised)
                Init();

            // KFreon: Fail if stream can't read.
            if (stream.CanRead)
            {
                // KFreon: Get data and size
                byte[] data = stream.ReadStreamFully();
                uint size = (uint)data.LongLength;

                return il2LoadL(img, ImageType.Unknown, data, size);
            }
            else
                return false;
        }


        /// <summary>
        /// Load image from filename.
        /// </summary>
        /// <param name="img">Ref to current image pointer.</param>
        /// <param name="Filename">Path to image file.</param>
        /// <returns>True if succeeds.</returns>
        public static bool LoadImage(ref IntPtr img, string Filename)
        {
            // KFreon: Initialise if necessary
            if (!isInitialised)
                Init();

            return il2LoadImage(img, Filename);
        }


        /// <summary>
        /// Load image from file specifying type of image.
        /// </summary>
        /// <param name="img">Ref to current image pointer.</param>
        /// <param name="Filename">Path to image file.</param>
        /// <param name="type">Type of image to force loading as.</param>
        /// <returns>True if succeeds.</returns>
        public static bool LoadImage(ref IntPtr img, string Filename, ImageType type)
        {
            return il2Load(img, type, Filename);
        }
        #endregion


        #region Misc
        /// <summary>
        /// Initialises ResIL subsystems.
        /// </summary>
        public static void Init()
        {
            il2Init();
            isInitialised = true;
            Settings.SetSquishCompression(true);   // KFreon: Set better compression. This may not be relevant. 
        }


        /// <summary>
        /// Gets the last error seen by ResIL.
        /// </summary>
        /// <returns>Last error seen by ResIL.</returns>
        public static ErrorType GetError()
        {
            return (ErrorType)il2GetError();
        }


        /// <summary>
        /// Generates an image entry in ResIL.
        /// </summary>
        /// <returns>Pointer to image in ResIL.</returns>
        public static IntPtr GenerateImage()
        {
            // KFreon: Initialise if necessary.
            if (!isInitialised)
                Init();
            return il2GenImage();
        }


        /// <summary>
        /// Removes image from ResIL. This invalidates its pointer.
        /// </summary>
        /// <param name="img">Pointer to image to remove.</param>
        /// <returns>True if succeeds.</returns>
        public static bool DeleteImage(IntPtr img)
        {
            // KFreon: Check if deleted already
            if (img == IntPtr.Zero)
                return false;

            try
            {
                il2DeleteImage(img);
            }
            catch
            {

            }
            return true;
        }


        /// <summary>
        /// Determines ImageType from raw data.
        /// </summary>
        /// <param name="imageData">Image data.</param>
        /// <returns>Type of image.</returns>
        public static ImageType DetermineImageType(byte[] imageData)
        {
            ImageType imageType = ImageType.Unknown;

            // KFreon: Check that image data is valid
            if (imageData != null && imageData.Length != 0)
            {
                uint size = (uint)imageData.LongLength;
                imageType = (ImageType)il2DetermineTypeL(imageData, size);
            }
            return imageType;
        }
        #endregion


        #region Saving
        /// <summary>
        /// Save image to byte[].
        /// </summary>
        /// <param name="handle">Pointer to current image.</param>
        /// <param name="type">Type of image to save as.</param>
        /// <param name="lump">OUT: Byte[] to save to.</param>
        /// <returns>Length of data written to array. 0 if failed.</returns>
        public static long SaveToArray(IntPtr handle, ImageType type, out byte[] lump)
        {
            long size = il2DetermineSize(handle, type);
            lump = new byte[size];
            return il2SaveL(handle, type, lump, (uint)size);
        }


        
        public static bool SaveImageAsStream(IntPtr handle, ImageType type, MemoryTributary stream)
        {
            byte[] lump = null;
            bool success = false;

            if (SaveToArray(handle, type, out lump) != 0)
            {
                stream.Write(lump, 0, lump.Length);
                success = stream.Length == lump.Length;
            }
            return success;
        }


        /// <summary>
        /// Save image to file.
        /// </summary>
        /// <param name="handle">Pointer to current image.</param>
        /// <param name="savePath">Path to save to.</param>
        /// <returns>True if succeeded.</returns>
        public static bool SaveImage(IntPtr handle, string savePath)
        {
            return il2SaveImage(handle, savePath);
        }


        /// <summary>
        /// Save image to file with a forced image type.
        /// </summary>
        /// <param name="handle">Pointer to current image.</param>
        /// <param name="savePath">Path to save image to.</param>
        /// <param name="type">Type of image to force saving as.</param>
        /// <returns>True if succeeds.</returns>
        public static bool SaveImage(IntPtr handle, string savePath, ImageType type)
        {
            return il2Save(handle, type, savePath);
        }


        /// <summary>
        /// Gets V8U8 data. Needs to be separate from the other get data as V8U8 needs to be manually coloured. 
        /// </summary>
        /// <param name="img">Pointer to current image.</param>
        /// <param name="Height">Height of image.</param>
        /// <param name="Width">Width of image.</param>
        /// <returns>List of bytes containing V8U8 data.</returns>
        public static List<byte> GetV8U8Data(IntPtr img, int Height, int Width)
        {
            List<byte> bytes = new List<byte>();
            using (MemoryTributary stream = new MemoryTributary())
            {
                bool success = SaveImageAsStream(img, ImageType.Bmp, stream);

                // KFreon: Step over each pixel.
                for (int h = 0; h < Height; h++)
                {
                    for (int w = 0; w < Width; w++)
                    {
                        // KFreon: Get data. NOTE not correct channel names. No idea what the actual names are cos alpha and blue are apparently either red or green.
                        byte alpha = (byte)stream.ReadByte();
                        byte red = (byte)stream.ReadByte();
                        byte green = (byte)stream.ReadByte();
                        byte blue = (byte)stream.ReadByte();

                        // KFreon: Write data
                        bytes.Add(alpha);
                        bytes.Add(blue);
                    }
                }
            }
            return bytes;
        }


        /// <summary>
        /// Gets V8U8 data as MemoryStream.
        /// </summary>
        /// <param name="img">Pointer to current image.</param>
        /// <param name="Height">Height of image.</param>
        /// <param name="Width">Width of image.</param>
        /// <returns>MemoryStream containing V8U8 data.</returns>
        public static MemoryStream GetV8U8DataAsStream(IntPtr img, int Height, int Width)
        {
            List<byte> data = GetV8U8Data(img, Height, Width);
            MemoryStream stream = new MemoryStream(data.ToArray());
            data.Clear();
            data = null;
            return stream;
        }
        #endregion


        /// <summary>
        /// Contains methods for setting various global options in ResIL.
        /// </summary>
        public static class Settings
        {
            /// <summary>
            /// Sets DXT format in ResIL. Note this is NOT threadsafe yet, so saving MUST be single threaded.
            /// </summary>
            /// <param name="surface">Surface format to save in.</param>
            public static void SetDXTcFormat(CompressedDataFormat surface)
            {
                il2SetInteger((uint)ILDefines.IL_DXTC_FORMAT, (uint)surface);
            }


            /// <summary>
            /// Setting to keep DXTC data for formatting purposes.
            /// </summary>
            /// <param name="keep">True = keep DXTC, False = discard.</param>
            public static void KeepDXTC(bool keep)
            {
                il2SetInteger((uint)ILDefines.IL_KEEP_DXTC_DATA, (uint)(keep ? 1 : 0));
            }


            #region Compression/Quality
            /// <summary>
            /// Setting for JPG quality. 
            /// </summary>
            /// <param name="quality">Quality of JPG images. Valid range 0-100.</param>
            public static void SetJPGQuality(uint quality)
            {
                if (quality > 0 && quality <= 100)
                    il2SetInteger((uint)ILDefines.IL_JPG_QUALITY, quality);
            }


            /// <summary>
            /// Setting for use of Squish compression library.
            /// </summary>
            /// <param name="squish">True = Use Squish library, False = Use nVidia.</param>
            public static void SetSquishCompression(bool squish)
            {
                il2SetInteger((uint)ILDefines.IL_SQUISH_COMPRESS, (uint)(squish ? 1 : 0));
            }
            #endregion

        }
        #endregion


        #region IL2 Native Methods
        // KFreon: Don't know why ordinal is required here, but name is not found...
        [DllImport(IL2DLL, EntryPoint="il2DetermineTypeL")]
        private static extern uint il2DetermineTypeL(byte[] lump, uint size);

        [DllImport(IL2DLL, EntryPoint = "il2GetError")]
        private static extern ErrorType il2GetError();



        [DllImport(IL2DLL, EntryPoint = "il2Init")]
        private static extern void il2Init();

        [DllImport(IL2DLL, EntryPoint = "il2GenImage")]
        private static extern IntPtr il2GenImage();


        [DllImport(IL2DLL, EntryPoint = "il2LoadImage")]
        private static extern bool il2LoadImage(IntPtr img, [MarshalAs(UnmanagedType.LPWStr)]string FileName);

        [DllImport(IL2DLL, EntryPoint = "il2Load")]
        private static extern bool il2Load(IntPtr img, ImageType type, [MarshalAs(UnmanagedType.LPWStr)]string FileName);

        [DllImport(IL2DLL, EntryPoint = "il2LoadL")]
        private static extern bool il2LoadL(IntPtr img, ImageType type, byte[] lump, uint Size);


        [DllImport(IL2DLL, EntryPoint = "il2SaveL")]
        private static extern Int64 il2SaveL(IntPtr img, ImageType type, byte[] lump, uint size);

        [DllImport(IL2DLL, EntryPoint = "il2SaveImage")]
        private static extern bool il2SaveImage(IntPtr img, [MarshalAs(UnmanagedType.LPWStr)]string FileName);

        [DllImport(IL2DLL, EntryPoint="il2Save")]
        private static extern bool il2Save(IntPtr img, ImageType type, [MarshalAs(UnmanagedType.LPWStr)]string FileName);


        [DllImport(IL2DLL, EntryPoint = "il2DetermineSize")]
        private static extern Int64 il2DetermineSize(IntPtr img, ImageType type);

        [DllImport(IL2DLL, EntryPoint = "il2DeleteImage")]
        private static extern void il2DeleteImage(IntPtr img);

        [DllImport(IL2DLL, EntryPoint = "il2SetInteger")]
        private static extern void il2SetInteger(uint Mode, uint param);
        #endregion

        
    }
}
