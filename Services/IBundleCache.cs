using System;

namespace RuntimeBundler.Services
{
    /// <summary>
    /// Abstraction for caching bundled output in memory (or any backing store).
    /// </summary>
    public interface IBundleCache
    {
        /// <summary>
        /// Attempts to retrieve a cached bundle.
        /// </summary>
        /// <param name="key">Logical bundle key (e.g. "cog").</param>
        /// <param name="value">If found &amp; valid, the cached bytes.</param>
        /// <returns>True if a non-expired entry exists; otherwise false.</returns>
        bool TryGet(string key, out byte[]? value);

        /// <summary>
        /// Inserts or replaces a cached bundle.
        /// </summary>
        /// <param name="key">Logical bundle key.</param>
        /// <param name="content">Concatenated byte array to cache.</param>
        /// <param name="ttl">Time-to-live before the entry expires.</param>
        void Set(string key, byte[] content, TimeSpan ttl);

        /// <summary>
        /// Removes a cached entry immediately (e.g., after a file change).
        /// </summary>
        /// <param name="key">Logical bundle key to invalidate.</param>
        void Invalidate(string key);
    }
}
