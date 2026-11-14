using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shelfie.Shared;

/// <summary>JSON settings shared by the API, the client and the database JSON columns.</summary>
public static class ShelfieJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

/// <summary>Validation that both the server and the offline client apply.</summary>
public static class ItemRules
{
    public const int MaxNameLength = 200;
    public const int MaxFields = 20;

    public static List<string> Validate(ItemDto item, CategoryDto? category)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(item.Name))
            errors.Add("Название не может быть пустым");
        else if (item.Name.Trim().Length > MaxNameLength)
            errors.Add($"Название не может быть длиннее {MaxNameLength} символов");

        if (item.Quantity < 0)
            errors.Add("Количество не может быть отрицательным");
        if (item.Weight < 0)
            errors.Add("Вес не может быть отрицательным");

        if (category != null && !item.IsDeleted)
        {
            foreach (var field in category.Fields)
            {
                item.Fields.TryGetValue(field.Key, out var value);
                if (string.IsNullOrWhiteSpace(value))
                {
                    if (field.Required)
                        errors.Add($"Поле «{field.Name}» обязательно");
                    continue;
                }

                if (!FieldValues.IsValid(field.Type, value))
                    errors.Add($"Поле «{field.Name}»: неверное значение");
            }
        }

        return errors;
    }

    /// <summary>Trims text and drops values of fields that the category does not define.</summary>
    public static void Normalize(ItemDto item, CategoryDto? category)
    {
        item.Name = item.Name?.Trim() ?? string.Empty;
        item.Brand = NullIfEmpty(item.Brand);
        item.Notes = NullIfEmpty(item.Notes);
        item.Location = NullIfEmpty(item.Location);
        item.Barcode = NullIfEmpty(item.Barcode);
        item.WeightUnit = NullIfEmpty(item.WeightUnit);

        var keys = category?.Fields.Select(f => f.Key).ToHashSet();
        item.Fields = (item.Fields ?? new())
            .Where(kv => keys == null || keys.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value?.Trim() ?? string.Empty);
    }

    public static List<string> ValidateCategory(CategoryDto category)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(category.Name))
            errors.Add("Название категории не может быть пустым");
        else if (category.Name.Trim().Length > 100)
            errors.Add("Название категории не может быть длиннее 100 символов");

        if (category.Fields.Count > MaxFields)
            errors.Add($"У категории может быть не больше {MaxFields} полей");
        if (category.Fields.Any(f => string.IsNullOrWhiteSpace(f.Key) || string.IsNullOrWhiteSpace(f.Name)))
            errors.Add("У каждого поля должны быть ключ и название");
        if (category.Fields.Select(f => f.Key).Distinct().Count() != category.Fields.Count)
            errors.Add("Ключи полей должны быть уникальными");

        return errors;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class FieldValues
{
    public static bool IsValid(FieldType type, string value) => type switch
    {
        FieldType.Number => double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out _),
        FieldType.Date => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        FieldType.Boolean => value is "true" or "false",
        _ => true
    };

    /// <summary>Text shown to the user (dates and booleans formatted, others as is).</summary>
    public static string Display(FieldType type, string? value, CultureInfo? culture = null)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        culture ??= CultureInfo.CurrentCulture;
        return type switch
        {
            FieldType.Date when DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                => d.ToString("d", culture),
            FieldType.Boolean => value == "true" ? "✓" : "✗",
            _ => value
        };
    }
}

public enum ExpiryStatus
{
    None,
    Fresh,
    Soon,
    Expired
}

public static class Expiry
{
    public const int DefaultSoonDays = 3;

    public static ExpiryStatus GetStatus(DateOnly? expiration, DateOnly today, int soonDays = DefaultSoonDays)
    {
        if (expiration is null)
            return ExpiryStatus.None;
        if (expiration.Value < today)
            return ExpiryStatus.Expired;
        return expiration.Value <= today.AddDays(soonDays) ? ExpiryStatus.Soon : ExpiryStatus.Fresh;
    }

    public static int DaysLeft(DateOnly expiration, DateOnly today) => expiration.DayNumber - today.DayNumber;
}

public static class SyncRules
{
    /// <summary>Last write wins; on a tie the incoming copy is taken so that re-sending is idempotent.</summary>
    public static bool IncomingWins(DateTime incomingUpdatedAt, DateTime existingUpdatedAt) =>
        incomingUpdatedAt >= existingUpdatedAt;
}

/// <summary>CSV export of the inventory (works on the server and offline on the device).</summary>
public static class CsvExporter
{
    public static string Export(IEnumerable<ItemDto> items, IEnumerable<CategoryDto> categories)
    {
        var categoryList = categories.ToList();
        var byId = categoryList.ToDictionary(c => c.Id);
        var extraFields = categoryList
            .SelectMany(c => c.Fields.OrderBy(f => f.Order).Select(f => f.Name))
            .Distinct()
            .ToList();

        var sb = new StringBuilder();
        var header = new List<string> { "Категория", "Название", "Бренд", "Количество", "Вес", "Ед.", "Годен до", "Место", "Штрихкод", "Заметки" };
        header.AddRange(extraFields);
        AppendRow(sb, header);

        foreach (var item in items.Where(i => !i.IsDeleted).OrderBy(i => byId.GetValueOrDefault(i.CategoryId)?.Name).ThenBy(i => i.Name))
        {
            var category = byId.GetValueOrDefault(item.CategoryId);
            var row = new List<string>
            {
                category?.Name ?? string.Empty,
                item.Name,
                item.Brand ?? string.Empty,
                item.Quantity.ToString(CultureInfo.InvariantCulture),
                item.Weight?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                item.WeightUnit ?? string.Empty,
                item.ExpirationDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                item.Location ?? string.Empty,
                item.Barcode ?? string.Empty,
                item.Notes ?? string.Empty,
            };

            foreach (var fieldName in extraFields)
            {
                var field = category?.Fields.FirstOrDefault(f => f.Name == fieldName);
                row.Add(field != null && item.Fields.TryGetValue(field.Key, out var v) ? v : string.Empty);
            }

            AppendRow(sb, row);
        }

        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, IEnumerable<string> cells)
    {
        sb.AppendJoin(';', cells.Select(Escape));
        sb.Append("\r\n");
    }

    internal static string Escape(string value)
    {
        // Formula injection protection for spreadsheet apps.
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@')
            value = "'" + value;

        return value.IndexOfAny([';', '"', '\n', '\r']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
