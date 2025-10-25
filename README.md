# Gravedigger

**DBISAM Shadow Copy Replication Tool**

Gravedigger is a Windows-based tool that replicates DBISAM databases using Volume Shadow Copy Service (VSS). It extracts database files from shadow copies (restore points) to create consistent backups without interrupting the running database.

## Features

- **Non-intrusive replication**: Uses Windows VSS to copy database files without locking or stopping the database
- **Automatic retry logic**: Configurable retry attempts with delays
- **File validation**: Validates copied files to ensure integrity
- **Generation management**: Keeps multiple generations of replicas and automatically cleans up old ones
- **Comprehensive logging**: Detailed logs with configurable log levels
- **Flexible configuration**: INI-style configuration file for easy customization
- **Error handling**: Robust error handling with detailed error messages

## Prerequisites

### System Requirements
- **Windows Server**: 2012, 2012 R2, 2016, 2019, 2022, or newer
- **Windows Client**: Windows 10 (version 1607+) or Windows 11
- Volume Shadow Copy Service enabled
- .NET 8.0 SDK (for building from source only)
- Administrative privileges (required for VSS access)
- Sufficient disk space for shadow copies and replicas

**Note**: The published executable is self-contained and does not require .NET to be installed on target machines. It will run on Windows Server 2012 and newer.

### Required Services
The following Windows services must be running:
1. **Volume Shadow Copy** (VSS)
2. **MS Software Shadow Copy Provider** (swprv)

**Quick verification**: Run the included verification script:
```cmd
verify-vss.bat
```

Or manually check service status:
```cmd
sc query VSS
sc query swprv
```

Start services if needed:
```cmd
net start VSS
net start swprv
```

**Note**: VSS has been available since Windows Server 2003, so it should work on any supported Windows version. See [COMPATIBILITY.md](COMPATIBILITY.md) for details.

## Installation

### Building from Source

**Requirements**: .NET 8.0 SDK or later ([Download here](https://dotnet.microsoft.com/download/dotnet/8.0))

1. Clone the repository:
```bash
git clone https://github.com/lawless-m/Gravedigger.git
cd Gravedigger
```

2. Build and publish the project:

**Option A: Using the build script (Windows)**
```cmd
build.bat
```

**Option B: Using dotnet CLI**
```bash
# Build only
dotnet build -c Release

# Publish as single-file executable (recommended)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

3. The executable will be in `bin\Release\net8.0-windows\win-x64\publish\Gravedigger.exe`

**Benefits of .NET 8**:
- Modern C# language features
- Better performance
- Long-term support (LTS)
- Self-contained deployment (no runtime installation needed on target machines)

### Pre-built Binaries
Download the latest release from the [Releases](https://github.com/lawless-m/Gravedigger/releases) page.

## Quick Start

1. **Create a configuration file**:
```cmd
Gravedigger.exe --create-config
```

2. **Edit the configuration file** (`gravedigger.config`) with your settings:
   - Set `Source.Volume` (e.g., `C:`)
   - Set `Source.DatabasePath` (path to your DBISAM database)
   - Set `Destination.Path` (where replicas will be stored)
   - Set `Logging.LogPath` (where logs will be written)

3. **Run the replication**:
```cmd
Gravedigger.exe
```

## Configuration

### Configuration File Format

Gravedigger uses an INI-style configuration file. Here's a complete example:

```ini
[Source]
Volume=C:
DatabasePath=C:\Database\Production
Extensions=*.dat,*.idx,*.blb,*.bak

[Destination]
Path=D:\Replicas\Production
RetainGenerations=3

[Schedule]
Frequency=Hourly
RetryOnFailure=True
RetryAttempts=3
RetryDelayMinutes=5

[Logging]
LogPath=C:\Gravedigger\Logs
LogLevel=Information
RetentionDays=30

[Monitoring]
AlertOnFailure=True
AlertEmail=dbadmin@company.com
MaxReplicaAge=2
```

### Configuration Options

#### [Source]
- **Volume**: Source volume (e.g., `C:`, `D:`)
- **DatabasePath**: Full path to the DBISAM database directory
- **Extensions**: Comma-separated list of file extensions to replicate (default: `*.dat,*.idx,*.blb,*.bak`)

#### [Destination]
- **Path**: Destination directory for replicas (timestamped subdirectories will be created)
- **RetainGenerations**: Number of replica generations to keep (default: 3)

#### [Schedule]
- **Frequency**: Description of replication frequency (for documentation)
- **RetryOnFailure**: Enable retry on failure (default: `True`)
- **RetryAttempts**: Number of retry attempts (default: 3)
- **RetryDelayMinutes**: Delay between retries in minutes (default: 5)

#### [Logging]
- **LogPath**: Directory for log files
- **LogLevel**: Log level (`Debug`, `Information`, `Warning`, `Error`, `Critical`)
- **RetentionDays**: Number of days to retain log files (default: 30)

#### [Monitoring]
- **AlertOnFailure**: Enable failure alerts (default: `True`)
- **AlertEmail**: Email address for alerts (requires additional SMTP configuration)
- **MaxReplicaAge**: Maximum acceptable age of shadow copy in hours (default: 2)

## Usage

### Basic Usage
```cmd
# Run with default config file (gravedigger.config)
Gravedigger.exe

# Run with custom config file
Gravedigger.exe C:\config\production.config
```

### Command Line Options
```cmd
# Show help
Gravedigger.exe --help

# Show version
Gravedigger.exe --version

# Create default config file
Gravedigger.exe --create-config

# Create config file at specific path
Gravedigger.exe --create-config C:\config\my.config
```

### Scheduled Execution

To run Gravedigger on a schedule, use Windows Task Scheduler:

1. Open Task Scheduler
2. Create a new task with:
   - **Trigger**: Define your schedule (e.g., hourly, daily)
   - **Action**: Run `Gravedigger.exe` with your config file path
   - **Settings**:
     - Run whether user is logged on or not
     - Run with highest privileges
     - Use a service account with admin rights

Example task action:
```
Program: C:\Gravedigger\Gravedigger.exe
Arguments: C:\Gravedigger\production.config
```

## How It Works

1. **VSS Service Check**: Verifies that the Volume Shadow Copy Service is running
2. **Shadow Copy Discovery**: Finds the latest shadow copy for the specified volume
3. **Age Validation**: Checks if the shadow copy is recent enough (based on `MaxReplicaAge`)
4. **Path Translation**: Translates the database path to the shadow copy path
5. **File Replication**: Copies all matching files from the shadow copy to a timestamped destination directory
6. **Validation**: Validates that all files were copied correctly (file count and size checks)
7. **Cleanup**: Removes old replica generations (keeping only `RetainGenerations` most recent)

## Logs

Gravedigger creates detailed log files in the configured log directory:

- **Log file naming**: `replication_YYYYMMDD_HHmmss.log`
- **Console output**: Real-time feedback with color-coded messages
- **Log levels**: Debug, Information, Warning, Error, Critical
- **Automatic cleanup**: Old logs are deleted based on `RetentionDays`

Example log output:
```
[2025-10-25 14:30:15.123] [INFORMATION] === Gravedigger Replication Session Started ===
[2025-10-25 14:30:15.124] [INFORMATION] Configuration loaded from: gravedigger.config
[2025-10-25 14:30:15.234] [INFORMATION] Starting Replication Process
[2025-10-25 14:30:15.345] [INFORMATION] Found shadow copy: \\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy12
[2025-10-25 14:30:15.456] [INFORMATION] Copying files with extension: *.dat
[2025-10-25 14:30:16.567] [INFORMATION] Copied: customers.dat (2.5 MB)
[2025-10-25 14:30:17.678] [INFORMATION] Validation complete: 15 files validated, 125.3 MB total
[2025-10-25 14:30:17.789] [INFORMATION] === Replication Completed Successfully ===
```

## Exit Codes

- **0**: Success
- **1**: Failure

You can use exit codes in scripts or task scheduler to detect failures:
```cmd
Gravedigger.exe
if %ERRORLEVEL% NEQ 0 (
    echo Replication failed!
    exit /b 1
)
```

## Troubleshooting

### No Shadow Copies Found

**Problem**: "No shadow copy found for volume C:"

**Solutions**:
- Verify that System Protection is enabled for the volume
- Check if shadow copies exist: `vssadmin list shadows /for=C:`
- Manually create a shadow copy: `wmic shadowcopy call create Volume=C:\`

### VSS Service Not Running

**Problem**: "VSS service is not running"

**Solution**:
```cmd
net start VSS
net start swprv
```

### Access Denied

**Problem**: Permission errors when accessing shadow copies

**Solution**:
- Ensure you're running Gravedigger as Administrator
- Verify the user account has admin privileges
- Check NTFS permissions on source and destination paths

### Old Shadow Copies

**Problem**: Warning about shadow copy age

**Solution**:
- Create more frequent shadow copies (System Protection settings)
- Adjust `MaxReplicaAge` in configuration
- Set up scheduled shadow copy creation

### Files Not Copied

**Problem**: Zero files copied or missing files

**Solutions**:
- Verify `DatabasePath` is correct
- Check `Extensions` configuration matches your DBISAM files
- Confirm files exist in the shadow copy path
- Review logs for specific error messages

## DBISAM Considerations

### File Consistency
Shadow copies capture point-in-time snapshots, but DBISAM may have:
- Uncommitted transactions
- Data in memory buffers not yet written to disk
- Mid-transaction file states

### Best Practices
1. **Schedule during low activity**: Run replication during off-peak hours
2. **Keep multiple generations**: Don't rely on a single replica
3. **Validate replicas**: Test that replicated databases can be opened
4. **Monitor regularly**: Check logs and replica validity
5. **Test restores**: Periodically test restoring from replicas

### Limitations
- **Not real-time**: Replication is only as current as the latest shadow copy
- **No transaction consistency guarantee**: Shadow copies are crash-consistent, not transaction-consistent
- **Depends on VSS**: Requires Windows shadow copies to be available

## Architecture

```
Gravedigger
├── src/
│   ├── Program.cs                    # Main entry point
│   ├── Config/
│   │   └── ReplicationConfig.cs      # Configuration management
│   ├── Logging/
│   │   └── ReplicationLogger.cs      # Logging system
│   ├── VSS/
│   │   └── ShadowCopyManager.cs      # VSS operations
│   ├── Validation/
│   │   └── FileValidator.cs          # File integrity validation
│   └── Engine/
│       └── ReplicationEngine.cs      # Main replication logic
├── Gravedigger.csproj                # Project file
├── README.md                         # This file
├── LICENSE                           # MIT License
└── DBISAM_Replication_Implementation_Plan.md  # Detailed implementation plan
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

For issues, questions, or contributions:
- **GitHub Issues**: [https://github.com/lawless-m/Gravedigger/issues](https://github.com/lawless-m/Gravedigger/issues)
- **Documentation**: See [DBISAM_Replication_Implementation_Plan.md](DBISAM_Replication_Implementation_Plan.md) for detailed implementation guidance

## Acknowledgments

- Uses Windows Volume Shadow Copy Service (VSS) for snapshot capabilities
- Designed for DBISAM database replication scenarios
- Inspired by the need for non-intrusive database backups

## Version History

### 1.0.0 (2025-10-25)
- Initial release
- Core replication functionality
- Configuration system
- Comprehensive logging
- File validation
- Generation management
- Retry logic
- Error handling

---

**Note**: Gravedigger is designed for defensive security purposes only - creating backups and replicas of databases. It should not be used for malicious purposes.
