using MauiApp1.Resources.Languages;
using Microsoft.Extensions.Localization;
using System.Globalization;

namespace MauiApp1.Services
{
    internal static class QuoteService
    {
        public static List<string> GetQuotes(IStringLocalizer<MyStrings> localizer)
        {
            return new List<string>
        {
            localizer["Quote1"],
            localizer["Quote2"],
            localizer["Quote3"]
        };
        }
    }
}