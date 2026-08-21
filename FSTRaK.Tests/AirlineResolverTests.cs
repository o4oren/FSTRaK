using System.IO;
using FSTRaK.Models;
using Xunit;

namespace FSTRaK.Tests
{
    public class AirlineResolverTests
    {
        private const string SampleCsv =
            "icao,name,callsign,country\n" +
            "BAW,British Airways,SPEEDBIRD,United Kingdom\n" +
            "ELY,El Al Israel Airlines,ELAL,Israel\n" +
            ",No Icao Airline,NONE,Nowhere\n" +
            "XXX,,BLANK,Nowhere\n";

        [Fact]
        public void ReadCsvAsDictionary_ParsesValidRowsAndSkipsBlankIcaoOrName()
        {
            var airlines = AirlineResolver.ReadCsvAsDictionary(new StringReader(SampleCsv));

            Assert.Equal(2, airlines.Count);
            Assert.Equal("British Airways", airlines["BAW"]);
            Assert.Equal("El Al Israel Airlines", airlines["ELY"]);
            Assert.False(airlines.ContainsKey("XXX"));
        }

        [Fact]
        public void BundledAirlinesCsv_ResolvesKnownCarriers()
        {
            // Walk up from the test output directory to the repo root (marked by FSTRaK.sln).
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FSTRaK.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);

            var csvPath = Path.Combine(dir.FullName, "FSTRaK", "Resources", "Data", "airlines.csv");
            var airlines = AirlineResolver.ReadCsvAsDictionary(csvPath);

            Assert.True(airlines.Count > 5000);
            Assert.Equal("British Airways", airlines["BAW"]);
            Assert.Equal("El Al Israel Airlines", airlines["ELY"]);
            Assert.Equal("Lufthansa", airlines["DLH"]);
        }
    }
}
