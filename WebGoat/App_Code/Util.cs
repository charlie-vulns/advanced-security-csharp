using System;
using System.Diagnostics;
using log4net;
using System.Reflection;
using System.IO;
using System.Threading;
using System.Collections.Generic;

namespace OWASP.WebGoat.NET.App_Code
{
    public class Util
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly HashSet<string> AllowedClientExecutables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mysql",
            "mysql.exe",
            "sqlite3",
            "sqlite3.exe"
        };
        private static readonly char[] DisallowedArgumentChars = { '&', '|', ';', '<', '>', '`', '\r', '\n', '\0' };
        
        public static int RunProcessWithInput(string cmd, string args, string input)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                WorkingDirectory = Settings.RootDir,
                FileName = GetSafeExecutableName(cmd),
                Arguments = GetSafeArguments(args),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            using (Process process = new Process())
            {
                process.EnableRaisingEvents = true;
                process.StartInfo = startInfo;

                process.OutputDataReceived += (sender, e) => {
                    if (e.Data != null)
                        log.Info(e.Data);
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        log.Error(e.Data);
                };

                AutoResetEvent are = new AutoResetEvent(false);

                process.Exited += (sender, e) => 
                {
                    Thread.Sleep(1000);
                    are.Set();
                    log.Info("Process exited");

                };

                process.Start();

                using (StreamReader reader = new StreamReader(new FileStream(input, FileMode.Open)))
                {
                    string line;
                    string replaced;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                            replaced = line.Replace("DB_Scripts/datafiles/", "DB_Scripts\\\\datafiles\\\\");
                        else
                            replaced = line;

                        log.Debug("Line: " + replaced);

                        process.StandardInput.WriteLine(replaced);
                    }
                }
    
                process.StandardInput.Close();
    

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
    
                //NOTE: Looks like we have a mono bug: https://bugzilla.xamarin.com/show_bug.cgi?id=6291
                //have a wait time for now.
                
                are.WaitOne(10 * 1000);

                if (process.HasExited)
                    return process.ExitCode;
                else //WTF? Should have exited dammit!
                {
                    process.Kill();
                    return 1;
                }
            }
        }

        private static string GetSafeExecutableName(string cmd)
        {
            string executableName = Path.GetFileName(cmd);

            if (!AllowedClientExecutables.Contains(executableName))
                throw new ArgumentException("Unsupported database client executable.", "cmd");

            return cmd;
        }

        private static string GetSafeArguments(string args)
        {
            if (string.IsNullOrEmpty(args))
                return string.Empty;

            if (args.IndexOfAny(DisallowedArgumentChars) >= 0)
                throw new ArgumentException("Invalid characters in process arguments.", "args");

            return args;
        }
    }
}
