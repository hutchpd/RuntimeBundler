using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// src/Extensions/BundlerExtensions.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RuntimeBundler.Middleware;
using RuntimeBundler.Services;
using RuntimeBundler.Models;

namespace RuntimeBundler.Extensions
{
    /// <summary>
    /// Extension helpers for adding / enabling RuntimeBundler.
    /// </summary>
    public static class BundlerExtensions
    {
        /// <summary>
        /// Registers the bundler services and binds bundle definitions from
        /// IConfiguration (expects a root section named "Bundles").
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="configuration">Application configuration (e.g., appsettings.json).</param>
        public static IServiceCollection AddRuntimeBundler(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // 1. Bind the bundle definitions (ordered file lists, cache TTL, etc.)
            services.Configure<BundleConfiguration>(configuration.GetSection("Bundles"));

            // 2. Register core services
            services.AddSingleton<IBundleCache, InMemoryBundleCache>();
            services.AddSingleton<IBundleProvider, FileBundleProvider>();

            return services;
        }

        /// <summary>
        /// Inserts the BundlingMiddleware so that requests to bundle endpoints
        /// (e.g., /scripts/cog.js) are intercepted and served by RuntimeBundler.
        /// Make sure UseRuntimeBundler() comes **after** UseStaticFiles() so that
        /// physical JS files still short-circuit the pipeline when they exist.
        /// </summary>
        public static IApplicationBuilder UseRuntimeBundler(this IApplicationBuilder app)
        {
            return app.UseMiddleware<BundlingMiddleware>();
        }
    }
}
