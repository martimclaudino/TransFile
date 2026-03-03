using Microsoft.Extensions.Logging;
using ZXing.Net.Maui.Controls;
using CommunityToolkit.Maui;

namespace Mobile
{
	public static class MauiProgram
	{
		public static MauiApp CreateMauiApp()
		{
			var builder = MauiApp.CreateBuilder();
			builder
				.UseMauiApp<App>()
				.UseBarcodeReader() // Initializes the QR Code Scanner
				.UseMauiCommunityToolkit() // Initializes the Toolkit for Folder Picker
				.ConfigureFonts(fonts =>
				{
					fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
					fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				});

#if DEBUG
			builder.Logging.AddDebug();
#endif

			return builder.Build();
		}
	}
}