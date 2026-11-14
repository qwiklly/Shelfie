using MauiApp1.Services;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using Shelfie.Client;
using Shelfie.Client.Storage;
using System.Globalization;
using ZXing.Net.Maui.Controls;

namespace MauiApp1
{
	public static class MauiProgram
	{
		public static MauiApp CreateMauiApp()
		{
			var builder = MauiApp.CreateBuilder();
			builder.Logging.AddDebug();

			builder
				.UseMauiApp<App>()
				.UseLocalNotification()
				.UseBarcodeReader()
				.ConfigureFonts(fonts =>
				{
					fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				});

			builder.Services.AddMauiBlazorWebView();
			builder.Services.AddLocalization();
			builder.Services.AddHttpClient();

			// Offline-first core: one local database for the app's lifetime.
			builder.Services.AddSingleton<AppSettings>();
			builder.Services.AddSingleton(TimeProvider.System);
			builder.Services.AddSingleton(_ => new LocalDatabase(Path.Combine(FileSystem.AppDataDirectory, "shelfie.db3")));
			builder.Services.AddSingleton<LocalInventory>();
			builder.Services.AddSingleton<ITokenStore, SecureTokenStore>();
			builder.Services.AddSingleton(sp => new ShelfieApi(
				sp.GetRequiredService<IHttpClientFactory>().CreateClient("shelfie"),
				() => sp.GetRequiredService<AppSettings>().ServerUrl,
				sp.GetRequiredService<ITokenStore>()));
			builder.Services.AddSingleton<SyncService>();
			builder.Services.AddSingleton<AccountManager>();
			builder.Services.AddSingleton(sp => new ProductLookup(
				sp.GetRequiredService<IHttpClientFactory>().CreateClient("openfoodfacts"),
				sp.GetRequiredService<LocalInventory>()));

			// Device features
			builder.Services.AddSingleton<NotificationScheduler>();
			builder.Services.AddSingleton<SyncCoordinator>();
			builder.Services.AddSingleton<ScannerService>();
			builder.Services.AddSingleton<PhotoService>();
			builder.Services.AddSingleton<Toasts>();

			var culture = new CultureInfo(Preferences.Get("language", Services.AppSettings.DefaultLanguage));
			CultureInfo.DefaultThreadCurrentCulture = culture;
			CultureInfo.DefaultThreadCurrentUICulture = culture;
#if DEBUG
			builder.Services.AddBlazorWebViewDeveloperTools();
			builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

			return builder.Build();
		}
	}
}
