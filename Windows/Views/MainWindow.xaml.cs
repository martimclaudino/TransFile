using Microsoft.Win32;
using System.Diagnostics;
using System.Windows;

namespace Windows.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void CloseQr_Click(object sender, RoutedEventArgs e)
        {
            BtnGenerateQr.IsChecked = false;
        }

        private void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            openFileDialog.Title = "Select files to share with your phone";

            if (openFileDialog.ShowDialog() == true)
            {
                string[] selectedFiles = openFileDialog.FileNames;
                foreach (string file in selectedFiles)
                {
                    Debug.WriteLine($"Ficheiro selecionado: {file}");
                }
            }
        }

        private void SelectPath_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog openFolderDialog = new OpenFolderDialog();
            openFolderDialog.Title = "Select default download folder";

            if (openFolderDialog.ShowDialog() == true)
            {
                BtnDownloadPath.Content = openFolderDialog.FolderName;
            }
        }
    }
}