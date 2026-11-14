using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Shelfie.Client;

namespace MauiApp1.Services
{
    /// <summary>Turns <see cref="NotificationPlanner"/> plans into scheduled local notifications.</summary>
    public sealed class NotificationScheduler
    {
        private static readonly TimeOnly DigestTime = new(10, 0);

        private readonly LocalInventory _inventory;
        private readonly AppSettings _settings;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public NotificationScheduler(LocalInventory inventory, AppSettings settings)
        {
            _inventory = inventory;
            _settings = settings;
        }

        /// <summary>Asks the OS for permission (Android 13+ / iOS). Returns whether notifications may be shown.</summary>
        public async Task<bool> RequestPermissionAsync()
        {
            try
            {
                if (await LocalNotificationCenter.Current.AreNotificationsEnabled())
                    return true;
                return await LocalNotificationCenter.Current.RequestNotificationPermission();
            }
            catch (Exception)
            {
                return false; // platform without notification support (e.g. Windows debug)
            }
        }

        /// <summary>
        /// Android 13+ / iOS show nothing until the user allows notifications, so ask once on first start.
        /// </summary>
        public async Task EnsurePermissionOnceAsync()
        {
            if (!_settings.NotificationsEnabled || Preferences.Get("notifications_asked", false))
                return;

            Preferences.Set("notifications_asked", true);
            if (!await MainThread.InvokeOnMainThreadAsync(RequestPermissionAsync))
                _settings.NotificationsEnabled = false; // reflect the user's choice in Settings
        }

        public async Task RescheduleAsync()
        {
            if (!await _lock.WaitAsync(0))
                return;
            try
            {
                LocalNotificationCenter.Current.CancelAll();
                if (!_settings.NotificationsEnabled)
                    return;

                var now = DateTime.Now;
                var today = DateOnly.FromDateTime(now);
                var items = await _inventory.GetItemsAsync();
                var plan = NotificationPlanner.Plan(items, now, _settings.SoonDays, DigestTime,
                    alreadyNotifiedToday: _settings.LastDigestDay == today);

                foreach (var planned in plan)
                {
                    await LocalNotificationCenter.Current.Show(new NotificationRequest
                    {
                        NotificationId = planned.Id,
                        Title = planned.Title,
                        Description = planned.Body,
                        Schedule = new NotificationRequestSchedule { NotifyTime = planned.NotifyAt }
                    });

                    if (DateOnly.FromDateTime(planned.NotifyAt) == today)
                        _settings.LastDigestDay = today;
                }
            }
            catch (Exception)
            {
                // Notifications are a convenience; never break the app because of them.
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
