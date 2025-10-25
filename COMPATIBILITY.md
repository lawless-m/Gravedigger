# Gravedigger Compatibility Guide

## Supported Windows Versions

Gravedigger (when built as a self-contained executable) is compatible with:

### Windows Server
- ✅ Windows Server 2022
- ✅ Windows Server 2019
- ✅ Windows Server 2016
- ✅ Windows Server 2012 R2
- ✅ Windows Server 2012
- ❌ Windows Server 2008 R2 (not officially supported by .NET 8)

### Windows Client
- ✅ Windows 11
- ✅ Windows 10 (version 1607 or later)
- ❌ Windows 8.1 and older (not supported)

## How to Check Your Windows Version

### Method 1: Using Command Prompt
```cmd
systeminfo | findstr /B /C:"OS Name" /C:"OS Version"
```

**Example Output:**
```
OS Name:                   Microsoft Windows Server 2012 R2 Datacenter
OS Version:                6.3.9600 N/A Build 9600
```

### Method 2: Using PowerShell
```powershell
Get-ComputerInfo | Select-Object WindowsProductName, WindowsVersion, OsHardwareAbstractionLayer
```

### Method 3: Using winver
```cmd
winver
```

This opens a dialog showing your Windows version.

## Version Mapping

| OS Name | Version Number | Supported |
|---------|----------------|-----------|
| Windows Server 2022 | 10.0.20348 | ✅ Yes |
| Windows Server 2019 | 10.0.17763 | ✅ Yes |
| Windows Server 2016 | 10.0.14393 | ✅ Yes |
| Windows Server 2012 R2 | 6.3.9600 | ✅ Yes |
| Windows Server 2012 | 6.2.9200 | ✅ Yes |
| Windows Server 2008 R2 | 6.1.7601 | ❌ No* |
| Windows Server 2008 | 6.0.6003 | ❌ No |

*While VSS exists on Server 2008 R2, .NET 8.0 does not officially support it.

## What If My Server Is Too Old?

### Option 1: Upgrade Windows Server (Recommended)
- Windows Server 2012 and 2012 R2 have reached end-of-life
- Consider upgrading to Server 2019 or 2022 for security updates

### Option 2: Use an Older .NET Version
If you absolutely cannot upgrade Windows, you could:
1. Modify the project to target .NET Framework 4.8 (supports Server 2008 R2+)
2. Change `<TargetFramework>net8.0-windows</TargetFramework>` to `<TargetFramework>net48</TargetFramework>` in Gravedigger.csproj
3. Rebuild the project

**Note**: .NET Framework 4.8 is older and won't get the performance benefits of .NET 8.

### Option 3: Use a Build Server
- Build on a modern machine with .NET 8 SDK
- Deploy the self-contained executable to your older server
- As long as your server is Windows Server 2012+, the self-contained .exe should work

## Volume Shadow Copy Service (VSS) Compatibility

VSS has been available since Windows Server 2003, so if your server is running Windows Server 2012 or newer, VSS will be available.

**Check if VSS is available:**
```cmd
vssadmin list providers
```

If this command works and shows providers, VSS is available.

## .NET Runtime Requirements

### Self-Contained Deployment (Recommended)
- **No .NET runtime required** on target machine
- Executable includes all dependencies
- Larger file size (~60-80 MB)
- Works on Windows Server 2012+

### Framework-Dependent Deployment
- **Requires .NET 8.0 Runtime** on target machine
- Smaller file size (~1-2 MB)
- Must install .NET 8.0 Runtime from: https://dotnet.microsoft.com/download/dotnet/8.0

## Recommended Configuration

For best compatibility and ease of deployment:

1. **Build as self-contained**: Use `build.bat` or `dotnet publish -c Release -r win-x64 --self-contained`
2. **Target platform**: Windows Server 2012 R2 or newer
3. **Ensure VSS is enabled**: System Protection enabled on volumes

## Testing on Your Server

To verify Gravedigger will work on your server:

1. **Check Windows version** (must be Server 2012+)
2. **Check VSS availability**:
   ```cmd
   vssadmin list shadows
   sc query VSS
   ```
3. **Test the executable**:
   ```cmd
   Gravedigger.exe --version
   ```
4. **Create a test config and run**:
   ```cmd
   Gravedigger.exe --create-config test.config
   # Edit test.config
   Gravedigger.exe test.config
   ```

## Still Unsure?

If you're uncertain about compatibility:

1. Note your **exact Windows version** from `systeminfo`
2. Check the [.NET 8 OS Support Matrix](https://github.com/dotnet/core/blob/main/release-notes/8.0/supported-os.md)
3. If still unsure, try running the self-contained executable - it will either work or show a clear error

## Support Contact

For compatibility questions or issues:
- Open an issue: https://github.com/lawless-m/Gravedigger/issues
- Include your Windows version from `systeminfo` output
