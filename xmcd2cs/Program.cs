using System.Diagnostics;
using System.IO;

namespace xmcd2cs
{
    class Program
    {
        static void Main(string[] args)
        {
            var parser = new Parser();
            string dir = args[0];
            var files = Directory.GetFiles(dir, "*.xmcd", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var content = parser.Parse(file);
                var dstDir = Path.GetDirectoryName(file).Replace(dir, Path.Combine(dir, "Net"));
                if(!Directory.Exists(dstDir))
                {
                    Directory.CreateDirectory(dstDir);
                }
                var source = Path.Combine(dstDir, Path.GetFileNameWithoutExtension(file) + ".cs");
                File.WriteAllText(source, content);
                BinaryExtractor.Extract(dstDir, file);
            };

            foreach (var impl in parser.ToImplement)
            {
                Debug.WriteLine($"Implement: {impl}");
            }
        }
    }
}
