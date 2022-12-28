/////////////////////////////////////////////////////////////////////////////////
//
// Camera RAW FileType plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (C) 2015-2018, 2021, 2022 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using PaintDotNet;
using PaintDotNet.AppModel;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace RawFileTypePlugin
{
    [PluginSupportInfo(typeof(PluginSupportInfo))]
    public sealed class RawFileType : FileType
    {
        private static readonly string ExecutablePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "LibRaw\\dcraw_emu.exe");
        private static readonly string OptionsFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "RawFileTypeOptions.txt");

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

            using (StreamReader reader = new StreamReader(OptionsFilePath, System.Text.Encoding.UTF8))
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

            string outputImagePath = string.Empty;
            bool useTIFF = options.Contains("-T");

            if (useTIFF)
            {
                // WIC requires a stream that supports seeking, so we save the image to a temporary file.
                outputImagePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            }

            // The - output file name instructs the LibRaw dcraw-emu example program
            // that the image data should be written to standard output.
            string arguments = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                             "{0} -Z {1} \"{2}\"",
                                             options,
                                             useTIFF ? "\"" + outputImagePath + "\"" : "-",
                                             file);
            ProcessStartInfo startInfo = new ProcessStartInfo(ExecutablePath, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = !useTIFF
            };

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();

                if (useTIFF)
                {
                    process.WaitForExit();

                    FileStreamOptions fileStreamOptions = new FileStreamOptions
                    {
                        Mode = FileMode.Open,
                        Access = FileAccess.Read,
                        Share = FileShare.Read,
                        Options = FileOptions.DeleteOnClose
                    };

                    using (FileStream stream = new(outputImagePath, fileStreamOptions))
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
                }
                else
                {
                    using (PixMapReader reader = new PixMapReader(process.StandardOutput.BaseStream, leaveOpen: true))
                    {
                        doc = reader.DecodePNM();
                    }

                    process.WaitForExit();
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
                using (FileStream output = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    if (input.CanSeek)
                    {
                        output.SetLength(input.Length);
                    }

                    // 81920 is the largest multiple of 4096 that is under the large object heap limit (85,000 bytes).
                    byte[] buffer = new byte[81920];

                    int bytesRead;
                    while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, bytesRead);
                    }
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
