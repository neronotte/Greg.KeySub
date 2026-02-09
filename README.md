# Greg.KeySub

A lightweight Windows utility that globally intercepts the `§` (section sign) key and replaces it with the backtick `` ` `` character.

## Why?

On many European keyboards (including Italian), the backtick character is difficult to access, while the `§` key is readily available. This tool is especially useful for developers who frequently use backticks for:

- Markdown code blocks
- JavaScript template literals
- Shell commands
- And more...

## Features

- **System tray application** - Runs silently in the background
- **Global key interception** - Works in any application
- **Toggle on/off** - Double-click the tray icon or use the context menu
- **Start with Windows** - Optional auto-start on login
- **Keyboard layout detection** - Automatically detects which key produces `§` on your keyboard

## Installation

### Option 1: Download Release

Download the latest version from the [Releases](../../releases) page. Two versions are available:

| File | Size | Requirements |
|------|------|--------------|
| `Greg.KeySub-SelfContained.exe` | ~49 MB | None - includes .NET runtime, runs on any Windows 10/11 |
| `Greg.KeySub-FrameworkDependent.exe` | ~676 KB | Requires [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) installed |

**Recommended:** Use the **Self-Contained** version for maximum compatibility. Use the **Framework-Dependent** version if you already have .NET 10 installed and want a smaller download.

### Option 2: Build from Source

```powershell
# Clone the repository
git clone https://github.com/yourusername/Greg.KeySub.git
cd Greg.KeySub

# Build self-contained (larger, no dependencies)
dotnet publish src/Greg.KeySub -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish

# Or build framework-dependent (smaller, requires .NET 10)
dotnet publish src/Greg.KeySub -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -o ./publish
```

## Usage

1. Run `Greg.KeySub.exe`
2. The application appears in the system tray
3. Press `§` (or `Shift+ù` on Italian keyboards) to type `` ` `` instead

### Tray Menu Options

- **Enabled** - Toggle key substitution on/off
- **Start with Windows** - Enable/disable auto-start on login
- **Diagnostics** - View detected keyboard layout information
- **About** - Application information
- **Exit** - Close the application

## Requirements

- Windows 10/11
- .NET 10.0 Runtime (included in self-contained builds)

## Building

```powershell
# Restore and build
dotnet build

# Run tests
dotnet test

# Publish single-file executable
dotnet publish src/Greg.KeySub -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Project Structure

```
Greg.KeySub/
├── src/
│   └── Greg.KeySub/           # Main application
│       ├── Program.cs
│       ├── GlobalKeyboardHook.cs
│       └── TrayApplicationContext.cs
├── tests/
│   └── Greg.KeySub.Tests/     # Unit tests (NUnit)
└── Greg.KeySub.sln
```

## How It Works

The application uses a low-level Windows keyboard hook (`WH_KEYBOARD_LL`) to intercept keystrokes globally. When the `§` character is detected:

1. The original keystroke is blocked
2. A backtick character is injected using `SendInput`

The hook runs efficiently by pre-detecting which virtual key codes produce `§` on startup, avoiding expensive character translation on every keystroke.

## License

MIT License - See [LICENSE](LICENSE) for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
