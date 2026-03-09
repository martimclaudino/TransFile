using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using ZXing.Net.Maui;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.IO;
using System.Text.Json;
using Microsoft.Maui.ApplicationModel;

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
	// ATUALIZADO: Fica com o caminho de Downloads por defeito!
	private string _downloadPath = "/storage/emulated/0/Download/TransFile";

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
		await SettingsOverlay.TranslateToAsync(-_screenWidth, 0, 300, Easing.CubicIn);

		// 1. PEDIR ACESSO TOTAL AOS FICHEIROS (Apenas aplicável se for Android)
#if ANDROID
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R) // Se for Android 11 ou superior
        {
            // Verifica se a app já tem a permissão especial de ver TUDO
            if (!Android.OS.Environment.IsExternalStorageManager)
            {
                await DisplayAlert("Permissão Necessária", "Para que o PC consiga ver todos os ficheiros, o Android exige que dês permissão de 'Gestão de todos os ficheiros'. Vais ser redirecionado para as definições. Ativa a opção para a TransFile e volta atrás.", "Entendido");
                
                // Abre a página exata das definições do Android para o utilizador ativar
                var intent = new Android.Content.Intent(Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
                intent.AddCategory("android.intent.category.DEFAULT");
                intent.SetData(Android.Net.Uri.Parse($"package:{AppInfo.Current.PackageName}"));
                Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.StartActivity(intent);
                
                return; // O utilizador vai às definições. Quando voltar, tem de clicar no botão "Phone" de novo!
            }
        }
        else
        {
            // Para Android 10 ou inferior, a permissão antiga chega
            var storageStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
            if (storageStatus != PermissionStatus.Granted)
            {
                await Permissions.RequestAsync<Permissions.StorageRead>();
            }
        }
#endif

		// 2. PEDIR ACESSO À CÂMARA E ABRIR O SCANNER
		var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
		if (status != PermissionStatus.Granted)
		{
			status = await Permissions.RequestAsync<Permissions.Camera>();
		}

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
			BarcodeReader.IsDetecting = false;

			Dispatcher.Dispatch(async () =>
			{
				CameraOverlay.IsVisible = false;
				_serverUrl = firstResult.Value;

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

			foreach (var file in SelectedFiles)
			{
				using var fileStream = File.OpenRead(file.FilePath);
				using var content = new StreamContent(fileStream);

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
	// ABRIR EXPLORADOR DO PC (NOVO)
	// ==============================================================
	private async void OpenPcExplorer_Clicked(object sender, EventArgs e)
	{
		if (string.IsNullOrEmpty(_serverUrl))
		{
			await DisplayAlert("Not connected", "Please scan the PC's QR code first!", "OK");
			return;
		}

		await Navigation.PushModalAsync(new PcExplorerPage(_serverUrl, _downloadPath));
	}

	// ==============================================================
	// Mini Server: Listen for PC files
	// ==============================================================
	private async Task StartMobileServer()
	{
		try
		{
			HttpListener listener = new HttpListener();
			listener.Prefixes.Add("http://*:8081/");
			listener.Start();

			while (true)
			{
				HttpListenerContext context = await listener.GetContextAsync();
				HttpListenerRequest request = context.Request;
				HttpListenerResponse response = context.Response;

				// 1. RECEIVE FILES FROM PC
				if (request.Url != null && request.Url.AbsolutePath.TrimEnd('/').Equals("/upload", StringComparison.OrdinalIgnoreCase) && request.HttpMethod == "POST")
				{
					try
					{
						string fileName = request.Headers["X-FileName"] ?? $"file_mobile_{DateTime.Now.Ticks}.dat";
						fileName = Uri.UnescapeDataString(fileName);

						// Usa o _downloadPath (que agora tem um valor padrão por defeito)
						string destinationFolder = string.IsNullOrEmpty(_downloadPath)
							? "/storage/emulated/0/Download/TransFile"
							: _downloadPath;

						// GARANTE QUE A PASTA EXISTE ANTES DE GRAVAR
						if (!Directory.Exists(destinationFolder))
						{
							Directory.CreateDirectory(destinationFolder);
						}

						string fullPath = Path.Combine(destinationFolder, fileName);

						using (var fileStream = new FileStream(fullPath, FileMode.Create))
						{
							await request.InputStream.CopyToAsync(fileStream);
						}

						// AVISAR O SISTEMA ANDROID PARA MOSTRAR O FICHEIRO NA GALERIA/EXPLORADOR
#if ANDROID
                        try
                        {
                            Android.Media.MediaScannerConnection.ScanFile(
                                Microsoft.Maui.ApplicationModel.Platform.AppContext, 
                                new string[] { fullPath }, 
                                null, 
                                null);
                        }
                        catch { /* Ignorar erros do scanner */ }
#endif

						MainThread.BeginInvokeOnMainThread(() =>
						{
							DisplayAlert("Received!", $"The PC sent you a file:\n{fileName}\n\nSaved in:\n{destinationFolder}", "OK");
						});

						response.StatusCode = 200;
					}
					catch { response.StatusCode = 500; }
				}
				// 2. SEND DIRECTORY LIST TO PC (FILE EXPLORER)
				else if (request.Url != null && request.Url.AbsolutePath.TrimEnd('/').Equals("/list", StringComparison.OrdinalIgnoreCase) && request.HttpMethod == "GET")
				{
					try
					{
						string requestedPath = request.QueryString["path"];

						if (string.IsNullOrEmpty(requestedPath))
						{
							requestedPath = "/storage/emulated/0"; // Diretoria principal no Android
						}

						var items = new System.Collections.Generic.List<object>();
						var di = new DirectoryInfo(requestedPath);

						if (di.Exists)
						{
							// Apanha as pastas e isola os erros
							try
							{
								foreach (var dir in di.GetDirectories())
								{
									items.Add(new { Name = dir.Name, Path = dir.FullName, IsDirectory = true });
								}
							}
							catch { /* Se o Android bloquear pastas de sistema, simplesmente ignora-as e avança */ }

							// Apanha os ficheiros e isola os erros
							try
							{
								foreach (var file in di.GetFiles())
								{
									items.Add(new { Name = file.Name, Path = file.FullName, IsDirectory = false });
								}
							}
							catch { /* Ignora se houver ficheiros bloqueados */ }
						}

						string json = JsonSerializer.Serialize(items);
						byte[] buf = System.Text.Encoding.UTF8.GetBytes(json);

						response.ContentType = "application/json";
						response.ContentLength64 = buf.Length;
						await response.OutputStream.WriteAsync(buf, 0, buf.Length);
						response.StatusCode = 200;
					}
					catch (Exception)
					{
						response.StatusCode = 500;
					}
				}
				// 3. SERVE SPECIFIC FILE TO PC BY FULL PATH
				else if (request.Url != null && request.Url.AbsolutePath.TrimEnd('/').Equals("/downloadfile", StringComparison.OrdinalIgnoreCase) && request.HttpMethod == "GET")
				{
					try
					{
						string filePath = request.QueryString["path"];

						if (!string.IsNullOrEmpty(filePath))
						{
							filePath = Uri.UnescapeDataString(filePath);

							if (File.Exists(filePath))
							{
								byte[] fileBytes = File.ReadAllBytes(filePath);
								response.ContentType = "application/octet-stream";
								response.ContentLength64 = fileBytes.Length;
								response.AddHeader("Content-Disposition", $"attachment; filename=\"{Path.GetFileName(filePath)}\"");

								await response.OutputStream.WriteAsync(fileBytes, 0, fileBytes.Length);
								response.StatusCode = 200;
							}
							else
							{
								response.StatusCode = 404; // File not found
							}
						}
						else
						{
							response.StatusCode = 400; // Bad request
						}
					}
					catch { response.StatusCode = 500; }
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