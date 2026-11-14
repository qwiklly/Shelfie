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
        }

        // MainPage setter is obsolete since .NET MAUI 9: the root page is provided through CreateWindow.
        protected override Window CreateWindow(IActivationState? activationState) => new(new MainPage());

        // Метод установки культуры
        private static void SetCulture(string language)
        {
            var culture = new CultureInfo(language);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
    }
}
