using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using ZXing.Net.Maui;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.IO;

namespace Mobile;

public class SharedFileItem
{
	public string FilePath { get; set; } = string.Empty;
	public string FileName { get; set; } = string.Empty;
}

public partial class MainPage : ContentPage
{
	public ObservableCollection<SharedFileItem> SelectedFiles { get; set; }
	private double _screenWidth;

	// New variables to store PC connection and Download Path
	private string _serverUrl = string.Empty;
	private string _downloadPath = string.Empty;

	public MainPage()
	{
		InitializeComponent();

		SelectedFiles = new ObservableCollection<SharedFileItem>();
		LstFiles.ItemsSource = SelectedFiles;
		SelectedFiles.CollectionChanged += SelectedFiles_CollectionChanged;

		// Configure the barcode reader to look for QR Codes specifically
		BarcodeReader.Options = new BarcodeReaderOptions
		{
			Formats = BarcodeFormats.TwoDimensional,
			AutoRotate = true,
			Multiple = false
		};

		// Inicia o mini-servidor do telemóvel em segundo plano!
		_ = StartMobileServer();
	}

	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		if (width > 0 && _screenWidth != width)
		{
			_screenWidth = width;
			TabIndicator.WidthRequest = width / 2;
			ViewAccess.TranslationX = width;
			SettingsOverlay.TranslationX = -width;
		}
	}

	private void SelectedFiles_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
	{
		bool hasFiles = SelectedFiles.Count > 0;
		BtnShare.IsEnabled = hasFiles;
		BtnShare.Opacity = hasFiles ? 1.0 : 0.5;
	}

	// ==============================================================
	// Tab Animations
	// ==============================================================
	private async void TabShare_Tapped(object? sender, TappedEventArgs e)
	{
		LblTabShare.TextColor = Color.FromArgb("#1F2937");
		LblTabShare.FontAttributes = FontAttributes.Bold;
		LblTabAccess.TextColor = Color.FromArgb("#6B7280");
		LblTabAccess.FontAttributes = FontAttributes.None;

		await Task.WhenAll(
			TabIndicator.TranslateToAsync(0, 0, 250, Easing.CubicOut),
			ViewShare.TranslateToAsync(0, 0, 250, Easing.CubicOut),
			ViewAccess.TranslateToAsync(_screenWidth, 0, 250, Easing.CubicOut)
		);
	}

	private async void TabAccess_Tapped(object? sender, TappedEventArgs e)
	{
		LblTabAccess.TextColor = Color.FromArgb("#1F2937");
		LblTabAccess.FontAttributes = FontAttributes.Bold;
		LblTabShare.TextColor = Color.FromArgb("#6B7280");
		LblTabShare.FontAttributes = FontAttributes.None;

		await Task.WhenAll(
			TabIndicator.TranslateToAsync(_screenWidth / 2, 0, 250, Easing.CubicOut),
			ViewShare.TranslateToAsync(-_screenWidth, 0, 250, Easing.CubicOut),
			ViewAccess.TranslateToAsync(0, 0, 250, Easing.CubicOut)
		);
	}

	// ==============================================================
	// Settings Overlay Animations
	// ==============================================================
	private async void OpenSettings_Tapped(object? sender, TappedEventArgs e)
	{
		await SettingsOverlay.TranslateToAsync(0, 0, 300, Easing.CubicOut);
	}

	private async void CloseSettings_Clicked(object? sender, EventArgs e)
	{
		await SettingsOverlay.TranslateToAsync(-_screenWidth, 0, 300, Easing.CubicIn);
	}

	// ==============================================================
	// File Management
	// ==============================================================
	private async void AddFiles_Clicked(object? sender, EventArgs e)
	{
		var results = await FilePicker.PickMultipleAsync();

		if (results != null)
		{
			foreach (var file in results)
			{
				SelectedFiles.Add(new SharedFileItem
				{
					FileName = file.FileName ?? "Unknown file",
					FilePath = file.FullPath ?? string.Empty
				});
			}
		}
	}

	private void RemoveFile_Clicked(object? sender, EventArgs e)
	{
		if (sender is Button btn && btn.CommandParameter is SharedFileItem fileToRemove)
		{
			SelectedFiles.Remove(fileToRemove);
		}
	}

	// ==============================================================
	// Camera & Connection Logic
	// ==============================================================
	private async void ConnectScanner_Clicked(object? sender, EventArgs e)
	{
		// 1. Close the Settings Menu smoothly
		await SettingsOverlay.TranslateToAsync(-_screenWidth, 0, 300, Easing.CubicIn);

		// 2. Ask Android for Camera permissions
		var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
		if (status != PermissionStatus.Granted)
		{
			status = await Permissions.RequestAsync<Permissions.Camera>();
		}

		// 3. If granted, show the camera overlay!
		if (status == PermissionStatus.Granted)
		{
			CameraOverlay.IsVisible = true;
			BarcodeReader.IsDetecting = true;
		}
		else
		{
			await DisplayAlert("Permission Denied", "Camera permission is required to scan the QR code.", "OK");
		}
	}

	private void CancelCamera_Click(object sender, EventArgs e)
	{
		BarcodeReader.IsDetecting = false;
		CameraOverlay.IsVisible = false;
	}

	private void BarcodeReader_BarcodesDetected(object sender, BarcodeDetectionEventArgs e)
	{
		var firstResult = e.Results?.FirstOrDefault();
		if (firstResult != null)
		{
			// Stop detecting immediately
			BarcodeReader.IsDetecting = false;

			// UI updates must be done on the Main Thread
			Dispatcher.Dispatch(async () =>
			{
				CameraOverlay.IsVisible = false;

				// Save the PC's IP address (e.g., http://192.168.1.10:8080)
				_serverUrl = firstResult.Value;

				// AVISA O PC QUE ESTAMOS LIGADOS PARA ELE GUARDAR O NOSSO IP
				try
				{
					using var client = new HttpClient();
					await client.GetAsync(_serverUrl.TrimEnd('/') + "/connect");
				}
				catch { }

				await DisplayAlert("Success", $"Connected to PC at:\n{_serverUrl}", "OK");
			});
		}
	}

	// ==============================================================
	// Folder Selection Logic (CommunityToolkit)
	// ==============================================================
	private async void SelectFolder_Click(object sender, EventArgs e)
	{
		try
		{
			var result = await FolderPicker.Default.PickAsync(default);
			if (result.IsSuccessful)
			{
				_downloadPath = result.Folder.Path;
				BtnDownloadFolder.Text = result.Folder.Name;
				await DisplayAlert("Folder Selected", $"Files will be saved to:\n{_downloadPath}", "OK");
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", $"Failed to select folder: {ex.Message}", "OK");
		}
	}

	// ==============================================================
	// Network Transfer: Send to PC
	// ==============================================================
	private async void ShareToPC_Clicked(object? sender, EventArgs e)
	{
		if (string.IsNullOrEmpty(_serverUrl))
		{
			await DisplayAlert("Hold on", "You need to connect to the PC first (Scan the QR Code)!", "OK");
			return;
		}

		BtnShare.Text = "Sending...";
		BtnShare.IsEnabled = false;

		try
		{
			using var client = new HttpClient();
			string uploadUrl = _serverUrl.TrimEnd('/') + "/upload";

			// Envia um ficheiro de cada vez
			foreach (var file in SelectedFiles)
			{
				using var fileStream = File.OpenRead(file.FilePath);
				using var content = new StreamContent(fileStream);

				// Colocamos o nome do ficheiro no cabeçalho (Header) para o PC saber o que está a receber
				content.Headers.Add("X-FileName", Uri.EscapeDataString(file.FileName));

				var response = await client.PostAsync(uploadUrl, content);

				if (!response.IsSuccessStatusCode)
				{
					await DisplayAlert("Error", $"PC rejected the file {file.FileName}. Status: {response.StatusCode}", "OK");
				}
			}

			await DisplayAlert("Success", "All files sent to PC successfully!", "OK");
			SelectedFiles.Clear();
		}
		catch (Exception ex)
		{
			await DisplayAlert("Network Error", $"Failed to send files: {ex.Message}", "OK");
		}
		finally
		{
			BtnShare.Text = "Share";
			BtnShare.IsEnabled = true;
		}
	}

	// ==============================================================
	// Mini Server: Listen for PC files
	// ==============================================================
	private async Task StartMobileServer()
	{
		try
		{
			HttpListener listener = new HttpListener();
			// O telemóvel fica à escuta na porta 8081
			listener.Prefixes.Add("http://*:8081/");
			listener.Start();

			while (true)
			{
				HttpListenerContext context = await listener.GetContextAsync();
				HttpListenerRequest request = context.Request;
				HttpListenerResponse response = context.Response;

				if (request.Url != null && request.Url.AbsolutePath.TrimEnd('/').Equals("/upload", StringComparison.OrdinalIgnoreCase) && request.HttpMethod == "POST")
				{
					try
					{
						string fileName = request.Headers["X-FileName"] ?? $"file_mobile_{DateTime.Now.Ticks}.dat";
						fileName = Uri.UnescapeDataString(fileName);

						// Se o utilizador não tiver selecionado uma pasta, guarda numa pasta temporária da app
						string destinationFolder = string.IsNullOrEmpty(_downloadPath)
							? FileSystem.Current.CacheDirectory
							: _downloadPath;

						string fullPath = Path.Combine(destinationFolder, fileName);

						using (var fileStream = new FileStream(fullPath, FileMode.Create))
						{
							await request.InputStream.CopyToAsync(fileStream);
						}

						// Mostra um aviso no ecrã do telemóvel
						MainThread.BeginInvokeOnMainThread(() =>
						{
							DisplayAlert("Recebido!", $"O PC enviou-te o ficheiro: {fileName}", "OK");
						});

						response.StatusCode = 200;
					}
					catch
					{
						response.StatusCode = 500;
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
			System.Diagnostics.Debug.WriteLine($"Erro no servidor mobile: {ex.Message}");
		}
	}
}