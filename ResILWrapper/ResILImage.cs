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
    /// <summary>
    /// Provides object-oriented/C# way of interacting with ResIL images.
    /// </summary>
    public class ResILImage : ResILImageBase
    {
        #region Image Properties
        public int BitsPerPixel { get; private set; }
        public DataFormat MemoryFormat { get; private set; }  // KFreon: Format as loaded by ResIL (Usually RGB)
        public int Channels { get; private set; }
        public int DataSize { get; private set; }
        public DataType DataType { get; private set; }

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
            ImageType = DetermineType(ImageType.Unknown, UsefulThings.General.GetExternalData(FilePath));
            LoadImage(FilePath);
            PopulateInfo();
        }

        public ResILImage(Stream stream)
        {
            Path = null;
            stream.Seek(0, SeekOrigin.Begin);
            ImageType = DetermineType(ImageType.Unknown, stream.ReadStreamFully());
            stream.Seek(0, SeekOrigin.Begin);
            LoadImage(stream);
            PopulateInfo();
        }

        public ResILImage(byte[] imgData, ImageType type = ImageType.Bmp)
        {
            Path = null;
            ImageType = DetermineType(type, imgData);
            LoadImage(imgData, ImageType);
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
                IL2.Settings.KeepDXTC(true);
                if (!IL2.LoadImage(ref handle, FilePath))
                {
                    Debug.WriteLine("Loading from file failed for some reason.");
                    //Debug.WriteLine(GET ERROR FUNCTION);
                return false;
                }
            
            return true;
            }

        /// <summary>
        /// Load image from byte[]. Returns true if successful.
        /// </summary>
        /// <param name="data">Data of image file, NOT raw pixel data.</param>
        /// <param name="type">Type of data (format etc jpg, dds, etc)</param>
        private bool LoadImage(byte[] data, ImageType type = ImageType.Bmp)
        {
                IL2.Settings.KeepDXTC(true);
                if (!IL2.LoadImageFromArray(ref handle, data, type))
                {
                    Debug.WriteLine("Loading image failed for some reason");
                return false;
                }
            
            return true;
        }


        /// <summary>
        /// Load image from MemoryStream. Returns true if successful.
        /// </summary>
        /// <param name="data">Data of image file, NOT raw pixel data.</param>asf
        private bool LoadImage(Stream data)
        {
            IL2.Settings.KeepDXTC(true);
            return IL2.LoadImageFromStream(ref handle, data);
        }

        /// <summary>
        /// Converts this image to WPF bitmap. Returns null on failure.
        /// </summary>
        /// <param name="type">Type of image to create.</param>
        /// <param name="quality">Quality of JPG image. Valid only if type is JPG. Range 0-100.</param>
        public override BitmapImage ToImage(ImageType type = ImageType.Jpg, int quality = 80, int width = 0, int height = 0)
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
        public override byte[] ToArray()
        {
            byte[] data = null;

            ChangeSurface(SurfaceFormat);
            if (IL2.SaveToArray(handle, ImageType, out data) != 0)
                return data;
            else
            {
                Debug.WriteLine("To Array failed for some reason.");
            }
            return null;
        }


        #region Static methods
        public static bool IsTextureDDS(string format)
        {
            return DDSFormats.Any(t => t.Contains(format, StringComparison.CurrentCultureIgnoreCase));
        }


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
            if (ResILImage.IsTextureDDS(format))
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
            // KFreon: V8U8 not valid for this class. Use V8U8Image instead.
            if (format.Contains("V8U8", StringComparison.OrdinalIgnoreCase))
                return false;


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


        public override bool BuildMipMaps(bool rebuild = false)
        {
            bool success = false;
            if (!rebuild && Mips > 1)
                return true;
            else
                success = ILU2.BuildMipmaps(handle);

            Mips = EstimateNumMips(Width, Height);
            return success;
        }

        public override bool RemoveMipMaps(bool forceRemoval = false)
        {
            bool success = false;

            if (!forceRemoval && Mips == 1)
                return false;
            else
                success = ILU2.RemoveMips(handle);

            Mips = 1;
            return true;
        }


        #region Manipulation
        /// <summary>
        /// Convert image to different types and save to path. Returns true if successful.
        /// </summary>
        /// <param name="type">Type of image to save as.</param>
        /// <param name="savePath">Path to save to.</param>
        /// <param name="surface">DDS Surface format to change to. Valid only if type is DDS.</param>
        /// <returns>True if success.</returns>
        public override bool ConvertAndSave(ImageType type, string savePath, MipMapMode MipsMode = MipMapMode.None, CompressedDataFormat surface = CompressedDataFormat.None, int quality = 80, bool SetJPGQuality = true)
        {
            /*if (SetJPGQuality && type == ImageType.Jpg)
                ResIL.Settings.SetJPGQuality(quality);

            bool mipsOperationSuccess = true;
            switch (MipsMode)
            {
                case MipMapMode.BuildAll:
                    mipsOperationSuccess = BuildMipMaps();
                    break;
                case MipMapMode.Rebuild:
                    mipsOperationSuccess = BuildMipMaps(true);
                    break;
                case MipMapMode.RemoveAllButOne:
                    mipsOperationSuccess = RemoveMipMaps();
                    break;
                case MipMapMode.ForceRemove:
                    mipsOperationSuccess = RemoveMipMaps(true);
                    break;
            }

            if (!mipsOperationSuccess)
                Console.WriteLine("Failed to build mips for {0}", savePath);

            ChangeSurface(surface);
            //IL2.SaveImage(handle, savePath + ".dds", type);
            return IL2.SaveImage(handle, savePath, type);*/

            using (FileStream fs = new FileStream(savePath, FileMode.CreateNew))
                return ConvertAndSave(type, fs, MipsMode, surface, quality, SetJPGQuality);
        }


        /// <summary>
        /// Converts image to different types and saves to stream. Returns true if successful.
        /// </summary>
        /// <param name="type">Desired image type.</param>
        /// <param name="stream">Stream to save to. Contains data of image file, NOT raw pixel data.</param>
        /// <param name="surface">Surface format. ONLY valid when type is DDS.</param>
        /// <param name="quality">JPG quality. ONLY valid when tpye is JPG.</param>
        /// <param name="SetJPGQuality">Sets JPG output quality if true.</param>
        public override bool ConvertAndSave(ImageType type, Stream stream, MipMapMode MipsMode = MipMapMode.None, CompressedDataFormat surface = CompressedDataFormat.None, int quality = 80, bool SetJPGQuality = true)
        {
            if (SetJPGQuality && type == ImageType.Jpg)
                ResIL.Settings.SetJPGQuality(quality);

            if (surface == CompressedDataFormat.V8U8)
            {
                byte[] imgdata = ToArray();

                if (imgdata == null)
                    return false;

                byte[] rawdata = null;
                using (MemoryTributary test = new MemoryTributary(imgdata))
                {
                    var frame = BitmapFrame.Create(test);
                    int stride = (Width * 32 + 7) / 8;
                    rawdata = new byte[stride * 1024];
                    frame.CopyPixels(rawdata, stride, 0);
                }

                using (V8U8Image img = new V8U8Image(rawdata, Width, Height, BitsPerPixel))
                {
                    return img.ConvertAndSave(type, stream, MipsMode, surface, quality, SetJPGQuality);
                }
            }
            else
            {
                bool mipsOperationSuccess = true;
                switch (MipsMode)
                {
                    case MipMapMode.BuildAll:
                        mipsOperationSuccess = BuildMipMaps();
                        break;
                    case MipMapMode.Rebuild:
                        mipsOperationSuccess = BuildMipMaps(true);
                        break;
                    case MipMapMode.RemoveAllButOne:
                        mipsOperationSuccess = RemoveMipMaps();
                        break;
                    case MipMapMode.ForceRemove:
                        mipsOperationSuccess = RemoveMipMaps(true);
                        break;
                }

                if (!mipsOperationSuccess)
                    Console.WriteLine("Failed to build mips for image.");


                ChangeSurface(surface);
                return IL2.SaveImageAsStream(handle, type, stream);
            }
        }

        
        /// <summary>
        /// Changes DDS surface format to specified format.
        /// </summary>
        /// <param name="surface">Desired DDS surface format.</param>
        private void ChangeSurface(CompressedDataFormat surface)
        {
            // KFreon: Change surface format of DDS's
            if (surface != CompressedDataFormat.None)
                IL2.Settings.SetDXTcFormat(surface);
        }


        /// <summary>
        /// Resizes image in ResIL. This is permenant in memory. On disk will not be altered.
        /// Returns true if successful.
        /// </summary>
        /// <param name="width">Width of image.</param>
        /// <param name="height">Height of image.</param>
        /// <returns>True if success.</returns>
        public override bool Resize(int width, int height)
        {
            // KFreon: Broken for now
            throw new NotImplementedException();
            //return ILU2.ResizeImage(handle, (uint)width, (uint)height, (byte)BitsPerPixel, (byte)Channels);  
        }
        #endregion


        private void Dispose(bool finalising)
        {
            if (handle == IntPtr.Zero)
                Debug.WriteLine("Image already deleted.");
            else
            {
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
