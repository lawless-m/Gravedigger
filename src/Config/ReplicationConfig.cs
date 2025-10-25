using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Gravedigger.Config
{
    /// <summary>
    /// Configuration class for DBISAM shadow copy replication
    /// </summary>
    public class ReplicationConfig
    {
        // Source configuration
        public string SourceVolume { get; set; }
        public string DatabasePath { get; set; }
        public List<string> FileExtensions { get; set; }

        // Destination configuration
        public string DestinationPath { get; set; }
        public int RetainGenerations { get; set; }

        // Schedule configuration
        public string Frequency { get; set; }
        public bool RetryOnFailure { get; set; }
        public int RetryAttempts { get; set; }
        public int RetryDelayMinutes { get; set; }

        // Logging configuration
        public string LogPath { get; set; }
        public string LogLevel { get; set; }
        public int LogRetentionDays { get; set; }

        // Monitoring configuration
        public bool AlertOnFailure { get; set; }
        public string AlertEmail { get; set; }
        public int MaxReplicaAgeHours { get; set; }

        public ReplicationConfig()
        {
            // Set defaults
            FileExtensions = new List<string> { "*.dat", "*.idx", "*.blb", "*.bak" };
            RetainGenerations = 3;
            RetryOnFailure = true;
            RetryAttempts = 3;
            RetryDelayMinutes = 5;
            LogLevel = "Information";
            LogRetentionDays = 30;
            AlertOnFailure = true;
            MaxReplicaAgeHours = 2;
        }

        /// <summary>
        /// Loads configuration from an INI-style file
        /// </summary>
        public static ReplicationConfig LoadFromFile(string configPath)
        {
            var config = new ReplicationConfig();

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"Configuration file not found: {configPath}");
            }

            var lines = File.ReadAllLines(configPath);
            string currentSection = "";

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Skip comments and empty lines
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";"))
                    continue;

                // Section header
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentSection = trimmed.Substring(1, trimmed.Length - 2);
                    continue;
                }

                // Key=Value pair
                var parts = trimmed.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                    continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                // Parse based on section
                switch (currentSection.ToLower())
                {
                    case "source":
                        ParseSourceSection(config, key, value);
                        break;
                    case "destination":
                        ParseDestinationSection(config, key, value);
                        break;
                    case "schedule":
                        ParseScheduleSection(config, key, value);
                        break;
                    case "logging":
                        ParseLoggingSection(config, key, value);
                        break;
                    case "monitoring":
                        ParseMonitoringSection(config, key, value);
                        break;
                }
            }

            return config;
        }

        private static void ParseSourceSection(ReplicationConfig config, string key, string value)
        {
            switch (key.ToLower())
            {
                case "volume":
                    config.SourceVolume = value;
                    break;
                case "databasepath":
                    config.DatabasePath = value;
                    break;
                case "extensions":
                    config.FileExtensions = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(e => e.Trim())
                        .ToList();
                    break;
            }
        }

        private static void ParseDestinationSection(ReplicationConfig config, string key, string value)
        {
            switch (key.ToLower())
            {
                case "path":
                    config.DestinationPath = value;
                    break;
                case "retaingenerations":
                    config.RetainGenerations = int.Parse(value);
                    break;
            }
        }

        private static void ParseScheduleSection(ReplicationConfig config, string key, string value)
        {
            switch (key.ToLower())
            {
                case "frequency":
                    config.Frequency = value;
                    break;
                case "retryonfailure":
                    config.RetryOnFailure = bool.Parse(value);
                    break;
                case "retryattempts":
                    config.RetryAttempts = int.Parse(value);
                    break;
                case "retrydelayminutes":
                    config.RetryDelayMinutes = int.Parse(value);
                    break;
            }
        }

        private static void ParseLoggingSection(ReplicationConfig config, string key, string value)
        {
            switch (key.ToLower())
            {
                case "logpath":
                    config.LogPath = value;
                    break;
                case "loglevel":
                    config.LogLevel = value;
                    break;
                case "retentiondays":
                    config.LogRetentionDays = int.Parse(value);
                    break;
            }
        }

        private static void ParseMonitoringSection(ReplicationConfig config, string key, string value)
        {
            switch (key.ToLower())
            {
                case "alertonfailure":
                    config.AlertOnFailure = bool.Parse(value);
                    break;
                case "alertemail":
                    config.AlertEmail = value;
                    break;
                case "maxreplicaage":
                    config.MaxReplicaAgeHours = int.Parse(value);
                    break;
            }
        }

        /// <summary>
        /// Validates the configuration
        /// </summary>
        public void Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(SourceVolume))
                errors.Add("SourceVolume is required");

            if (string.IsNullOrWhiteSpace(DatabasePath))
                errors.Add("DatabasePath is required");

            if (string.IsNullOrWhiteSpace(DestinationPath))
                errors.Add("DestinationPath is required");

            if (string.IsNullOrWhiteSpace(LogPath))
                errors.Add("LogPath is required");

            if (FileExtensions == null || FileExtensions.Count == 0)
                errors.Add("At least one file extension is required");

            if (RetainGenerations < 1)
                errors.Add("RetainGenerations must be at least 1");

            if (RetryAttempts < 0)
                errors.Add("RetryAttempts must be non-negative");

            if (errors.Any())
            {
                throw new InvalidOperationException(
                    "Configuration validation failed:\n" + string.Join("\n", errors));
            }
        }

        public override string ToString()
        {
            return $"Source: {DatabasePath} on {SourceVolume}\n" +
                   $"Destination: {DestinationPath}\n" +
                   $"Extensions: {string.Join(", ", FileExtensions)}\n" +
                   $"Retain Generations: {RetainGenerations}\n" +
                   $"Log Path: {LogPath}";
        }
    }
}
