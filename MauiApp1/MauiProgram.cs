using Blazored.LocalStorage;
using MauiApp1.Services;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace MauiApp1
{
	public static class MauiProgram
	{
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

			builder.Services.AddScoped(sp => new HttpClient
			{
                BaseAddress = new Uri("http://10.0.2.2:5111/api/")
            });

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
			builder.Logging.AddDebug();
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

