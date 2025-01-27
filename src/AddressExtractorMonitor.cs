using System.Collections.Concurrent;
using System.Diagnostics;

namespace MyAddressExtractor {
    public class AddressExtractorMonitor : IAsyncDisposable {
        private readonly AddressExtractor Extractor = new();
        protected readonly IDictionary<string, Count> Files = new ConcurrentDictionary<string, Count>();
        protected readonly ISet<string> Addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        protected readonly Stopwatch Stopwatch = Stopwatch.StartNew();
        private readonly Timer Timer;

        public AddressExtractorMonitor(): this(TimeSpan.FromMinutes(1)) {}
        public AddressExtractorMonitor(TimeSpan iterate)
        {
            this.Timer = new Timer(_ => this.Log(), null, iterate, iterate);
        }

        public async ValueTask RunAsync(IEnumerable<string> files, CancellationToken cancellation = default)
        {
            foreach (var inputFilePath in files)
            {
                var count = new Count();
                var addresses = 0;

                this.Files.Add(inputFilePath, count);
                await foreach(var email in this.Extractor.ExtractAddressesFromFileAsync(inputFilePath, cancellation))
                {
                    this.Addresses.Add(email);
                    count.Value = addresses++;
                }
            }
        }

        public virtual void Log()
        {
            Console.WriteLine($"Extraction time: {this.Stopwatch.ElapsedMilliseconds:n0}ms");
            Console.WriteLine($"Addresses extracted: {this.Addresses.Count:n0}");
            // Extraction does not currently process per row, so we do not have the row count at this time
            long rate = (long)(this.Addresses.Count / (this.Stopwatch.ElapsedMilliseconds / 1000.0));
            Console.WriteLine($"Extraction rate: {rate:n0}/s\n");
        }

        public async ValueTask SaveAsync(string outputFilePath, string reportFilePath, CancellationToken cancellation = default)
        {
            await this.Extractor.SaveAddressesAsync(outputFilePath, this.Addresses, cancellation);
            await this.Extractor.SaveReportAsync(reportFilePath, this.Files, cancellation);
        }

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);

            await this.Timer.DisposeAsync();
            this.Stopwatch.Stop();
        }
    }
}
