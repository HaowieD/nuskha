namespace Nuskha.Web.Pages.Account;

public class LogoutModel(SignInManager<AppUser> signIn) : AppPageModel
{
    public IActionResult OnGet() => Redirect("/");

    public async Task<IActionResult> OnPostAsync()
    {
        await signIn.SignOutAsync();
        return Redirect("/");
    }
}
