using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ShelfieBackend.Pages
{
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class ProductsModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
