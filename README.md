# RuntimeBundler

**Because all the other bundlers suck and it's still somehow 2009.**

Welcome to `RuntimeBundler`, the elegant Frankenstein’s monster of .NET middleware that bundles and minifies your JavaScript, CSS, and even... *LESS* files. Yes, LESS. Because this project is a stubborn time capsule from a bygone era and no one had the courage to say “convert it to SCSS already.”

This is the bundler you write in a sleep-deprived fugue state on a Saturday because:

* `dotless` is on life support and smells like Silverlight,
* `BuildBundlerMinifier` is a historical artifact,
* `BundlerMinifier.Core` gave up on life,
* and `LigerShark.WebOptimizer.Core` can't sort a list to save its own legacy.

---

## Features

* **Maintains File Order**
  Unlike some projects that treat order like a polite suggestion, we *actually* keep it.

* **Minifies Output**
  Tired of fat JS/CSS payloads? Us too. Tick the `Minify` flag and feel 23% smarter.

* **LESS Compilation**
  Why? Because the project uses LESS. Don’t ask questions. It just does.

*  **Smart Caching with TTL + Invalidation**
  Bundles are cached. If the source changes, we *notice*. Like a clingy ex. But helpful.

* **Middleware Integration**
  Plug it into your ASP.NET Core pipeline like it’s 2016 again.

* **Graceful Degradation**
  Missing source files? We just skip them. No tears. No drama. No stack traces.

*  **Battle-Tested**
  Over 25 tests ensuring it doesn't implode when touched.

---

## Installation

```bash
dotnet add package RuntimeBundler
```

Then summon it into your project like the arcane relic it is.

---

##  Example Usage

Here’s how to throw it into your app like a disgruntled dev tossing spaghetti at the wall:

```csharp
app.UseMiddleware<BundlingMiddleware>();
```

Configure your bundles in `Startup` (or wherever your soul-crushing legacy app allows):

```csharp
var config = new BundleConfiguration();
config.Bundles["site"] = new BundleDefinition
{
    UrlPath = "/styles/site.css",
    SourceFiles = new[] { "Styles/main.less", "Styles/theme.css" },
    Minify = true,
    CacheDuration = TimeSpan.FromMinutes(30)
};
```

Inject `IBundleProvider` and enjoy the single-build mechanism that guarantees only one build per bundle key:

```csharp
services.AddSingleton<IBundleProvider>(new FileBundleProvider(...));
```

This mechanism ensures that concurrent requests for the same bundle key share the same build result, reducing redundant processing and improving efficiency.

---

## Tests That Pass (We Swear)

All the following horrors are *actually* tested:

* ✅ LESS compiles and minifies
* ✅ JS and CSS preserve order like they’re in the military
* ✅ Missing files don’t kill the app
* ✅ Middleware returns proper content types
* ✅ Bundles are cached and cache TTLs respected
* ✅ Multiple concurrent requests don’t start a race war
* ✅ Single-build per key ensures no redundant builds under concurrent requests
Also: exceptions are thrown when you write malformed LESS. It’s not *that* forgiving.

---

## Why Does This Exist?

Because existing bundlers either:

* Don't work with LESS
* Break JS order
* Depend on .NET Framework-era packages
* Or bring in deprecated junk that makes you question every decision you've made in your life

So... we wrote this instead. Not because we wanted to. But because we had to. Like Frodo carrying that dumb ring.

---

## Roadmap

* Maybe SCSS support? (lol jk)
* Sourcemaps?
* TypeScript?
* Willpower?

Contributions welcome. Pity is also accepted.

---

## Authors

Written by a tired, bitter developer trapped in a CSS preprocessor hellscape.

*Please send snacks.*

---

## License

Unlicence. Because even the license is like “do what you want, I’m tired.”

## Setup Instructions

You’ve got an ASP.NET Core app that still thinks CSS preprocessors are cool. Here’s how to get `RuntimeBundler` doing its thing:

### 1. Add the NuGet Package

```bash
dotnet add package RuntimeBundler
```

### 2. Create Your Bundle Configuration

This defines what gets bundled, where it gets served, and how tightly it gets squished:

```csharp
var config = new BundleConfiguration();
config.Bundles["site"] = new BundleDefinition
{
    UrlPath = "/styles/site.css", // public URL
    SourceFiles = new[] {
        "Styles/reset.css",
        "Styles/main.less",   // yep, LESS is supported
        "Styles/theme.css"
    },
    Minify = true,
    CacheDuration = TimeSpan.FromMinutes(30)
};
```

### 3. Register Services

Stick this in your `Startup.cs` or wherever your app's dependency injection happens:

```csharp
services.AddSingleton<IBundleProvider>(
    new FileBundleProvider(
        env,                                // IWebHostEnvironment
        Options.Create(config),             // your BundleConfiguration
        new InMemoryBundleCache(),          // or your own IBundleCache
        NullLogger<FileBundleProvider>.Instance
    )
);

services.AddSingleton<IOptions<BundleConfiguration>>(Options.Create(config));
```

### 4. Plug the Middleware Into the Pipeline

Right after static files is a good spot:

```csharp
app.UseStaticFiles(); // if you're using this
app.UseMiddleware<BundlingMiddleware>();
```

### 5. Profit

Once it’s set up, requests to `/styles/site.css` or `/scripts/bundle.js` will return your bundled, optionally minified, possibly LESSified output.

```

Below are two **minimal but realistic** examples you can drop straight into your repo.

---

### **`appsettings.json`**

```jsonc
{
  // any other ASP.NET config you already have …
  "Logging": {
    "LogLevel": { "Default": "Information" }
  },

  // ------------------ RuntimeBundler ------------------
  "Bundles": {
    "cog": {                             // — logical key (case-insensitive)
      "UrlPath": "/scripts/cog.js",      // public URL
      "SourceFiles": [
        "Scripts/popper.min.js",
        "Scripts/bootstrap.js",
        "Scripts/utils.js",
        "Scripts/app.js"
      ],
      "CacheDuration": "00:10:00",       // 10 minutes (HH:MM:SS)
      "Minify":  true                    // run NUglify after concat
    },

    "siteCss": {
      "UrlPath": "/styles/site.css",
      "SourceFiles": [
        "Styles/reset.css",
        "Styles/layout.less",            // mixed CSS + LESS
        "Styles/theme.css"
      ],
      "CacheDuration": "01:00:00",       // 1 hour
      "Minify": true,
      "IsStyleBundle": true              // overrides extension detection
    },

    "vendor": {
      "UrlPath": "/scripts/vendor.js",
      "SourceFiles": [
        "Scripts/libs/jquery.js",
        "Scripts/libs/underscore.js"
      ],
      "Minify": false                    // keep readable for debugging
    }
  }
}
```

*Anything omitted uses the defaults in your model (e.g. `CacheDuration` → 5 min).*

---

### **`bundleconfig.json` (optional override / CLI-style file)**

If you’d like a **stand-alone file**, e.g. to keep front-end folk out of the main
settings, you can create a sibling `bundleconfig.json` like this:

```json
{
  "Bundles": {
    "admin": {
      "UrlPath": "/scripts/admin.js",
      "SourceFiles": [
        "Scripts/Admin/grid.js",
        "Scripts/Admin/widgets.js"
      ],
      "CacheDuration": "00:30:00",
      "Minify": true
    },

    "print": {
      "UrlPath": "/styles/print.css",
      "SourceFiles": [
        "Styles/print.less"
      ],
      "Minify": true
    }
  }
}
```

Then, in `Program.cs` or `Startup.cs`:

```csharp
builder.Configuration
       .AddJsonFile("bundleconfig.json", optional: true, reloadOnChange: true);

builder.Services.AddRuntimeBundler(builder.Configuration);
// …
app.UseStaticFiles();
app.UseRuntimeBundler();      // after static files
```

---

#### Quick tips

| Setting         | Effect                                                                                   |
| --------------- | ---------------------------------------------------------------------------------------- |
| `UrlPath`       | Must begin with `/`; dictates the request path the middleware intercepts.                |
| `SourceFiles`   | **Exact** order matters – files are concatenated as listed.                              |
| `CacheDuration` | Controls both in-memory cache TTL **and** `Cache-Control: max-age` in the HTTP response. |
| `Minify`        | Runs NUglify (CSS/JS) after concatenation. Leave `false` in development if you prefer.   |
| `IsStyleBundle` | Force CSS processing even if `UrlPath` doesn’t end with `.css` (rare but supported).     |
| Setting         | Effect                                                                                   |
| Single-Build    | Guarantees only one build operation per bundle key, even under concurrent requests.      |


That’s all you need – drop the JSON in, wire `AddRuntimeBundler` / `UseRuntimeBundler`, and your bundles will build on first request, cache, auto-invalidate on file change, and serve with the correct headers.