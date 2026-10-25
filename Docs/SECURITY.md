# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| Latest Release | ✅ Full Support |
| Previous Release | ⚠️ Best Effort |
| Older Versions | ❌ No Support |

## Reporting Security Vulnerabilities

### Responsible Disclosure

We take security seriously. If you believe you have found a security vulnerability in Universal Device Toolkit, please report it responsibly through our coordinated disclosure process.

### Reporting Process

1. **Do NOT** open a public GitHub issue
2. **Do NOT** disclose the vulnerability publicly
3. **Do** send a detailed report to: 3992237161@qq.com

### What to Include

Your report should include:
- Description of the vulnerability
- Steps to reproduce the issue
- Affected components or versions
- Potential impact assessment
- Suggested remediation (if any)

### Response Timeline

| Phase | Timeline |
|-------|----------|
| Initial Acknowledgment | 24 hours |
| Vulnerability Assessment | 3-5 business days |
| Fix Development | Based on complexity |
| Security Update Release | Coordinated disclosure |

## Security Commitments

### Our Promises

1. **No Telemetry**: UDT contains no data collection or tracking
2. **No Background Services**: Application only runs when actively used
3. **Local-Only Operation**: No cloud dependencies or remote servers
4. **Privacy-First Design**: User data stays on the user's machine

### Data Collection

UDT does NOT collect:
- ❌ Usage statistics
- ❌ Hardware identifiers
- ❌ Software inventory
- ❌ User behavior patterns
- ❌ Personal information

## Security Architecture

### Application Security

| Component | Security Measure |
|-----------|-----------------|
| Electron Renderer | Chromium sandbox, context isolation, no Node.js integration |
| IPC Boundary | Main-frame validation, narrow preload API, method and argument validation |
| Settings Storage | Local JSON files under user profile (no cloud sync) |
| Network Requests | HTTPS-only external navigation and normal certificate validation |
| Hardware Access | Minimal required permissions |
| Auto-Updates | HTTPS transport and mandatory SHA-256 verification before extraction or launch |

### Renderer and IPC Security

The plugin system was retired in 6.1. The shipping Electron renderer runs
sandboxed and reaches privileged operations only through the preload bridge.
Unexpected top-level navigation and new windows are denied. Main-process
handlers validate the current main frame, restrict external URLs to HTTP(S),
and reject executable or script paths supplied by the renderer.

## Dependencies Security

### Dependency Management

- **NuGet Packages**: Regularly updated
- **Security Scanning**: GitHub Dependabot enabled
- **Vulnerability Alerts**: Automatic notifications
- **License Compliance**: Review of all dependencies

### Critical Dependencies

| Dependency | Purpose | Security Note |
|------------|---------|---------------|
| .NET 10 | Runtime | Microsoft security updates |
| Autofac | DI Container | Mature, well-audited |
| System.Management | WMI/management APIs | Microsoft-maintained package |
| Octokit | GitHub API integration | Mature and widely used |

## Hardening Guidelines

### For Users

1. **Download from Official Sources**
   - GitHub Releases only
   - Verify the release SHA256 manifest
   - Windows release packages are unsigned; Azure Trusted Signing is no longer part of the release

2. **Permission Management**
   - Review requested permissions
   - Run with minimal privileges
   - Disable unused features

3. **Update Safety**
   - Install releases from the official repository
   - Keep checksum manifests with downloaded installers
   - Compare the downloaded file with the release SHA256 manifest before installation

### For Developers

1. **Code Security**
   - All input validation
   - No hardcoded credentials
   - Secure string handling
   - FxCop analyzers enabled

2. **Dependency Updates**
   - Regular dependency audits
   - Automated PRs for updates
   - Security patches prioritized

3. **Testing Requirements**
   - Security tests for hardware interfaces
   - Electron sandbox and IPC boundary tests
   - Path and update-integrity tests

## Known Security Considerations

### Hardware-Level Access

Some features require elevated permissions:
- WMI access for power management
- ACPI communication for firmware
- USB/HID access for RGB control

These are necessary for hardware control but increase the application's trust boundary.

### Renderer Boundary

Renderer compromise is treated as a trust-boundary failure, not as permission
to execute arbitrary local programs. Keep the preload API narrow, validate the
calling frame in privileged IPC handlers, and do not re-enable Node.js
integration or disable the Chromium sandbox.

### Auto-Updates

The update mechanism:
- Uses HTTPS for all downloads
- Verifies the downloaded payload against a SHA-256 release digest or manifest
- Launches only the installer path produced by the verified downloader
- Allows manual update rejection

## Compliance

### Standards Alignment

- **OWASP**: Application security guidelines followed
- **CWE**: Common Weakness Enumeration awareness
- **NIST**: Cybersecurity framework considerations

### Privacy Compliance

- GDPR: No personal data collection
- CCPA: No data sale or sharing
- LGPD: No international data transfers

## Security Contacts

| Contact | Purpose |
|---------|---------|
| security@lenovolegiontoolkit.dev | Security vulnerability reports |
| support@lenovolegiontoolkit.dev | General support and issues |
| contributors@lenovolegiontoolkit.dev | Plugin developer questions |

## Acknowledgments

We thank the security research community for helping us keep Universal Device Toolkit secure. Responsible disclosure allows us to address vulnerabilities before they affect users.

## Security Updates

### Staying Informed

- Watch GitHub Releases for updates
- Enable auto-updates in settings
- Follow project announcements

### Update Notifications

Security updates are:
- Marked clearly in release notes
- Prioritized over feature releases
- Documented with CVE references (if applicable)

---

**Last Updated**: February 2026
**Version**: 1.0
