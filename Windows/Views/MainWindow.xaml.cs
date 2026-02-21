using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace Windows.Views
{
    public class SharedFileItem
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    public partial class MainWindow : Window
    {
        public ObservableCollection<SharedFileItem> SelectedFiles { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            SelectedFiles = new ObservableCollection<SharedFileItem>();
            LstFiles.ItemsSource = SelectedFiles;

            SelectedFiles.CollectionChanged += SelectedFiles_CollectionChanged;
        }

        // ==============================================================
        // Enable/Disable Share button
        // ==============================================================
        private void SelectedFiles_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            bool hasFiles = SelectedFiles.Count > 0;

            BtnShare.IsEnabled = hasFiles;
            BtnShare.Opacity = hasFiles ? 1.0 : 0.5;
        }

        // ==============================================================
        // Share tab
        // ==============================================================
        private void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            openFileDialog.Title = "Select files to share with your phone";

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string file in openFileDialog.FileNames)
                {
                    SelectedFiles.Add(new SharedFileItem
                    {
                        FilePath = file,
                        FileName = Path.GetFileName(file)
                    });
                }
            }
        }

        private void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is SharedFileItem fileToRemove)
            {
                SelectedFiles.Remove(fileToRemove);
            }
        }

        // ==============================================================
        // Settings tab
        // ==============================================================
        private void SelectPath_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog openFolderDialog = new OpenFolderDialog();
            openFolderDialog.Title = "Select default download folder";

            if (openFolderDialog.ShowDialog() == true)
            {
                BtnDownloadPath.Content = openFolderDialog.FolderName;
            }
        }

        // ==============================================================
        // QR codes overlay
        // ==============================================================
        private void ShowConnectionQr_Click(object sender, RoutedEventArgs e)
        {
            QrOverlayText.Text = "scan with the app on your phone to connect";
            QrOverlay.Visibility = Visibility.Visible;
        }

        private void ShowDownloadAppQr_Click(object sender, RoutedEventArgs e)
        {
            QrOverlayText.Text = "scan to download the Android app";
            QrOverlay.Visibility = Visibility.Visible;
        }

        private void CloseQr_Click(object sender, RoutedEventArgs e)
        {
            QrOverlay.Visibility = Visibility.Collapsed;
        }
    }
}