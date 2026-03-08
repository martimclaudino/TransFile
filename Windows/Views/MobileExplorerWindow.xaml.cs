using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Windows.Views
{
    public partial class MobileExplorerWindow : Window
    {
        private readonly string _mobileIp;
        // CORREÇÃO: A raiz do armazenamento dos utilizadores no Android é /storage/emulated/0
        private string _currentPath = "/storage/emulated/0";

        // CORREÇÃO DE WARNING: Adicionado '?' para indicar que pode ser nulo no início
        public RemoteFileItem? SelectedFileToDownload { get; private set; }

        public MobileExplorerWindow(string mobileIp)
        {
            InitializeComponent();
            _mobileIp = mobileIp;
            _ = LoadDirectory(_currentPath);
        }

        private async Task LoadDirectory(string path)
        {
            try
            {
                TxtCurrentPath.Text = path;
                using var client = new HttpClient();

                // CORREÇÃO: O endpoint na app Mobile é /list e não /listfiles
                string url = $"http://{_mobileIp}:8081/list?path={Uri.EscapeDataString(path)}";

                string json = await client.GetStringAsync(url);

                var items = JsonSerializer.Deserialize<List<RemoteFileItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (items != null)
                {
                    // Ordena: Pastas primeiro, ficheiros depois
                    var sortedItems = items.OrderByDescending(x => x.IsDirectory).ThenBy(x => x.Name).ToList();

                    // Define o ícone com base em ser pasta ou não
                    foreach (var item in sortedItems)
                    {
                        item.Icon = item.IsDirectory ? "📁" : "📄";
                    }

                    LstExplorer.ItemsSource = sortedItems;
                    _currentPath = path;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load directory: {ex.Message}");
            }
        }

        private void LstExplorer_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LstExplorer.SelectedItem is RemoteFileItem selectedItem)
            {
                if (selectedItem.IsDirectory)
                {
                    // Se for pasta, entra nela!
                    _ = LoadDirectory(selectedItem.Path);
                }
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            // Evita que o utilizador consiga recuar além da pasta de armazenamento interno permitida pelo Android
            if (_currentPath != "/storage/emulated/0" && !string.IsNullOrEmpty(_currentPath))
            {
                int lastSlash = _currentPath.TrimEnd('/').LastIndexOf('/');
                if (lastSlash >= 0)
                {
                    string parentPath = lastSlash == 0 ? "/" : _currentPath.Substring(0, lastSlash);

                    // Se tentar subir demais, trava-o no root do utilizador
                    if (parentPath.Length < "/storage/emulated/0".Length && _currentPath.StartsWith("/storage/emulated/0"))
                    {
                        parentPath = "/storage/emulated/0";
                    }

                    _ = LoadDirectory(parentPath);
                }
            }
        }

        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            if (LstExplorer.SelectedItem is RemoteFileItem selectedItem)
            {
                if (selectedItem.IsDirectory)
                {
                    MessageBox.Show("Please select a file, not a folder, to download.");
                    return;
                }

                // Guarda o ficheiro e fecha a janela dizendo que teve sucesso
                SelectedFileToDownload = selectedItem;
                this.DialogResult = true;
                this.Close();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }

    // A tua classe de modelo
    public class RemoteFileItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public string Icon { get; set; } = string.Empty;
    }
}