using Shelfie.Client;

namespace MauiApp1.Services
{
    /// <summary>User preferences stored on the device.</summary>
    public sealed class AppSettings
    {
        public static string DefaultServerUrl =>
            DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5111" : "http://localhost:5111";

        public string ServerUrl
        {
            get => Preferences.Get("server_url", DefaultServerUrl);
            set => Preferences.Set("server_url", string.IsNullOrWhiteSpace(value) ? DefaultServerUrl : value.Trim().TrimEnd('/'));
        }

        public string Language
        {
            get => Preferences.Get("language", "ru-RU");
            set => Preferences.Set("language", value);
        }

        /// <summary>Use the app without an account; data stays on this device until the user signs in.</summary>
        public bool GuestMode
        {
            get => Preferences.Get("guest_mode", false);
            set => Preferences.Set("guest_mode", value);
        }

        public bool NotificationsEnabled
        {
            get => Preferences.Get("notifications", true);
            set => Preferences.Set("notifications", value);
        }

        /// <summary>Warn this many days before an item expires (also the "soon" threshold in lists).</summary>
        public int SoonDays
        {
            get => Preferences.Get("soon_days", 3);
            set => Preferences.Set("soon_days", Math.Clamp(value, 0, 30));
        }

        public bool AutoShoppingList
        {
            get => Preferences.Get("auto_shopping", true);
            set => Preferences.Set("auto_shopping", value);
        }

        public DateOnly? LastDigestDay
        {
            get => int.TryParse(Preferences.Get("last_digest", ""), out var d) ? DateOnly.FromDayNumber(d) : null;
            set => Preferences.Set("last_digest", value?.DayNumber.ToString() ?? "");
        }
    }

    /// <summary>JWT in the platform secure storage (Keystore / Keychain).</summary>
    public sealed class SecureTokenStore : ITokenStore
    {
        private const string Key = "auth_token";

        public async Task<string?> GetAsync()
        {
            try { return await SecureStorage.Default.GetAsync(Key); }
            catch (Exception) { return null; } // storage can be reset by the OS (e.g. after a restore)
        }

        public Task SetAsync(string token) => SecureStorage.Default.SetAsync(Key, token);

        public void Clear() => SecureStorage.Default.Remove(Key);
    }

    /// <summary>Short messages shown at the bottom of the screen.</summary>
    public sealed class Toasts
    {
        public event Action<string, bool>? Shown;
        public void Show(string message, bool isError = false) => Shown?.Invoke(message, isError);
    }
}
