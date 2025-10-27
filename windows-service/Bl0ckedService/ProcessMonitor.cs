using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace Bl0ckedService
{
    /// <summary>
    /// Monitors running processes and terminates any that are not on the whitelist
    /// when lockdown is active. Uses WMI for process creation events.
    /// </summary>
    public class ProcessMonitor : IDisposable
    {
        private ManagementEventWatcher? _processStartWatcher;
        private bool _enabled = false;
        private readonly object _monitorLock = new object();
        private CancellationTokenSource? _cancellationTokenSource;

        // Explicitly blocked process names (case-insensitive)
        private readonly HashSet<string> _blockedProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "taskmgr.exe",
            "cmd.exe",
            "powershell.exe",
            "pwsh.exe",
            "regedit.exe",
            "msconfig.exe",
            "services.msc",
            "chrome.exe",
            "firefox.exe",
            "msedge.exe",
            "opera.exe",
            "brave.exe",
            "iexplore.exe",
            // Add common game launchers
            "steam.exe",
            "epicgameslauncher.exe",
            "origin.exe",
            "uplay.exe",
            // Add common remote desktop tools
            "mstsc.exe",
            "teamviewer.exe",
            "anydesk.exe"
        };

        // Allowed process names (case-insensitive) - these are ALWAYS allowed
        private readonly HashSet<string> _allowedProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "system",
            "registry",
            "smss.exe",
            "csrss.exe",
            "wininit.exe",
            "services.exe",
            "lsass.exe",
            "svchost.exe",
            "winlogon.exe",
            "dwm.exe",
            "explorer.exe",
            "fontdrvhost.exe",
            "conhost.exe",
            "runtimebroker.exe",
            "taskhostw.exe",
            "sihost.exe",
            "ctfmon.exe",
            "dllhost.exe",
            "searchindexer.exe",
            "searchprotocolhost.exe",
            "searchfilterhost.exe",
            "spoolsv.exe",
            "audiodg.exe",
            "bl0ckedservice.exe",
            // Windows Update
            "wuauclt.exe",
            "trustedinstaller.exe",
            "tiworker.exe",
            // Windows Defender
            "msmpeng.exe",
            "nissrv.exe",
            "securityhealthservice.exe",
            "securityhealthsystray.exe"
        };

        // Allowed directory paths - processes in these directories are always allowed
        private readonly string[] _allowedDirectories = new string[]
        {
            @"C:\Windows\System32",
            @"C:\Windows\System",
            @"C:\Windows\SysWOW64",
            @"C:\Program Files\Windows Defender",
            @"C:\Program Files (x86)\Windows Defender"
        };

        /// <summary>
        /// Starts monitoring for new processes
        /// </summary>
        public void Start()
        {
            lock (_monitorLock)
            {
                if (_enabled)
                    return; // Already started

                _enabled = true;
                _cancellationTokenSource = new CancellationTokenSource();

                // Start WMI event watcher for process creation
                try
                {
                    var query = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace");
                    _processStartWatcher = new ManagementEventWatcher(query);
                    _processStartWatcher.EventArrived += OnProcessStarted;
                    _processStartWatcher.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error starting process monitor: {ex.Message}");
                }

                // Also scan existing processes
                Task.Run(() => ScanExistingProcesses(), _cancellationTokenSource.Token);
            }
        }

        /// <summary>
        /// Stops monitoring processes
        /// </summary>
        public void Stop()
        {
            lock (_monitorLock)
            {
                _enabled = false;

                if (_processStartWatcher != null)
                {
                    _processStartWatcher.Stop();
                    _processStartWatcher.EventArrived -= OnProcessStarted;
                    _processStartWatcher.Dispose();
                    _processStartWatcher = null;
                }

                _cancellationTokenSource?.Cancel();
            }
        }

        /// <summary>
        /// Called when a new process is started
        /// </summary>
        private void OnProcessStarted(object sender, EventArrivedEventArgs e)
        {
            if (!_enabled)
                return;

            try
            {
                uint processId = Convert.ToUInt32(e.NewEvent.Properties["ProcessID"].Value);
                string? processName = e.NewEvent.Properties["ProcessName"].Value?.ToString();

                if (processName != null)
                {
                    Task.Run(() => CheckAndTerminateProcess((int)processId, processName));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling process start event: {ex.Message}");
            }
        }

        /// <summary>
        /// Scans all currently running processes and terminates blocked ones
        /// </summary>
        private void ScanExistingProcesses()
        {
            if (!_enabled)
                return;

            try
            {
                Process[] processes = Process.GetProcesses();
                foreach (Process process in processes)
                {
                    try
                    {
                        if (!_enabled)
                            break;

                        string processName = process.ProcessName + ".exe";
                        CheckAndTerminateProcess(process.Id, processName);
                    }
                    catch
                    {
                        // Process may have already exited, ignore
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning existing processes: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if a process should be allowed and terminates it if not
        /// </summary>
        private void CheckAndTerminateProcess(int processId, string processName)
        {
            if (!_enabled)
                return;

            try
            {
                // Check if process is explicitly allowed
                if (_allowedProcessNames.Contains(processName))
                    return;

                // Check if process is explicitly blocked
                if (_blockedProcessNames.Contains(processName))
                {
                    TerminateProcess(processId, processName);
                    return;
                }

                // Check if process is in an allowed directory
                try
                {
                    Process process = Process.GetProcessById(processId);
                    string? executablePath = process.MainModule?.FileName;

                    if (executablePath != null)
                    {
                        foreach (string allowedDir in _allowedDirectories)
                        {
                            if (executablePath.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase))
                            {
                                return; // Allow it
                            }
                        }

                        // Check if it's in Program Files\Bl0cked (our own service)
                        if (executablePath.Contains(@"\Bl0cked\", StringComparison.OrdinalIgnoreCase))
                        {
                            return; // Allow it
                        }
                    }
                }
                catch
                {
                    // Couldn't get process info, may have exited
                    return;
                }

                // If we got here, the process is not explicitly allowed or blocked
                // By default, block it during lockdown
                TerminateProcess(processId, processName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking process {processName} (PID: {processId}): {ex.Message}");
            }
        }

        /// <summary>
        /// Terminates a process by ID
        /// </summary>
        private void TerminateProcess(int processId, string processName)
        {
            try
            {
                Process process = Process.GetProcessById(processId);
                if (!process.HasExited)
                {
                    Console.WriteLine($"Terminating blocked process: {processName} (PID: {processId})");
                    process.Kill();
                    process.WaitForExit(1000); // Wait up to 1 second for graceful exit
                }
            }
            catch (ArgumentException)
            {
                // Process doesn't exist or already exited
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error terminating process {processName} (PID: {processId}): {ex.Message}");
            }
        }

        /// <summary>
        /// Adds a process name to the allowed list
        /// </summary>
        public void AddAllowedProcess(string processName)
        {
            lock (_monitorLock)
            {
                _allowedProcessNames.Add(processName);
            }
        }

        /// <summary>
        /// Adds a process name to the blocked list
        /// </summary>
        public void AddBlockedProcess(string processName)
        {
            lock (_monitorLock)
            {
                _blockedProcessNames.Add(processName);
            }
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
