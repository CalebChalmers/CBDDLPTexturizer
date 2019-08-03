using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBDDLPTexturizer
{
    public static class PixelHelper
    {
        public static readonly int CHUNK_SIZE = 1024;

        /// <summary>Read layer pixel data and decode into bool array (true=white, false=black)</summary>
        public static void ReadPixelData(FileStream inStream, int dataLength, ref bool[] pixels)
        {
            byte[] chunkBuffer = new byte[CHUNK_SIZE];
            int pixelIndex = 0;

            for (int remLen = dataLength; remLen > 0; remLen -= CHUNK_SIZE)
            {
                int readSize = Math.Min(remLen, CHUNK_SIZE);
                inStream.Read(chunkBuffer, 0, readSize);

                for (int i = 0; i < readSize; i++)
                {
                    byte b = chunkBuffer[i];
                    bool color = (b & 0x80) > 0;
                    int repeat = b & 0x7F;

                    for (int j = 0; j < repeat; j++)
                    {
                        pixels[pixelIndex++] = color;
                    }
                }
            }
        }

        /// <summary>Encode pixel bool array and write to output file</summary>
        public static int WritePixelData(FileStream outStream, bool[] pixels)
        {
            int dataLength = 0;
            int repeat = 0;

            byte[] chunkBuffer = new byte[CHUNK_SIZE];
            int indexInChunk = 0;

            for (int i = 0; i < pixels.Length; i++)
            {
                bool isLastPixel = i == pixels.Length - 1;
                bool pixel = pixels[i];

                repeat++;

                if (isLastPixel || repeat == 125 || pixel != pixels[i + 1])
                {
                    chunkBuffer[indexInChunk++] = (byte)((pixel ? 0x80 : 0x00) | repeat);
                    dataLength++;
                    repeat = 0;
                }

                if (isLastPixel || indexInChunk == CHUNK_SIZE)
                {
                    outStream.Write(chunkBuffer, 0, indexInChunk);
                    indexInChunk = 0;
                }
            }

            return dataLength;
        }
    }
}
