using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Diagnostics;

namespace CBDDLPTexturizer
{
    class Program
    {
        private static readonly uint VALID_FILE_SIGNATURE = 0x12FD0019;
        private static readonly int LAYERDEF_SIZE = 9 * 4;
        private static readonly int OFFSET_LAYERDEF_DATA_ADDRESS = 12;
        private static readonly int OFFSET_LAYERDEF_DATA_LENGTH = 16;

        private static void LogError(string msg)
        {
            Console.Error.WriteLine("ERROR: " + msg);
        }

        private static void PrintHelpMessage()
        {
            string version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;

            Console.WriteLine("CBDDLP Texturizer v" + version);
            Console.WriteLine("Applies a specified pixel pattern to the top layers of a *.cbddlp or *.photon file to add texture to 3D prints.");
            Console.WriteLine("");
            Console.WriteLine("Usage: CBDDLPTexturizer.exe <input file> <pattern png> <number of layers to modify>");
        }

        static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                PrintHelpMessage();
                return 1;
            }

            string inputPath = args[0];
            string patternPath = args[1];
            int patternLayerCount;

            if (!int.TryParse(args[2], out patternLayerCount) || patternLayerCount == 0)
            {
                LogError("Invalid layer count");
                return 1;
            }

            if (!File.Exists(inputPath))
            {
                LogError($"Input file not found \"{inputPath}\"");
                return 1;
            }

            if (!File.Exists(patternPath))
            {
                LogError($"Pattern file not found \"{patternPath}\"");
                return 1;
            }

            // Get pattern data
            Pattern pattern;
            try
            {
                pattern = new Pattern(patternPath);
            }
            catch (ArgumentException)
            {
                LogError("Invalid pattern image");
                return 1;
            }

            string outputPath = Path.Combine(Path.GetDirectoryName(inputPath), Path.GetFileNameWithoutExtension(inputPath) + "_textured" + Path.GetExtension(inputPath));
            string tmpOutputPath = outputPath + ".tmp";

            if (File.Exists(outputPath))
            {
                LogError($"Can't write to \"{outputPath}\". File already exists");
                return 1;
            }

            using (FileStream inStream = File.OpenRead(inputPath))
            {
                // Get file signature and version
                byte[] headerBuffer = new byte[8];
                inStream.Read(headerBuffer, 0, 8);
                uint signature = BitConverter.ToUInt32(headerBuffer, 0);
                uint version = BitConverter.ToUInt32(headerBuffer, 4);

                if (signature != VALID_FILE_SIGNATURE) // Is this a CBDDLP file?
                {
                    LogError("Invalid file format");
                    return 1;
                }

                if (version != 1 && version != 2)
                {
                    LogError("Unsupported CBDDLP version: " + version);
                    return 1;
                }

                // Get image resolution
                inStream.Position = 0x34;
                inStream.Read(headerBuffer, 0, 8);
                int resolutionX = BitConverter.ToInt32(headerBuffer, 0);
                int resolutionY = BitConverter.ToInt32(headerBuffer, 4);

                // Get layer count and address of layerDefs
                inStream.Position = 0x40;
                inStream.Read(headerBuffer, 0, 8);
                uint startLayerDefsAddress = BitConverter.ToUInt32(headerBuffer, 0);
                int layerCount = BitConverter.ToInt32(headerBuffer, 4);

                Console.WriteLine("CBDDLP Version: " + version);
                Console.WriteLine($"Image Resolution: {resolutionX}x{resolutionY}");
                Console.WriteLine("Total Layers: " + layerCount);
                Console.WriteLine();

                Console.WriteLine($"Creating new file: \"{outputPath}\"");
                File.Copy(inputPath, tmpOutputPath);

                using (FileStream outStream = File.OpenWrite(tmpOutputPath))
                {
                    // Read relevant layer defs
                    uint layerDataWriteAddress = 0;
                    byte[] layerDefBuffer = new byte[LAYERDEF_SIZE];
                    bool[] pixels = new bool[resolutionX * resolutionY];

                    for (int i = 0; i < patternLayerCount; i++)
                    {
                        int layer = layerCount - patternLayerCount + i;
                        uint layerDefAddress = startLayerDefsAddress + (uint)(LAYERDEF_SIZE * layer);

                        Console.WriteLine();
                        Console.WriteLine($"--- Layer {layer+1} ---");

                        // Get layer data (image) address and length
                        Console.WriteLine("Reading old metadata");
                        inStream.Position = layerDefAddress;
                        inStream.Read(layerDefBuffer, 0, LAYERDEF_SIZE);

                        uint layerDataAddress = BitConverter.ToUInt32(layerDefBuffer, OFFSET_LAYERDEF_DATA_ADDRESS);
                        int layerDataLength = BitConverter.ToInt32(layerDefBuffer, OFFSET_LAYERDEF_DATA_LENGTH);

                        if (i == 0)
                        {
                            layerDataWriteAddress = layerDataAddress;
                        }

                        // Read and decode layer pixel data into memory
                        Console.WriteLine("Reading old pixel data");
                        inStream.Position = layerDataAddress;
                        PixelHelper.ReadPixelData(inStream, layerDataLength, ref pixels);

                        // Apply pattern to pixel data
                        Console.WriteLine("Applying pattern");
                        pattern.ApplyTo(ref pixels, resolutionX);

                        // Encode and write result to output file
                        Console.WriteLine("Writing new pixel data");
                        outStream.Position = layerDataWriteAddress;
                        int writeLength = PixelHelper.WritePixelData(outStream, pixels);

                        // Write updated layer data length and address to output file
                        Console.WriteLine("Writing new metadata");
                        outStream.Position = layerDefAddress + OFFSET_LAYERDEF_DATA_ADDRESS;
                        outStream.Write(BitConverter.GetBytes(layerDataWriteAddress), 0, 4);
                        outStream.Write(BitConverter.GetBytes(writeLength), 0, 4);

                        layerDataWriteAddress += (uint)writeLength;
                    }

                    outStream.SetLength(layerDataWriteAddress);
                }
            }

            File.Move(tmpOutputPath, outputPath);

            Console.WriteLine();
            Console.WriteLine("Done!");

            return 0;
        }
    }
}
