using System.Globalization;

namespace Shelfie.Shared;

/// <summary>
/// User-facing texts of the shared/core code in Russian and English. The language follows
/// <see cref="CultureInfo.CurrentUICulture"/>: Russian for "ru", English for everything else.
/// </summary>
public static class Texts
{
    public static bool IsRussian => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru";

    public static string Get(string key) =>
        (IsRussian ? Ru : En).TryGetValue(key, out var text) ? text : key;

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    /// <summary>All translations of a key (used to recognise default names saved in another language).</summary>
    public static IEnumerable<string> AllVariants(string key)
    {
        if (Ru.TryGetValue(key, out var ru)) yield return ru;
        if (En.TryGetValue(key, out var en)) yield return en;
    }

    private static readonly Dictionary<string, string> Ru = new()
    {
        ["CatProducts"] = "Продукты",
        ["CatMedications"] = "Медикаменты",
        ["CatBooks"] = "Книги",
        ["FieldAuthor"] = "Автор",
        ["FieldYear"] = "Год",
        ["FieldGenre"] = "Жанр",

        ["LocFridge"] = "Холодильник",
        ["LocFreezer"] = "Морозилка",
        ["LocPantry"] = "Кладовая",
        ["LocFirstAid"] = "Аптечка",
        ["LocCupboard"] = "Шкаф",
        ["LocShelf"] = "Полка",

        ["NameRequired"] = "Название не может быть пустым",
        ["NameTooLong"] = "Название не может быть длиннее {0} символов",
        ["QuantityNegative"] = "Количество не может быть отрицательным",
        ["WeightNegative"] = "Вес не может быть отрицательным",
        ["FieldRequired"] = "Поле «{0}» обязательно",
        ["FieldInvalid"] = "Поле «{0}»: неверное значение",
        ["CategoryNameRequired"] = "Название категории не может быть пустым",
        ["CategoryNameTooLong"] = "Название категории не может быть длиннее {0} символов",
        ["TooManyFields"] = "У категории может быть не больше {0} полей",
        ["FieldNeedsName"] = "У каждого поля должно быть название",
        ["FieldKeysUnique"] = "Ключи полей должны быть уникальными",
        ["CategoryExists"] = "Категория с таким названием уже существует",
        ["CategoryNotFound"] = "Категория не найдена",
        ["BuiltInCannotDelete"] = "Встроенную категорию нельзя удалить",

        ["CsvCategory"] = "Категория",
        ["CsvName"] = "Название",
        ["CsvBrand"] = "Бренд",
        ["CsvQuantity"] = "Количество",
        ["CsvWeight"] = "Вес",
        ["CsvUnit"] = "Ед.",
        ["CsvExpires"] = "Годен до",
        ["CsvLocation"] = "Место",
        ["CsvBarcode"] = "Штрихкод",
        ["CsvNotes"] = "Заметки",

        ["NotifyOne"] = "Скоро истекает срок годности",
        ["NotifyMany"] = "Скоро истекает срок: {0} шт.",
        ["NotifyAndMore"] = " и ещё {0}",

        ["SignInRequired"] = "Войдите в аккаунт",
        ["WrongCredentials"] = "Неверный email или пароль",
        ["EmptyResponse"] = "Пустой ответ сервера",
        ["ServerUnavailable"] = "Сервер недоступен",
        ["Unauthorized"] = "Требуется вход",
        ["Forbidden"] = "Нет доступа",
        ["NotFound"] = "Не найдено",
        ["ServerError"] = "Ошибка сервера ({0})",
        ["SessionExpired"] = "Сессия истекла — войдите снова",
        ["ServerRejected"] = "Сервер отклонил изменений: {0}",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["CatProducts"] = "Food",
        ["CatMedications"] = "Medicines",
        ["CatBooks"] = "Books",
        ["FieldAuthor"] = "Author",
        ["FieldYear"] = "Year",
        ["FieldGenre"] = "Genre",

        ["LocFridge"] = "Fridge",
        ["LocFreezer"] = "Freezer",
        ["LocPantry"] = "Pantry",
        ["LocFirstAid"] = "First-aid kit",
        ["LocCupboard"] = "Cupboard",
        ["LocShelf"] = "Shelf",

        ["NameRequired"] = "Name cannot be empty",
        ["NameTooLong"] = "Name cannot be longer than {0} characters",
        ["QuantityNegative"] = "Quantity cannot be negative",
        ["WeightNegative"] = "Weight cannot be negative",
        ["FieldRequired"] = "\"{0}\" is required",
        ["FieldInvalid"] = "\"{0}\": invalid value",
        ["CategoryNameRequired"] = "Category name cannot be empty",
        ["CategoryNameTooLong"] = "Category name cannot be longer than {0} characters",
        ["TooManyFields"] = "A category can have at most {0} fields",
        ["FieldNeedsName"] = "Every field needs a name",
        ["FieldKeysUnique"] = "Field keys must be unique",
        ["CategoryExists"] = "A category with this name already exists",
        ["CategoryNotFound"] = "Category not found",
        ["BuiltInCannotDelete"] = "Built-in categories cannot be deleted",

        ["CsvCategory"] = "Category",
        ["CsvName"] = "Name",
        ["CsvBrand"] = "Brand",
        ["CsvQuantity"] = "Quantity",
        ["CsvWeight"] = "Weight",
        ["CsvUnit"] = "Unit",
        ["CsvExpires"] = "Best before",
        ["CsvLocation"] = "Location",
        ["CsvBarcode"] = "Barcode",
        ["CsvNotes"] = "Notes",

        ["NotifyOne"] = "Expiring soon",
        ["NotifyMany"] = "{0} items are expiring soon",
        ["NotifyAndMore"] = " and {0} more",

        ["SignInRequired"] = "Please sign in",
        ["WrongCredentials"] = "Wrong e-mail or password",
        ["EmptyResponse"] = "Empty server response",
        ["ServerUnavailable"] = "Server is unreachable",
        ["Unauthorized"] = "Sign-in required",
        ["Forbidden"] = "Access denied",
        ["NotFound"] = "Not found",
        ["ServerError"] = "Server error ({0})",
        ["SessionExpired"] = "Session expired — please sign in again",
        ["ServerRejected"] = "The server rejected {0} change(s)",
    };
}
