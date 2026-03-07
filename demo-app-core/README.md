# CSPGuardian Demo Application (.NET Core)

This is a .NET Core version of the demo application that can run with just the .NET SDK.

## Quick Start

### Prerequisites
- .NET 8.0 SDK or later

### Run the Application

```bash
cd demo-app-core
dotnet run
```

The application will start at `http://localhost:5000`

Open your browser and navigate to:
- http://localhost:5000 - Home page
- http://localhost:5000/Home/About - About page
- http://localhost:5000/Home/Contact - Contact page

## Testing with CSPGuardian

Once the app is running, you can test CSPGuardian on it:

```bash
# From the root directory
cspguard scan --path ./demo-app-core --framework modern-dotnet

# With hash cleanup
cspguard scan --path ./demo-app-core --framework modern-dotnet --cleanup hash --output demo-policy.csp

# Generate report
cspguard scan --path ./demo-app-core --framework modern-dotnet --report-format json
```

## Note

This is a simplified .NET Core version. For the full demo with all violations, see the `demo-app` folder (requires Visual Studio or IIS Express).

