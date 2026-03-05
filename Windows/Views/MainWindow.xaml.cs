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
    public class SharedFileItem
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    public partial class MainWindow : Window
    {
        public ObservableCollection<SharedFileItem> SelectedFiles { get; set; }

        // NOVA VARIÁVEL: Guarda a pasta de destino (por defeito vai para os Downloads do PC)
        private string _downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        public MainWindow()
        {
            InitializeComponent();

            SelectedFiles = new ObservableCollection<SharedFileItem>();
            LstFiles.ItemsSource = SelectedFiles;
            SelectedFiles.CollectionChanged += SelectedFiles_CollectionChanged;

            _ = StartWebServer();
        }

        private void SelectedFiles_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            bool hasFiles = SelectedFiles.Count > 0;
            BtnShare.IsEnabled = hasFiles;
            BtnShare.Opacity = hasFiles ? 1.0 : 0.5;
        }

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

        private void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SharedFileItem itemToRemove)
            {
                SelectedFiles.Remove(itemToRemove);
            }
        }

        private void SelectPath_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog folderDialog = new OpenFolderDialog
            {
                Title = "Select Download Folder"
            };

            if (folderDialog.ShowDialog() == true)
            {
                // ATUALIZADO: Guarda o caminho real para a variável e mostra no botão
                _downloadPath = folderDialog.FolderName;
                BtnDownloadPath.Content = _downloadPath;
            }
        }

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

        private void ShowConnectionQr_Click(object sender, RoutedEventArgs e)
        {
            string myIp = GetLocalIPAddress();
            string connectionUrl = $"http://{myIp}:8080";

            QrImage.Source = GenerateQrCodeImage(connectionUrl);
            QrOverlayText.Text = $"Scan with the app on your phone to connect\nIP: {myIp}";
            QrOverlay.Visibility = Visibility.Visible;
        }

        private void ShowDownloadAppQr_Click(object sender, RoutedEventArgs e)
        {
            string myIp = GetLocalIPAddress();
            string downloadUrl = $"http://{myIp}:8080/download";

            QrImage.Source = GenerateQrCodeImage(downloadUrl);
            QrOverlayText.Text = $"Scan to download the Android app\nLink: {downloadUrl}";
            QrOverlay.Visibility = Visibility.Visible;
        }

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
            listener.Prefixes.Add($"http://{ip}:8080/");

            try
            {
                listener.Start();

                while (true)
                {
                    HttpListenerContext context = await listener.GetContextAsync();
                    HttpListenerRequest request = context.Request;
                    HttpListenerResponse response = context.Response;

                    // 1. ROTA DE DOWNLOAD DO APK
                    if (request.Url != null && request.Url.AbsolutePath.Equals("/download", StringComparison.OrdinalIgnoreCase))
                    {
                        string currentDirectory = Directory.GetCurrentDirectory();
                        string apkPath = Path.Combine(currentDirectory, "TransFileMobile.apk");

                        if (File.Exists(apkPath))
                        {
                            byte[] fileBytes = File.ReadAllBytes(apkPath);
                            response.ContentType = "application/vnd.android.package-archive";
                            response.ContentLength64 = fileBytes.Length;
                            response.AddHeader("Content-Disposition", "attachment; filename=\"TransFile.apk\"");
                            await response.OutputStream.WriteAsync(fileBytes, 0, fileBytes.Length);
                        }
                        else
                        {
                            response.StatusCode = 404;
                        }
                    }
                    // 2. NOVA ROTA: RECEBER FICHEIROS DO TELEMÓVEL
                    else if (request.Url != null && request.Url.AbsolutePath.TrimEnd('/').Equals("/upload", StringComparison.OrdinalIgnoreCase) && request.HttpMethod == "POST")
                    {
                        try
                        {
                            // Apanha o nome do ficheiro que o telemóvel enviou no cabeçalho
                            string fileName = request.Headers["X-FileName"] ?? $"file_{DateTime.Now.Ticks}.dat";
                            fileName = Uri.UnescapeDataString(fileName); // Descodifica espaços e acentos

                            string fullPath = Path.Combine(_downloadPath, fileName);

                            // Pega nos dados (Stream) e guarda diretamente na pasta selecionada
                            using (var fileStream = new FileStream(fullPath, FileMode.Create))
                            {
                                await request.InputStream.CopyToAsync(fileStream);
                            }

                            // Mostra um aviso na interface do PC a dizer que recebeu o ficheiro
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"File received successfully:\n{fileName}", "Incoming File", MessageBoxButton.OK, MessageBoxImage.Information);
                            });

                            response.StatusCode = 200;
                        }
                        catch (Exception ex)
                        {
                            response.StatusCode = 500;
                            Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                        }
                    }
                    else
                    {
                        response.StatusCode = 200;
                    }

                    response.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Server error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}