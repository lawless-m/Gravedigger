# Gravedigger Deployment Guide

This guide walks you through deploying Gravedigger in a production environment.

## Table of Contents
- [Pre-Deployment Checklist](#pre-deployment-checklist)
- [Building the Application](#building-the-application)
- [Initial Setup](#initial-setup)
- [Scheduling Replication](#scheduling-replication)
- [Testing](#testing)
- [Production Rollout](#production-rollout)
- [Monitoring](#monitoring)
- [Troubleshooting](#troubleshooting)

## Pre-Deployment Checklist

Before deploying Gravedigger, ensure:

- [ ] Windows Server 2016+ with Volume Shadow Copy Service enabled
- [ ] Administrative privileges for installation and VSS access
- [ ] Sufficient disk space:
  - Source volume: For shadow copies (typically 10-20% of volume size)
  - Destination: For replicas (size of database × RetainGenerations)
- [ ] .NET 8.0 SDK (for building) - **Not required if using self-contained executable**
- [ ] VSS services are running and set to automatic startup
- [ ] Network path accessible (if using network destination)
- [ ] Service account created with appropriate permissions

### Verify VSS Services

```cmd
# Check VSS service
sc query VSS

# Check Shadow Copy Provider
sc query swprv

# If not running, start them
net start VSS
net start swprv

# Set to automatic startup
sc config VSS start= auto
sc config swprv start= auto
```

### Verify Shadow Copies Exist

```cmd
# List shadow copies
vssadmin list shadows /for=C:

# If none exist, create one
wmic shadowcopy call create Volume=C:\
```

## Building the Application

**Prerequisites**: Install .NET 8.0 SDK from https://dotnet.microsoft.com/download/dotnet/8.0

### Option 1: Using the Build Script (Recommended)

```cmd
# On Windows - builds both debug and self-contained release
build.bat
```

This creates a **single-file, self-contained executable** that includes all dependencies.

### Option 2: Using .NET CLI

```cmd
# Build only (requires .NET 8 runtime on target)
dotnet build -c Release

# Publish as self-contained single-file executable (recommended)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### Output Locations

- **Self-contained**: `bin\Release\net8.0-windows\win-x64\publish\Gravedigger.exe`
- **Framework-dependent**: `bin\Release\net8.0-windows\win-x64\Gravedigger.exe`

**Recommended**: Use the self-contained version (`publish\Gravedigger.exe`) so target machines don't need .NET installed.

## Initial Setup

### 1. Create Installation Directory

```cmd
# Create application directory
mkdir C:\Gravedigger
mkdir C:\Gravedigger\Logs

# Copy self-contained executable (recommended - no .NET runtime required)
copy bin\Release\net8.0-windows\win-x64\publish\Gravedigger.exe C:\Gravedigger\

# OR copy framework-dependent version (requires .NET 8 runtime)
# copy bin\Release\net8.0-windows\win-x64\Gravedigger.exe C:\Gravedigger\
```

### 2. Create Configuration File

```cmd
cd C:\Gravedigger
Gravedigger.exe --create-config
```

### 3. Edit Configuration

Edit `gravedigger.config` with your settings:

```ini
[Source]
Volume=C:
DatabasePath=C:\Database\Production
Extensions=*.dat,*.idx,*.blb,*.bak

[Destination]
Path=D:\Replicas\Production
RetainGenerations=3

[Logging]
LogPath=C:\Gravedigger\Logs
LogLevel=Information
RetentionDays=30

[Monitoring]
MaxReplicaAge=2
```

### 4. Create Service Account

```cmd
# Create dedicated service account (optional but recommended)
net user GravediggerSvc P@ssw0rd123! /add /fullname:"Gravedigger Service Account" /comment:"Account for Gravedigger replication service"

# Add to Administrators group (required for VSS access)
net localgroup Administrators GravediggerSvc /add

# Set password to never expire
wmic useraccount where name='GravediggerSvc' set PasswordExpires=false
```

**Security Note**: In production, use a strong password and follow your organization's password policies.

### 5. Set Permissions

```cmd
# Grant permissions to Gravedigger directory
icacls C:\Gravedigger /grant GravediggerSvc:(OI)(CI)F /T

# Grant permissions to destination
icacls D:\Replicas /grant GravediggerSvc:(OI)(CI)F /T

# Grant permissions to log directory
icacls C:\Gravedigger\Logs /grant GravediggerSvc:(OI)(CI)F /T
```

## Scheduling Replication

### Using Windows Task Scheduler

1. **Open Task Scheduler**:
   - Press `Win+R`, type `taskschd.msc`, press Enter

2. **Create New Task**:
   - Click "Create Task" (not "Create Basic Task")

3. **General Tab**:
   - Name: `Gravedigger DBISAM Replication`
   - Description: `Replicates DBISAM database using shadow copies`
   - Security options:
     - User account: `GravediggerSvc` (or `SYSTEM` if not using service account)
     - ✓ Run whether user is logged on or not
     - ✓ Run with highest privileges
     - Configure for: `Windows Server 2016` (or your version)

4. **Triggers Tab**:
   - Click "New"
   - Choose schedule:
     - **Hourly**: Begin: `[Today's date]`, Repeat task every: `1 hour`
     - **Daily**: Daily, Recur every: `1 day`, Start: `02:00:00 AM`
     - **After shadow copy**: Use Event Viewer trigger (advanced)
   - ✓ Enabled

5. **Actions Tab**:
   - Click "New"
   - Action: `Start a program`
   - Program/script: `C:\Gravedigger\Gravedigger.exe`
   - Arguments: `C:\Gravedigger\gravedigger.config`
   - Start in: `C:\Gravedigger`

6. **Conditions Tab**:
   - Power:
     - ☐ Start the task only if the computer is on AC power (uncheck for servers)
   - Network:
     - ✓ Start only if the following network connection is available (if using network destination)

7. **Settings Tab**:
   - ✓ Allow task to be run on demand
   - ✓ Run task as soon as possible after a scheduled start is missed
   - If the task fails, restart every: `15 minutes`, Attempt to restart up to: `3 times`
   - Stop the task if it runs longer than: `2 hours`
   - If the running task does not end when requested: `Stop the task`

8. **Save the Task**:
   - Enter password for service account when prompted

### Command Line Alternative

```cmd
# Create scheduled task
schtasks /create /tn "Gravedigger DBISAM Replication" ^
  /tr "C:\Gravedigger\Gravedigger.exe C:\Gravedigger\gravedigger.config" ^
  /sc hourly ^
  /ru GravediggerSvc ^
  /rp P@ssw0rd123! ^
  /rl HIGHEST ^
  /f

# Verify task was created
schtasks /query /tn "Gravedigger DBISAM Replication"

# Run task manually to test
schtasks /run /tn "Gravedigger DBISAM Replication"
```

## Testing

### 1. Manual Test Run

```cmd
cd C:\Gravedigger
Gravedigger.exe gravedigger.config
```

Expected output:
```
==============================================
   Gravedigger - DBISAM Shadow Copy Tool
   Version 1.0
==============================================

Loading configuration from: gravedigger.config
Configuration validated successfully

[2025-10-25 14:30:15] [INFORMATION] Starting Replication Process
[2025-10-25 14:30:15] [INFORMATION] Found shadow copy...
...
==============================================
   REPLICATION SUCCESSFUL
   Files: 15
   Bytes: 125.3 MB
   Duration: 12.34 seconds
==============================================
```

### 2. Verify Replicas

```cmd
# Check destination directory
dir D:\Replicas\Production

# Should see timestamped directories like:
#   20251025_143015
#   20251025_150015
#   etc.

# Check files in latest replica
dir D:\Replicas\Production\20251025_143015
```

### 3. Validate Logs

```cmd
# Check log directory
dir C:\Gravedigger\Logs

# View latest log
type C:\Gravedigger\Logs\replication_20251025_143015.log
```

### 4. Test Scheduled Task

```cmd
# Run scheduled task manually
schtasks /run /tn "Gravedigger DBISAM Replication"

# Check task status
schtasks /query /tn "Gravedigger DBISAM Replication" /v /fo list

# View task history in Event Viewer
eventvwr.msc
# Navigate to: Applications and Services Logs > Microsoft > Windows > TaskScheduler
```

## Production Rollout

### Phase 1: Pilot Testing (1 week)

1. Deploy to non-critical database first
2. Run manual replications 2-3 times to verify
3. Enable scheduled task
4. Monitor daily for first week
5. Perform test restore from replica

### Phase 2: Production Deployment

1. **Schedule maintenance window** (optional, for initial test)
2. **Deploy to production server**
3. **Run initial replication manually**
4. **Verify first replica**
5. **Enable scheduled task**
6. **Monitor for 48 hours**

### Post-Deployment Checklist

- [ ] Initial replication successful
- [ ] Logs are being written correctly
- [ ] Scheduled task runs successfully
- [ ] Replicas are being created on schedule
- [ ] Old generations are being cleaned up
- [ ] Disk space is adequate
- [ ] Service account has proper permissions
- [ ] Operations team trained on monitoring

## Monitoring

### Daily Checks

```cmd
# Check latest log
type C:\Gravedigger\Logs\replication_*.log | findstr "SUCCESSFUL\|FAILED"

# Check replica age
dir D:\Replicas\Production /o-d
```

### Weekly Checks

1. Review all logs for errors or warnings
2. Verify shadow copy service health
3. Check disk space on source and destination
4. Review Task Scheduler history

### Monthly Checks

1. **Test restore from replica**:
   - Copy replica to test environment
   - Attempt to open database with DBISAM application
   - Run queries to verify data consistency

2. **Review and adjust**:
   - Retention policies
   - Schedule frequency
   - Log retention

### Automated Monitoring

Create a monitoring script:

```batch
@echo off
REM Monitor Gravedigger replication

REM Check if last log shows success
findstr /C:"REPLICATION SUCCESSFUL" C:\Gravedigger\Logs\replication_*.log > nul
if %ERRORLEVEL% NEQ 0 (
    echo WARNING: Last replication failed!
    REM Send alert email here
)

REM Check replica age (should be less than 2 hours old)
forfiles /P "D:\Replicas\Production" /M * /D -0 /C "cmd /c echo @fname" > nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo WARNING: No recent replicas found!
    REM Send alert email here
)
```

## Troubleshooting

### Common Issues

#### 1. "No shadow copy found for volume"

**Cause**: Shadow copies don't exist or have been deleted

**Solution**:
```cmd
# Verify shadow copies exist
vssadmin list shadows /for=C:

# Create shadow copy manually
wmic shadowcopy call create Volume=C:\

# Enable System Protection
# Control Panel > System > System Protection > Configure
```

#### 2. "VSS service is not running"

**Cause**: Volume Shadow Copy service not running

**Solution**:
```cmd
net start VSS
net start swprv
sc config VSS start= auto
sc config swprv start= auto
```

#### 3. "Access Denied" errors

**Cause**: Insufficient permissions

**Solution**:
```cmd
# Run as administrator
# OR
# Verify service account is in Administrators group
net localgroup Administrators

# Grant permissions
icacls C:\Gravedigger /grant GravediggerSvc:(OI)(CI)F /T
```

#### 4. Task Scheduler shows "The operator or administrator has refused the request"

**Cause**: User account doesn't have "Log on as batch job" right

**Solution**:
1. Open Local Security Policy: `secpol.msc`
2. Navigate to: Local Policies > User Rights Assignment
3. Open "Log on as a batch job"
4. Add service account

#### 5. Replica validation fails

**Cause**: File copy incomplete or corrupted

**Solution**:
- Check disk space on destination
- Review logs for specific errors
- Increase RetryAttempts in configuration
- Check network stability (if using network destination)

### Log Files

Important log messages to watch for:

- `[ERROR]`: Replication errors - investigate immediately
- `[WARNING] Shadow copy is X hours old`: Shadow copies too old - create more frequently
- `[WARNING] File count mismatch`: Missing files - verify source and extensions
- `Validation failed`: File integrity issues - investigate source files

## Support

For additional help:
- GitHub Issues: https://github.com/lawless-m/Gravedigger/issues
- Documentation: See README.md
- Implementation Plan: See DBISAM_Replication_Implementation_Plan.md

---

**Remember**: Always test in a non-production environment first!
