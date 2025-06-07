// src/Middleware/BundlingMiddleware.cs
using System;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using RuntimeBundler.Models;
using RuntimeBundler.Services;

namespace RuntimeBundler.Middleware
{
    /// <summary>
    /// Intercepts requests for bundle URLs (e.g. /scripts/cog.js), retrieves the
    /// concatenated bytes from <see cref="IBundleProvider"/>, and streams the
    /// result to the client. All non-bundle requests are passed through.
    /// </summary>
    internal sealed class BundlingMiddleware
    {
        private const string JsContentType = "application/javascript; charset=utf-8";
        private const string CssContentType = "text/css; charset=utf-8";

        private readonly RequestDelegate _next;
        private readonly IBundleProvider _provider;
        private readonly BundleConfiguration _config;
        private readonly ILogger<BundlingMiddleware> _logger;

        public BundlingMiddleware(
            RequestDelegate next,
            IBundleProvider provider,
            IOptions<BundleConfiguration> opt,
            ILogger<BundlingMiddleware> logger)
        {
            _next = next;
            _provider = provider;
            _config = opt.Value ?? new BundleConfiguration();
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var requestPath = context.Request.Path.Value ?? string.Empty;

            // Quick lookup: find the bundle key whose UrlPath matches the request
            var match = _config.Bundles
                               .FirstOrDefault(kvp =>
                                   string.Equals(kvp.Value.UrlPath, requestPath,
                                                 StringComparison.OrdinalIgnoreCase));

            if (match.Equals(default(KeyValuePair<string, BundleDefinition>)))
            {
                // Not a bundle path – continue down pipeline
                await _next(context);
                return;
            }

            var bundleKey = match.Key;
            var isCss = match.Value.UrlPath.EndsWith(".css", StringComparison.OrdinalIgnoreCase);
            var bundle = match.Value;

            var bytes = await _provider.GetBundleAsync(bundleKey);
            if (bytes is null || bytes.Length == 0)
            {
                _logger.LogWarning("Bundle '{Key}' resolved to no content", bundleKey);
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var ttl = bundle.CacheDuration == default
             ? TimeSpan.FromMinutes(5) 
             : bundle.CacheDuration;

            var h = context.Response.GetTypedHeaders();
            h.CacheControl = new CacheControlHeaderValue
            {
                Public = true,
                MaxAge = ttl
            };
            h.Expires = DateTimeOffset.UtcNow.Add(ttl);

            context.Response.ContentType = isCss ? CssContentType : JsContentType;
            context.Response.ContentLength = bytes.Length;

            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}
