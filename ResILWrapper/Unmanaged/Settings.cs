using ResIL.Unmanaged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResIL
{
    public static class Settings
    {
        /// <summary>
        /// Sets DXTC surface format globally in ResIL.
        /// </summary>
        /// <param name="format">Surface format to set.</param>
        public static void SetDXTCFormat(CompressedDataFormat format)
        {
            IL2.Settings.SetDXTcFormat(format);
        }


        /// <summary>
        /// Setting for keeping DXTC data when loading.
        /// </summary>
        /// <param name="keep">True = Keep DXTC.</param>
        public static void KeepDXTC(bool keep)
        {
            IL2.Settings.KeepDXTC(keep);
        }

        #region Compression/Quality
        /// <summary>
        /// Setting for JPG quality.
        /// </summary>
        /// <param name="quality">Quality. Valid range: 0-100.</param>
        public static void SetJPGQuality(int quality)
        {
            IL2.Settings.SetJPGQuality((uint)quality);
        }

        
        /// <summary>
        /// Setting for using the Squish library.
        /// </summary>
        /// <param name="squish">True = use Squish, False = nVidia.</param>
        public static void SetSquishCompression(bool squish)
        {
            IL2.Settings.SetSquishCompression(squish);
        }
        #endregion

    }
}
