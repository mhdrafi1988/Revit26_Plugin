# AutoSlopeByPoint

## Overview
AutoSlopeByPoint is a Revit plugin feature that automatically applies slope to roof elements based on selected points. This tool helps automate the process of setting slope directions and values for roof drainage design.

## Folder Structure

This folder contains multiple versions of the AutoSlopeByPoint functionality:

### Active Versions

1. **AutoSlopeByPoint_Classic** - Stable classic implementation
   - Full MVVM architecture
   - Organized folder structure with Commands, Engine, Models, ViewModels, Views
   - Includes Dijkstra pathfinding algorithm for optimal slope calculation
   - External event handling for safe Revit API operations

2. **AutoSlope_ByPoint_03_007** - Version 03.007
   - Enhanced version with improved UI and functionality
   - Similar structure to Classic version
   - Additional helper utilities

3. **AutoSlopeCommand_Point_003_0** - Version 003.0
   - Simplified single-folder implementation
   - All files in one directory for easy integration

### Archived Versions
- `AutoSlope_ByPoint_0304.rar` - Archived version 03.04
- `AutoSlope_ByPoint_0304_Succes.rar` - Successful working version 03.04
- `AutoSlope_ByPoint_03_07.rar` - Archived version 03.07
- `AutoSlope_ByPoint_04_00_Working.rar` - Working version 04.00

## Features

- Automatic slope calculation based on selected points
- Visual feedback with color-coded logging
- Real-time preview of slope directions
- Support for complex roof geometries
- Integration with Revit's slope arrow parameters
- User-friendly WPF interface

## How to Use

### Downloading This Folder

You can download just the AutoSlopeByPoint folder in several ways:

#### Option 1: Download via GitHub (Recommended)
1. Navigate to the GitHub repository
2. Go to: `Menu/RoofTools/RoofSlope/AutoSlopeByPoint/`
3. Click the "Download" button or use GitHub's download functionality for this specific folder

#### Option 2: Clone and Extract
```bash
# Clone the entire repository
git clone https://github.com/mhdrafi1988/Revit26_Plugin.git

# Navigate to the AutoSlopeByPoint folder
cd Revit26_Plugin/Menu/RoofTools/RoofSlope/AutoSlopeByPoint/
```

#### Option 3: Use Pre-packaged Archive
Look for `AutoSlopeByPoint.zip` or `AutoSlopeByPoint.rar` in the root of the repository for a ready-to-download package.

### Integration into Your Project

1. **Choose a Version**: Select one of the active versions based on your needs
   - Use `AutoSlopeByPoint_Classic` for the most stable and organized version
   - Use `AutoSlope_ByPoint_03_007` for the latest features
   - Use `AutoSlopeCommand_Point_003_0` for quick integration

2. **Copy Files**: Copy the chosen version folder into your Revit plugin project

3. **Update References**: Ensure your project references include:
   - Autodesk Revit API assemblies (RevitAPI.dll, RevitAPIUI.dll)
   - .NET Framework 4.8 or compatible version
   - WPF components (PresentationCore, PresentationFramework, WindowsBase)

4. **Register Command**: Add the AutoSlopeCommand to your ribbon or menu

5. **Build and Test**: Compile your project and test the functionality in Revit

## Requirements

- Autodesk Revit 2026 (or compatible version)
- .NET Framework 4.8
- RevitAPI.dll and RevitAPIUI.dll references
- Windows Presentation Foundation (WPF)

## Components

### Core Components
- **AutoSlopeCommand**: Main command class that initializes the tool
- **AutoSlopeEngine**: Core algorithm for slope calculation
- **AutoSlopeGeometry**: Geometric calculations and transformations
- **DijkstraPathEngine**: Pathfinding algorithm for optimal slope paths
- **AutoSlopeParameterWriter**: Writes slope parameters to Revit elements

### UI Components
- **AutoSlopeWindow**: Main WPF window interface
- **AutoSlopeViewModel**: View model handling business logic and UI state
- **LogColorHelper**: Helper for color-coded logging
- **InverseBoolConverter**: WPF converter for boolean inversion

### Event Handling
- **AutoSlopeEventManager**: Manages external events
- **AutoSlopeHandler**: Handles Revit API operations safely in external events

## Usage in Revit

1. Open a Revit project with roof elements
2. Launch the AutoSlopeByPoint command
3. Select reference points or drain locations
4. Configure slope parameters in the dialog
5. Apply slopes to generate automatic slope arrows

## Support

For issues, questions, or contributions, please refer to the main repository documentation or create an issue on GitHub.

## Version History

- **v04.00**: Working version with enhanced features (archived)
- **v03.07**: Stable release (archived)
- **v03.04**: Success version with proven functionality (archived)
- **v03.007**: Current active version with improvements
- **v003.0**: Simplified implementation for easy integration
- **Classic**: Stable long-term version

## License

This code is part of the Revit26_Plugin repository. Please refer to the repository's license file for usage terms.
