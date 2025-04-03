using System.Globalization;

namespace MauiApp1
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            var language = Preferences.Get("language", "ru-RU");

            SetCulture(language);

            MainPage = new MainPage();
        }

        // Метод установки культуры
        private void SetCulture(string language)
        {
            var culture = new CultureInfo(language);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
    }
}