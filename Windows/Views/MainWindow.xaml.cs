using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using QRCoder;
using System.Net;
using System.Net.Sockets;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System;

namespace Windows.Views
{
    // Class that represents each file in the list
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

            // Initialize the file list
            SelectedFiles = new ObservableCollection<SharedFileItem>();
            LstFiles.ItemsSource = SelectedFiles;
            SelectedFiles.CollectionChanged += SelectedFiles_CollectionChanged;

            // Start our mailman (web server) in the background!
            _ = StartWebServer();
        }

        // Enable or disable the "Share" button based on whether there are files in the list
        private void SelectedFiles_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            bool hasFiles = SelectedFiles.Count > 0;
            BtnShare.IsEnabled = hasFiles;
            BtnShare.Opacity = hasFiles ? 1.0 : 0.5;
        }

        // ==============================================================
        // Button Functions (Add, Remove, Select Folder)
        // ==============================================================

        // Click on "+" to add files
        private void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Select files to share"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string filename in openFileDialog.FileNames)
                {
                    SelectedFiles.Add(new SharedFileItem
                    {
                        FilePath = filename,
                        FileName = Path.GetFileName(filename)
                    });
                }
            }
        }

        // Click on the red "X" to remove a file from the list
        private void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SharedFileItem itemToRemove)
            {
                SelectedFiles.Remove(itemToRemove);
            }
        }

        // Click on the Downloads folder button in settings
        private void SelectPath_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog folderDialog = new OpenFolderDialog
            {
                Title = "Select Download Folder"
            };

            if (folderDialog.ShowDialog() == true)
            {
                BtnDownloadPath.Content = folderDialog.FolderName;
            }
        }

        // ==============================================================
        // QR Code Overlay and Network Logic
        // ==============================================================

        // Get the PC's IP address on the Wi-Fi network
        private string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "127.0.0.1";
        }

        // Convert text into an Image (BitmapImage) for the screen
        private BitmapImage GenerateQrCodeImage(string textToEncode)
        {
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            {
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(textToEncode, QRCodeGenerator.ECCLevel.Q);
                using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
                {
                    byte[] qrCodeBytes = qrCode.GetGraphic(20);
                    using (var stream = new MemoryStream(qrCodeBytes))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                        return bitmap;
                    }
                }
            }
        }

        // Generate QR code for the App to connect to the PC
        private void ShowConnectionQr_Click(object sender, RoutedEventArgs e)
        {
            string myIp = GetLocalIPAddress();
            string connectionUrl = $"http://{myIp}:8080";

            QrImage.Source = GenerateQrCodeImage(connectionUrl);
            QrOverlayText.Text = $"Scan with the app on your phone to connect\nIP: {myIp}";
            QrOverlay.Visibility = Visibility.Visible;
        }

        // Generate QR code to download the APK
        private void ShowDownloadAppQr_Click(object sender, RoutedEventArgs e)
        {
            string myIp = GetLocalIPAddress();
            string downloadUrl = $"http://{myIp}:8080/download";

            QrImage.Source = GenerateQrCodeImage(downloadUrl);
            QrOverlayText.Text = $"Scan to download the Android app\nLink: {downloadUrl}";
            QrOverlay.Visibility = Visibility.Visible;
        }

        // Close the QR Code window
        private void CloseQr_Click(object sender, RoutedEventArgs e)
        {
            QrOverlay.Visibility = Visibility.Collapsed;
        }

        // ==============================================================
        // MINI WEB SERVER (The Mailman)
        // ==============================================================
        private async Task StartWebServer()
        {
            string ip = GetLocalIPAddress();
            HttpListener listener = new HttpListener();

            // Tell the server to listen on this IP and port
            listener.Prefixes.Add($"http://{ip}:8080/");

            try
            {
                listener.Start();

                // The server runs in an infinite loop waiting for requests
                while (true)
                {
                    HttpListenerContext context = await listener.GetContextAsync();
                    HttpListenerRequest request = context.Request;
                    HttpListenerResponse response = context.Response;

                    // If the phone requests /download (when it reads the QR Code)
                    if (request.Url != null && request.Url.AbsolutePath.Equals("/download", StringComparison.OrdinalIgnoreCase))
                    {
                        // Look for the file in the folder where we run the 'dotnet run' command
                        string currentDirectory = Directory.GetCurrentDirectory();
                        string apkPath = Path.Combine(currentDirectory, "TransFileMobile.apk");

                        if (File.Exists(apkPath))
                        {
                            byte[] fileBytes = File.ReadAllBytes(apkPath);

                            // Prepare the header to force the download in the phone's browser
                            response.ContentType = "application/vnd.android.package-archive";
                            response.ContentLength64 = fileBytes.Length;
                            response.AddHeader("Content-Disposition", "attachment; filename=\"TransFile.apk\"");

                            // Send the file!
                            await response.OutputStream.WriteAsync(fileBytes, 0, fileBytes.Length);
                        }
                        else
                        {
                            // If you forget to paste the APK there, it returns a 404 error
                            response.StatusCode = 404;
                        }
                    }
                    else
                    {
                        // For any other request, return OK
                        response.StatusCode = 200;
                    }

                    // Close the connection
                    response.Close();
                }
            }
            catch (HttpListenerException)
            {
                MessageBox.Show("Error: Windows blocked the server. Try running the command prompt as Administrator!", "Network Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Server error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}