using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace Hangfire.Monitor.Web;

/// <summary>
/// Resolves the per-browser numeric display culture preference from a cookie.
/// Presentation only — Domain and Infrastructure stay culture-independent.
/// </summary>
public static class NumberCulturePreference
{
    public const string CookieName = "hm.numberCulture";
    public const string DefaultToken = "en-US";
    public const string EnglishUnitedStatesToken = "en-US";
    public const string SpanishSpainToken = "es-ES";

    private static readonly CultureInfo EnglishUnitedStates =
        CultureInfo.GetCultureInfo(EnglishUnitedStatesToken);

    private static readonly CultureInfo SpanishSpain =
        CultureInfo.GetCultureInfo(SpanishSpainToken);

    /// <summary>
    /// Returns a supported culture token, or <see cref="DefaultToken"/> when missing/unsupported.
    /// Only exact whitelist matches are accepted (no case folding).
    /// </summary>
    public static string ResolveToken(string? cookieValue) =>
        cookieValue switch
        {
            EnglishUnitedStatesToken => EnglishUnitedStatesToken,
            SpanishSpainToken => SpanishSpainToken,
            _ => DefaultToken
        };

    /// <summary>
    /// Resolves the display culture from the request cookies.
    /// </summary>
    public static CultureInfo ResolveCulture(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ResolveCulture(request.Cookies);
    }

    /// <summary>
    /// Resolves the display culture from a cookie collection.
    /// </summary>
    public static CultureInfo ResolveCulture(IRequestCookieCollection cookies)
    {
        ArgumentNullException.ThrowIfNull(cookies);
        cookies.TryGetValue(CookieName, out var value);
        return ResolveCultureFromToken(ResolveToken(value));
    }

    /// <summary>
    /// Maps a validated token to its <see cref="CultureInfo"/>.
    /// Unknown tokens map to the default culture.
    /// </summary>
    public static CultureInfo ResolveCultureFromToken(string? token) =>
        ResolveToken(token) switch
        {
            SpanishSpainToken => SpanishSpain,
            _ => EnglishUnitedStates
        };

    /// <summary>
    /// Builds cookie options for the number-culture preference.
    /// </summary>
    public static CookieOptions CreateCookieOptions(bool secure) =>
        new()
        {
            Path = "/",
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(365)
        };

    /// <summary>
    /// Appends the preference cookie after whitelist validation.
    /// Unsupported values are stored as <see cref="DefaultToken"/>.
    /// </summary>
    public static void AppendCookie(IResponseCookies cookies, string? requestedToken, bool secure)
    {
        ArgumentNullException.ThrowIfNull(cookies);

        var token = ResolveToken(requestedToken);
        cookies.Append(CookieName, token, CreateCookieOptions(secure));
    }

    /// <summary>
    /// Returns <paramref name="returnUrl"/> when <paramref name="isLocalUrl"/> accepts it; otherwise <c>/</c>.
    /// Callers should pass <c>Url.IsLocalUrl</c> from the Razor Page.
    /// </summary>
    public static string ResolveReturnUrl(string? returnUrl, Func<string, bool> isLocalUrl)
    {
        ArgumentNullException.ThrowIfNull(isLocalUrl);

        if (!string.IsNullOrEmpty(returnUrl) && isLocalUrl(returnUrl))
        {
            return returnUrl;
        }

        return "/";
    }
}
