using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace MauiApp1.Services
{
    /// <summary>Opens a native full-screen camera page over the Blazor UI and returns the scanned code.</summary>
    public sealed class ScannerService
    {
        public bool IsSupported => DeviceInfo.Platform == DevicePlatform.Android || DeviceInfo.Platform == DevicePlatform.iOS;

        /// <returns>The barcode, or null when the user cancelled or no camera permission was given.</returns>
        public async Task<string?> ScanAsync(string title, string cancelText)
        {
            if (!IsSupported)
                return null;

            var status = await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<Permissions.Camera>);
            if (status != PermissionStatus.Granted)
                return null;

            var page = new ScannerPage(title, cancelText);
            await MainThread.InvokeOnMainThreadAsync(() => CurrentPage().Navigation.PushModalAsync(page));
            return await page.Result;
        }

        private static Page CurrentPage() =>
            Application.Current?.Windows.FirstOrDefault()?.Page ?? throw new InvalidOperationException("No window");
    }

    internal sealed class ScannerPage : ContentPage
    {
        private readonly TaskCompletionSource<string?> _result = new();
        private readonly CameraBarcodeReaderView _reader;

        public Task<string?> Result => _result.Task;

        public ScannerPage(string title, string cancelText)
        {
            BackgroundColor = Colors.Black;

            _reader = new CameraBarcodeReaderView
            {
                Options = new BarcodeReaderOptions
                {
                    Formats = BarcodeFormats.OneDimensional | BarcodeFormat.QrCode,
                    AutoRotate = true,
                    Multiple = false,
                    TryHarder = true
                },
                IsDetecting = true
            };
            _reader.BarcodesDetected += OnDetected;

            var cancel = new Button
            {
                Text = cancelText,
                BackgroundColor = Color.FromArgb("#2cd687"),
                TextColor = Colors.Black,
                CornerRadius = 24,
                Margin = new Thickness(32, 16, 32, 32)
            };
            cancel.Clicked += async (_, _) => await CloseAsync(null);

            var grid = new Grid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)] };
            grid.Add(new Label { Text = title, TextColor = Colors.White, FontSize = 18, HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(16, 48, 16, 16) }, 0, 0);
            grid.Add(_reader, 0, 1);
            grid.Add(cancel, 0, 2);
            Content = grid;
        }

        private void OnDetected(object? sender, BarcodeDetectionEventArgs e)
        {
            var value = e.Results?.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Value))?.Value;
            if (value == null || _result.Task.IsCompleted)
                return;

            _reader.IsDetecting = false;
            MainThread.BeginInvokeOnMainThread(async () => await CloseAsync(value.Trim()));
        }

        private async Task CloseAsync(string? value)
        {
            if (!_result.TrySetResult(value))
                return;
            _reader.BarcodesDetected -= OnDetected;
            await Navigation.PopModalAsync();
        }

        protected override bool OnBackButtonPressed()
        {
            _ = CloseAsync(null);
            return true;
        }
    }

    /// <summary>Item photos: stored as small JPEGs in the app folder (device only, not synchronised).</summary>
    public sealed class PhotoService
    {
        private const int MaxSide = 640;
        private readonly Dictionary<string, string> _dataUrls = new();

        private static string Folder
        {
            get
            {
                var folder = Path.Combine(FileSystem.AppDataDirectory, "photos");
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        public bool CanCapture => MediaPicker.Default.IsCaptureSupported;

        /// <returns>Path of the saved photo, or null when cancelled.</returns>
        public async Task<string?> TakeAsync(Guid itemId, bool fromCamera)
        {
            FileResult? file;
            if (fromCamera)
            {
                if (await Permissions.RequestAsync<Permissions.Camera>() != PermissionStatus.Granted)
                    return null;
                file = await MediaPicker.Default.CapturePhotoAsync();
            }
            else
            {
                file = (await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = 1 }))?.FirstOrDefault();
            }

            if (file == null)
                return null;

            var path = Path.Combine(Folder, $"{itemId:N}-{DateTime.UtcNow.Ticks}.jpg");
            await using (var source = await file.OpenReadAsync())
            await using (var target = File.Create(path))
            {
                await SaveResizedAsync(source, target);
            }
            return path;
        }

        private static async Task SaveResizedAsync(Stream source, Stream target)
        {
#if ANDROID || IOS || MACCATALYST
            var image = Microsoft.Maui.Graphics.Platform.PlatformImage.FromStream(source);
            var resized = image.Width > MaxSide || image.Height > MaxSide ? image.Downsize(MaxSide, disposeOriginal: true) : image;
            await resized.SaveAsync(target, ImageFormat.Jpeg, quality: 0.8f);
#else
            await source.CopyToAsync(target);
#endif
        }

        /// <summary>The photo as a data: URL for the Blazor web view.</summary>
        public async Task<string?> GetDataUrlAsync(string? path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            if (_dataUrls.TryGetValue(path, out var cached))
                return cached;

            var url = "data:image/jpeg;base64," + Convert.ToBase64String(await File.ReadAllBytesAsync(path));
            _dataUrls[path] = url;
            return url;
        }

        public void Delete(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            _dataUrls.Remove(path);
            try { File.Delete(path); } catch (IOException) { }
        }
    }
}
