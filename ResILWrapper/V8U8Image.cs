public class V8U8Image : IResILImage
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
	
	// stuff that IResILImage forces me to use
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
		using (FileStream stream = new FileStream(Path))
			LoadImage(stream);
	}
	
	public V8U8Image(MemoryTributary stream)
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
		ddsStream.Seek(0, SeekOrigin.Begin);
        BinaryReader r = new BinaryReader(ddsStream);
        int dwMagic = r.ReadInt32();
        if (dwMagic != 0x20534444)
            throw new Exception("This is not a DDS! Magic number wrong.");

        DDS_HEADER header = new DDS_HEADER();
        Read_DDS_HEADER(header, r);

        if (((header.ddspf.dwFlags & 0x00000004) != 0) && (header.ddspf.dwFourCC == 0x30315844 /*DX10*/))
        {
            throw new Exception("DX10 not supported yet!");
        }
		
		if (header.ddspf.dwFourCC != V8U8)
			throw new Exception("DDS not V8U8.")

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
            MipMaps[i] = new MipMap(r.ReadBytes(mipMapBytes), DDSFormat.V8U8, w, h);
        }
        
        Width = header.dwWidth;
        Height = header.dwHeight;
	}
	
	public BitmapImage ToImage(ImageType type = ImageType.Jpg, int quality = 80, int width = 0, int height = 0)
	{
		right?
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
		
		return bmp;
	}
	
	public byte[] ToArray(ImageType type, int jpgQuality = 80)
	{
		
	}
	
	
	
	public bool BuildMipMaps(bool rebuild = false)
    {
        bool success = false;
        
        int width = V8U8Mips[0].width / 2;
        int height = V8U8Mips[0].height / 2;
        
        Debug.WriteLine("Building V8U8 Mips with starting MIP size of {0} x {1}.", width, height);
        
        byte[] data = V8U8Mips[0].data;
        try
        {
            int count = 1;
            while (width > 1 && height > 1)
            {
                using (ResILImage mipmap = new ResILImage(data))
                {
                    mipmap.Resize(width, height);  // Note integer division - need to round somewhere
                    byte[] tempdata = mipmap.ToArray(ImageType.Png);  // KFreon: Just RGB stuff. Gets read back in and dealt with accordingly (I hope...)
                    data = tempdata;
                    V8U8Mips[count++] = new MipMap(tempdata, width, height);
                }
                
                height = (int)(height / 2);
                width = (int)(width / 2);   
            }

            success = true;
        }
        catch(Exception e)
        {
            Console.WriteLine(e);
        }
        return success;
    }
	
	public bool Resize()
	{
		load as a wic image and resize? cant really if you don't have wic
	}
	
	public bool ConvertAndSave()
	{
		
	}
	
	
	#region Reading/Writing V8U8 File
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
    /// <param name="bytes">V8U8 data as List.</param>
    /// <param name="savepath">Path to save to.</param>
    /// <param name="height">Height of image.</param>
    /// <param name="width">Width of image.</param>
    /// <param name="Mips">Number of mips in image.</param>
    private static bool WriteV8U8(List<sbyte> bytes, string savepath, int height, int width, int Mips)
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
	#endregion Reading/Writing V8U8 File
	
	public void Dispose()
	{
		// don't really need it here
	}
}