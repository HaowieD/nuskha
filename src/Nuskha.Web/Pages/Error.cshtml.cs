namespace Nuskha.Web.Pages;

[IgnoreAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorModel : PageModel
{
    [BindProperty(SupportsGet = true)] public int Code { get; set; } = 500;
    public void OnGet() { if (Code < 400 || Code > 599) Code = 500; }
}
