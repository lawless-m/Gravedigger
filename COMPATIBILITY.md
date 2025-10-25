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

VSS has been available since **Windows Server 2003**, so it should work on any Windows Server version that can run .NET 8.

### VSS Version History
- ✅ Windows Server 2022 - VSS 1.0+
- ✅ Windows Server 2019 - VSS 1.0+
- ✅ Windows Server 2016 - VSS 1.0+
- ✅ Windows Server 2012 R2 - VSS 1.0+
- ✅ Windows Server 2012 - VSS 1.0+
- ✅ Windows Server 2008 R2 - VSS 1.0+
- ✅ Windows Server 2008 - VSS 1.0+
- ✅ Windows Server 2003 R2 - VSS 1.0 (original)

**VSS is NOT a concern for compatibility** - if Windows is installed, VSS is available.

### Quick VSS Verification

Run the included verification script:
```cmd
verify-vss.bat
```

Or manually check:

**Check if VSS is available:**
```cmd
vssadmin list providers
```

**Check if VSS service is running:**
```cmd
sc query VSS
```

**Check for existing shadow copies:**
```cmd
vssadmin list shadows /for=C:
```

**Start VSS service if not running:**
```cmd
net start VSS
net start swprv
```

### Common VSS Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| "VSS service not found" | VSS not installed | VSS is built into Windows - may indicate system corruption |
| "No shadow copies found" | System Protection disabled | Enable System Protection on the volume |
| "Access denied" | Not running as admin | Run command prompt as Administrator |
| "Insufficient storage" | Not enough disk space | Free up space or adjust shadow copy storage |

### Enabling System Protection (Shadow Copies)

If shadow copies don't exist, you need to enable System Protection:

**GUI Method:**
1. Right-click "This PC" → Properties
2. Click "System Protection" (left sidebar)
3. Select your drive (C:) → Configure
4. Select "Turn on system protection"
5. Set max usage (recommended: 10-20% of drive)
6. Click OK

**Command Line Method:**
```cmd
REM Enable System Protection on C:
vssadmin resize shadowstorage /for=C: /on=C: /maxsize=20%

REM Create a manual shadow copy
wmic shadowcopy call create Volume=C:\
```

### VSS Storage Considerations

Shadow copies consume disk space:
- **Default**: 10% of volume or 10GB, whichever is less
- **Recommended**: 10-20% of volume for database servers
- **Minimum**: At least 300MB

Check current storage allocation:
```cmd
vssadmin list shadowstorage
```

Resize shadow copy storage:
```cmd
vssadmin resize shadowstorage /for=C: /on=C: /maxsize=20GB
```

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
