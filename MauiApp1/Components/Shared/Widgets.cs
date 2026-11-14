using MauiApp1.Resources.Languages;
using Microsoft.Extensions.Localization;
using Shelfie.Shared;

namespace MauiApp1.Components.Shared
{
    /// <summary>Small UI helpers shared by several pages.</summary>
    public static class Widgets
    {
        /// <summary>Localised name of a history change type.</summary>
        public static string ChangeName(IStringLocalizer<MyStrings> l, string changeType) => changeType switch
        {
            ChangeTypes.Added => l["ChangeAdded"],
            ChangeTypes.Updated => l["ChangeUpdated"],
            ChangeTypes.Removed => l["ChangeRemoved"],
            ChangeTypes.Consumed => l["ChangeConsumed"],
            ChangeTypes.Wasted => l["ChangeWasted"],
            ChangeTypes.Restocked => l["ChangeRestocked"],
            _ => changeType
        };

        public static string FieldTypeName(IStringLocalizer<MyStrings> l, FieldType type) => type switch
        {
            FieldType.Number => l["TypeNumber"],
            FieldType.Date => l["TypeDate"],
            FieldType.Boolean => l["TypeBoolean"],
            _ => l["TypeText"]
        };

        /// <summary>Weight units offered in forms (stored as written).</summary>
        public static readonly string[] Units = ["г", "кг", "мл", "л", "шт"];
    }
}
