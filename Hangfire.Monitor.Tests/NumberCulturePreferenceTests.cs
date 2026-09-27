using Hangfire.Monitor.Web;
using Microsoft.AspNetCore.Http;

namespace Hangfire.Monitor.Tests;

public class NumberCulturePreferenceTests
{
    [Fact]
    public void CookieName_IsExpected()
    {
        Assert.Equal("hm.numberCulture", NumberCulturePreference.CookieName);
    }

    [Fact]
    public void DefaultToken_IsEnUs()
    {
        Assert.Equal("en-US", NumberCulturePreference.DefaultToken);
    }

    [Theory]
    [InlineData(null, "en-US")]
    [InlineData("", "en-US")]
    [InlineData("en-US", "en-US")]
    [InlineData("es-ES", "es-ES")]
    [InlineData("fr-FR", "en-US")]
    [InlineData("de-DE", "en-US")]
    [InlineData("EN-US", "en-US")]
    [InlineData("es-es", "en-US")]
    [InlineData("anything", "en-US")]
    public void ResolveToken_UsesWhitelistAndDefault(string? cookieValue, string expected)
    {
        Assert.Equal(expected, NumberCulturePreference.ResolveToken(cookieValue));
    }

    [Theory]
    [InlineData(null, "en-US")]
    [InlineData("en-US", "en-US")]
    [InlineData("es-ES", "es-ES")]
    [InlineData("fr-FR", "en-US")]
    public void ResolveCultureFromToken_ReturnsMatchingCulture(string? token, string expectedName)
    {
        var culture = NumberCulturePreference.ResolveCultureFromToken(token);

        Assert.Equal(expectedName, culture.Name);
    }

    [Fact]
    public void ResolveCulture_WhenCookieMissing_ReturnsEnUs()
    {
        var cookies = new RequestCookieCollection();

        var culture = NumberCulturePreference.ResolveCulture(cookies);

        Assert.Equal("en-US", culture.Name);
    }

    [Fact]
    public void ResolveCulture_WhenCookieIsEsEs_ReturnsEsEs()
    {
        var cookies = new RequestCookieCollection(
            new Dictionary<string, string>
            {
                [NumberCulturePreference.CookieName] = "es-ES"
            });

        var culture = NumberCulturePreference.ResolveCulture(cookies);

        Assert.Equal("es-ES", culture.Name);
        Assert.Equal(
            "es-ES",
            NumberCulturePreference.ResolveCultureFromToken("es-ES").Name);
    }

    [Fact]
    public void ResolveCulture_WhenCookieInvalid_ReturnsEnUs()
    {
        var cookies = new RequestCookieCollection(
            new Dictionary<string, string>
            {
                [NumberCulturePreference.CookieName] = "fr-FR"
            });

        var culture = NumberCulturePreference.ResolveCulture(cookies);

        Assert.Equal("en-US", culture.Name);
    }

    [Fact]
    public void CreateCookieOptions_UsesExpectedSecurityDefaults()
    {
        var before = DateTimeOffset.UtcNow;
        var options = NumberCulturePreference.CreateCookieOptions(secure: true);
        var after = DateTimeOffset.UtcNow;

        Assert.Equal("/", options.Path);
        Assert.True(options.HttpOnly);
        Assert.True(options.Secure);
        Assert.Equal(SameSiteMode.Lax, options.SameSite);
        Assert.NotNull(options.Expires);

        var expectedMin = before.AddDays(365).AddMinutes(-1);
        var expectedMax = after.AddDays(365).AddMinutes(1);
        Assert.InRange(options.Expires.Value, expectedMin, expectedMax);
    }

    [Fact]
    public void CreateCookieOptions_WhenNotSecure_SetsSecureFalse()
    {
        var options = NumberCulturePreference.CreateCookieOptions(secure: false);

        Assert.False(options.Secure);
    }

    [Fact]
    public void AppendCookie_WritesWhitelistedToken()
    {
        var context = new DefaultHttpContext();

        NumberCulturePreference.AppendCookie(context.Response.Cookies, "es-ES", secure: true);

        var setCookie = Assert.Single(context.Response.Headers.SetCookie);
        Assert.Contains("hm.numberCulture=es-ES", setCookie, StringComparison.Ordinal);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppendCookie_WhenUnsupported_WritesDefaultToken()
    {
        var context = new DefaultHttpContext();

        NumberCulturePreference.AppendCookie(context.Response.Cookies, "fr-FR", secure: false);

        var setCookie = Assert.Single(context.Response.Headers.SetCookie);
        Assert.Contains("hm.numberCulture=en-US", setCookie, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/storage-health", true, "/storage-health")]
    [InlineData("/storage-health/AppA", true, "/storage-health/AppA")]
    [InlineData(null, true, "/")]
    [InlineData("", true, "/")]
    [InlineData("https://example.com", false, "/")]
    [InlineData("//example.com", false, "/")]
    public void ResolveReturnUrl_UsesLocalUrlPredicate(
        string? returnUrl,
        bool isLocal,
        string expected)
    {
        var resolved = NumberCulturePreference.ResolveReturnUrl(
            returnUrl,
            _ => isLocal);

        Assert.Equal(expected, resolved);
    }

    /// <summary>
    /// Minimal cookie collection for preference resolution tests (no ASP.NET host required).
    /// </summary>
    private sealed class RequestCookieCollection : Dictionary<string, string>, IRequestCookieCollection
    {
        public RequestCookieCollection()
            : base(StringComparer.Ordinal)
        {
        }

        public RequestCookieCollection(IDictionary<string, string> values)
            : base(values, StringComparer.Ordinal)
        {
        }

        ICollection<string> IRequestCookieCollection.Keys => Keys;

        string? IRequestCookieCollection.this[string key] =>
            TryGetValue(key, out var value) ? value : null;
    }
}
