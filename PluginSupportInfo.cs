/////////////////////////////////////////////////////////////////////////////////
//
// Camera RAW FileType plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (C) 2015-2018, 2021 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using PaintDotNet;
using System;
using System.Reflection;

namespace RawFileTypePlugin
{
    public sealed class PluginSupportInfo : IPluginSupportInfo
    {
        public string Author
        {
            get
            {
                return ((AssemblyCompanyAttribute)(typeof(RawFileType).Assembly.GetCustomAttributes(typeof(AssemblyCompanyAttribute), false)[0])).Company;
            }
        }

        public string Copyright
        {
            get
            {
                return ((AssemblyCopyrightAttribute)(typeof(RawFileType).Assembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false)[0])).Copyright;
            }
        }

        public string DisplayName
        {
            get
            {
                return "Raw FileType";
            }
        }

        public Version Version
        {
            get
            {
                return typeof(RawFileType).Assembly.GetName().Version;
            }
        }

        public Uri WebsiteUri
        {
            get
            {
                return new Uri("https://forums.getpaint.net/index.php?/topic/31998-raw-filetype");
            }
        }
    }
}
