// SPDX-License-Identifier: Apache-2.0
// Adapted from BundleTransformer.Less under Apache-2.0

namespace RuntimeBundler.Less
{
    /// <summary>
    /// Outcome of a single LESS compilation.
    /// </summary>
    public class CompilationResult
    {
        /// <summary>
        /// The CSS text produced.
        /// </summary>
        public string CompiledContent { get; }

        /// <summary>
        /// Any “imported” file paths that less included.
        /// </summary>
        public IReadOnlyList<string> IncludedFilePaths { get; }

        public CompilationResult(string css, IReadOnlyList<string> deps)
        {
            CompiledContent = css;
            IncludedFilePaths = deps;
        }
    }
}
