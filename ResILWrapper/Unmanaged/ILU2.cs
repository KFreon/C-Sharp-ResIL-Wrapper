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

        public static bool BuildMipmaps(IntPtr handle)
        {
            return ilu2BuildMipmaps(handle);
        }


        public static bool RemoveMips(IntPtr handle)
        {
            return ilu2DestroyMipmaps(handle);
        }

        [DllImport(ILU2DLL, EntryPoint = "ilu2BuildMipmaps")]
        private static extern bool ilu2BuildMipmaps(IntPtr img);

        [DllImport(ILU2DLL, EntryPoint = "ilu2DestroyMipmaps")]  // Dunno why need ordinal here, but name doesn't work
        private static extern bool ilu2DestroyMipmaps(IntPtr img);
    }
}
