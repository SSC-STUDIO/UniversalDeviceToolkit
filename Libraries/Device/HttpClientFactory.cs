using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UniversalDeviceToolkit.Lib.Utils;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace UniversalDeviceToolkit.Lib;

public class HttpClientFactory
{
    private Uri? _url;
    private string? _username;
    private string? _password;

    public virtual HttpClientHandler CreateHandler()
    {
        var handler = new HttpClientHandler();
        // Do not hard-require CRL/OCSP at the SCHANNEL level: in networks where CA
        // revocation endpoints are unreachable the TLS handshake stalls until the
        // client timeout. ValidateServerCertificate already tolerates exactly this
        // "revocation unavailable" case while still rejecting broken chains.
        handler.CheckCertificateRevocationList = false;
        handler.ServerCertificateCustomValidationCallback = ValidateServerCertificate;

        if (_url is not null)
        {
            handler.UseProxy = true;
            handler.Proxy = new WebProxy(_url)
            {
                UseDefaultCredentials = false,
                BypassProxyOnLocal = false,
            };

            if (_username is not null && _password is not null)
                handler.DefaultProxyCredentials = new NetworkCredential(_username, _password);
        }

        return handler;
    }

    public virtual HttpClient Create() => new(CreateHandler(), true);

    public void SetProxy(Uri? url, string? username, string? password, bool allowAllCerts)
    {
        _url = url;
        _username = username;
        _password = password;
        _ = allowAllCerts;
    }

    internal static bool ValidateServerCertificate(HttpRequestMessage message, X509Certificate2? cert, X509Chain? chain, SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None)
            return true;

        if (errors != SslPolicyErrors.RemoteCertificateChainErrors || chain is null)
        {
            Log.Instance.Warning($"SSL certificate validation error: {errors}");
            return false;
        }

        var chainStatus = chain.ChainStatus;
        if (IsOnlyRevocationAvailabilityFailure(chainStatus))
        {
            Log.Instance.Warning($"SSL certificate revocation status unavailable for {message.RequestUri}; continuing with otherwise valid certificate chain.");
            return true;
        }

        Log.Instance.Warning($"SSL certificate chain validation failed for {message.RequestUri}: {string.Join(", ", chainStatus.Select(status => status.Status))}");
        return false;
    }

    internal static bool IsOnlyRevocationAvailabilityFailure(IReadOnlyCollection<X509ChainStatus> chainStatus)
    {
        if (chainStatus.Count == 0)
            return false;

        return chainStatus
            .Select(status => status.Status & ~X509ChainStatusFlags.RevocationStatusUnknown & ~X509ChainStatusFlags.OfflineRevocation)
            .All(status => status == X509ChainStatusFlags.NoError);
    }
}
