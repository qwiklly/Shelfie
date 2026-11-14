using Shelfie.Client;

namespace MauiApp1.Services
{
    /// <summary>
    /// Decides when to synchronise: on start, a few seconds after local changes, when the network comes back
    /// and every few minutes. Also reschedules expiry notifications after every change.
    /// The app never waits for the server: all screens read the local database.
    /// </summary>
    public sealed class SyncCoordinator : IDisposable
    {
        private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan Period = TimeSpan.FromMinutes(5);

        private readonly SyncService _sync;
        private readonly LocalInventory _inventory;
        private readonly NotificationScheduler _notifications;
        private readonly Timer _debounceTimer;
        private readonly Timer _periodicTimer;
        private bool _started;

        public SyncCoordinator(SyncService sync, LocalInventory inventory, NotificationScheduler notifications)
        {
            _sync = sync;
            _inventory = inventory;
            _notifications = notifications;
            _debounceTimer = new Timer(_ => _ = SyncNowAsync(), null, Timeout.Infinite, Timeout.Infinite);
            _periodicTimer = new Timer(_ => _ = SyncNowAsync(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public SyncStatus Status => _sync.Status;

        public event EventHandler<SyncStatus>? StatusChanged
        {
            add => _sync.StatusChanged += value;
            remove => _sync.StatusChanged -= value;
        }

        public void Start()
        {
            if (_started)
                return;
            _started = true;

            _inventory.Changed += OnInventoryChanged;
            Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
            _periodicTimer.Change(Period, Period);
            _ = SyncNowAsync();
            _ = StartNotificationsAsync();
        }

        private async Task StartNotificationsAsync()
        {
            await _notifications.EnsurePermissionOnceAsync();
            await _notifications.RescheduleAsync();
        }

        public async Task<SyncStatus> SyncNowAsync()
        {
            // No server configured: the app is purely local, nothing to try.
            if (!Preferences.Get("server_url", "").Contains("://")
                || Connectivity.Current.NetworkAccess == NetworkAccess.None)
                return await _sync.RefreshStatusAsync();

            try
            {
                return await _sync.SyncAsync();
            }
            catch (Exception)
            {
                return await _sync.RefreshStatusAsync();
            }
        }

        private void OnInventoryChanged(object? sender, EventArgs e)
        {
            _debounceTimer.Change(Debounce, Timeout.InfiniteTimeSpan);
            _ = _notifications.RescheduleAsync();
        }

        private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
        {
            if (e.NetworkAccess == NetworkAccess.Internet)
                _ = SyncNowAsync();
            else
                _ = _sync.RefreshStatusAsync();
        }

        public void Dispose()
        {
            _inventory.Changed -= OnInventoryChanged;
            Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
            _debounceTimer.Dispose();
            _periodicTimer.Dispose();
        }
    }
}
