using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ResILWrapper
{
    /// <summary>
    /// View model for ResIL Wrapper converter.
    /// </summary>
    public class ViewModel : INotifyPropertyChanged
    {
        #region Properties

        // KFreon: Indicates whether an image is loaded or not.
        bool loaded = false;
        public bool isLoaded
        {
            get
            {
                return loaded;
            }
            set
            {
                loaded = value;
                OnPropertyChanged();
            }
        }

        // KFreon: Indicates whether save path is set.
        bool validsave = false;
        public bool ValidSavePath
        {
            get
            {
                return validsave;
            }
            set
            {
                validsave = value;
                OnPropertyChanged();
            }
        }

        // KFreon: Indicates whether a suitable format has been set in GUI.
        bool validformat = false;
        public bool ValidFormatSelected
        {
            get
            {
                return validformat;
            }
            set
            {
                validformat = value;
                OnPropertyChanged();
            }
        }

        // KFreon: Indicates whether all properties related to saving are set.
        bool savable = false;
        public bool isSavable
        {
            get
            {
                return savable;
            }
            set
            {
                savable = value;
                OnPropertyChanged();
            }
        }

        // KFreon: Loaded image as loaded into ResIL
        ResILImage im = null;
        public ResILImage img
        {
            get
            {
                return im;
            }
            set
            {
                im = value;
                bitmap = im.ToImage();
                isLoaded = true;
                OnPropertyChanged();
            }
        }

        // KFreon: Path to save new image to.
        string savepath = null;
        public string SavePath
        {
            get
            {
                return savepath;
            }
            set
            {
                savepath = value;
                ValidSavePath = !String.IsNullOrEmpty(value);
                isSavable = isLoaded && ValidFormatSelected && ValidSavePath;
                OnPropertyChanged();
            }
        }

        // KFreon: Formats used in the Combobox
        public List<string> Formats { get; private set; }
        string form = null;

        // KFreon: Format as selected in Combobox
        public string SelectedFormat
        {
            get
            {
                return form;
            }
            set
            {
                form = value;
                ValidFormatSelected = value != null;
                isSavable = isLoaded && ValidFormatSelected && ValidSavePath;
                OnPropertyChanged();
            }
        }

        // KFreon: Status indicator
        string status = null;
        public string Status
        {
            get
            {
                return status;
            }
            set
            {
                status = value;
                OnPropertyChanged();
            }
        }

        // KFreon: Valid image extensions. Probably not limited to these, but there needs to be some limitations.
        public List<string> exts = new List<string> { ".dds", ".png", ".jpg", ".bmp", ".gif" };

        // KFreon: Image preview
        BitmapImage bmp = null;
        public BitmapImage bitmap
        {
            get
            {
                return bmp;
            }
            set
            {
                bmp = value;
                OnPropertyChanged();
            }
        }
        #endregion

        bool generateMips = true;
        public bool GenerateMips
        {
            get
            {
                return generateMips;
            }
            set
            {
                generateMips = value;
                OnPropertyChanged();
            }
        }

        public ViewModel(ResILImage im) : this()
        {
            img = im;
        }

        public ViewModel()
        {
            Formats = new List<string>(ResILImage.ValidFormats);
            Status = "Ready.";
        }


        /// <summary>
        /// Loads specified image into ResIL.
        /// </summary>
        /// <param name="file">File to load.</param>
        public void LoadImage(string file)
        {
            img = new ResILImage(file);
            Status = "Loaded.";
        }


        /// <summary>
        /// Saves loaded image using GUI set format and save path.
        /// </summary>
        public void Save()
        {
            Status = "Saving...";

            string tempformat = SelectedFormat;
            if (tempformat.Contains("\\"))
                tempformat = tempformat.Split('\\')[0];

            if (tempformat.Contains('/'))
                tempformat = tempformat.Split('/')[0];

            // KFreon: DDS's
            if (SelectedFormat.Contains("DXT") || SelectedFormat.Contains("V8U8") || SelectedFormat.Contains("ATI"))
            {
                ResIL.Unmanaged.CompressedDataFormat surface = (ResIL.Unmanaged.CompressedDataFormat)Enum.Parse(typeof(ResIL.Unmanaged.CompressedDataFormat), tempformat);

                img.ConvertAndSave(ResIL.Unmanaged.ImageType.Dds, SavePath, GenerateMips ? ResILImage.MipMapMode.BuildAll : ResILImage.MipMapMode.None, surface);
            }
            else  // KFreon: Everything else
            {
                ResIL.Unmanaged.ImageType type = (ResIL.Unmanaged.ImageType)Enum.Parse(typeof(ResIL.Unmanaged.ImageType), tempformat);
                img.ConvertAndSave(type, SavePath);
            }
            Status = "Saved!";
        }


        #region Property Change Notification
        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
        #endregion
    }
}
