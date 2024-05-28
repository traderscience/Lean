using System;
using System.IO;
using System.Threading.Tasks;
using Xaye.Fred;
using System.Collections.Generic;

namespace QuantConnect.ToolBox.FredDownloader
{
    public class FredReleaseDownloader
    {
        private readonly Fred _fredClient;

        public FredReleaseDownloader(string apiKey)
        {
            _fredClient = new Fred(apiKey);
        }

        public void DownloadReleaseData(int releaseId, string outputDirectory)
        {
            // Fetch the release, which includes a list of series
            var releases = _fredClient.GetReleases();
            var release = _fredClient.GetRelease(releaseId);

            // Ensure the output directory exists
            Directory.CreateDirectory(outputDirectory);

            // Iterate through each series in the release and download the data
            foreach (var series in release.GetSeries())
            {
                // Fetch observations for the current series
                var observations = _fredClient.GetSeriesObservations(series.Id);

                // Prepare the output path for the current series
                string outputPath = Path.Combine(outputDirectory, $"{series.Id}.csv");

                // Write the series data to a CSV file in the QuantConnect format
                using (var writer = new StreamWriter(outputPath))
                {
                    foreach (var observation in observations)
                    {
                        string line = $"{observation.Date:yyyyMMdd},{observation.Value}";
                        writer.WriteLine(line);
                    }
                }
            }
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            string apiKey = "YOUR_FRED_API_KEY"; // Replace with your actual FRED API key
            int releaseId = 0; // Specify the FRED release ID
            string outputDirectory = @"Path\To\Your\Output\Directory"; // Define your output directory

            var downloader = new FredReleaseDownloader(apiKey);
            downloader.DownloadReleaseData(releaseId, outputDirectory);

            Console.WriteLine("Release data download complete.");
        }
    }
}
