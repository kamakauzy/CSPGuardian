namespace CSPGuardian.Adapters;

/// <summary>
/// Adapter for .NET framework handling (modern .NET Core/Blazor and legacy MVC 4/Web Forms)
/// </summary>
public class DotNetAdapter
{
    private readonly string _framework;
    private readonly bool _legacyMode;

    public DotNetAdapter(string framework, bool legacyMode)
    {
        _framework = framework;
        _legacyMode = legacyMode;
    }

    /// <summary>
    /// Generate nonce injection code for .NET middleware/tag helpers
    /// </summary>
    public string GenerateNonceMiddleware()
    {
        if (_legacyMode || _framework == "legacy-dotnet")
        {
            return GenerateLegacyNonceCode();
        }

        return GenerateModernNonceCode();
    }

    /// <summary>
    /// Generate code snippet for modern .NET Core/Blazor
    /// </summary>
    private string GenerateModernNonceCode()
    {
        return @"// Add to Startup.cs or Program.cs
app.Use(async (context, next) =>
{
    var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    context.Items[""Nonce""] = nonce;
    context.Response.Headers.Add(""Content-Security-Policy"", 
        $""script-src 'self' 'nonce-{nonce}'; style-src 'self' 'nonce-{nonce}';"");
    await next();
});

// In Razor views, use:
// <script nonce=""@Context.Items[""Nonce""]"">...</script>";
    }

    /// <summary>
    /// Generate code snippet for legacy .NET (MVC 4/Web Forms)
    /// </summary>
    private string GenerateLegacyNonceCode()
    {
        return @"// For MVC 4, add to Global.asax.cs Application_BeginRequest:
protected void Application_BeginRequest(object sender, EventArgs e)
{
    var nonce = Convert.ToBase64String(new byte[16]);
    HttpContext.Current.Items[""Nonce""] = nonce;
    Response.AddHeader(""Content-Security-Policy"", 
        $""script-src 'self' 'nonce-{nonce}'; style-src 'self' 'nonce-{nonce}';"");
}

// In views (.cshtml), use:
// <script nonce=""@HttpContext.Current.Items[""Nonce""]"">...</script>

// For Web Forms, consider using BundleConfig to externalize scripts:
// BundleTable.Bundles.Add(new ScriptBundle(""~/bundles/scripts"").Include(""~/Scripts/*.js""));";
    }

    /// <summary>
    /// Suggest migration path for legacy .NET applications
    /// </summary>
    public string GetMigrationSuggestion()
    {
        if (_legacyMode || _framework == "legacy-dotnet")
        {
            return @"Legacy .NET Migration Suggestions:
1. Use BundleConfig to externalize inline scripts
2. Replace inline event handlers with addEventListener
3. Consider migrating to .NET Core/Blazor for better CSP support
4. Use WebOptimizer for bundling and minification
5. Review ViewState usage - consider disabling if not needed";
        }

        return "Modern .NET applications should use NetEscapades.AspNetCore.SecurityHeaders package for CSP management.";
    }
}

