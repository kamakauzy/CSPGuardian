# 🛡️ CSPGuardian

> **Your codebase's security superhero, fighting XSS villains one inline script at a time!** 🦸‍♂️

Tired of pentesters giving you the side-eye because your CSP policy is basically `script-src 'unsafe-inline' 'unsafe-eval' *`? 😬  
Worried about that legacy .NET app that's held together with inline scripts and duct tape? 🔧  
Want to actually pass a security audit without rewriting your entire codebase? ✨

**CSPGuardian is here to save the day!** 🎉

A powerful CLI tool that helps .NET/C# developers transform their codebases from CSP-violation nightmares into security-compliant masterpieces. Automatically detect, fix, and generate proper Content Security Policies—because `unsafe-inline` is not a security feature, it's a cry for help. 😅

## 🎯 What CSPGuardian Does

Think of CSPGuardian as your personal security consultant that:
- 🔍 **Scans** your entire codebase (HTML, Razor, Web Forms, JS, CSS—you name it!)
- 🧹 **Cleans up** inline scripts and styles (externalize, hash, or nonce them)
- 📋 **Generates** proper CSP policies that actually work
- 🏛️ **Handles legacy .NET** without requiring a full rewrite
- 📊 **Reports** everything in beautiful JSON/CSV/Markdown formats

All while keeping your sanity intact! 🧠✨

## ✨ Features That'll Make You Smile

### 🔎 Codebase Scanning
Recursively hunts down those pesky inline scripts and styles like a bloodhound. Finds:
- Inline `<script>` tags (the usual suspects)
- Inline `<style>` tags (sneaky CSS violators)
- Event handlers (`onclick`, `onload`, etc.—the old-school way)
- Dynamic code execution (`eval()`, `Function()`—the dangerous stuff)
- Legacy .NET patterns (WebResource.axd, ViewState shenanigans)

### 🧹 Cleanup Options (Pick Your Poison)

**Externalize** 🚀  
*"Get that script out of my HTML!"*  
Extracts inline content to proper `.js`/`.css` files. Your HTML will thank you.

**Hashing** 🔐  
*"Fingerprint everything!"*  
Computes SHA-256/384/512 hashes for CSP whitelisting. Because trust, but verify.

**Noncing** 🎲  
*"One-time use only!"*  
Generates per-request nonces with ready-to-use code snippets. Fresh and secure every time.

### 📋 CSP Policy Generation
Builds proper CSP headers that won't make security auditors cry. Includes:
- `script-src` and `style-src` directives (with hashes/nonces)
- `strict-dynamic` support (for the modern apps)
- Report-only mode (for the cautious)
- Legacy .NET compatibility notes (for the brave)

### 🎨 Framework Support
Works with:
- ✅ Modern .NET Core/Blazor (the cool kids)
- ✅ Legacy .NET (MVC 4/Web Forms—we don't judge)
- ✅ Static sites (simple but effective)
- ✅ JS frameworks (React, Angular, Vue—the modern stack)

### 📊 Reporting & Auditing
Generate beautiful reports in:
- **Markdown** (for humans)
- **JSON** (for machines)
- **CSV** (for spreadsheets)

Perfect for pentest handoffs, compliance audits, or just showing your boss you care about security! 📈

## 🚀 Quick Start

### Prerequisites
- .NET 8.0 SDK or later (because we're modern like that)

### Installation

```bash
# Clone the repo
git clone https://github.com/kamakauzy/CSPGuardian.git
cd CSPGuardian

# Build it
dotnet build

# Publish it (optional, but recommended)
dotnet publish -c Release
```

## 💻 Usage Examples

### Basic Scan (The "What Have I Done?" Command)
```bash
cspguard scan --path ./MyApp --framework modern-dotnet
```
*See what violations you have. Knowledge is power!*

### Scan with Hash Cleanup (The "Fix It Now" Command)
```bash
cspguard scan --path ./MyApp --framework modern-dotnet --cleanup hash --output policy.csp
```
*Automatically hash everything and generate a CSP policy. Magic! ✨*

### Legacy .NET Mode (The "Help Me, I'm Stuck" Command)
```bash
cspguard scan --path ./LegacyApp --framework legacy-dotnet --legacy-mode --cleanup externalize
```
*For those old MVC 4/Web Forms apps that refuse to die. We've got your back!*

### CI/CD Mode (The "Break the Build" Command)
```bash
cspguard scan --path ./MyApp --ci-mode --report-format json
```
*Fail your CI pipeline if violations are found. Because security matters! 🔒*

### Dry Run (The "Just Looking" Command)
```bash
cspguard scan --path ./MyApp --cleanup externalize --dry-run
```
*See what would happen without actually doing it. Safety first!*

## 🎛️ Command Options

| Option | Short | Description |
|--------|-------|-------------|
| `--path` | `-p` | Path to scan (required) |
| `--framework` | `-f` | Framework: `modern-dotnet`, `legacy-dotnet`, `static`, `js-modern`, `js-legacy` |
| `--cleanup` | `-c` | Strategy: `externalize`, `hash`, `nonce`, or `none` |
| `--output` | `-o` | Output file for CSP policy (default: `policy.csp`) |
| `--dry-run` | | Preview changes without applying them |
| `--legacy-mode` | | Enable legacy .NET mode (MVC 4/Web Forms) |
| `--ci-mode` | | Exit with error code if violations found |
| `--report-format` | `-r` | Report format: `json`, `csv`, `md` |

## 📁 What You'll Get

After running CSPGuardian, you'll find:
- `policy.csp` - Your shiny new CSP policy (ready to deploy!)
- `report.md` (or `.json`/`.csv`) - A detailed report of all violations and fixes

## 📄 Supported File Types

CSPGuardian can scan:
- HTML/HTM files
- CSHTML (Razor views)
- ASPX (Web Forms—yes, those still exist)
- ASCX (User Controls)
- JavaScript files (.js)
- CSS files (.css)

## 🚨 Violation Types

CSPGuardian detects and reports:

| Type | Description | Severity |
|------|-------------|----------|
| **InlineScript** | Inline `<script>` tags without `src` | 🔴 High |
| **InlineStyle** | Inline `<style>` tags | 🟡 Medium |
| **EventHandler** | Event handler attributes (`onclick`, etc.) | 🔴 High |
| **DynamicInline** | Dynamic code (`eval()`, `Function()`) | 🔴 High |
| **LegacyWebResource** | WebResource.axd references | 🟢 Low |
| **ViewStateEmbedded** | ViewState with embedded script | 🟡 Medium |

## 🎓 Real-World Examples

### Example 1: Modern .NET App
```bash
cspguard scan --path ./src --framework modern-dotnet --cleanup hash --output csp-policy.txt
```
*Perfect for your shiny new .NET 8 app!*

### Example 2: That Old MVC 4 App Nobody Wants to Touch
```bash
cspguard scan --path ./LegacyMVC4 --framework legacy-dotnet --legacy-mode --cleanup externalize
```
*Because sometimes you have to work with what you've got.*

### Example 3: CI/CD Pipeline Integration
```bash
cspguard scan --path ./app --ci-mode --report-format json
```
*Automate security checks in your pipeline. Set it and forget it!*

## 🤝 Contributing

Found a bug? Have an idea? Want to make CSPGuardian even better?

**We'd love your help!** 🎉

1. Fork the repo
2. Create a feature branch
3. Make your changes
4. Submit a Pull Request

Together, we can make the web a safer place! 🌐✨

## 📜 License

MIT License - because security tools should be free and open! 🎁

## 🏷️ Version

**1.0.0 (MVP)** - The first step in your journey to CSP compliance! 🚀

---

**Made with ❤️ for developers who care about security (and their sanity)**

*Remember: `unsafe-inline` is not a feature, it's a temporary solution that became permanent. Let CSPGuardian help you fix that!* 😉
