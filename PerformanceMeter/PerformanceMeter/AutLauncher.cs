﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using log4net;
using PerformanceMeter.Settings;

namespace PerformanceMeter
{
    /// <summary>
    /// Runs Application Under Test on specified hardware and software resources.
    /// </summary>
    internal sealed class AutLauncher
    {
        ILog log = LogManager.GetLogger(typeof(AutLauncher));

        public Process Aut { get; private set; }

        public ProcessStartInfo AutStartInfo { get; private set; }

        public AutLauncher()
        {
            AutStartInfo = new ProcessStartInfo()
            {
                FileName = ArgumentParser.AutPath.FullName,
                RedirectStandardInput = ApplicationSettings.AutRedirectStandardInput,
                RedirectStandardOutput = ApplicationSettings.AutRedirectStandardOutput,
                RedirectStandardError = ApplicationSettings.AutRedirectStandardError,
                UseShellExecute = !(ApplicationSettings.AutRedirectStandardInput || ApplicationSettings.AutRedirectStandardOutput || ApplicationSettings.AutRedirectStandardError)
            };
        }

        public void StartAut()
        {
            Aut.ProcessorAffinity = new IntPtr(1);
            Aut.EnableRaisingEvents = true;
            Aut = Process.Start(AutStartInfo);
            log.Info($"AUT has been started. Starting time: {Aut.StartTime}");
            log.Info($"AUT Input arguments: {AutStartInfo.Arguments}");
            log.Info($"AUT Main Thread ID: {Aut.Id}");
            Aut.PriorityBoostEnabled = true;
            Aut.PriorityClass = ProcessPriorityClass.AboveNormal;
            log.Info($"AUT Process priority: {Aut.PriorityClass}");

            
            Aut.Exited += new EventHandler(AutExit);
            if (!AutStartInfo.RedirectStandardInput)
                log.Warn("AUT Standard Input redirection is disabled. AUT may require user interaction to receive input.");

            if (AutStartInfo.RedirectStandardOutput)
                Aut.OutputDataReceived += new DataReceivedEventHandler(LogAutOutput);
            else
                log.Warn("AUT Standard Output redirection is disabled. AUT may require user attention.");

            if (AutStartInfo.RedirectStandardError)
                Aut.ErrorDataReceived += new DataReceivedEventHandler(LogAutError);
            else
                log.Warn($"AUT Standard Error redirection is disabled. AUT may cause unhandled by Performance Meter errors.");
            Aut.WaitForExit();
        }

        private void AutExit(object sendingProcess, EventArgs output)
        {
            log.Info($"AUT has terminated with exit code: {Aut.ExitCode}. Exit time: {Aut.ExitTime.ToLongTimeString()}");
        }

        private void LogAutOutput(object sendingProcess, DataReceivedEventArgs output)
        {
            if (!string.IsNullOrEmpty(output.Data))
                log.Info(output.Data);
        }

        private void LogAutError(object sendingProcess, DataReceivedEventArgs output)
        {
            if (!string.IsNullOrEmpty(output.Data))
                log.Info(output.Data);
        }
    }
}