using Blazored.LocalStorage;
using MauiApp1.Services;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace MauiApp1
{
	public static class MauiProgram
	{
		/// <summary>
		/// The Android emulator reaches the host machine through 10.0.2.2; other platforms use localhost.
		/// </summary>
		public static string ApiBaseUrl =>
			DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5111/api/" : "http://localhost:5111/api/";

		public static MauiApp CreateMauiApp()
		{
			var builder = MauiApp.CreateBuilder();
			builder.Logging.AddDebug();
			builder.Logging.SetMinimumLevel(LogLevel.Debug);

			builder
				.UseMauiApp<App>()
				.ConfigureFonts(fonts =>
				{
					fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				});

			builder.Services.AddMauiBlazorWebView();

			// One HttpClient for the API; the handler attaches the JWT to every request.
			builder.Services.AddTransient<AuthHeaderHandler>();
			builder.Services.AddHttpClient("ShelfieApi", client =>
				{
					client.BaseAddress = new Uri(ApiBaseUrl);
					client.Timeout = TimeSpan.FromSeconds(30);
				})
				.AddHttpMessageHandler<AuthHeaderHandler>();
			builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("ShelfieApi"));

			// Services to work with API
			builder.Services.AddScoped<AuthService>();
			builder.Services.AddScoped<ProductService>();
			builder.Services.AddScoped<MedicationService>();
			builder.Services.AddScoped<CategoryService>();
			builder.Services.AddScoped<FieldService>();
			builder.Services.AddScoped<FieldValueService>();

			builder.Services.AddLocalization();

			builder.Services.AddBlazoredLocalStorage();

			var language = Preferences.Get("language", "ru-RU");
			SetCulture(language);
#if DEBUG
			builder.Services.AddBlazorWebViewDeveloperTools();
#endif

			return builder.Build();
		}

		private static void SetCulture(string language)
		{
			var culture = new CultureInfo(language);
			CultureInfo.DefaultThreadCurrentCulture = culture;
			CultureInfo.DefaultThreadCurrentUICulture = culture;
		}
	}
}
