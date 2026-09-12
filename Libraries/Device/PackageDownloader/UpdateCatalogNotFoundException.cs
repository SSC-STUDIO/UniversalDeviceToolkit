using System;

namespace UniversalDeviceToolkit.Lib.PackageDownloader;

public class UpdateCatalogNotFoundException(string? message, Exception? ex) : Exception(message, ex);
