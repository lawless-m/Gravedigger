using System;
using System.IO;
using System.Diagnostics;

namespace DBISAMReplication
{
    public class DBISAMShadowReplication
    {
        /// <summary>
        /// Copies DBISAM database files from the latest shadow copy to a destination
        /// </summary>
        /// <param name="sourceVolume">Source volume (e.g., "C:")</param>
        /// <param name="dbPath">Full path to database directory (e.g., "C:\Database\MyDB")</param>
        /// <param name="destPath">Destination path for copied files</param>
        public static void CopyFromLatestShadow(string sourceVolume, string dbPath, string destPath)
        {
            // Get the latest shadow copy for the volume
            string shadowPath = GetLatestShadowCopyPath(sourceVolume);
            
            if (shadowPath != null)
            {
                Console.WriteLine($"Found shadow copy: {shadowPath}");
                
                // Build the shadow copy path to your database
                // Remove drive letter and colon (e.g., "C:" becomes "")
                string relativePath = dbPath.Substring(2);
                string shadowDbPath = shadowPath + relativePath;
                
                Console.WriteLine($"Shadow DB path: {shadowDbPath}");
                
                // Copy all DBISAM files
                CopyDBISAMFiles(shadowDbPath, destPath);
            }
            else
            {
                Console.WriteLine("No shadow copy found for volume: " + sourceVolume);
            }
        }
        
        /// <summary>
        /// Gets the device path of the latest shadow copy for a given volume
        /// </summary>
        private static string GetLatestShadowCopyPath(string volume)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "vssadmin",
                    Arguments = $"list shadows /for={volume}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            
            // Parse output to find shadow copy device object
            // Format: "Shadow Copy Volume: \\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy123"
            string shadowDevice = null;
            
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("Shadow Copy Volume:"))
                {
                    // Extract the device path
                    int colonIndex = lines[i].IndexOf(':', "Shadow Copy Volume:".Length);
                    if (colonIndex > 0)
                    {
                        shadowDevice = lines[i].Substring(colonIndex + 1).Trim();
                        break;
                    }
                }
            }
            
            return shadowDevice;
        }
        
        /// <summary>
        /// Lists all shadow copies for a volume
        /// </summary>
        public static void ListShadowCopies(string volume)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "vssadmin",
                    Arguments = $"list shadows /for={volume}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            
            Console.WriteLine(output);
        }
        
        /// <summary>
        /// Copies all DBISAM-related files from source to destination
        /// </summary>
        private static void CopyDBISAMFiles(string sourceDir, string destDir)
        {
            string[] extensions = { "*.dat", "*.idx", "*.blb", "*.bak" };
            
            // Create destination directory if it doesn't exist
            Directory.CreateDirectory(destDir);
            
            int filesCopied = 0;
            
            foreach (var ext in extensions)
            {
                try
                {
                    var files = Directory.GetFiles(sourceDir, ext);
                    foreach (var file in files)
                    {
                        string destFile = Path.Combine(destDir, Path.GetFileName(file));
                        File.Copy(file, destFile, true);
                        Console.WriteLine($"Copied: {Path.GetFileName(file)}");
                        filesCopied++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error copying {ext} files: {ex.Message}");
                }
            }
            
            Console.WriteLine($"Total files copied: {filesCopied}");
        }
        
        /// <summary>
        /// Example usage
        /// </summary>
        public static void Main(string[] args)
        {
            // Example: Copy DBISAM database from shadow copy
            string sourceVolume = "C:";
            string dbPath = @"C:\Database\MyDB";
            string destPath = @"D:\Backup\MyDB";
            
            try
            {
                Console.WriteLine("Starting DBISAM shadow copy replication...");
                CopyFromLatestShadow(sourceVolume, dbPath, destPath);
                Console.WriteLine("Replication complete!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
