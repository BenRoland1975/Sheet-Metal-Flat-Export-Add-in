# Roeslein SolidWorks Add-in

A SolidWorks add-in to extend functionality for Roeslein users. The add-in provides tools for automating common SolidWorks tasks.

## Features

- **Process Assembly for Sheet Metal Parts**: Automatically creates DXF flat patterns from sheet metal parts in an assembly.
- **Process Single Part**: Export DXF flat pattern from the currently open sheet metal part.
- **Settings Menu**: Configure output folder (Hot Folder) for DXF files and manage logging options.

## Requirements

- SolidWorks 2018 or later (tested with SW 2020)
- .NET Framework 4.7.2 or higher
- Visual Studio 2017 or later (for development)

## Building the Add-in

1. Open the solution in Visual Studio
2. Add SolidWorks Interop references:
   - Create a folder named `SolidWorksReferences` in the project directory
   - Copy the following files from your SolidWorks installation folder (typically `C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist`) to the `SolidWorksReferences` folder:
     - `SolidWorks.Interop.sldworks.dll`
     - `SolidWorks.Interop.swconst.dll`
     - `SolidWorks.Interop.swpublished.dll`
3. Add icon files to the Resources folder:
   - `dxf_export.bmp` - Icon for the DXF export button (assembly processing)
   - `dxf_single.bmp` - Icon for the single part processing button
   - `settings.bmp` - Icon for the settings button
4. Build the solution (this will register the add-in for COM)

## Configuration

The add-in includes a settings dialog that allows you to configure:

- **DXF Output Folder (Hot Folder)**: The directory where DXF files will be exported, watched by Laser Programming software
- **Log File Path**: The path where operation logs will be saved
- **Enable Logging**: Toggle to enable/disable logging of operations

These settings are stored in the registry at:
```
HKEY_CURRENT_USER\Software\RoesleinAddIn\Settings
```

## Installation

### Automatic Registration (During Build)
When built with Visual Studio, the add-in should automatically register with COM due to the `RegisterForComInterop` setting in the project.

### Manual Registration
If needed, you can register the add-in manually:
1. Run Command Prompt as Administrator
2. Navigate to the .NET Framework directory:
   ```
   cd C:\Windows\Microsoft.NET\Framework\v4.0.30319
   ```
3. Register the DLL:
   ```
   regasm.exe /codebase "C:\Path\To\RoesleinAddIn.dll"
   ```

## Usage

1. Open SolidWorks
2. The add-in should automatically load, adding a "Roeslein Tools" toolbar
3. Click the "Settings" button to configure your Hot Folder and logging preferences
4. To process an assembly:
   - Open an assembly containing sheet metal parts
   - Click the "Process Assembly for Sheet Metal Parts" button in the toolbar
5. To process a single part:
   - Open a sheet metal part file
   - Click the "Process Single Part for Sheet Metal DXF" button in the toolbar
6. DXF files will be created in the configured Hot Folder

## Troubleshooting

- If the add-in doesn't appear in SolidWorks, check the following:
  - Verify the DLL is correctly registered
  - Check Tools > Add-ins in SolidWorks to ensure the add-in is enabled
  - Look for error messages in the Windows Event Viewer
- The add-in creates detailed logs at the configured log file location (if logging is enabled)
- If a part cannot be processed, verify that it is a proper sheet metal part with a flat pattern feature

## Development Notes

This add-in was developed based on a VBA macro originally created for the same purpose. The functionality has been enhanced and integrated into a proper SolidWorks add-in framework with configuration options. 