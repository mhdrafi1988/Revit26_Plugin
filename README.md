# Revit26_Plugin

A comprehensive Revit 2026 plugin with various tools for roof design and analysis.

## Features

This plugin includes several specialized tools for Revit 2026, including:

- **AutoSlopeByPoint**: Automatic slope calculation for roof elements based on selected points
- **RoofTools**: Various tools for roof design, drainage, and analysis
- **SectionManager**: Tools for managing sections in Revit projects
- And more...

## Download Individual Components

### Downloading AutoSlopeByPoint Folder Only

If you only need the AutoSlopeByPoint functionality, you can download it separately:

#### Method 1: Download Pre-packaged Archive (Recommended)
Download the ready-to-use archive from the repository root:
- **[AutoSlopeByPoint.zip](./AutoSlopeByPoint.zip)** - 52KB compressed archive containing all AutoSlopeByPoint components

#### Method 2: Manual Download via GitHub
1. Navigate to `Menu/RoofTools/RoofSlope/AutoSlopeByPoint/` in the repository
2. Download the folder using GitHub's interface or git sparse-checkout

#### Method 3: Clone Repository and Extract
```bash
# Clone the repository
git clone https://github.com/mhdrafi1988/Revit26_Plugin.git

# Navigate to AutoSlopeByPoint folder
cd Revit26_Plugin/Menu/RoofTools/RoofSlope/AutoSlopeByPoint/

# Copy to your project location
cp -r . /path/to/your/project/AutoSlopeByPoint/
```

### AutoSlopeByPoint Documentation

For detailed documentation on AutoSlopeByPoint, including:
- Feature overview
- Version information
- Integration instructions
- Usage guide

Please see: [AutoSlopeByPoint/README.md](./Menu/RoofTools/RoofSlope/AutoSlopeByPoint/README.md)

## Requirements

- Autodesk Revit 2026
- .NET Framework 4.8
- Windows Presentation Foundation (WPF)

## Installation

### Full Plugin Installation

1. Clone or download this repository
2. Build the solution in Visual Studio
3. Copy the built DLL to your Revit 2026 plugins folder
4. Restart Revit

### Installing Just AutoSlopeByPoint

1. Download `AutoSlopeByPoint.zip` from the repository root
2. Extract to a temporary location
3. Copy the desired version folder into your Revit plugin project
4. Add the files to your Visual Studio project
5. Build and test

## Project Structure

```
Revit26_Plugin/
├── AutoSlopeByPoint.zip          # Pre-packaged AutoSlopeByPoint download
├── Menu/
│   └── RoofTools/
│       ├── RoofSlope/
│       │   └── AutoSlopeByPoint/  # AutoSlopeByPoint feature folder
│       ├── Points/                # Roof point tools
│       └── SlopeDirections_V04/   # Slope direction analysis
├── Revit26_Plugin/
│   └── SectionManager_V07/        # Section management tools
├── ViewModels/                    # Shared view models
└── Utilities/                     # Shared utilities
```

## Usage

### Using AutoSlopeByPoint

1. Open Revit with a project containing roof elements
2. Access the AutoSlopeByPoint command from the Revit ribbon
3. Select drain points or reference points on your roof
4. Configure slope parameters in the dialog
5. Apply to generate automatic slope arrows

## Development

### Building from Source

```bash
# Clone the repository
git clone https://github.com/mhdrafi1988/Revit26_Plugin.git

# Open in Visual Studio
cd Revit26_Plugin
start Revit26_Plugin.sln

# Build solution (Ctrl+Shift+B in Visual Studio)
```

### Adding AutoSlopeByPoint to Your Project

See the [AutoSlopeByPoint README](./Menu/RoofTools/RoofSlope/AutoSlopeByPoint/README.md) for detailed integration instructions.

## Contributing

Contributions are welcome! Please feel free to submit issues, fork the repository, and create pull requests.

## License

Please refer to the LICENSE file in the repository for license information.

## Support

For issues, questions, or feature requests:
- Create an issue on GitHub
- Check existing documentation in component-specific README files

## Version Information

- **Revit Version**: 2026
- **Target Framework**: .NET Framework 4.8
- **Last Updated**: 2026-02-03
