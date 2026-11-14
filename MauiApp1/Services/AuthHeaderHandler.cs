using System.Net.Http.Headers;

namespace MauiApp1.Services
{
    /// <summary>
    /// Adds the stored JWT to every API request. Before this, only some services set the header and
    /// FieldService / FieldValueService worked only if CategoryService happened to run first.
    /// </summary>
    public class AuthHeaderHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.Authorization == null)
            {
                var token = await TokenStore.GetValidTokenAsync();
                if (!string.IsNullOrEmpty(token))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
