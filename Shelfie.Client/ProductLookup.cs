using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Shelfie.Client;

public sealed record ProductInfo(string Name, string? Brand, double? Weight, string? WeightUnit, string Source);

/// <summary>
/// Barcode → product data. First looks at the user's own items (works offline), then asks Open Food Facts.
/// </summary>
public sealed partial class ProductLookup
{
    private readonly HttpClient _http;
    private readonly LocalInventory _inventory;

    public ProductLookup(HttpClient http, LocalInventory inventory)
    {
        _http = http;
        _inventory = inventory;
    }

    public static bool IsValidBarcode(string? code) => code != null && BarcodeRegex().IsMatch(code);

    public async Task<ProductInfo?> LookupAsync(string barcode, CancellationToken ct = default)
    {
        barcode = barcode.Trim();
        if (!IsValidBarcode(barcode))
            return null;

        var known = await _inventory.FindByBarcodeAsync(barcode);
        if (known != null)
            return new ProductInfo(known.Name, known.Brand, known.Weight, known.WeightUnit, "local");

        return await LookupOpenFoodFactsAsync(barcode, ct);
    }

    internal async Task<ProductInfo?> LookupOpenFoodFactsAsync(string barcode, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(8));

            // Open Food Facts asks clients to identify themselves; no personal data is sent.
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://world.openfoodfacts.org/api/v2/product/{barcode}.json?fields=product_name,product_name_ru,brands,quantity");
            request.Headers.UserAgent.ParseAdd("Shelfie/2.0");

            using var response = await _http.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadFromJsonAsync<OffResponse>(cts.Token);
            return Parse(body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return null; // offline or unknown product: the user fills the form manually
        }
    }

    internal static ProductInfo? Parse(OffResponse? body)
    {
        if (body is not { Status: 1, Product: { } p })
            return null;

        var name = FirstNonEmpty(p.ProductNameRu, p.ProductName);
        if (name == null)
            return null;

        var brand = p.Brands?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        var (weight, unit) = ParseQuantity(p.Quantity);
        return new ProductInfo(name, brand, weight, unit, "openfoodfacts");
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrEmpty(v));

    /// <summary>"1 l", "500 g", "0,5 л", "6 x 330 ml", "33cl" → (value, г|кг|мл|л).</summary>
    public static (double? Weight, string? Unit) ParseQuantity(string? quantity)
    {
        if (string.IsNullOrWhiteSpace(quantity))
            return (null, null);

        var match = QuantityRegex().Match(quantity.ToLowerInvariant());
        if (!match.Success)
            return (null, null);

        var value = double.Parse(match.Groups["value"].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
        return match.Groups["unit"].Value switch
        {
            "kg" or "кг" => (value, "кг"),
            "g" or "gr" or "г" => (value, "г"),
            "l" or "л" => (value, "л"),
            "ml" or "мл" => (value, "мл"),
            "cl" => (value * 10, "мл"),
            _ => (null, null)
        };
    }

    [GeneratedRegex(@"^\d{8,14}$")]
    private static partial Regex BarcodeRegex();

    // The last "number unit" pair: in "6 x 330 ml" that is the size of one unit.
    [GeneratedRegex(@"(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>kg|кг|gr|g|г|ml|мл|cl|l|л)\b(?!.*\d+(?:[.,]\d+)?\s*(?:kg|кг|gr|g|г|ml|мл|cl|l|л)\b)")]
    private static partial Regex QuantityRegex();

    internal sealed class OffResponse
    {
        [JsonPropertyName("status")] public int Status { get; set; }
        [JsonPropertyName("product")] public OffProduct? Product { get; set; }
    }

    internal sealed class OffProduct
    {
        [JsonPropertyName("product_name")] public string? ProductName { get; set; }
        [JsonPropertyName("product_name_ru")] public string? ProductNameRu { get; set; }
        [JsonPropertyName("brands")] public string? Brands { get; set; }
        [JsonPropertyName("quantity")] public string? Quantity { get; set; }
    }
}
