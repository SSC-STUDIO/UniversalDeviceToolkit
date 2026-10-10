using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.PackageDownloader.Detectors;
using UniversalDeviceToolkit.Lib.PackageDownloader.Detectors.Rules;
using Xunit;

namespace UniversalDeviceToolkit.Tests.PackageDownloader;

[Trait("Category", TestCategories.Security)]
public sealed class DetectorFalsePositiveTests
{
    [Fact]
    public void AndPackageRule_WithNoChildRules_ShouldNotCreate()
    {
        AndPackageRule.TryCreate([], out _).Should().BeFalse();
    }

    [Fact]
    public void OrPackageRule_WithNoChildRules_ShouldNotCreate()
    {
        OrPackageRule.TryCreate([], out _).Should().BeFalse();
    }

    [Fact]
    public void PnPIdPackageRule_WithEmptyOrGenericId_ShouldNotCreate()
    {
        PnPIdPackageRule.TryCreate(Element("<_PnPID></_PnPID>"), out _).Should().BeFalse();
        PnPIdPackageRule.TryCreate(Element("<_PnPID>PCI</_PnPID>"), out _).Should().BeFalse();
        PnPIdPackageRule.TryCreate(Element(@"<_PnPID>PCI\VEN_10DE</_PnPID>"), out _).Should().BeFalse();
    }

    [Fact]
    public async Task PnPIdPackageRule_WithSpecificId_ShouldCreate()
    {
        PnPIdPackageRule.TryCreate(Element(@"<_PnPID>PCI\VEN_10DE&amp;DEV_28E0</_PnPID>"), out var rule).Should().BeTrue();

        var cache = new List<DriverInfo>
        {
            new(@"PCI\VEN_10DE&DEV_28E0&SUBSYS_1234", @"PCI\VEN_10DE&DEV_28E0&SUBSYS_1234", null, null)
        };

        (await rule.CheckDependenciesSatisfiedAsync(cache, null!, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public void HardwareIdMatch_ShouldRejectPrefixCollision()
    {
        HardwareIdMatch.Matches(@"PCI\VEN_10DE&DEV_28E0", @"PCI\VEN_10DE&DEV_28E").Should().BeFalse();
        HardwareIdMatch.Matches(@"PCI\VEN_10DE&DEV_28E0&SUBSYS_1", @"PCI\VEN_10DE&DEV_28E0").Should().BeTrue();
        HardwareIdMatch.Matches(@"PCI\VEN_10DE&DEV_28E0", "PCI").Should().BeFalse();
        HardwareIdMatch.Matches(@"PCI\VEN_10DE&DEV_28E0", "").Should().BeFalse();
    }

    [Fact]
    public void DriverPackageRule_WithEmptyHardwareId_ShouldNotCreate()
    {
        DriverPackageRule.TryCreate(Element("""
                                            <_Driver>
                                              <HardwareID></HardwareID>
                                              <Version>1.0.0</Version>
                                            </_Driver>
                                            """), out _).Should().BeFalse();
    }

    [Fact]
    public async Task DriverPackageRule_WhenHardwarePresentButNotNewer_ShouldNotRequestInstall()
    {
        DriverPackageRule.TryCreate(Element("""
                                            <_Driver>
                                              <HardwareID>PCI\VEN_10DE&amp;DEV_28E0</HardwareID>
                                              <Version>31.0.15.0</Version>
                                            </_Driver>
                                            """), out var rule).Should().BeTrue();

        var cache = new List<DriverInfo>
        {
            new(@"PCI\VEN_10DE&DEV_28E0", @"PCI\VEN_10DE&DEV_28E0", new Version(31, 0, 15, 0), null)
        };

        (await rule.CheckDependenciesSatisfiedAsync(cache, null!, CancellationToken.None)).Should().BeTrue();
        (await rule.DetectInstallNeededAsync(cache, null!, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task DriverPackageRule_WhenPackageIsNewer_ShouldRequestInstall()
    {
        DriverPackageRule.TryCreate(Element("""
                                            <_Driver>
                                              <HardwareID>PCI\VEN_10DE&amp;DEV_28E0</HardwareID>
                                              <Version>32.0.0.0</Version>
                                            </_Driver>
                                            """), out var rule).Should().BeTrue();

        var cache = new List<DriverInfo>
        {
            new(@"PCI\VEN_10DE&DEV_28E0", @"PCI\VEN_10DE&DEV_28E0", new Version(31, 0, 15, 0), null)
        };

        (await rule.CheckDependenciesSatisfiedAsync(cache, null!, CancellationToken.None)).Should().BeTrue();
        (await rule.DetectInstallNeededAsync(cache, null!, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public void RegistryKeyPackageRule_WithTraversalKey_ShouldNotCreate()
    {
        RegistryKeyPackageRule.TryCreate(Element("""
                                                 <_RegistryKey>
                                                   <Key>HKLM\..\SYSTEM</Key>
                                                 </_RegistryKey>
                                                 """), out _).Should().BeFalse();
    }

    [Fact]
    public void RegistryKeyValuePackageRule_WithValidKey_ShouldCreate()
    {
        RegistryKeyValuePackageRule.TryCreate(Element("""
                                                      <_RegistryKeyValue>
                                                        <Key>HKEY_LOCAL_MACHINE\SOFTWARE\Lenovo\Test</Key>
                                                        <KeyName>Version</KeyName>
                                                        <Version>1.2.3</Version>
                                                      </_RegistryKeyValue>
                                                      """), out _).Should().BeTrue();
    }

    [Fact]
    public void WindowsBuildVersionPackageRule_ShouldParseDottedBuildAndRejectOsVersionOnly()
    {
        WindowsBuildVersionPackageRule.TryCreate(Element("""
                                                         <_WindowsBuildVersion>
                                                           <BuildVersion>10.0.19041.1</BuildVersion>
                                                         </_WindowsBuildVersion>
                                                         """), out var dotted).Should().BeTrue();

        WindowsBuildVersionPackageRule.TryCreate(Element("""
                                                         <_WindowsBuildVersion>
                                                           <BuildVersion>10.0</BuildVersion>
                                                         </_WindowsBuildVersion>
                                                         """), out _).Should().BeFalse();

        dotted.Should().NotBeNull();
    }

    [Fact]
    public void ExternalDetectionRule_WithTraversalFileName_ShouldNotCreate()
    {
        var document = new XmlDocument();
        document.LoadXml("""
                         <Package id="pkg">
                           <Files>
                             <External>
                               <File>
                                 <Name>..\..\evil.exe</Name>
                                 <CRC>0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF</CRC>
                               </File>
                             </External>
                           </Files>
                           <_ExternalDetection rc="0">detect.exe</_ExternalDetection>
                         </Package>
                         """);

        ExternalDetectionRule.TryCreate(document.SelectSingleNode("/Package/_ExternalDetection"), document, "https://download.lenovo.com/pkg", out _)
            .Should().BeFalse();
    }

    [Fact]
    public void ExternalDetectionRule_WithoutCatalogHash_ShouldNotCreate()
    {
        var document = new XmlDocument();
        document.LoadXml("""
                         <Package id="pkg">
                           <Files>
                             <External>
                               <File>
                                 <Name>detect.exe</Name>
                               </File>
                             </External>
                           </Files>
                           <_ExternalDetection rc="0">detect.exe</_ExternalDetection>
                         </Package>
                         """);

        ExternalDetectionRule.TryCreate(document.SelectSingleNode("/Package/_ExternalDetection"), document, "https://download.lenovo.com/pkg", out _)
            .Should().BeFalse();
    }

    [Fact]
    public async Task DetectAsync_WhenDetectInstallIsMissing_ShouldNotMarkAsUpdate()
    {
        var document = new XmlDocument();
        document.LoadXml("""
                         <Package id="pkg">
                           <Dependencies>
                             <_PnPID>PCI\VEN_10DE&amp;DEV_28E0</_PnPID>
                           </Dependencies>
                         </Package>
                         """);

        var detector = new VantagePackageUpdateDetector();
        detector.ReplaceDriverInfoCache(
        [
            new DriverInfo(@"PCI\VEN_10DE&DEV_28E0", @"PCI\VEN_10DE&DEV_28E0", new Version(1, 0), null)
        ]);

        using var httpClient = new HttpClient(new NotFoundHandler(), disposeHandler: true);
        var isUpdate = await detector.DetectAsync(httpClient, document, "https://download.lenovo.com/pkg", CancellationToken.None);
        isUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task DetectAsync_WhenDetectInstallRulesAreUnparseable_ShouldNotMarkAsUpdate()
    {
        var document = new XmlDocument();
        document.LoadXml("""
                         <Package id="pkg">
                           <Dependencies>
                             <_PnPID>PCI\VEN_10DE&amp;DEV_28E0</_PnPID>
                           </Dependencies>
                           <DetectInstall>
                             <_UnknownRule>ignore</_UnknownRule>
                           </DetectInstall>
                         </Package>
                         """);

        var detector = new VantagePackageUpdateDetector();
        detector.ReplaceDriverInfoCache(
        [
            new DriverInfo(@"PCI\VEN_10DE&DEV_28E0", @"PCI\VEN_10DE&DEV_28E0", new Version(1, 0), null)
        ]);

        using var httpClient = new HttpClient(new NotFoundHandler(), disposeHandler: true);
        var isUpdate = await detector.DetectAsync(httpClient, document, "https://download.lenovo.com/pkg", CancellationToken.None);
        isUpdate.Should().BeFalse();
    }

    private static XmlNode Element(string xml)
    {
        var document = new XmlDocument();
        document.LoadXml(xml);
        return document.DocumentElement!;
    }

    private sealed class NotFoundHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
