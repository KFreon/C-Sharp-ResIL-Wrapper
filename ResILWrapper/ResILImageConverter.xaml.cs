using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ResILWrapper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class ResILImageConverter : Window
    {
        ViewModel vm = null;
        public ResILImageConverter()
        {
            InitializeComponent();
            vm = new ViewModel();
            DataContext = vm;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Select destination";
            sfd.Filter = String.IsNullOrEmpty(vm.SelectedFormat) ? "" : ResILImage.GetFilterString(ResILImage.GetExtensionFromFormat(vm.SelectedFormat));
            if (sfd.ShowDialog() == true)
                vm.SavePath = sfd.FileName;
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select image";
            ofd.Filter = UsefulThings.General.GetExtsAsFilter(vm.exts, "Image files");
            if (ofd.ShowDialog() == true)
                vm.LoadImage(ofd.FileName);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            vm.Save();
        }
    }
}
