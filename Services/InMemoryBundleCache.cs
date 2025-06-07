using System;
using System.Collections.Concurrent;

namespace RuntimeBundler.Services
{
    /// <summary>
    /// Thread-safe, in-memory implementation of <see cref="IBundleCache"/>.
    /// </summary>
    internal sealed class InMemoryBundleCache : IBundleCache
    {
        private sealed record CacheEntry(byte[] Bytes, DateTimeOffset Expires);

        private readonly ConcurrentDictionary<string, CacheEntry> _entries =
            new(StringComparer.OrdinalIgnoreCase);

        public bool TryGet(string key, out byte[]? value)
        {
            value = null;

            if (_entries.TryGetValue(key, out var entry))
            {
                if (DateTimeOffset.UtcNow < entry.Expires)
                {
                    value = entry.Bytes;
                    return true;
                }

                // expired – purge and fall through
                _entries.TryRemove(key, out _);
            }

            return false;
        }

        public void Set(string key, byte[] content, TimeSpan ttl)
        {
            var expires = DateTimeOffset.UtcNow.Add(ttl);
            var entry = new CacheEntry(content, expires);
            _entries[key] = entry;
        }

        public void Invalidate(string key)
        {
            _entries.TryRemove(key, out _);
        }
    }
}
