/*
* Copyright (c) 2012 Nicholas Woodfield
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in
* all copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
* THE SOFTWARE.
*/



using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ResIL.Unmanaged
{
    [StructLayoutAttribute(LayoutKind.Sequential)]
    public class SIO
    {
        // KFreon: Callback function pointers as delegates
        public delegate void openReadOnly();
        public delegate void openWrite();
        public delegate void close();
        public delegate void read();
        public delegate void seek();
        public delegate void eof();
        public delegate void getc();
        public delegate void tell();
        public delegate void putc();
        public delegate void write();

        [MarshalAs(UnmanagedType.FunctionPtr)]
        public openReadOnly OpenReadOnly;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public openWrite OpenWrite;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public close Close;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public read Read;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public seek Seek;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public eof EOF;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public getc Getc;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public tell Tell;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public putc Putc;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public write Write;

        public Int64 rwPos;
        [MarshalAs(UnmanagedType.LPArray)]
        public byte[] lump;
        public uint lumpSize;
        public uint handle;
        public Int64 ReadFileStart, WriteFileStart;
        public Int64 MaxPos;
    }

    [StructLayoutAttribute(LayoutKind.Sequential)]
    public class ILPalette
    {
        private IntPtr mPalette;
        private uint mPalSize;
        private PaletteType mPalType;
        private uint mNumCols;
        private sbyte mNumComponents, mRedOffset, mGreenOffset, mBlueOffset, mAlphaOffset;

        public ILPalette()
        {
            setup();
        }

        public ILPalette(ILPalette pal)
        {
            setup();

            use(pal);
        }

        private void setup()
        {
            mPalette = (IntPtr)null;
            mPalSize = 0;
            mPalType = PaletteType.None;
            mNumCols = 0;
            mNumComponents = 0;
            mRedOffset = 0;
            mGreenOffset = 0;
            mBlueOffset = 0;
            mAlphaOffset = 0;
        }

        bool use(ILPalette pal)
        {
            return use(pal.mNumCols, pal.mPalette, pal.mPalType);
        }

        bool use (uint aNumCols, IntPtr aPal, PaletteType aPalType)
	    {
		    if (mPalette != null) 
			    mPalette = (IntPtr)null;

		switch(aPalType) 
        {
		    case PaletteType.RGB24:
			    mNumComponents = 3;
			    mRedOffset = 0;
			    mGreenOffset = 1;
			    mBlueOffset = 2;
			    mAlphaOffset = -1;
			    break;
		    case PaletteType.RGB32:
			    mNumComponents = 4;
			    mRedOffset = 0;
			    mGreenOffset = 1;
			    mBlueOffset = 2;
			    mAlphaOffset = -1;
			    break;
		    case PaletteType.RGBA32:
			    mNumComponents = 4;
			    mRedOffset = 0;
			    mGreenOffset = 1;
			    mBlueOffset = 2;
			    mAlphaOffset = 3;
			    break;
		    case PaletteType.BGR24:
			    mNumComponents = 3;
			    mRedOffset = 2;
			    mGreenOffset = 1;
			    mBlueOffset = 0;
			    mAlphaOffset = -1;
			    break;
		    case PaletteType.BGR32:
			    mNumComponents = 4;
			    mRedOffset = 2;
			    mGreenOffset = 1;
			    mBlueOffset = 0;
			    mAlphaOffset = -1;
			    break;
		    case PaletteType.BGRA32:
			    mNumComponents = 4;
			    mRedOffset = 3;
			    mGreenOffset = 2;
			    mBlueOffset = 1;
			    mAlphaOffset = 0;
			    break;
		    case PaletteType.None:
		    default:
			    mNumComponents = 0;
			    mRedOffset = 0;
			    mGreenOffset = 0;
			    mBlueOffset = 0;
			    mAlphaOffset = 0;
			    break;
		}

		if (mNumComponents > 0 && aNumCols > 0) 
        {
			mNumCols = aNumCols;
			mPalSize = (uint)(aNumCols * mNumComponents);
			if (mPalette != null) 
            {
				if (aPal != null)
					mPalette = aPal;
				mPalType = aPalType;
				return true;
			} 
            else
				return false;
		} 
        else
			return true;
	}

        public bool hasPallete()
        {
            return mPalette != null && mPalSize > 0 && mPalType != PaletteType.None;
        }
    }

    [StructLayoutAttribute(LayoutKind.Sequential)]
    public class ILImage
    {
        public uint Width;
        public uint Height;
        public uint Depth;
        public byte BytesPerPixel;
        public byte BytesPerChannel;
        public uint BitsPerScanline;
        public List<byte> Data;
        public uint SizeOfData;
        public uint SizeOfPlane;
        public DataFormat Format;
        public DataType Type;
        public OriginLocation Origin;
        public ILPalette Palette;
        public uint Duration;
        public CubeMapFace CubeFlags;
        public IntPtr Mipmaps;
        public IntPtr Next;
        public IntPtr Faces;
        public IntPtr Layers;
        public IntPtr Animlist;
        public uint AmimSize;
        public IntPtr Profile;
        public uint ProfileSize;
        public uint OffX;
        public uint OffY;
        public IntPtr DxtcData;
        public CompressedDataFormat DxtcFormat;
        public uint DxtcSize;
        public IntPtr io;

        public bool HasDXTC
        {
            get
            {
                return DxtcFormat != CompressedDataFormat.None;
            }
        }

        public bool IsCubeMap
        {
            get
            {
                return CubeFlags != CubeMapFace.None && CubeFlags != CubeMapFace.SphereMap;
            }
        }

        public bool IsSphereMap
        {
            get
            {
                return CubeFlags == CubeMapFace.SphereMap;
            }
        }
    }


    [StructLayoutAttribute(LayoutKind.Sequential)]
    public struct PointF
    {
        float X;
        float Y;

        public PointF(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayoutAttribute(LayoutKind.Sequential)]
    public struct PointI
    {
        int X;
        int Y;

        public PointI(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
