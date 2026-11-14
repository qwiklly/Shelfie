using System.IdentityModel.Tokens.Jwt;

namespace MauiApp1.Services
{
    /// <summary>Keeps the JWT in SecureStorage and drops it once it has expired.</summary>
    public static class TokenStore
    {
        private const string Key = "auth_token";

        public static Task SaveAsync(string token) => SecureStorage.Default.SetAsync(Key, token);

        public static void Clear() => SecureStorage.Default.Remove(Key);

        /// <summary>Returns the stored token, or null when there is none or it has expired.</summary>
        public static async Task<string?> GetValidTokenAsync()
        {
            var token = await SecureStorage.Default.GetAsync(Key);
            if (string.IsNullOrEmpty(token))
                return null;

            if (IsExpired(token))
            {
                Clear();
                return null;
            }

            return token;
        }

        public static bool IsExpired(string token)
        {
            try
            {
                var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
                return jwt.ValidTo <= DateTime.UtcNow;
            }
            catch (ArgumentException)
            {
                return true; // not a readable JWT
            }
        }
    }
}
