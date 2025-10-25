# DBISAM Shadow Copy Replication - Implementation Plan

## Overview
This document outlines the implementation plan for using Windows Volume Shadow Copy Service (VSS) restore points as a replication mechanism for DBISAM databases.

## Background
DBISAM is a file-based database system that stores data in `.dat`, `.idx`, `.blb`, and `.bak` files. Since DBISAM doesn't natively support replication, we're using Windows shadow copies (restore points) to create consistent snapshots of the database files for replication purposes.

## Prerequisites

### System Requirements
- Windows Server 2012, 2012 R2, 2016, 2019, 2022, or newer with Volume Shadow Copy Service enabled
- Administrative privileges for shadow copy access
- Sufficient disk space for shadow copies
- .NET 8.0 SDK (for building) or .NET 8.0 Runtime (for running framework-dependent builds)
- **Note**: Self-contained deployments don't require .NET runtime on target machines (works on Server 2012+)

### Services That Must Be Running
1. **Volume Shadow Copy** service
2. **MS Software Shadow Copy Provider** service

Check service status:
```cmd
sc query VSS
sc query swprv
```

Start services if needed:
```cmd
net start VSS
net start swprv
```

### DBISAM Considerations
- Database files must be on an NTFS volume
- Identify all file extensions your DBISAM version uses (typically: .dat, .idx, .blb, .bak)
- Document the exact path(s) to your database files
- Note any database-specific configuration or auxiliary files

## Implementation Phases

### Phase 1: Environment Setup and Testing

#### 1.1 Verify Shadow Copy Configuration
- Confirm restore points are being created automatically on the source system
- Verify you can manually create restore points
- Check available shadow copies:
  ```cmd
  vssadmin list shadows
  ```

#### 1.2 Test Manual File Access
Before implementing the automated solution:
1. Create a manual restore point
2. Use the provided code to list shadow copies
3. Manually navigate to shadow copy path (requires admin)
4. Verify DBISAM files are accessible and readable

#### 1.3 Development Environment Setup
- Create a test environment that mirrors production
- Install Visual Studio or .NET SDK
- Compile the DBISAMShadowReplication.cs class
- Test with non-critical database first

### Phase 2: Initial Implementation

#### 2.1 Configuration
Create a configuration file (app.config or settings file) with:
- Source volume (e.g., "C:")
- Source database path (e.g., "C:\Database\Production")
- Destination path (e.g., "D:\Replicas\Production" or network path)
- File extensions to copy (in case your DBISAM uses additional files)

#### 2.2 Build and Deploy
1. Compile the C# application
2. Deploy to a location accessible to scheduled tasks
3. Create a dedicated service account with:
   - Administrative privileges (required for VSS access)
   - Read access to source database location
   - Write access to destination location

#### 2.3 Initial Testing
Test scenarios:
- Copy from latest shadow copy to local destination
- Copy to network share destination
- Handle missing shadow copies gracefully
- Verify all file types are copied
- Confirm file integrity (checksums/sizes match)

### Phase 3: Scheduling and Automation

#### 3.1 Windows Task Scheduler Setup
Create scheduled task with:
- **Trigger**: Define replication frequency
  - Option A: After each restore point creation
  - Option B: Fixed schedule (e.g., hourly, daily)
  - Option C: On-demand via manual trigger
  
- **Action**: Run the compiled .exe
  ```cmd
  C:\DBISAMReplication\DBISAMShadowReplication.exe
  ```

- **Settings**:
  - Run whether user is logged on or not
  - Run with highest privileges
  - Use service account credentials

#### 3.2 Logging Implementation
Enhance the code to add:
- Log file output to track each replication
- Timestamp each operation
- Record success/failure status
- Log file sizes copied
- Alert on failures

Example log location: `C:\DBISAMReplication\Logs\replication_YYYYMMDD.log`

#### 3.3 Monitoring Setup
Implement monitoring for:
- Replication job completion
- Failed replications
- Shadow copy availability
- Disk space on destination
- Age of last successful replication

### Phase 4: Validation and Rollout

#### 4.1 Replica Validation Process
Before considering replica usable:
1. **File integrity check**
   - Verify all expected files present
   - Compare file sizes with source
   - Optional: Calculate checksums

2. **DBISAM accessibility test**
   - Attempt to open replica database with DBISAM tools
   - Run basic queries to verify consistency
   - Check for corruption indicators

3. **Recovery drill**
   - Simulate production failure
   - Restore from replica
   - Verify application functionality

#### 4.2 Production Rollout
1. Schedule initial replication during maintenance window
2. Monitor first 24-48 hours closely
3. Verify replica creation at each scheduled interval
4. Document any issues and resolutions
5. Create runbook for operations team

### Phase 5: Ongoing Operations

#### 5.1 Regular Maintenance Tasks
Daily:
- Check replication job status
- Verify latest replica timestamp

Weekly:
- Review replication logs for errors
- Verify shadow copy service health
- Check destination disk space

Monthly:
- Test restore from replica
- Review and adjust shadow copy retention
- Archive old replication logs

#### 5.2 Disaster Recovery Procedures
Document step-by-step process for:
1. Detecting primary database failure
2. Validating replica integrity
3. Switching applications to replica
4. Failing back to primary after recovery

## Known Limitations and Considerations

### Shadow Copy Limitations
- **Retention**: Windows automatically deletes old shadow copies when space runs low
- **Storage**: Shadow copies consume disk space on the source volume
- **Frequency**: Restore points typically created daily or on schedule
- **Not real-time**: Replication is only as current as the latest shadow copy

### DBISAM-Specific Considerations
- **Consistency**: Shadow copies capture a point-in-time snapshot, but if DBISAM has uncommitted transactions, files may be in mid-transaction state
- **Cache/Memory**: DBISAM may have data in memory buffers not yet written to disk
- **File locking**: Active connections don't prevent shadow copy creation, but consistency depends on DBISAM's write behavior

### Mitigation Strategies
1. **Schedule replication during low-activity periods** when possible
2. **Implement application-level consistency checks** after replication
3. **Keep multiple generations** of replicas to recover from corrupt snapshots
4. **Consider forcing a flush** before shadow copy creation (if DBISAM supports it)
5. **Monitor replica validity** - don't assume all replicas are usable

## Alternative Approaches to Consider

If shadow copy replication proves insufficient:

### 1. Scheduled File Copy with Application Quiesce
- Stop application services
- Create shadow copy or direct file copy
- Restart services
- More downtime, but guaranteed consistency

### 2. DBISAM Transaction Log Shipping
- If your DBISAM version supports transaction logging
- Ship and replay transaction logs to replica
- Closer to real-time replication

### 3. Application-Level Replication
- Implement at application layer
- Capture changes as they occur
- Write to both primary and replica
- More development effort but most reliable

### 4. Third-Party Replication Tools
- File-based replication solutions (e.g., DFS Replication)
- Storage-level replication
- May provide better consistency guarantees

## Success Criteria

Replication implementation is successful when:
- [ ] Replicas are created on schedule with >99% success rate
- [ ] Replica files can be opened by DBISAM without errors
- [ ] Test restores complete successfully
- [ ] Recovery time objective (RTO) meets business requirements
- [ ] Replica lag time is acceptable for business needs
- [ ] Operations team trained on monitoring and recovery procedures
- [ ] Disaster recovery drills pass validation

## Risk Assessment

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Inconsistent replica due to mid-transaction snapshot | High | Medium | Keep multiple replica generations; validate before use |
| Shadow copy service failure | High | Low | Monitor service health; alert on failure |
| Insufficient disk space for shadow copies | Medium | Medium | Monitor disk space; configure retention policy |
| Network interruption during copy | Medium | Low | Implement retry logic; copy to local staging first |
| Application compatibility issues with replica | High | Low | Thorough testing before production rollout |

## Rollback Plan

If replication implementation encounters critical issues:
1. Disable scheduled task immediately
2. Revert to previous backup strategy
3. Preserve logs and replica files for analysis
4. Document specific failure scenario
5. Address root cause before re-attempting

## Timeline Estimate

- **Phase 1** (Setup and Testing): 2-3 days
- **Phase 2** (Initial Implementation): 3-5 days
- **Phase 3** (Automation): 2-3 days
- **Phase 4** (Validation): 5-7 days
- **Phase 5** (Production Rollout): 1-2 days

**Total estimated time**: 2-3 weeks with testing and validation

## Appendix A: Quick Reference Commands

### Check Shadow Copies
```cmd
vssadmin list shadows /for=C:
```

### Create Manual Shadow Copy
```cmd
wmic shadowcopy call create Volume=C:\
```

### Check Service Status
```cmd
sc query VSS
sc query swprv
```

### Delete Old Shadow Copies
```cmd
vssadmin delete shadows /for=C: /oldest
```

## Appendix B: Configuration Template

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
LogPath=C:\DBISAMReplication\Logs
LogLevel=Information
RetentionDays=30

[Monitoring]
AlertOnFailure=True
AlertEmail=dbadmin@company.com
MaxReplicaAge=2
```

## Document Control

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-10-25 | Initial | Initial planning document |

---

**Next Steps**: Review this plan with stakeholders, adjust timeline and scope as needed, then proceed with Phase 1 implementation.
