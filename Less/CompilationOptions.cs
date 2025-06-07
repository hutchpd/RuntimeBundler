// SPDX-License-Identifier: Apache-2.0
// Adapted from BundleTransformer.Less under Apache-2.0

namespace RuntimeBundler.Less
{
    /// <summary>
    /// Options controlling how LESS is compiled.
    /// </summary>
    public class CompilationOptions
    {
        public bool EnableNativeMinification { get; set; }
        public IList<string> IncludePaths { get; set; } = new List<string>();
        public bool IeCompat { get; set; }
        public MathMode Math { get; set; } = MathMode.Strict;
        public bool StrictUnits { get; set; }
        public LineNumbersMode DumpLineNumbers { get; set; } = LineNumbersMode.None;
        public bool JavascriptEnabled { get; set; } = true;
        public string? GlobalVariables { get; set; }
        public string? ModifyVariables { get; set; }
        public int Severity { get; set; } = 0;
    }

    public enum MathMode { Strict, Loose }
    public enum LineNumbersMode { None, Comments, MediaQuery }
}
