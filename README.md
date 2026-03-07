# CSPGuardian

CSPGuardian is a .NET 8 CLI for finding CSP-hostile patterns in web codebases, planning remediation, and generating starter CSP policies.

It is aimed at legacy ASP.NET, Razor, static HTML, and JavaScript-heavy projects where inline scripts, inline styles, event handlers, and `javascript:` URLs have accumulated over time.

## Repository note

The intended primary branch for this repository is `main`. If GitHub still shows `CronosProd` as the default branch, treat that as a repository-settings issue rather than the branch of record.

Local git metadata in this repo now points `origin/HEAD` at `main`, but GitHub's server-side default branch must still be changed in repository settings if it has not been updated yet.

## What the tool does today

- Scans supported files recursively for CSP-relevant findings
- Detects inline `<script>` and `<style>` blocks
- Detects inline event handlers such as `onclick`
- Detects `style="..."` attributes
- Detects `javascript:` URLs in HTML and CSS
- Detects `eval()` / `Function(...)` usage in JavaScript
- Detects legacy .NET patterns such as `WebResource.axd` and ViewState/script combinations
- Supports three remediation modes:
  - `hash` - compute CSP hashes for inline `<script>` and `<style>` blocks
  - `nonce` - generate nonce guidance for inline `<script>` and `<style>` blocks
  - `externalize` - extract inline `<script>` and `<style>` blocks into `.js` / `.css` files and rewrite the source file
- Generates Markdown, JSON, or CSV reports
- Generates a starter CSP policy file
- Includes experimental Go and Python scan/report ports for cross-language adoption

## Current limits

- Automatic externalization only applies to inline `<script>` and `<style>` blocks.
- Event handlers, `style` attributes, `javascript:` URLs, and dynamic JavaScript execution are reported with manual remediation guidance.
- Nonce mode generates guidance and policy placeholders; it does not patch your application runtime for you.
- The scanner currently targets `.html`, `.htm`, `.cshtml`, `.aspx`, `.ascx`, `.js`, and `.css`.
- The JavaScript framework modes (`js-modern`, `js-legacy`) add lightweight heuristics; they are not full framework-aware parsers.
- The Go and Python ports currently focus on scan/report workflows. They support `none`, `hash`, `nonce`, and externalize guidance, but they do not yet rewrite source files or emit CSP policy files.

## Prerequisites

- .NET 8 SDK

## Build

```bash
git clone https://github.com/kamakauzy/CSPGuardian.git
cd CSPGuardian
dotnet restore CSPGuardian.sln
dotnet build CSPGuardian.sln
```

## Run

Use `dotnet run` from the repo, or run the published binary after `dotnet publish`.

```bash
dotnet run --project src/CSPGuardian/CSPGuardian.csproj -- \
  scan --path ./demo-app --framework legacy-dotnet --legacy-mode
```

## Experimental ports

### Go port

Build and test:

```bash
cd go
go test ./...
go build ./cmd/cspguard-go
```

Run:

```bash
cd go
go run ./cmd/cspguard-go scan \
  --path ../demo-app-core \
  --framework static \
  --cleanup hash \
  --report-format md \
  --report-output ../artifacts/go-report.md
```

### Python port

Run tests:

```bash
PYTHONPATH=python python3 -m unittest discover -s python/tests -v
```

Run:

```bash
PYTHONPATH=python python3 -m cspguardian_py scan \
  --path ./demo-app \
  --framework legacy-dotnet \
  --cleanup hash \
  --report-format json \
  --report-output ./artifacts/python-report.json
```

## Common examples

### Basic scan

```bash
dotnet run --project src/CSPGuardian/CSPGuardian.csproj -- \
  scan --path ./demo-app --framework legacy-dotnet
```

### Hash inline blocks and emit a JSON report

```bash
dotnet run --project src/CSPGuardian/CSPGuardian.csproj -- \
  scan --path ./demo-app-core --framework modern-dotnet \
  --cleanup hash \
  --output ./artifacts/policy.csp \
  --report-format json \
  --report-output ./artifacts/report.json
```

### Externalize inline scripts and styles

```bash
dotnet run --project src/CSPGuardian/CSPGuardian.csproj -- \
  scan --path ./demo-app --framework legacy-dotnet \
  --cleanup externalize
```

### Dry run without writing files

```bash
dotnet run --project src/CSPGuardian/CSPGuardian.csproj -- \
  scan --path ./demo-app-core --framework modern-dotnet \
  --cleanup externalize \
  --dry-run
```

### Exclude generated folders

```bash
dotnet run --project src/CSPGuardian/CSPGuardian.csproj -- \
  scan --path ./src --framework modern-dotnet \
  --exclude bin --exclude obj --exclude node_modules
```

## Command options

| Option | Short | Description |
|--------|-------|-------------|
| `--path` | `-p` | File or directory to scan |
| `--framework` | `-f` | `modern-dotnet`, `legacy-dotnet`, `static`, `js-modern`, or `js-legacy` |
| `--cleanup` | `-c` | `none`, `externalize`, `hash`, or `nonce` |
| `--output` | `-o` | Policy output path. Defaults to `policy.csp` |
| `--report-format` | `-r` | `md`, `json`, or `csv` |
| `--report-output` | | Report output path. Defaults to `report.<format>` |
| `--exclude` | | Directory names or paths to exclude from recursive scans |
| `--dry-run` | | Preview findings and fixes without writing any files |
| `--legacy-mode` | | Enable extra legacy .NET scanning |
| `--ci-mode` | | Exit with a nonzero code when violations are found |

## Default outputs

When not running in `--dry-run`, the CLI writes:

- a policy file to `policy.csp` unless `--output` is overridden
- a report file to `report.<format>` unless `--report-output` is overridden

## Supported findings

| Type | Description |
|------|-------------|
| `InlineScript` | Inline `<script>` block without `src` |
| `InlineStyle` | Inline `<style>` block |
| `StyleAttribute` | Inline `style="..."` attribute |
| `EventHandler` | Inline event handler such as `onclick` |
| `JavaScriptUrl` | `javascript:` URL in HTML or CSS |
| `DynamicInline` | `eval()` / `Function(...)` usage and lightweight JS-framework heuristics |
| `LegacyWebResource` | `WebResource.axd` reference |
| `ViewStateEmbedded` | ViewState/script combination detection |

## Demo content in this repo

The repository includes intentionally noisy sample applications for exercising the scanner:

- `demo-app/` - legacy MVC / Web Forms style examples
- `demo-app-core/` - ASP.NET Core examples

## Testing

```bash
dotnet test tests/CSPGuardian.Tests/CSPGuardian.Tests.csproj
cd go && go test ./...
PYTHONPATH=python python3 -m unittest discover -s python/tests -v
```

## CI

GitHub Actions CI is configured to restore, build, and test the .NET, Go, and Python ports on pushes and pull requests.

## Contributing

1. Fork the repo
2. Create a feature branch
3. Make your changes
4. Run the targeted test suite
5. Open a pull request

## License

MIT
