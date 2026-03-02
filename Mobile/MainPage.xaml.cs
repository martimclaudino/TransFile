using System.Collections.ObjectModel;

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

	public MainPage()
	{
		InitializeComponent();

		SelectedFiles = new ObservableCollection<SharedFileItem>();
		LstFiles.ItemsSource = SelectedFiles;
		SelectedFiles.CollectionChanged += SelectedFiles_CollectionChanged;
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
				// Os '?? string.Empty' protegem contra valores nulos
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

	private async void ConnectScanner_Clicked(object? sender, EventArgs e)
	{
		await DisplayAlertAsync("Camera", "Here we will open the camera to scan the PC's QR Code!", "OK");
	}
}