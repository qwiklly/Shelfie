namespace ShelfieBackend.States
{
    public static class ApiEndpoints
    {
        public const string Base = "api/application";
        public const string GetUsers = $"{Base}/getUsers";
        public const string GetUser = $"{Base}/getUser?email=";
        public const string Login = $"{Base}/login";
        public const string Register = $"{Base}/register";
        public const string DeleteUser = $"{Base}/deleteUser/";
        public const string UpdateUser = $"{Base}/updateUser";

        // Эндпоинты для продуктов
        public const string ProductBase = "api/products";
        public const string GetProducts = $"{ProductBase}/getAll";
        public const string GetProduct = $"{ProductBase}/get/";
        public const string AddProduct = $"{ProductBase}/add";
        public const string UpdateProduct = $"{ProductBase}/update";
        public const string DeleteProduct = $"{ProductBase}/delete/";

        // Эндпоинты для истории изменений
        public const string HistoryBase = "api/history";
        public const string GetAllHistory = $"{HistoryBase}/getAll";

        // Эндпоинты для категорий 
        public const string CategoryBase = "api/category";
        public const string CreateCategory = $"{CategoryBase}/createCategory";
        public const string GetAllCategories = $"{CategoryBase}/getAllCategories";
    }
}
