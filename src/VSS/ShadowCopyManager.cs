using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Gravedigger.Logging;

namespace Gravedigger.VSS
{
    /// <summary>
    /// Manages Windows Volume Shadow Copy Service operations
    /// </summary>
    public class ShadowCopyManager
    {
        private readonly ReplicationLogger _logger;

        public class ShadowCopyInfo
        {
            public string DevicePath { get; set; }
            public DateTime CreationTime { get; set; }
            public string ShadowCopyId { get; set; }
            public string OriginalVolume { get; set; }
        }

        public ShadowCopyManager(ReplicationLogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gets the latest shadow copy for a given volume
        /// </summary>
        public ShadowCopyInfo GetLatestShadowCopy(string volume)
        {
            _logger.LogInformation($"Searching for shadow copies on volume: {volume}");

            var allShadowCopies = ListAllShadowCopies(volume);

            if (allShadowCopies == null || allShadowCopies.Count == 0)
            {
                _logger.LogWarning($"No shadow copies found for volume: {volume}");
                return null;
            }

            // Get the most recent shadow copy
            var latest = allShadowCopies.OrderByDescending(sc => sc.CreationTime).First();

            _logger.LogInformation($"Found latest shadow copy:");
            _logger.LogInformation($"  Device Path: {latest.DevicePath}");
            _logger.LogInformation($"  Creation Time: {latest.CreationTime:yyyy-MM-dd HH:mm:ss}");
            _logger.LogInformation($"  Shadow Copy ID: {latest.ShadowCopyId}");

            return latest;
        }

        /// <summary>
        /// Lists all shadow copies for a given volume
        /// </summary>
        public List<ShadowCopyInfo> ListAllShadowCopies(string volume)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "vssadmin",
                        Arguments = $"list shadows /for={volume}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    _logger.LogError($"vssadmin failed with exit code {process.ExitCode}: {error}");
                    return new List<ShadowCopyInfo>();
                }

                return ParseVssAdminOutput(output);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error listing shadow copies: {ex.Message}", ex);
                return new List<ShadowCopyInfo>();
            }
        }

        /// <summary>
        /// Parses vssadmin output to extract shadow copy information
        /// </summary>
        private List<ShadowCopyInfo> ParseVssAdminOutput(string output)
        {
            var shadowCopies = new List<ShadowCopyInfo>();
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            ShadowCopyInfo currentShadow = null;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // New shadow copy entry starts with "Contents of shadow copy set"
                if (trimmed.StartsWith("Contents of shadow copy set"))
                {
                    if (currentShadow != null)
                    {
                        shadowCopies.Add(currentShadow);
                    }
                    currentShadow = new ShadowCopyInfo();
                }

                if (currentShadow == null)
                    continue;

                // Extract Shadow Copy Volume (device path)
                if (trimmed.StartsWith("Shadow Copy Volume:"))
                {
                    var colonIndex = trimmed.IndexOf(':', "Shadow Copy Volume:".Length);
                    if (colonIndex > 0)
                    {
                        currentShadow.DevicePath = trimmed.Substring(colonIndex + 1).Trim();
                    }
                }

                // Extract Original Volume
                if (trimmed.StartsWith("Original Volume:"))
                {
                    var colonIndex = trimmed.IndexOf(':', "Original Volume:".Length);
                    if (colonIndex > 0)
                    {
                        currentShadow.OriginalVolume = trimmed.Substring(colonIndex + 1).Trim();
                    }
                }

                // Extract Creation Time
                if (trimmed.StartsWith("Creation Time:"))
                {
                    var colonIndex = trimmed.IndexOf(':', "Creation Time:".Length);
                    if (colonIndex > 0)
                    {
                        var timeStr = trimmed.Substring(colonIndex + 1).Trim();
                        if (DateTime.TryParse(timeStr, out var creationTime))
                        {
                            currentShadow.CreationTime = creationTime;
                        }
                    }
                }

                // Extract Shadow Copy ID
                if (trimmed.StartsWith("Shadow Copy ID:"))
                {
                    var colonIndex = trimmed.IndexOf(':', "Shadow Copy ID:".Length);
                    if (colonIndex > 0)
                    {
                        currentShadow.ShadowCopyId = trimmed.Substring(colonIndex + 1).Trim();
                    }
                }
            }

            // Add the last shadow copy
            if (currentShadow != null && !string.IsNullOrEmpty(currentShadow.DevicePath))
            {
                shadowCopies.Add(currentShadow);
            }

            _logger.LogDebug($"Parsed {shadowCopies.Count} shadow copy entries from vssadmin output");

            return shadowCopies;
        }

        /// <summary>
        /// Checks if VSS service is running
        /// </summary>
        public bool IsVssServiceRunning()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sc",
                        Arguments = "query VSS",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output.Contains("RUNNING");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking VSS service status: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Builds the full path to a file in the shadow copy
        /// </summary>
        public string GetShadowCopyPath(ShadowCopyInfo shadowCopy, string originalPath)
        {
            // Remove drive letter and colon (e.g., "C:" becomes "")
            // Example: "C:\Database\MyDB" becomes "\Database\MyDB"
            string relativePath = originalPath.Substring(2);

            // Combine shadow device path with relative path
            string shadowPath = shadowCopy.DevicePath + relativePath;

            _logger.LogDebug($"Translated path: {originalPath} -> {shadowPath}");

            return shadowPath;
        }
    }
}
