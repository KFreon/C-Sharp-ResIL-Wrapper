public interface IResILImage : IDisposable
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
	
	Dispose();
}