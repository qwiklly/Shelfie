namespace Shelfie.Shared;

/// <summary>
/// Categories every user has. They have well-known ids so that devices and the server agree on them without
/// creating rows. A user may still override name or fields: the override is stored as a normal category
/// with the same id (categories are keyed by user + id).
/// </summary>
public static class BuiltInCategories
{
    public static readonly Guid Products = new("00000000-0000-0000-0000-000000000001");
    public static readonly Guid Medications = new("00000000-0000-0000-0000-000000000002");
    public static readonly Guid Books = new("00000000-0000-0000-0000-000000000003");

    public static bool IsBuiltIn(Guid id) => id == Products || id == Medications || id == Books;

    public static CategoryKind KindOf(Guid id) =>
        id == Products ? CategoryKind.Products :
        id == Medications ? CategoryKind.Medications :
        id == Books ? CategoryKind.Books :
        CategoryKind.Custom;

    private static readonly Dictionary<Guid, string> NameKeys = new()
    {
        [Products] = "CatProducts",
        [Medications] = "CatMedications",
        [Books] = "CatBooks",
    };

    private static readonly Dictionary<string, string> BookFieldKeys = new()
    {
        ["author"] = "FieldAuthor",
        ["year"] = "FieldYear",
        ["genre"] = "FieldGenre",
    };

    /// <summary>Default definitions in the current UI language (fresh copies on every call).</summary>
    public static IReadOnlyList<CategoryDto> Defaults() =>
    [
        new CategoryDto { Id = Products, Name = Texts.Get("CatProducts"), Kind = CategoryKind.Products },
        new CategoryDto { Id = Medications, Name = Texts.Get("CatMedications"), Kind = CategoryKind.Medications },
        new CategoryDto
        {
            Id = Books,
            Name = Texts.Get("CatBooks"),
            Kind = CategoryKind.Books,
            Fields =
            [
                new FieldDefinition { Key = "author", Name = Texts.Get("FieldAuthor"), Type = FieldType.Text, Order = 1 },
                new FieldDefinition { Key = "year", Name = Texts.Get("FieldYear"), Type = FieldType.Number, Order = 2 },
                new FieldDefinition { Key = "genre", Name = Texts.Get("FieldGenre"), Type = FieldType.Text, Order = 3 },
            ]
        },
    ];

    /// <summary>Built-in defaults overlaid with the user's stored categories (overrides and custom ones).</summary>
    public static List<CategoryDto> Merge(IEnumerable<CategoryDto> stored)
    {
        var byId = stored.ToDictionary(c => c.Id);
        var result = new List<CategoryDto>();

        foreach (var builtIn in Defaults())
        {
            if (byId.Remove(builtIn.Id, out var overridden))
            {
                overridden.Kind = builtIn.Kind;
                overridden.IsDeleted = false; // built-ins cannot be deleted
                Localize(overridden);
                result.Add(overridden);
            }
            else
            {
                result.Add(builtIn);
            }
        }

        result.AddRange(byId.Values.Where(c => !c.IsDeleted).OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase));
        return result;
    }

    /// <summary>
    /// An override that still carries a default name (in any language) shows the name in the current language;
    /// names the user typed are kept as they are.
    /// </summary>
    private static void Localize(CategoryDto category)
    {
        if (NameKeys.TryGetValue(category.Id, out var nameKey) && Texts.AllVariants(nameKey).Contains(category.Name))
            category.Name = Texts.Get(nameKey);

        if (category.Id != Books)
            return;

        foreach (var field in category.Fields)
        {
            if (BookFieldKeys.TryGetValue(field.Key, out var fieldKey) && Texts.AllVariants(fieldKey).Contains(field.Name))
                field.Name = Texts.Get(fieldKey);
        }
    }
}

/// <summary>Suggested storage locations (current UI language); users may type any other.</summary>
public static class StorageLocations
{
    public static IReadOnlyList<string> Suggestions =>
        new[] { "LocFridge", "LocFreezer", "LocPantry", "LocFirstAid", "LocCupboard", "LocShelf" }.Select(Texts.Get).ToList();
}
