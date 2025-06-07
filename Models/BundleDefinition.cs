using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuntimeBundler.Models
{
    /// <summary>
    /// Describes a single logical bundle (e.g. "/scripts/cog.js").
    /// </summary>
    public sealed class BundleDefinition
    {
        /// <summary>
        /// The public URL path clients will request (must start with "/").
        /// Example: "/scripts/cog.js".
        /// </summary>
        public string UrlPath { get; set; } = string.Empty;

        /// <summary>
        /// Source files in the exact order they should be concatenated.  These are
        /// absolute or web‑root‑relative file paths such as
        /// "wwwroot/scripts/popper.min.js".
        /// </summary>
        public IList<string> SourceFiles { get; set; } = new List<string>();

        /// <summary>
        /// How long the generated bundle should stay in the in‑memory cache before
        /// re‑reading from disk.  Default is 5 minutes.
        /// </summary>
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// If true, the bundle will be minified (e.g. whitespace removed).
        /// using NUglify
        /// </summary>
        public bool Minify { get; set; } = false;

        /// <summary>
        /// If true, the bundle is a CSS style bundle.
        /// This affects how the bundle is processed and served (e.g. using
        /// BundleTransformer for LESS).
        /// Autodetected if not set.
        /// </summary>
        public bool? IsStyleBundle { get; set; }
    }
}
