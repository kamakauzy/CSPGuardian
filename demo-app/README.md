# CSPGuardian Demo Application

A comprehensive ASP.NET MVC demo application created specifically to test CSPGuardian. This application contains various real-world CSP violations commonly found in .NET applications.

## Purpose

This demo app simulates a typical enterprise .NET application with:
- Multiple controllers and views
- Partial views and view components
- Inline scripts in Razor views
- Inline styles throughout the application
- Event handlers (onclick, onload, onsubmit, etc.)
- Dynamic code execution (eval, Function constructor)
- AJAX calls with inline handlers
- Form validation scripts
- Legacy .NET Web Forms patterns
- Modern .NET Core patterns
- Third-party library integration patterns

## Application Structure

```
demo-app/
├── Controllers/
│   ├── HomeController.cs          - Main controller
│   ├── ProductsController.cs      - Product management
│   ├── OrdersController.cs         - Order processing
│   └── AdminController.cs          - Admin panel
├── Views/
│   ├── Home/
│   │   ├── Index.cshtml           - Dashboard with multiple violations
│   │   ├── About.cshtml           - Event handlers
│   │   └── Contact.cshtml         - Dynamic code (eval)
│   ├── Products/
│   │   ├── Index.cshtml           - Product listing with inline scripts
│   │   ├── Create.cshtml          - Form with validation
│   │   └── Edit.cshtml            - Edit form with AJAX
│   ├── Orders/
│   │   ├── Index.cshtml           - Order list with DataTable
│   │   └── Details.cshtml         - Order details with inline styles
│   ├── Admin/
│   │   └── Dashboard.cshtml       - Admin dashboard
│   ├── Shared/
│   │   ├── _Layout.cshtml         - Main layout
│   │   ├── _Navigation.cshtml     - Navigation partial
│   │   ├── _Footer.cshtml          - Footer partial
│   │   └── _ValidationScripts.cshtml - Validation scripts
│   └── Components/
│       └── UserProfile/
│           └── Default.cshtml     - View component
├── Scripts/
│   ├── custom.js                  - Custom scripts with violations
│   ├── validation.js             - Validation scripts
│   ├── ajax-helpers.js            - AJAX helper functions
│   └── legacy.js                  - Legacy patterns
├── Content/
│   └── site.css                   - External styles (good practice)
├── App_Code/
│   └── Helpers.cshtml             - Helper methods
└── Web.config                     - Configuration (legacy)
```

## Running CSPGuardian

### Basic Scan
```bash
cspguard scan --path ./demo-app --framework modern-dotnet
```

### Scan with Hash Cleanup
```bash
cspguard scan --path ./demo-app --framework modern-dotnet --cleanup hash --output demo-policy.csp
```

### Scan with Externalize Cleanup
```bash
cspguard scan --path ./demo-app --framework modern-dotnet --cleanup externalize
```

### Legacy Mode Scan
```bash
cspguard scan --path ./demo-app --framework legacy-dotnet --legacy-mode
```

### Generate JSON Report
```bash
cspguard scan --path ./demo-app --framework modern-dotnet --report-format json
```

## Expected Violations

This demo app should trigger a comprehensive set of violations:

### InlineScript Violations
- Multiple inline `<script>` tags in views
- Scripts in partial views
- Scripts in view components
- Scripts in layout files
- Scripts in helper files

### InlineStyle Violations
- Inline `<style>` tags in views
- Styles in partial views
- Styles in layout files
- Inline style attributes (style="...")

### EventHandler Violations
- onclick, onload, onsubmit, onchange
- onmouseover, onmouseout, onfocus, onblur
- onerror, onresize, onscroll
- Event handlers in forms and buttons

### DynamicInline Violations
- eval() usage for JSON parsing
- Function() constructor
- setTimeout/setInterval with strings
- Dynamic script injection

### Legacy Patterns
- WebResource.axd references
- ViewState embedded scripts
- Web Forms patterns
- Master page inline scripts

## Real-World Scenarios Included

1. **Dashboard with Charts** - Inline chart initialization scripts
2. **Data Tables** - jQuery DataTable with inline configuration
3. **Form Validation** - Inline validation scripts
4. **AJAX Calls** - Inline success/error handlers
5. **Third-party Integration** - Inline initialization for libraries
6. **Dynamic Content Loading** - eval() for parsing responses
7. **Legacy Migration** - Web Forms patterns mixed with MVC
8. **Admin Panels** - Complex inline scripts for admin functionality

## Testing Checklist

- [ ] Scan detects all inline scripts
- [ ] Scan detects all inline styles
- [ ] Scan detects all event handlers
- [ ] Scan detects eval() usage
- [ ] Scan detects Function() usage
- [ ] Hash cleanup generates proper CSP hashes
- [ ] Externalize cleanup extracts scripts/styles
- [ ] Nonce cleanup generates nonce suggestions
- [ ] Policy generation creates valid CSP
- [ ] Reports are generated correctly (JSON/CSV/MD)
- [ ] Legacy mode detects Web Forms patterns

## Note

This is a demo/test application only. It is intentionally designed with CSP violations to test CSPGuardian's detection and cleanup capabilities. It is not meant to be run as a production web application.
