using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public sealed class HttpClientFactoryTests
{
    [Fact]
    public void IsOnlyRevocationAvailabilityFailure_WhenOnlyRevocationStatusIsUnavailable_ReturnsTrue()
    {
        var result = HttpClientFactory.IsOnlyRevocationAvailabilityFailure(
            [
                new X509ChainStatus { Status = X509ChainStatusFlags.RevocationStatusUnknown },
                new X509ChainStatus { Status = X509ChainStatusFlags.OfflineRevocation },
                new X509ChainStatus { Status = X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation },
            ]);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsOnlyRevocationAvailabilityFailure_WhenTrustFailureIsPresent_ReturnsFalse()
    {
        var result = HttpClientFactory.IsOnlyRevocationAvailabilityFailure(
            [
                new X509ChainStatus { Status = X509ChainStatusFlags.RevocationStatusUnknown },
                new X509ChainStatus { Status = X509ChainStatusFlags.UntrustedRoot },
            ]);

        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateServerCertificate_WhenChainHasTrustFailure_ReturnsFalse()
    {
        using var certificate = CreateSelfSignedCertificate();
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.Build(certificate);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");

        var result = HttpClientFactory.ValidateServerCertificate(
            request,
            certificate,
            chain,
            SslPolicyErrors.RemoteCertificateChainErrors);

        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateServerCertificate_WhenNameMismatchExists_ReturnsFalse()
    {
        using var certificate = CreateSelfSignedCertificate();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");

        var result = HttpClientFactory.ValidateServerCertificate(
            request,
            certificate,
            null,
            SslPolicyErrors.RemoteCertificateNameMismatch);

        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateServerCertificate_WhenChainErrorHasNoStatus_ReturnsFalse()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");
        using var chain = new X509Chain();

        var result = HttpClientFactory.ValidateServerCertificate(
            request,
            null,
            chain,
            SslPolicyErrors.RemoteCertificateChainErrors);

        result.Should().BeFalse();
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=example.test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));
    }
}
