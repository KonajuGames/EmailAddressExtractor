using System.Collections;
using System.Collections.Concurrent;

namespace MyAddressExtractor {
    internal sealed class FileCollection : IEnumerable<string>
    {
        private readonly ISet<string> Files;

        public FileCollection(IEnumerable<string> inputs)
        {
            this.Files = this.CreateSystemSet();
            this.Files.UnionWith(this.GatherFiles(inputs));
            this.Log();
        }

        private IEnumerable<string> GatherFiles(IEnumerable<string> inputs)
        {
            foreach (string file in inputs) {
                FileAttributes attributes = File.GetAttributes(file);
                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    foreach (string enumerated in Directory.EnumerateFiles(file))
                    {
                        yield return enumerated;
                    }
                }
                else if (File.Exists(file))
                {
                    yield return file;
                }
            }
        }

        /// <summary>
        /// Gather our <see cref="IEnumerable{String}"/> as a Set so that we don't have duplicates
        /// Windows uses a Case-Insensitive File system, so on it we can mostly ignore casing
        /// </summary>
        private ISet<string> CreateSystemSet()
        {
            OperatingSystem os = Environment.OSVersion;
            if (os.Platform is PlatformID.Win32S or PlatformID.Win32Windows or PlatformID.Win32NT or PlatformID.WinCE)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return new HashSet<string>();
        }

        private void Log()
        {
            var infos = new ConcurrentDictionary<string, ExtensionInfo>();
            foreach (string path in this)
            {
                var file = new FileInfo(path);
                if (file.Extension is {Length: >0} extension)
                {
                    var info = infos.GetOrAdd(extension, _ => new ExtensionInfo(extension));
                    info.AddFile(file);
                }
            }

            var sorted = infos.Values
                .OrderBy(info => info.Parsing.Read ? -info.Count : 0);
            Console.WriteLine($"Found {this.Files.Count:n0} files:");
            foreach (ExtensionInfo info in sorted)
            {
                Console.WriteLine($"{info.Extension}: {info.Count} files : {info.Bytes} bytes{(info.Parsing.Read ? string.Empty : $", Skipping ({info.Parsing.Error})")}");
            }
        }

        /// <inheritdoc />
        public IEnumerator<string> GetEnumerator()
            => this.Files.GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
            => this.GetEnumerator();

        private class ExtensionInfo {
            public readonly string Extension;
            public readonly FileExtensionParsing Parsing;

            public int Count { get; private set; }
            public long Bytes { get; private set; }

            public ExtensionInfo(string extension)
            {
                this.Extension = extension;
                this.Parsing = FileExtensionParsing.Get(extension);
            }

            public void AddFile(FileInfo info)
            {
                this.Count++;
                this.Bytes += info.Length;
            }
        }
    }
}
