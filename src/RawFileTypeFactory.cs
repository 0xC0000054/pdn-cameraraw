/////////////////////////////////////////////////////////////////////////////////
//
// Camera RAW FileType plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (C) 2015-2018, 2021, 2022, 2023 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using PaintDotNet;

namespace RawFileTypePlugin
{
    public sealed class RawFileTypeFactory : IFileTypeFactory2
    {
        public FileType[] GetFileTypeInstances(IFileTypeHost host)
        {
            return new FileType[] { new RawFileType(host) };
        }
    }
}
