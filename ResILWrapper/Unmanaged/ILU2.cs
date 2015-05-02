using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ResIL.Unmanaged
{
    public static class ILU2
    {
        const string ILU2DLL = "ILU.dll";
        /// <summary>
        /// Resizes image in ResIL. Permenant in memory. Disk is unchanged.
        /// </summary>
        /// <param name="handle">Pointer to current image.</param>
        /// <param name="width">Width of image.</param>
        /// <param name="height">Height of image.</param>
        /// <param name="bpp">Bits per pixel of current image.</param>
        /// <param name="bpc">Bits per channel of current image.</param>
        /// <returns>True if success.</returns>
        public static bool ResizeImage(IntPtr handle, uint width, uint height, byte bpp, byte bpc)
        {
            return il2ResizeImage(handle, width, height, 1, bpp, bpc);
        }


        public static bool BuildMipmaps(IntPtr handle)
        {
            return ilu2BuildMipmaps(handle);
        }


        public static bool RemoveMips(IntPtr handle)
        {
            return ilu2DestroyMipmaps(handle);
        }


        [DllImport(ILU2DLL, EntryPoint = "il2ResizeImage")]
        private static extern bool il2ResizeImage(IntPtr img, uint width, uint height, uint depth, byte bpp, byte bpc);

        [DllImport(ILU2DLL, EntryPoint = "ilu2BuildMipmaps")]
        private static extern bool ilu2BuildMipmaps(IntPtr img);

        [DllImport(ILU2DLL, EntryPoint = "ilu2DestroyMipmaps")]  // Dunno why need ordinal here, but name doesn't work
        private static extern bool ilu2DestroyMipmaps(IntPtr img);
    }
}
