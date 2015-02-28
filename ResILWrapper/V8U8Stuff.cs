using ResILWrapper.V8U8Stuff.AmaroK86Stuff;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResILWrapper.V8U8Stuff
{
    namespace AmaroK86Stuff
    {
        public class ImageSize : IComparable
        {
            public readonly uint width;
            public readonly uint height;

            public ImageSize(uint width, uint height)
            {
                if (!checkIsPower2(width))
                    new FormatException("Invalid width value, must be power of 2");
                if (!checkIsPower2(width))
                    new FormatException("Invalid height value, must be power of 2");
                if (width == 0)
                    width = 1;
                if (height == 0)
                    height = 1;
                this.width = width;
                this.height = height;
            }

            private bool checkIsPower2(uint val)
            {
                uint power = 1;
                while (power < val)
                {
                    power *= 2;
                }
                return val == power;
            }

            public int CompareTo(object obj)
            {
                if (obj is ImageSize)
                {
                    ImageSize temp = (ImageSize)obj;
                    if ((temp.width * temp.height) == (this.width * this.height))
                        return 0;
                    if ((temp.width * temp.height) > (this.width * this.height))
                        return -1;
                    else
                        return 1;
                }
                throw new ArgumentException();
            }

            public override string ToString()
            {
                return this.width + "x" + this.height;
            }

            public override bool Equals(System.Object obj)
            {
                // If parameter is null return false.
                if (obj == null)
                {
                    return false;
                }

                // If parameter cannot be cast to Point return false.
                ImageSize p = obj as ImageSize;
                if ((System.Object)p == null)
                {
                    return false;
                }

                // Return true if the fields match:
                return (this.width == p.width) && (this.height == p.height);
            }

            public bool Equals(ImageSize p)
            {
                // If parameter is null return false:
                if ((object)p == null)
                {
                    return false;
                }

                // Return true if the fields match:
                return (this.width == p.width) && (this.height == p.height);
            }

            public override int GetHashCode()
            {
                return (int)(width ^ height);
            }

            public static bool operator ==(ImageSize a, ImageSize b)
            {
                // If both are null, or both are same instance, return true.
                if (System.Object.ReferenceEquals(a, b))
                {
                    return true;
                }

                // If one is null, but not both, return false.
                if (((object)a == null) || ((object)b == null))
                {
                    return false;
                }

                // Return true if the fields match:
                return a.width == b.width && a.height == b.height;
            }

            public static bool operator !=(ImageSize a, ImageSize b)
            {
                return !(a == b);
            }

            public static ImageSize operator /(ImageSize a, int b)
            {
                return new ImageSize((uint)(a.width / b), (uint)(a.height / b));
            }

            public static ImageSize operator *(ImageSize a, int b)
            {
                return new ImageSize((uint)(a.width * b), (uint)(a.height * b));
            }

            public static ImageSize stringToSize(string input)
            {
                string[] parsed = input.Split('x');
                if (parsed.Length != 2)
                    throw new FormatException();
                uint width = Convert.ToUInt32(parsed[0]);
                uint height = Convert.ToUInt32(parsed[1]);
                return new ImageSize(width, height);
            }
        }

        public enum FourCC : uint
        {
            DXT1 = 0x31545844,
            DXT3 = 0x33545844,
            DXT5 = 0x35545844,
            ATI2 = 0x32495441
        }

        public class DDS_HEADER
        {
            public int dwSize;
            public int dwFlags;
            /*	DDPF_ALPHAPIXELS   0x00000001 
                DDPF_ALPHA   0x00000002 
                DDPF_FOURCC   0x00000004 
                DDPF_RGB   0x00000040 
                DDPF_YUV   0x00000200 
                DDPF_LUMINANCE   0x00020000 
             */
            public int dwHeight;
            public int dwWidth;
            public int dwPitchOrLinearSize;
            public int dwDepth;
            public int dwMipMapCount;
            public int[] dwReserved1 = new int[11];
            public DDS_PIXELFORMAT ddspf = new DDS_PIXELFORMAT();
            public int dwCaps;
            public int dwCaps2;
            public int dwCaps3;
            public int dwCaps4;
            public int dwReserved2;
        }

        public class DDS_PIXELFORMAT
        {
            public int dwSize;
            public int dwFlags;
            public int dwFourCC;
            public int dwRGBBitCount;
            public int dwRBitMask;
            public int dwGBitMask;
            public int dwBBitMask;
            public int dwABitMask;

            public DDS_PIXELFORMAT()
            {
            }
        }

        public enum DDSFormat
        {
            DXT1, DXT3, DXT5, V8U8, ATI2, G8, ARGB
        }
    }
    

    // KFreon: From saltisgood's work with the ME3Explorer project me3explorer.freeforums.org
    public class SaltisgoodDDSPreview
    {
        private const UInt32 DDSMagic = 0x20534444;
        private UInt32 DDSFlags;
        public bool mips;
        public UInt32 Height;
        public UInt32 Width;
        public UInt32 NumMips;
        public DDSFormat Format;
        public string FormatString;
        private byte[] imgData;
        private float BPP;

        private UInt32 pfFlags;
        private UInt32 fourCC;
        private UInt32 rgbBitCount;
        private UInt32 rBitMask;
        private UInt32 gBitMask;
        private UInt32 bBitMask;
        private UInt32 aBitMask;
        private bool compressed;
        private bool stdDDS;

        public SaltisgoodDDSPreview(Stream data)
        {
            if (UsefulThings.General.StreamBitConverter.ToUInt32(data, 0) != DDSMagic)
                throw new FormatException("Invalid DDS Magic Number");
            if (UsefulThings.General.StreamBitConverter.ToUInt32(data, 4) != 0x7C)
                throw new FormatException("Invalid header size");
            DDSFlags = UsefulThings.General.StreamBitConverter.ToUInt32(data, 8);
            if ((DDSFlags & 0x1) != 0x1 || (DDSFlags & 0x2) != 0x2 || (DDSFlags & 0x4) != 0x4 || (DDSFlags & 0x1000) != 0x1000)
                throw new FormatException("Invalid DDS Flags");
            mips = (DDSFlags & 0x20000) == 0x20000;
            Height = UsefulThings.General.StreamBitConverter.ToUInt32(data, 12);
            Width = UsefulThings.General.StreamBitConverter.ToUInt32(data, 16);
            // Skip pitch and depth, unimportant
            NumMips = UsefulThings.General.StreamBitConverter.ToUInt32(data, 28);
            uint pxformat = UsefulThings.General.StreamBitConverter.ToUInt32(data, 84);
            Format = GetDDSFormat(pxformat);
            imgData = new byte[data.Length - 0x80];

            // KFreon: Seek in stream and write to imgData
            data.Seek(0x80, SeekOrigin.Begin);
            data.Read(imgData, 0, imgData.Length);
            //Array.Copy(data, 0x80, imgData, 0, imgData.Length);

            pfFlags = UsefulThings.General.StreamBitConverter.ToUInt32(data, 0x50);
            fourCC = UsefulThings.General.StreamBitConverter.ToUInt32(data, 0x54);
            rgbBitCount = UsefulThings.General.StreamBitConverter.ToUInt32(data, 0x58);
            rBitMask = UsefulThings.General.StreamBitConverter.ToUInt32(data, 0x5C);
            gBitMask = UsefulThings.General.StreamBitConverter.ToUInt32(data, 0x60);
            bBitMask = UsefulThings.General.StreamBitConverter.ToUInt32(data, 0x64);
            aBitMask = UsefulThings.General.StreamBitConverter.ToUInt32(data, 0x68);

            FormatString = GetFormat();

            switch (FormatString)
            {
                case "DXT1": BPP = 0.5F; stdDDS = true; compressed = true; break;
                case "DXT5": BPP = 1F; stdDDS = true; compressed = true; break;
                case "V8U8": BPP = 2F; stdDDS = true; compressed = false; break;
                case "ATI2": BPP = 1F; stdDDS = true; compressed = true; break;
                case "A8R8G8B8": BPP = 4F; stdDDS = false; compressed = false; break;
                case "R8G8B8": BPP = 3F; stdDDS = false; compressed = false; break;
                default: BPP = 1; stdDDS = false; compressed = false; break;
            }
        }

        // KFreon: Get format in string form
        private String GetFormat()
        {
            if ((pfFlags & 0x4) == 0x4) // DXT
            {
                switch ((int)fourCC)
                {
                    case (int)FourCC.DXT1: return "DXT1";
                    case (int)FourCC.DXT3: return "DXT3";
                    case (int)FourCC.DXT5: return "DXT5";
                    case (int)FourCC.ATI2: return "ATI2";
                    default: throw new FormatException("Unknown 4CC");
                }
            }
            else if ((pfFlags & 0x40) == 0x40) // Uncompressed RGB
            {
                if (rBitMask == 0xFF0000 && gBitMask == 0xFF00 && bBitMask == 0xFF)
                {
                    if ((pfFlags & 0x1) == 0x1 && aBitMask == 0xFF000000 && rgbBitCount == 0x20)
                        return "A8R8G8B8";
                    else if ((pfFlags & 0x1) == 0x0 && rgbBitCount == 0x18)
                        return "R8G8B8";
                }
            }
            else if ((pfFlags & 0x80000) == 0x80000 && rgbBitCount == 0x10 && rBitMask == 0xFF && gBitMask == 0xFF00) // V8U8
                return "V8U8";

            else if ((pfFlags & 0x20000) == 0x20000 && rgbBitCount == 0x8 && rBitMask == 0xFF)
                return "G8";

            throw new FormatException("Unknown format");
        }

        private DDSFormat GetDDSFormat(uint fourcc)
        {
            string hex = System.Convert.ToString(fourcc, 16);
            switch (fourcc)
            {
                case 0x31545844: return DDSFormat.DXT1;
                case 0x35545844: return DDSFormat.DXT5;
                case 0x33545844: return DDSFormat.DXT3;
                case 0x38553856:
                case 0x0: return DDSFormat.V8U8;
                case 0x32495441: return DDSFormat.ATI2;
                default:
                    throw new FormatException("Unknown/ Unsupported DDS Format");
            }
        }

        public byte[] GetMipData()
        {
            int len = (int)(BPP * Height * Width);
            byte[] img = new byte[len];
            Array.Copy(imgData, 0, img, 0, len);
            return img;
        }

        public long GetMipMapDataSize()
        {
            return GetMipMapDataSize(new ImageSize(Width, Height));
        }

        private long GetMipMapDataSize(ImageSize imgsize)
        {
            uint w = imgsize.width;
            uint h = imgsize.height;
            if (compressed)
            {
                if (w < 4)
                    w = 4;
                if (h < 4)
                    h = 4;
            }
            long totalBytes = (long)((float)(w * h) * BPP);
            w = imgsize.width;
            h = imgsize.height;
            if (w == 1 && h == 1)
                return totalBytes;
            if (w != 1)
                w = imgsize.width / 2;
            if (h != 1)
                h = imgsize.height / 2;
            return totalBytes + GetMipMapDataSize(new ImageSize(w, h));
        }
    }
}
