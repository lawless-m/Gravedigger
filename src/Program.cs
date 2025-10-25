using System;
using System.IO;
using System.Reflection;
using Gravedigger.Config;
using Gravedigger.Engine;
using Gravedigger.Logging;

namespace Gravedigger
{
    /// <summary>
    /// Gravedigger - DBISAM Shadow Copy Replication Tool
    /// </summary>
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("   Gravedigger - DBISAM Shadow Copy Tool");
            Console.WriteLine("   Version 1.0");
            Console.WriteLine("==============================================");
            Console.WriteLine();

            try
            {
                // Parse command line arguments
                string configPath = "gravedigger.config";

                if (args.Length > 0)
                {
                    if (args[0] == "--help" || args[0] == "-h" || args[0] == "/?")
                    {
                        ShowHelp();
                        return 0;
                    }
                    else if (args[0] == "--version" || args[0] == "-v")
                    {
                        ShowVersion();
                        return 0;
                    }
                    else if (args[0] == "--create-config")
                    {
                        CreateDefaultConfig(args.Length > 1 ? args[1] : "gravedigger.config");
                        return 0;
                    }
                    else
                    {
                        configPath = args[0];
                    }
                }

                // Check if config file exists
                if (!File.Exists(configPath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"ERROR: Configuration file not found: {configPath}");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine();
                    Console.WriteLine("To create a default configuration file, run:");
                    Console.WriteLine($"  Gravedigger.exe --create-config {configPath}");
                    Console.WriteLine();
                    Console.WriteLine("For more help, run:");
                    Console.WriteLine("  Gravedigger.exe --help");
                    return 1;
                }

                // Load configuration
                Console.WriteLine($"Loading configuration from: {configPath}");
                var config = ReplicationConfig.LoadFromFile(configPath);

                // Validate configuration
                config.Validate();
                Console.WriteLine("Configuration validated successfully");
                Console.WriteLine();

                // Initialize logger
                var logger = new ReplicationLogger(config.LogPath, config.LogLevel);
                logger.LogInformation($"Configuration loaded from: {configPath}");
                logger.LogInformation(config.ToString());

                // Cleanup old logs
                logger.CleanupOldLogs(config.LogRetentionDays);

                // Create replication engine
                var engine = new ReplicationEngine(config, logger);

                // Execute replication
                var result = engine.ExecuteReplication();

                // Log result
                string summary = result.Success
                    ? $"Successfully replicated {result.FilesReplicated} files ({FormatBytes(result.BytesReplicated)}) in {result.Duration.TotalSeconds:F2} seconds"
                    : $"Replication failed: {result.ErrorMessage}";

                logger.LogSessionEnd(result.Success, summary);

                // Print summary to console
                Console.WriteLine();
                Console.WriteLine("==============================================");
                if (result.Success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("   REPLICATION SUCCESSFUL");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"   Files: {result.FilesReplicated}");
                    Console.WriteLine($"   Bytes: {FormatBytes(result.BytesReplicated)}");
                    Console.WriteLine($"   Duration: {result.Duration.TotalSeconds:F2} seconds");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("   REPLICATION FAILED");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"   Error: {result.ErrorMessage}");
                }

                if (result.Warnings.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"   Warnings: {result.Warnings.Count}");
                    Console.ForegroundColor = ConsoleColor.White;
                }

                Console.WriteLine($"   Log File: {logger.GetLogFilePath()}");
                Console.WriteLine("==============================================");

                return result.Success ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine();
                Console.WriteLine("CRITICAL ERROR:");
                Console.WriteLine(ex.Message);
                Console.WriteLine();
                Console.WriteLine("Stack Trace:");
                Console.WriteLine(ex.StackTrace);
                Console.ForegroundColor = ConsoleColor.White;
                return 1;
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("USAGE:");
            Console.WriteLine("  Gravedigger.exe [config_file]");
            Console.WriteLine("  Gravedigger.exe --create-config [config_file]");
            Console.WriteLine("  Gravedigger.exe --help");
            Console.WriteLine("  Gravedigger.exe --version");
            Console.WriteLine();
            Console.WriteLine("OPTIONS:");
            Console.WriteLine("  config_file              Path to configuration file (default: gravedigger.config)");
            Console.WriteLine("  --create-config [path]   Create a default configuration file");
            Console.WriteLine("  --help, -h, /?           Show this help message");
            Console.WriteLine("  --version, -v            Show version information");
            Console.WriteLine();
            Console.WriteLine("DESCRIPTION:");
            Console.WriteLine("  Gravedigger replicates DBISAM databases using Windows Volume Shadow Copy");
            Console.WriteLine("  Service (VSS). It extracts database files from shadow copies (restore points)");
            Console.WriteLine("  to create consistent backups without interrupting the running database.");
            Console.WriteLine();
            Console.WriteLine("PREREQUISITES:");
            Console.WriteLine("  - Windows with Volume Shadow Copy Service enabled");
            Console.WriteLine("  - Administrative privileges");
            Console.WriteLine("  - Active shadow copies on the source volume");
            Console.WriteLine();
            Console.WriteLine("EXAMPLES:");
            Console.WriteLine("  # Run with default config file");
            Console.WriteLine("  Gravedigger.exe");
            Console.WriteLine();
            Console.WriteLine("  # Run with custom config file");
            Console.WriteLine("  Gravedigger.exe C:\\config\\production.config");
            Console.WriteLine();
            Console.WriteLine("  # Create default config file");
            Console.WriteLine("  Gravedigger.exe --create-config");
            Console.WriteLine();
            Console.WriteLine("EXIT CODES:");
            Console.WriteLine("  0 - Success");
            Console.WriteLine("  1 - Failure");
        }

        static void ShowVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Console.WriteLine($"Gravedigger version {version}");
            Console.WriteLine("DBISAM Shadow Copy Replication Tool");
            Console.WriteLine("Copyright (c) 2025");
        }

        static void CreateDefaultConfig(string path)
        {
            var defaultConfig = @"# Gravedigger Configuration File
# DBISAM Shadow Copy Replication

[Source]
# Volume to get shadow copies from (e.g., C:, D:)
Volume=C:

# Full path to the database directory
DatabasePath=C:\Database\Production

# File extensions to replicate (comma-separated)
Extensions=*.dat,*.idx,*.blb,*.bak

[Destination]
# Destination path for replicated files
# Each replication creates a timestamped subdirectory
Path=D:\Replicas\Production

# Number of replica generations to keep (older ones are deleted)
RetainGenerations=3

[Schedule]
# Frequency description (for documentation only)
Frequency=Hourly

# Enable retry on failure
RetryOnFailure=True

# Number of retry attempts
RetryAttempts=3

# Delay between retries (in minutes)
RetryDelayMinutes=5

[Logging]
# Directory for log files
LogPath=C:\Gravedigger\Logs

# Log level: Debug, Information, Warning, Error, Critical
LogLevel=Information

# Number of days to retain log files
RetentionDays=30

[Monitoring]
# Send alerts on failure
AlertOnFailure=True

# Email address for alerts (requires additional configuration)
AlertEmail=dbadmin@company.com

# Maximum acceptable age of shadow copy (in hours)
MaxReplicaAge=2
";

            try
            {
                File.WriteAllText(path, defaultConfig);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Created default configuration file: {path}");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine();
                Console.WriteLine("Please edit this file with your specific settings before running Gravedigger.");
                Console.WriteLine("At minimum, you must configure:");
                Console.WriteLine("  - Source.Volume");
                Console.WriteLine("  - Source.DatabasePath");
                Console.WriteLine("  - Destination.Path");
                Console.WriteLine("  - Logging.LogPath");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: Failed to create configuration file: {ex.Message}");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}
