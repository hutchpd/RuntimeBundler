// SPDX-License-Identifier: Apache-2.0
// Adapted & simplified from BundleTransformer.Less.Internal.LessCompiler

using System;
using System.Collections.Generic;
using System.IO;
using JavaScriptEngineSwitcher.Core;

namespace RuntimeBundler.Less
{
    /// <summary>
    /// A simple LESS→CSS compiler using JavaScriptEngineSwitcher.
    /// </summary>
    public class LessCompiler : IDisposable
    {
        private readonly IJsEngine _engine;
        private readonly VirtualFileManager _fs;
        private readonly CompilationOptions _opts;

        /// <summary>
        /// Create a new compiler.
        /// </summary>
        /// <param name="createJsEngine">Factory for the JS engine (e.g. ChakraCore).</param>
        /// <param name="fileSystem">Helper to resolve "@import" paths under webroot.</param>
        /// <param name="options">Compilation options.</param>
        public LessCompiler(
    Func<IJsEngine> createJsEngine,
    VirtualFileManager fileSystem,
    CompilationOptions options)
        {
            _engine = createJsEngine();
            _fs = fileSystem;
            _opts = options;

            _engine.Execute(@"
                var global = this;
                var window = global;
                var document = {
                    createElement: function () { return { style:{}, firstChild:null, removeChild:function(){}, appendChild:function(){} }; },
                    createTextNode: function () { return {}; },
                    getElementsByTagName: function () { return []; },
                    head: { appendChild: function(){} }
                };
                var navigator = {};
                var location  = { href:'/' };
                var module  = { exports: {} };
                var exports = module.exports;
            ");

            var asm = typeof(LessCompiler).Assembly;
            using var stream = asm.GetManifestResourceStream("RuntimeBundler.Less.runtime.less.min.js")
                             ?? throw new InvalidOperationException("Embedded less.min.js not found");
            using var reader = new StreamReader(stream);
            var lessJs = reader.ReadToEnd();
            _engine.Execute(lessJs);

            _engine.Execute("var less = module.exports;");

            _engine.Execute(@"
(function(fr, tree){
  // helpers we want to guard
  var names = ['ceil','floor','round','sqrt','pow','abs','min','max'];

  function asDim(n){
    var u = (n && n.unit) ? n.unit.clone() : new tree.Unit([]);
    return new tree.Dimension(0, u);
  }

  names.forEach(function(name){
    var orig = fr.get(name);
    if (!orig) return;                 // skip if Less drops one later
    fr.add(
      name,
      function(n,a){                 // keep extra arg for pow, min, max…
        try { return orig.apply(this, arguments); }
        catch(_) { return asDim(n); }   // fall back to 0<unit>
      },
      /* overwrite = */ true
    );
  });
})(less.functions.functionRegistry, less.tree);
");



        }




        /// <summary>
        /// Compile a LESS string into CSS.
        /// </summary>
        /// <param name="lessCode">The raw LESS text.</param>
        /// <param name="virtualPath">Virtual path ("/styles/foo.less") for @import resolution.</param>
        public CompilationResult Compile(string lessCode, string virtualPath)
        {
            // Expose browser globals before loading less.js
            _engine.Execute(@"
                var window   = {};
                var document = { getElementsByTagName: function(){ return []; } };
                var navigator = {};
                var location  = { href: '/' };
            ");

            // Build .NET options object
            var opts = new
            {
                filename = virtualPath,
                paths = _opts.IncludePaths,
                ieCompat = _opts.IeCompat,
                math = _opts.Math.ToString().ToLowerInvariant(),
                strictMath = _opts.StrictUnits,
                dumpLineNumbers = _opts.DumpLineNumbers.ToString().ToLowerInvariant(),
                javascriptEnabled = _opts.JavascriptEnabled,
                globalVars = _opts.GlobalVariables,
                modifyVars = _opts.ModifyVariables
            };

            // Serialize the options object to JSON
            var jsonOpts = System.Text.Json.JsonSerializer.Serialize(opts);

            // Inject the LESS code and options into the JS context using JSON serialization
            _engine.Execute($"var LESS_CODE = {System.Text.Json.JsonSerializer.Serialize(lessCode)};");
            _engine.Execute($"var LESS_OPTS = {jsonOpts};");

            // Call less.render synchronously via a shim to capture CSS output
            var compileScript = @"
              var __css = '';
              var __err = null;

              less.render(LESS_CODE, LESS_OPTS)
                  .then(function (out) { __css = out.css; })
                  .catch(function (e)  { __err = JSON.stringify(e); });

              if (__err) { throw new Error(__err); }
              __css;
            ";
                
            var css = _engine.Evaluate<string>(compileScript);

            // Return the result (no imported files tracked here)
            return new CompilationResult(css, Array.Empty<string>());
        }

        public void Dispose() => _engine.Dispose();
    }

    /// <summary>
    /// Very minimal file-system abstraction,
    /// to resolve @import paths & host the less.min.js script.
    /// </summary>
    public class VirtualFileManager
    {
        public string RootPath { get; }

        public VirtualFileManager(string webRootPath)
        {
            RootPath = webRootPath;
        }
    }
}
