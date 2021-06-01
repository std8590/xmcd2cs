using System;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

namespace xmcd2cs
{
    public static class BinaryExtractor
    {
        private static readonly XNamespace ws = "http://schemas.mathsoft.com/worksheet30";
        public static void Extract(string dstDir, string file)
        {
            var root = XElement.Load(file);

            var binaryContent = root.Element(ws + "binaryContent");
            if (binaryContent == null)
            {
                return;
            }
            foreach (var bin in binaryContent.Elements())
            {
                var id = bin.Attribute("item-id").Value;
                var enc = bin.Attribute("content-encoding");
                var ext = ".png";
                var valueBytes = Convert.FromBase64String(bin.Value);
                if (enc != null)
                {
                    using (var inStream = new MemoryStream(valueBytes))
                    using (var bigStream = new GZipStream(inStream, CompressionMode.Decompress))
                    using (var bigStreamOut = new MemoryStream())
                    {
                        bigStream.CopyTo(bigStreamOut);
                        valueBytes = bigStreamOut.ToArray();
                    }
                }

                var binDir = Path.Combine(dstDir, "bin");
                if (!Directory.Exists(binDir))
                {
                    Directory.CreateDirectory(binDir);
                }
                var fileDir = Path.Combine(binDir, Path.GetFileNameWithoutExtension(file));
                if (!Directory.Exists(fileDir))
                {
                    Directory.CreateDirectory(fileDir);
                }
                var fileName = Path.Combine(fileDir, "item" + id + ext);
                File.WriteAllBytes(fileName, valueBytes);
            }
        }
    }
}
