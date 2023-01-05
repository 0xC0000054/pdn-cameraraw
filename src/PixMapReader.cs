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
using System;
using System.IO;

namespace RawFileTypePlugin
{
    internal sealed class PixMapReader : IDisposable
    {
        private const ushort GrayscaleBinary = 0x5035;
        private const ushort ColorBinary = 0x5036;

        private EndianBinaryReader reader;

        /// <summary>
        /// Initializes a new instance of the <see cref="PixMapReader"/> class.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="leaveOpen">
        /// <see langword="true"/> if the stream should be left open when the class is disposed; otherwise, <see langword="false"/>.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        public PixMapReader(Stream stream, bool leaveOpen)
        {
            // LibRaw always writes PNM files using big-endian byte order.
            reader = new EndianBinaryReader(stream, Endianess.Big, leaveOpen);
        }

        /// <summary>
        /// Decodes a PNM file.
        /// </summary>
        /// <returns>A single layer Document containing the decoded image.</returns>
        /// <exception cref="FormatException">
        /// The file is not a supported format.
        /// -or-
        /// The image dimensions are invalid.
        /// -or-
        /// The file uses an unsupported color depth.
        /// </exception>
        /// <exception cref="ObjectDisposedException">The class has been disposed.</exception>
        public Document DecodePNM()
        {
            if (reader is null)
            {
                throw new ObjectDisposedException(nameof(PixMapReader));
            }

            ushort format = reader.ReadUInt16();
            if (format != GrayscaleBinary && format != ColorBinary)
            {
                throw new FormatException("The file is not a supported format.");
            }

            int width = ReadASCIIEncodedInt32();
            int height = ReadASCIIEncodedInt32();

            if (width < 1 || height < 1)
            {
                throw new FormatException("The image dimensions are invalid.");
            }

            int maxValue = ReadASCIIEncodedInt32();
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
                    tempDocument.Layers.Add(DecodeColor(width, height, sixteenBit));
                }
                else
                {
                    tempDocument.Layers.Add(DecodeGrayScale(width, height, sixteenBit));
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

        public void Dispose()
        {
            if (reader != null)
            {
                reader.Dispose();
                reader = null;
            }
        }

        /// <summary>
        /// Gets the next non-comment character from the stream.
        /// </summary>
        /// <returns>The next non-comment character read from the current stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
        private char GetNextChar()
        {
            char value = (char)reader.ReadByte();

            // Skip any comments.
            if (value == '#')
            {
                do
                {
                    value = (char)reader.ReadByte();

                } while (value != '\r' && value != '\n');
            }

            return value;
        }

        /// <summary>
        /// Gets the next character from the stream that is not white space.
        /// </summary>
        /// <returns>The next character from the stream that is not white space.</returns>
        private char GetNextNonWhiteSpaceChar()
        {
            char value;

            do
            {
                value = GetNextChar();
            } while (char.IsWhiteSpace(value));

            return value;
        }

        /// <summary>
        /// Reads an ASCII encoded decimal number from the stream.
        /// </summary>
        /// <returns>A 4-byte signed integer read from the current stream.</returns>
        /// <exception cref="FormatException">Expected a decimal number.</exception>
        private int ReadASCIIEncodedInt32()
        {
            char ch = GetNextNonWhiteSpaceChar();

            if (ch < '0' || ch > '9')
            {
                throw new FormatException("Expected a decimal number");
            }

            int value = 0;

            do
            {
                int temp = ch - '0';
                value = (value * 10) + temp;
                ch = GetNextChar();
            } while (ch >= '0' && ch <= '9');

            return value;
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
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="sixteenBit"><c>true</c> if the image data is 16 bits-per-channel; otherwise, <c>false</c>.</param>
        /// <returns>A BitmapLayer containing the decoded image.</returns>
        private unsafe BitmapLayer DecodeGrayScale(int width, int height, bool sixteenBit)
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
                            ushort value = reader.ReadUInt16();
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
                            p->R = p->G = p->B = reader.ReadByte();
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
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="sixteenBit"><c>true</c> if the image data is 16 bits-per-channel; otherwise, <c>false</c>.</param>
        /// <returns>A BitmapLayer containing the decoded image.</returns>
        private unsafe BitmapLayer DecodeColor(int width, int height, bool sixteenBit)
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
                            ushort red = reader.ReadUInt16();
                            ushort green = reader.ReadUInt16();
                            ushort blue = reader.ReadUInt16();

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
                            p->R = reader.ReadByte();
                            p->G = reader.ReadByte();
                            p->B = reader.ReadByte();
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
    }
}
