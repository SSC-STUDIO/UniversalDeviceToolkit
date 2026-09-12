using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.PackageDownloader;
using Xunit;

namespace UniversalDeviceToolkit.Tests.PackageDownloader;

[Trait("Category", TestCategories.Unit)]
public sealed class VantagePackageDownloaderTests
{
    [Fact]
    public async Task GetPackagesAsync_WithMalformedPackageXml_ShouldSkipInvalidEntries()
    {
        const string catalogXml = """
                                  <packages>
                                    <package>
                                      <location>https://download.lenovo.com/packages/good.xml</location>
                                      <category>Driver</category>
                                    </package>
                                    <package>
                                      <location>https://download.lenovo.com/packages/bad.xml</location>
                                      <category>Driver</category>
                                    </package>
                                  </packages>
                                  """;

        const string goodPackageXml = """
                                      <Package id="GOOD01" version="1.0">
                                        <Title><Desc>Good Driver</Desc></Title>
                                        <Files>
                                          <Installer>
                                            <File>
                                              <Name>good.exe</Name>
                                              <Size>1048576</Size>
                                            </File>
                                          </Installer>
                                        </Files>
                                        <ReleaseDate>2024-01-01</ReleaseDate>
                                        <Reboot type="0" />
                                      </Package>
                                      """;

        const string badPackageXml = """
                                     <Package id="BAD01" version="1.0">
                                       <Title><Desc>Bad Driver</Desc></Title>
                                       <Files>
                                         <Installer>
                                           <File>
                                             <Name>bad.exe</Name>
                                             <Size>not-a-number</Size>
                                           </File>
                                         </Installer>
                                       </Files>
                                       <ReleaseDate>not-a-date</ReleaseDate>
                                       <Reboot type="0" />
                                     </Package>
                                     """;

        var responses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://download.lenovo.com/catalog/83DE_win11.xml"] = catalogXml,
            ["https://download.lenovo.com/packages/good.xml"] = goodPackageXml,
            ["https://download.lenovo.com/packages/bad.xml"] = badPackageXml
        };

        var downloader = new VantagePackageDownloader(new TestHttpClientFactory(responses));
        var packages = await downloader.GetPackagesAsync("83DE", OS.Windows11, token: CancellationToken.None);

        packages.Should().ContainSingle(package => package.Id == "GOOD01");
        packages[0].FileLocation.Should().Be("https://download.lenovo.com/packages/good.exe");
        packages[0].IsUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task GetPackagesAsync_WithInvalidMachineType_ShouldReject()
    {
        var downloader = new VantagePackageDownloader(new TestHttpClientFactory(new Dictionary<string, string>()));
        var act = async () => await downloader.GetPackagesAsync("../83DE", OS.Windows11, token: CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetPackagesAsync_WithTraversalFileName_ShouldSkipPackage()
    {
        const string catalogXml = """
                                  <packages>
                                    <package>
                                      <location>https://download.lenovo.com/packages/traverse.xml</location>
                                      <category>Driver</category>
                                    </package>
                                  </packages>
                                  """;

        const string traverseXml = """
                                   <Package id="TRAVERSE" version="1.0">
                                     <Title><Desc>Bad Path</Desc></Title>
                                     <Files>
                                       <Installer>
                                         <File>
                                           <Name>../evil.exe</Name>
                                           <Size>1048576</Size>
                                         </File>
                                       </Installer>
                                     </Files>
                                     <ReleaseDate>2024-01-01</ReleaseDate>
                                   </Package>
                                   """;

        var responses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://download.lenovo.com/catalog/83DE_win11.xml"] = catalogXml,
            ["https://download.lenovo.com/packages/traverse.xml"] = traverseXml
        };

        var downloader = new VantagePackageDownloader(new TestHttpClientFactory(responses));
        var packages = await downloader.GetPackagesAsync("83DE", OS.Windows11, token: CancellationToken.None);
        packages.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPackagesAsync_WithOffHostPackageLocation_ShouldSkipEntry()
    {
        const string catalogXml = """
                                  <packages>
                                    <package>
                                      <location>https://evil.example/packages/good.xml</location>
                                      <category>Driver</category>
                                    </package>
                                  </packages>
                                  """;

        var responses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://download.lenovo.com/catalog/83DE_win11.xml"] = catalogXml
        };

        var downloader = new VantagePackageDownloader(new TestHttpClientFactory(responses));
        var packages = await downloader.GetPackagesAsync("83DE", OS.Windows11, token: CancellationToken.None);
        packages.Should().BeEmpty();
    }

    private sealed class TestHttpClientFactory(IReadOnlyDictionary<string, string> responses) : HttpClientFactory
    {
        public override HttpClient Create() => new(new TestHandler(responses), disposeHandler: true);
    }

    private sealed class TestHandler(IReadOnlyDictionary<string, string> responses) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var key = request.RequestUri?.ToString() ?? string.Empty;
            if (!responses.TryGetValue(key, out var response))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/xml")
            });
        }
    }
}
