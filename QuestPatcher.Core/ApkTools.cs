﻿using Serilog.Core;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace QuestPatcher.Core
{
    /// <summary>
    /// Abstraction around uber-apk-signer used during patching.
    /// Used to abstract apktool as well, but QuestPatcher no longer uses this, relying on its own manifest decompiler instead.
    /// </summary>
    public class ApkTools
    {
        private readonly Logger _logger;
        private readonly ExternalFilesDownloader _filesDownloader;
        private string _javaExecutableName;

        public ApkTools(Logger logger, ExternalFilesDownloader filesDownloader)
        {
            _logger = logger;
            _filesDownloader = filesDownloader;
            _javaExecutableName = OperatingSystem.IsWindows() ? "java.exe" : "java";
        }

        private Task<ProcessOutput> InvokeJava(string args)
        {
            return ProcessUtil.InvokeAndCaptureOutput(_javaExecutableName, args);
        }

        private async Task<ProcessOutput> InvokeJavaTool(ExternalFileType type, string args)
        {
            string path = await _filesDownloader.GetFileLocation(type); // Downloads the file if it hasn't been already

            ProcessOutput output = await InvokeJava($"-jar \"{path}\" {args}");
            _logger.Verbose($"Standard output: {output.StandardOutput}");
            if (output.ErrorOutput.Length > 0)
            {
                _logger.Verbose($"Error output: {output.ErrorOutput}");
            }

            return output;
        }

        public async Task PrepareJavaInstall()
        {
            bool isInstalled = true;
            try
            {
                string javaVersion = (await InvokeJava("-version")).ErrorOutput;

                // Mac OS displays a special message when you try to use Java when it isn't installed
                if (javaVersion.StartsWith("The operation couldn’t be completed. Unable to locate a Java Runtime.") || javaVersion.StartsWith("No Java runtime present, requesting install."))
                {
                    isInstalled = false;
                }
                else
                {
                    _logger.Information("Located Java install on PATH");
                    _logger.Debug($"Java version: {javaVersion}");
                }
            }
            catch (Win32Exception) // Thrown if the executable is missing, even on Linux. Slight .NET quirk.
            {
                isInstalled = false;
            }

            if (!isInstalled)
            {
                // Download Java if it is not installed
                _javaExecutableName = await _filesDownloader.GetFileLocation(ExternalFileType.Jre);
            }
        }

        public Task SignApk(string apkPath)
        {
            return InvokeJavaTool(ExternalFileType.UberApkSigner, $"--debug --skipZipAlign --apks \"{apkPath}\"");
        }
    }
}
