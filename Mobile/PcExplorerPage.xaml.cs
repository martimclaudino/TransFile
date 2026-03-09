using System.Text.Json;

namespace Mobile;

public partial class PcExplorerPage : ContentPage
{
    private readonly string _pcUrl;
    private readonly string _downloadPath; // NOVA VARIÁVEL
    private string _currentPath = "root";

    // ATUALIZADO: Agora recebe também o downloadPath
    public PcExplorerPage(string pcUrl, string downloadPath)
    {
        InitializeComponent();
        _pcUrl = pcUrl.TrimEnd('/');
        _downloadPath = downloadPath;
        _ = LoadDirectory("root");
    }

    private async Task LoadDirectory(string path)
    {
        try
        {
            Loader.IsRunning = true;
            Loader.IsVisible = true;
            ColFiles.ItemsSource = null;

            LblCurrentPath.Text = path == "root" ? "This PC" : path;

            using var client = new HttpClient();
            string url = $"{_pcUrl}/list?path={Uri.EscapeDataString(path)}";
            string json = await client.GetStringAsync(url);

            var items = JsonSerializer.Deserialize<List<PcFileItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (items != null)
            {
                var sortedItems = items.OrderByDescending(x => x.IsDirectory).ThenBy(x => x.Name).ToList();
                foreach (var item in sortedItems)
                {
                    item.Icon = item.IsDirectory ? "📁" : "📄";
                }
                ColFiles.ItemsSource = sortedItems;
                _currentPath = path;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not load PC files: {ex.Message}", "OK");
        }
        finally
        {
            Loader.IsRunning = false;
            Loader.IsVisible = false;
        }
    }

    private void BtnBack_Clicked(object sender, EventArgs e)
    {
        if (_currentPath != "root")
        {
            int lastSlash = _currentPath.TrimEnd('\\').LastIndexOf('\\');
            if (lastSlash >= 0)
            {
                string parentPath = _currentPath.Substring(0, lastSlash + 1);
                _ = LoadDirectory(parentPath);
            }
            else
            {
                _ = LoadDirectory("root");
            }
        }
        else
        {
            // Se já estiver no root e clicar em back, fecha o explorador
            Navigation.PopModalAsync();
        }
    }

    private async void ColFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is PcFileItem selectedItem)
        {
            ColFiles.SelectedItem = null;

            if (selectedItem.IsDirectory)
            {
                _ = LoadDirectory(selectedItem.Path);
            }
            else
            {
                bool answer = await DisplayAlert("Download", $"Do you want to download '{selectedItem.Name}' to your phone?", "Yes", "No");
                if (answer)
                {
                    await DownloadFileFromPc(selectedItem);
                }
            }
        }
    }

    private async Task DownloadFileFromPc(PcFileItem fileItem)
    {
        try
        {
            Loader.IsRunning = true;
            Loader.IsVisible = true;

            using var client = new HttpClient();
            string url = $"{_pcUrl}/downloadfile?path={Uri.EscapeDataString(fileItem.Path)}";

            byte[] fileBytes = await client.GetByteArrayAsync(url);

            // Evitar a pasta raiz ou pastas restritas (como DCIM). 
            // Usa a pasta Downloads por defeito.
            string destFolder = string.IsNullOrEmpty(_downloadPath) || _downloadPath.Contains("DCIM")
                ? "/storage/emulated/0/Download/TransFile"
                : _downloadPath;

            if (!Directory.Exists(destFolder))
            {
                Directory.CreateDirectory(destFolder);
            }

            string savePath = Path.Combine(destFolder, fileItem.Name);
            File.WriteAllBytes(savePath, fileBytes);

            // AVISAR O SISTEMA ANDROID QUE O FICHEIRO EXISTE
            // (Isto força o ficheiro a aparecer instantaneamente nas apps de Ficheiros e Galeria)
#if ANDROID
            try
            {
                Android.Media.MediaScannerConnection.ScanFile(
                    Microsoft.Maui.ApplicationModel.Platform.AppContext, 
                    new string[] { savePath }, 
                    null, 
                    null);
            }
            catch { /* Ignorar se o scanner falhar */ }
#endif

            await DisplayAlert("Success", $"File downloaded to:\n{savePath}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to download: {ex.Message}", "OK");
        }
        finally
        {
            Loader.IsRunning = false;
            Loader.IsVisible = false;
        }
    }
}

public class PcFileItem
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public string Icon { get; set; } = string.Empty;
}