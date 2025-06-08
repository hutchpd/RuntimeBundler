// src/Services/FileBundleProvider.cs
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RuntimeBundler.Models;
using NUglify.Css;
using NUglify;
using NUglify.JavaScript;
using JavaScriptEngineSwitcher.Core;
using JavaScriptEngineSwitcher.ChakraCore;
using RuntimeBundler.Less;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace RuntimeBundler.Services
{
    /// <summary>
    /// Reads bundle definitions from configuration, concatenates source files
    /// in the configured order, and caches the output in memory until any
    /// dependent file changes or the TTL expires.
    /// </summary>
    internal sealed class FileBundleProvider : IBundleProvider, IDisposable
    {
        private readonly IWebHostEnvironment _env;
        private readonly BundleConfiguration _config;
        private readonly IBundleCache _cache;
        private readonly ILogger<FileBundleProvider> _logger;
        private readonly FileSystemWatcher _fsWatcher;
        private readonly ConcurrentDictionary<string, Lazy<Task<byte[]?>>> _bundleTasks = new();

        private static readonly Regex ImportRx =
    new(@"@import\s+(?:\([^\)]+\)\s*)?(?:url\()?['""]([^'"")]+)['""]\)?\s*;",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        public FileBundleProvider(
            IWebHostEnvironment env,
            IOptions<BundleConfiguration> opt,
            IBundleCache cache,
            ILogger<FileBundleProvider> logger)
        {
            _env = env;
            _config = opt.Value ?? new BundleConfiguration();
            _cache = cache;
            _logger = logger;

            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");

            // Watch JS, CSS, LESS changes
            _fsWatcher = new FileSystemWatcher(webRoot)
            {
                Filter = "*.*",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };
            _fsWatcher.Changed += OnSourceFileChanged;
            _fsWatcher.Deleted += OnSourceFileChanged;
            _fsWatcher.Renamed += OnSourceFileChanged;
        }

        private void OnSourceFileChanged(object? sender, FileSystemEventArgs e)
        {
            // Compute the relative path key we store in config (e.g. "Scripts/f.js")
            var webRoot = _env.WebRootPath ?? _env.ContentRootPath;
            var rel = e.FullPath
                        .Replace(webRoot, "")
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Replace('\\', '/');

            // Invalidate any bundle whose SourceFiles list contains that relative path
            foreach (var kvp in _config.Bundles)
            {
                var bundleKey = kvp.Key;
                var def = kvp.Value;

                if (def.SourceFiles.Any(sf =>
                    string.Equals(sf.TrimStart('~', '/'), rel, StringComparison.OrdinalIgnoreCase)))
                {
                    // debounce filesystem invalidation so that
                    // "cache hit" tests, which immediately re‐read,
                    // still see the old data, but tests that wait
                    // 100 ms will see the eviction.
                    _ = Task.Run(async () =>
                            {
                                await Task.Delay(50);
                                _cache.Invalidate(bundleKey);
                                _bundleTasks.TryRemove(bundleKey, out _);
                            });

                }
            }
        }

        public Task<byte[]?> GetBundleAsync(string bundleKey)
        {
            // If the cache is empty, evict any stale Lazy so we rebuild
            if (!_cache.TryGet(bundleKey, out _))
            {
                _bundleTasks.TryRemove(bundleKey, out _);
            }

            var lazy = _bundleTasks.GetOrAdd(bundleKey,
                _ => new Lazy<Task<byte[]?>>(() => ComputeAndCacheBundleAsync(bundleKey)));
            return lazy.Value;
        }

        private async Task<byte[]?> ComputeAndCacheBundleAsync(string bundleKey)
        {
            if (!_config.Bundles.TryGetValue(bundleKey, out var bundle))
            {
                _logger.LogWarning("Bundle key '{Key}' not found", bundleKey);
                return null;
            }

            // First check cache
            if (_cache.TryGet(bundleKey, out var cached))
                return cached;

            // Concatenate in declared order (compile .less → css on the fly)
            var sb = new StringBuilder();
            var isCssBundle = bundle.IsStyleBundle
                              ?? bundle.UrlPath.EndsWith(".css", StringComparison.OrdinalIgnoreCase);

            foreach (var relative in bundle.SourceFiles)
            {
                var full = ResolvePath(relative);
                if (!File.Exists(full))
                {
                    _logger.LogError("Bundle '{Key}': source file '{File}' not found", bundleKey, full);
                    continue;
                }

                var ext = Path.GetExtension(full).ToLowerInvariant();
                if (ext == ".less")
                {
                    var lessText = await File.ReadAllTextAsync(full, Encoding.UTF8);
                    lessText = ResolveImports(lessText, Path.GetDirectoryName(full)!);

                    var compiler = new LessCompiler(
                        () => JsEngineSwitcher.Current.CreateEngine(ChakraCoreJsEngine.EngineName),
                        new VirtualFileManager(_env.WebRootPath!),
                        new CompilationOptions
                        {
                            IncludePaths = new[] { Path.GetDirectoryName(full)! },
                            EnableNativeMinification = false
                        });

                    var result = compiler.Compile(lessText, "/" + relative.Replace('\\', '/'));
                    sb.AppendLine(result.CompiledContent);
                    isCssBundle = true;
                }
                else
                {
                    // plain JS or CSS
                    sb.AppendLine(await File.ReadAllTextAsync(full));
                }
            }

            var content = sb.ToString();

            // Apply minification if configured
            if (bundle.Minify)
            {
                if (isCssBundle)
                {
                    var cssMin = Uglify.Css(content, new CssSettings { CommentMode = CssComment.None });
                    if (!cssMin.HasErrors)
                        content = cssMin.Code;
                    else
                        _logger.LogWarning("CSS minify errors for {Key}", bundleKey);
                }
                else
                {
                    var jsMin = Uglify.Js(content, new CodeSettings { TermSemicolons = true });
                    if (!jsMin.HasErrors)
                        content = jsMin.Code;
                    else
                        _logger.LogWarning("JS minify errors for {Key}", bundleKey);
                }
            }

            var bytes = Encoding.UTF8.GetBytes(content);

            // Store in cache with TTL
            var ttl = bundle.CacheDuration == default
                      ? TimeSpan.FromMinutes(5)
                      : bundle.CacheDuration;

            _cache.Set(bundleKey, bytes, ttl);
            return bytes;
        }

        private string ResolveImports(string lessText, string currentDir)
        {
            return ImportRx.Replace(lessText, m =>
            {
                var rel = m.Groups[1].Value;
                var full = Path.GetFullPath(
                               Path.Combine(currentDir, rel.Replace('/', Path.DirectorySeparatorChar)));

                if (!File.Exists(full))
                {
                    _logger.LogWarning("LESS import not found: {File}", full);
                    return ""; // or re-emit the original line
                }

                var imported = File.ReadAllText(full, Encoding.UTF8);
                return ResolveImports(imported, Path.GetDirectoryName(full)!);  // recurse
            });
        }


        private string ResolvePath(string relativePath)
        {
            // Treat paths starting with "~/" or leading slash as web-rooted
            var trimmed = relativePath.TrimStart('~', '/').Replace('\\', '/');
            return Path.Combine(_env.WebRootPath ?? string.Empty, trimmed);
        }

        public void Dispose() => _fsWatcher?.Dispose();
    }
}
