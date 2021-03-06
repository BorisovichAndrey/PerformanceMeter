﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        private AutLogger autLogger;

        public static Process Aut { get; private set; }

        public static Stopwatch ElapsedTime { get; private set; }

        public ProcessStartInfo AutStartInfo { get; private set; }

        public CancellationTokenSource InputCancellationSource { get; private set; }

        public AutLauncher()
        {
            AutStartInfo = new ProcessStartInfo()
            {
                FileName = ArgumentParser.AutPath.FullName,
                Arguments = ArgumentParser.AutArgs,
                RedirectStandardInput = ApplicationSettings.AutRedirectStandardInput,
                RedirectStandardOutput = ApplicationSettings.AutRedirectStandardOutput,
                RedirectStandardError = ApplicationSettings.AutRedirectStandardError,
                UseShellExecute = !(ApplicationSettings.AutRedirectStandardInput || ApplicationSettings.AutRedirectStandardOutput || ApplicationSettings.AutRedirectStandardError),
                CreateNoWindow = !ApplicationSettings.AutCreateWindow,
            };
            InputCancellationSource = new CancellationTokenSource();
            Aut = new Process{ StartInfo = AutStartInfo };
            autLogger = new AutLogger();
            ElapsedTime = new Stopwatch();
            AutSetEvents();
        }

        public void StartAut()
        {
            Aut.Start();
            ElapsedTime.Start();
            if (SystemInfo.GetAffinity() != null)
                Aut.ProcessorAffinity = SystemInfo.GetAffinity().Value;
            Aut.PriorityClass = Enum.Parse<ProcessPriorityClass>(ArgumentParser.AutPriority);
            Aut.BeginOutputReadLine();
            Aut.BeginErrorReadLine();
            log.Info($"AUT has been started. Starting time: {Aut.StartTime}");
            log.Info($"AUT Input arguments: {AutStartInfo.Arguments}");
            log.Info($"AUT Main Thread ID: {Aut.Id}");
            log.Info($"AUT Process priority: {Aut.PriorityClass}");
        }

        private void AutSetEvents()
        {
            Aut.EnableRaisingEvents = true;
            Aut.Exited += new EventHandler(StopTimer);
            Aut.Exited += new EventHandler(autLogger.LogExit);
            
            if (!AutStartInfo.RedirectStandardInput)
                log.Warn("AUT Standard Input redirection is disabled. AUT may require user interaction to receive input.");
            if (AutStartInfo.RedirectStandardOutput)
                Aut.OutputDataReceived += new DataReceivedEventHandler(autLogger.LogOutput);
            else
                log.Warn("AUT Standard Output redirection is disabled. AUT may require user attention.");
            if (AutStartInfo.RedirectStandardError)
                Aut.ErrorDataReceived += new DataReceivedEventHandler(autLogger.LogError);
            else
                log.Warn($"AUT Standard Error redirection is disabled. AUT may cause unhandled by Performance Meter errors.");
        }

        public void StopTimer(object sendingProcess, EventArgs output) => ElapsedTime.Stop();

        public void ReadConsoleAsync()
        {
            log.Info("-- Redirecting user input to AUT --");
            Task autInput = null;
            if (!Aut.HasExited)
                autInput = Task.Run(() => Aut.StandardInput.WriteLine(Console.ReadLine()), InputCancellationSource.Token);
            while(!Aut.HasExited)
            {
                if (autInput.Status != TaskStatus.Running)
                    autInput = Task.Run(() => Aut.StandardInput.WriteLine(Console.ReadLine()), InputCancellationSource.Token);
            }
        }

        ~AutLauncher()
        {
            Aut.Exited -= StopTimer;
            Aut.Exited -= autLogger.LogExit;
        }
    }
}