/////////////////////////////////////////////////////////////////////////////////
//
// Camera RAW FileType plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (C) 2015-2017 Nicholas Hayes
// 
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using PaintDotNet;
using System;
using System.IO;

namespace RawFileTypePlugin
{
    internal static class PixMapReader
    {
        private const ushort GrayscaleBinary = 0x5035;
        private const ushort ColorBinary = 0x5036;

        /// <summary>
        /// Gets the next non-comment character from the stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>The next non-comment character read from the current stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
        private static char GetNextChar(Stream stream)
        {
            int next = stream.ReadByte();

            if (next == -1)
            {
                throw new EndOfStreamException();
            }

            char value = (char)next;

            // Skip any comments.
            if (value == '#')
            {
                do
                {
                    next = stream.ReadByte();

                    if (next == -1)
                    {
                        throw new EndOfStreamException();
                    }
                    value = (char)next;

                } while (value != '\r' && value != '\n');
            }

            return value;
        }

        /// <summary>
        /// Gets the next character from the stream that is not white space.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>The next character from the stream that is not white space.</returns>
        private static char GetNextNonWhiteSpaceChar(Stream stream)
        {
            char value;

            do
            {
                value = GetNextChar(stream);
            } while (char.IsWhiteSpace(value));

            return value;
        }

        /// <summary>
        /// Reads an ASCII encoded decimal number from the stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>A 4-byte signed integer read from the current stream.</returns>
        /// <exception cref="FormatException">Expected a decimal number.</exception>
        private static int ReadASCIIEncodedInt32(Stream stream)
        {
            char ch = GetNextNonWhiteSpaceChar(stream);

            if (ch < '0' || ch > '9')
            {
                throw new FormatException("Expected a decimal number");
            }

            int value = 0;

            do
            {
                int temp = ch - '0';
                value = (value * 10) + temp;
                ch = GetNextChar(stream);
            } while (ch >= '0' && ch <= '9');

            return value;
        }

        /// <summary>
        /// Reads the next byte from the stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>The next byte read from the current stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
        private static byte ReadByte(Stream stream)
        {
            int value = stream.ReadByte();

            if (value == -1)
            {
                throw new EndOfStreamException();
            }

            return (byte)value;
        }

        /// <summary>
        /// Reads a 2-byte unsigned integer from the stream in big endian byte order.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>A 2-byte unsigned integer read from this stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
        private static ushort ReadUInt16BigEndian(Stream stream)
        {
            int byte1 = stream.ReadByte();
            if (byte1 == -1)
            {
                throw new EndOfStreamException();
            }

            int byte2 = stream.ReadByte();
            if (byte2 == -1)
            {
                throw new EndOfStreamException();
            }

            return (ushort)((byte1 << 8) + byte2);
        }

        /// <summary>
        /// Creates the lookup table used to convert 16-bit pixel data to 8-bit.
        /// </summary>
        /// <returns>The resulting lookup table.</returns>
        private static byte[] CreateEightBitLookupTable()
        {
            byte[] map = new byte[65536];

            for (int i = 0; i < map.Length; i++)
            {
                map[i] = (byte)(((i * 255) + 32767) / 65535);
            }

            return map;
        }

        /// <summary>
        /// Decodes the gray scale PixMap into a BitmapLayer.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="sixteenBit"><c>true</c> if the image data is 16 bits-per-channel; otherwise, <c>false</c>.</param>
        /// <returns>A BitmapLayer containing the decoded image.</returns>
        private static unsafe BitmapLayer DecodeGrayScale(Stream stream, int width, int height, bool sixteenBit)
        {
            BitmapLayer layer = null;
            BitmapLayer tempLayer = null;

            try
            {
                tempLayer = Layer.CreateBackgroundLayer(width, height);
                Surface surface = tempLayer.Surface;

                if (sixteenBit)
                {
                    byte[] map = CreateEightBitLookupTable();

                    for (int y = 0; y < height; y++)
                    {
                        ColorBgra* p = surface.GetRowAddressUnchecked(y);
                        for (int x = 0; x < width; x++)
                        {
                            ushort value = ReadUInt16BigEndian(stream);
                            p->R = p->G = p->B = map[value];
                            p->A = 255;

                            p++;
                        }
                    }
                }
                else
                {
                    for (int y = 0; y < height; y++)
                    {
                        ColorBgra* p = surface.GetRowAddressUnchecked(y);
                        for (int x = 0; x < width; x++)
                        {
                            p->R = p->G = p->B = ReadByte(stream);
                            p->A = 255;

                            p++;
                        }
                    }
                }

                layer = tempLayer;
                tempLayer = null;
            }
            finally
            {
                if (tempLayer != null)
                {
                    tempLayer.Dispose();
                    tempLayer = null;
                }
            }

            return layer;
        }

        /// <summary>
        /// Decodes the color PixMap into a BitmapLayer.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="sixteenBit"><c>true</c> if the image data is 16 bits-per-channel; otherwise, <c>false</c>.</param>
        /// <returns>A BitmapLayer containing the decoded image.</returns>
        private static unsafe BitmapLayer DecodeColor(Stream stream, int width, int height, bool sixteenBit)
        {
            BitmapLayer layer = null;
            BitmapLayer tempLayer = null;

            try
            {
                tempLayer = Layer.CreateBackgroundLayer(width, height);
                Surface surface = tempLayer.Surface;

                if (sixteenBit)
                {
                    byte[] map = CreateEightBitLookupTable();

                    for (int y = 0; y < height; y++)
                    {
                        ColorBgra* p = surface.GetRowAddressUnchecked(y);
                        for (int x = 0; x < width; x++)
                        {
                            ushort red = ReadUInt16BigEndian(stream);
                            ushort green = ReadUInt16BigEndian(stream);
                            ushort blue = ReadUInt16BigEndian(stream);

                            p->R = map[red];
                            p->G = map[green];
                            p->B = map[blue];
                            p->A = 255;

                            p++;
                        }
                    }
                }
                else
                {
                    for (int y = 0; y < height; y++)
                    {
                        ColorBgra* p = surface.GetRowAddressUnchecked(y);
                        for (int x = 0; x < width; x++)
                        {
                            p->R = ReadByte(stream);
                            p->G = ReadByte(stream);
                            p->B = ReadByte(stream);
                            p->A = 255;

                            p++;
                        }
                    }
                }

                layer = tempLayer;
                tempLayer = null;
            }
            finally
            {
                if (tempLayer != null)
                {
                    tempLayer.Dispose();
                    tempLayer = null;
                }
            }

            return layer;
        }

        /// <summary>
        /// Decodes a PNM file.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <returns>A single layer Document containing the decoded image.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        /// <exception cref="FormatException">
        /// The file is not a supported format.
        /// -or-
        /// The image dimensions are invalid.
        /// -or-
        /// The file uses an unsupported color depth.
        /// </exception>
        internal static Document DecodePNM(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            ushort format = ReadUInt16BigEndian(stream);
            if (format != GrayscaleBinary && format != ColorBinary)
            {
                throw new FormatException("The file is not a supported format.");
            }

            int width = ReadASCIIEncodedInt32(stream);
            int height = ReadASCIIEncodedInt32(stream);

            if (width < 1 || height < 1)
            {
                throw new FormatException("The image dimensions are invalid.");
            }

            int maxValue = ReadASCIIEncodedInt32(stream);
            if (maxValue != 255 && maxValue != 65535)
            {
                throw new FormatException("The file uses an unsupported color depth.");
            }

            bool sixteenBit = maxValue == 65535;

            Document document = null;
            Document tempDocument = null;

            try
            {
                tempDocument = new Document(width, height);

                if (format == ColorBinary)
                {
                    tempDocument.Layers.Add(DecodeColor(stream, width, height, sixteenBit));
                }
                else
                {
                    tempDocument.Layers.Add(DecodeGrayScale(stream, width, height, sixteenBit));
                }

                document = tempDocument;
                tempDocument = null;
            }
            finally
            {
                if (tempDocument != null)
                {
                    tempDocument.Dispose();
                    tempDocument = null;
                }
            }

            return document;
        }
    }
}
