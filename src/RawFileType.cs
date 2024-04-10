/////////////////////////////////////////////////////////////////////////////////
//
// Camera RAW FileType plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (C) 2015-2018, 2021, 2022, 2023, 2024 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using PaintDotNet;
using System;
using System.Diagnostics;
using System.IO;

namespace RawFileTypePlugin
{
    [PluginSupportInfo(typeof(PluginSupportInfo))]
    public sealed class RawFileType : FileType
    {
        private static readonly string ExecutablePath = Path.Combine(Path.GetDirectoryName(typeof(RawFileType).Assembly.Location), "LibRaw\\dcraw_emu.exe");
        private static readonly string OptionsFilePath = Path.Combine(Path.GetDirectoryName(typeof(RawFileType).Assembly.Location), "RawFileTypeOptions.txt");

        private readonly IFileTypeHost host;

        public RawFileType(IFileTypeHost host) : base(
            "RAW File",
            new FileTypeOptions()
            {
                LoadExtensions = new string[] { ".3fr", ".arw", ".cr2", ".cr3", ".crw", ".dcr", ".dng", ".erf", ".kc2", ".kdc", ".mdc", ".mef", ".mos", ".mrw",
                    ".nef", ".nrw", ".orf", ".pef", ".ptx", ".pxn", ".raf", ".raw", ".rw2", ".sr2", ".srf", ".srw", ".x3f" },
                SaveExtensions = Array.Empty<string>()
            })
        {
            this.host = host;
        }

        private static string RemoveCommentsAndWhiteSpace(string line)
        {
            int commentIndex = line.IndexOf(";", StringComparison.Ordinal);
            if (commentIndex >= 0)
            {
                return line.Substring(0, commentIndex).Trim();
            }
            else
            {
                return line.Trim();
            }
        }

        private static string GetDCRawOptions()
        {
            string options = string.Empty;

            using (StreamReader reader = new(OptionsFilePath, System.Text.Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = RemoveCommentsAndWhiteSpace(line);
                    if (trimmed.Length > 0)
                    {
                        options = trimmed;
                        break;
                    }
                }
            }

            return options;
        }

        private Document GetRAWImageDocument(string file)
        {
            Document doc = null;

            string options = GetDCRawOptions();
            bool useTIFF = options.Contains("-T");

            // The processed image is saved to a temporary file to allow us to read the process exit code
            // and standard error output.
            string outputImagePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            string arguments = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                             "{0} -Z \"{1}\" \"{2}\"",
                                             options,
                                             outputImagePath,
                                             file);
            ProcessStartInfo startInfo = new(ExecutablePath, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using (Process process = new())
            {
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();

                    if (string.IsNullOrWhiteSpace(error))
                    {
                        error = $"dcraw_emu returned exit code {process.ExitCode}.";
                    }

                    throw new FormatException(error);
                }

                FileStreamOptions fileStreamOptions = new()
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.Read,
                    Options = FileOptions.DeleteOnClose
                };

                if (!useTIFF)
                {
                    // The PixMapReader performs its own buffering and uses sequential reads.
                    fileStreamOptions.BufferSize = 1;
                    fileStreamOptions.Options |= FileOptions.SequentialScan;
                }

                using (FileStream stream = new(outputImagePath, fileStreamOptions))
                {
                    if (useTIFF)
                    {
                        IFileTypesService fileTypesService = host.Services.GetService<IFileTypesService>();

                        IFileTypeInfo tiffFileTypeInfo = fileTypesService?.FindFileTypeForLoadingExtension(".tif");

                        if (tiffFileTypeInfo != null)
                        {
                            FileType tiffFileType = tiffFileTypeInfo.GetInstance();

                            doc = tiffFileType.Load(stream);
                        }
                        else
                        {
                            throw new FormatException($"Failed to get the {nameof(IFileTypeInfo)} for the TIFF FileType.");
                        }
                    }
                    else
                    {
                        using (PixMapReader reader = new(stream, leaveOpen: true))
                        {
                            doc = reader.DecodePNM();
                        }
                    }
                }
            }

            return doc;
        }

        protected override Document OnLoad(Stream input)
        {
            Document doc = null;
            string tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                // Write the input stream to a temporary file for LibRaw to load.
                FileStreamOptions fileStreamOptions = new()
                {
                    Mode = FileMode.Create,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    PreallocationSize = input.CanSeek ? input.Length : 0
                };

                using (FileStream output = new(tempFile, fileStreamOptions))
                {
                    input.CopyTo(output);
                }

                doc = GetRAWImageDocument(tempFile);
            }
            finally
            {
                File.Delete(tempFile);
            }

            return doc;
        }
    }
}
