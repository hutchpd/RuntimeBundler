using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuntimeBundler.Models
{
    /// <summary>
    /// Root object that gets bound from configuration (e.g., "Bundles" section of
    /// appsettings.json).  The key is an arbitrary bundle name ("cog", "admin" …).
    /// </summary>
    public sealed class BundleConfiguration
    {
        /// <summary>
        /// All bundle definitions keyed by name.  Key is case‑insensitive to make
        /// JSON hand‑editing easier.
        /// </summary>
        public IDictionary<string, BundleDefinition> Bundles { get; set; } =
            new Dictionary<string, BundleDefinition>(StringComparer.OrdinalIgnoreCase);
    }
}
