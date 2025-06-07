namespace RuntimeBundler.Services
{
    /// <summary>
    /// Provides pre-concatenated bundle bytes for a logical bundle key.
    /// </summary>
    public interface IBundleProvider
    {
        /// <summary>
        /// Returns the concatenated bundle content for the named bundle.
        /// The caller supplies the bundle key (e.g. "cog").
        /// </summary>
        /// <param name="bundleKey">Dictionary key defined in BundleConfiguration.</param>
        /// <returns>Byte array representing the bundled JavaScript (or null if not found).</returns>
        Task<byte[]?> GetBundleAsync(string bundleKey);
    }
}