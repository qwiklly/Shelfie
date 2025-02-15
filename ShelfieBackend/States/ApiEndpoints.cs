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
    }
}
