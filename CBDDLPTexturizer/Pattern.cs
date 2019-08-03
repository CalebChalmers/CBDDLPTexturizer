using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBDDLPTexturizer
{
    public class Pattern
    {
        public int Width { get; }
        public int Height { get; }
        public bool[] Pixels { get; }

        public Pattern(string fileName)
        {
            using (Bitmap bitmap = new Bitmap(fileName))
            {
                this.Width = bitmap.Width;
                this.Height = bitmap.Height;
                this.Pixels = new bool[Width * Height];

                for (int y = 0; y < this.Height; y++)
                {
                    for (int x = 0; x < this.Width; x++)
                    {
                        Color color = bitmap.GetPixel(x, y);
                        bool pixel = (color.R + color.B + color.G) > 382; // white=true, black=false
                        this.Pixels[y * Width + x] = pixel;
                    }
                }
            }
        }

        public void ApplyTo(ref bool[] pixels, int width)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                int layerX = i % width;
                int layerY = i / width;
                int patternX = layerX % this.Width;
                int patternY = layerY % this.Height;
                int patternPixelIndex = patternY * this.Width + patternX;
                pixels[i] = pixels[i] && this.Pixels[patternPixelIndex];
            }
        }
    }
}
