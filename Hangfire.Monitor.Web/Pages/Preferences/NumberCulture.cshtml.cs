using Hangfire.Monitor.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hangfire.Monitor.Web.Pages.Preferences;

public class NumberCultureModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index");

    public IActionResult OnPost(string? culture, string? returnUrl)
    {
        NumberCulturePreference.AppendCookie(
            Response.Cookies,
            culture,
            Request.IsHttps);

        var safeReturnUrl = NumberCulturePreference.ResolveReturnUrl(
            returnUrl,
            Url.IsLocalUrl);

        return LocalRedirect(safeReturnUrl);
    }
}
