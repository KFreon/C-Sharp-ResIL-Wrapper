public abstract class ResILImageBase : IDisposable
{
	public enum MipMapMode
    {
        BuildAll, RemoveAllButOne, Rebuild, ForceRemove, None
    }
		
	string Path {get; private set;}
	CompressedDataFormat SurfaceFormat {get; private set;}
	int Width {get;private set;}
	int Width {get;private set;}	
	int Mips {get;private set;}
	
	BitmapImage ToImage(ImageType type = ImageType.Jpg, int quality = 80, int width = 0, int height = 0);
	byte[] ToArray(ImageType type, int jpgQuality);
	
	bool BuildMipMaps(bool rebuild = false);
	bool RemoveMipMaps(bool force = false);
	
	bool ConvertAndSave(ImageType type, string savePath, MipMapMode MipsMode = MipMapMode.BuildAll, CompressedDataFormat surface =CompressedDataFormat.None, int quality = 80, bool SetJPGQuality = true);
	bool ConvertAndSave(ImageType type, MemoryTributary stream, MipMapMode MipsMode = MipMapMode.BuildAll, CompressedDataFormat surface =CompressedDataFormat.None, int quality = 80, bool SetJPGQuality = true);
	bool Resize(int width, int height);
	
	public void Dispose()
	{
		
	}
	
	
	#region Creation
	public static ResILImageBase Create(string filepath)
	{
		using (FileStream stream = new FileStream(filepath))
			return Create(stream);
	}
	
	public static ResILImageBase Create(MemoryTributary stream)
	{
		bool isv8u8 = IsV8U8(stream);
		if (isv8u8 == true)
			return new V8U8Image(stream);
		else if (isv8u8 == false)
			return new ResILImage(stream);
			
		throw new InvalidDataetc();
	}
	
	public static ResILImage Create(byte[] imageData)
	{
		using (MemoryTributary stream = new MemoryTributary(imageData))
			return Create(stream);
	}
	
	static bool? IsV8U8(MemoryTributary stream)
	{
		stream.Seek(0, SeekOrigin.Begin);
		using (BinaryReader r = new BinaryReader(stream, true)
		{
			int dwMagic = r.ReadInt32();
	        if (dwMagic != 0x20534444)
	            return null;
	
	        DDS_HEADER header = new DDS_HEADER();
	        Read_DDS_HEADER(header, r);
	
	        if (((header.ddspf.dwFlags & 0x00000004) != 0) && (header.ddspf.dwFourCC == 0x30315844 /*DX10*/))
	        {
	            throw new Exception("DX10 not supported yet!");
	        }
			
			return header.ddspf.dwFourCC == V8U8;
		}
	}
	#endregion Creation
}