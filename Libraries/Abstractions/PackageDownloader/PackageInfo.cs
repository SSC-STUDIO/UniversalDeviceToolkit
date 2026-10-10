namespace UniversalDeviceToolkit.Abstractions.PackageDownloader;

/// <summary>
/// Cross-platform representation of a downloadable driver/software package.
/// </summary>
public readonly struct PackageInfo
{
    private readonly string? _id;
    private readonly string? _title;
    private readonly string? _description;
    private readonly string? _version;
    private readonly string? _category;
    private readonly string? _fileName;
    private readonly string? _fileSize;
    private readonly string? _fileLocation;
    private readonly string? _index;

    public string Id
    {
        get => _id ?? string.Empty;
        init => _id = value;
    }

    public string Title
    {
        get => _title ?? string.Empty;
        init => _title = value;
    }

    public string Description
    {
        get => _description ?? string.Empty;
        init => _description = value;
    }

    public string Version
    {
        get => _version ?? string.Empty;
        init => _version = value;
    }

    public string Category
    {
        get => _category ?? string.Empty;
        init => _category = value;
    }

    public string FileName
    {
        get => _fileName ?? string.Empty;
        init => _fileName = value;
    }

    public string FileSize
    {
        get => _fileSize ?? string.Empty;
        init => _fileSize = value;
    }

    public string? FileCrc { get; init; }
    public DateTime ReleaseDate { get; init; }
    public string? Readme { get; init; }

    public string FileLocation
    {
        get => _fileLocation ?? string.Empty;
        init => _fileLocation = value;
    }

    public bool IsUpdate { get; init; }

    public string Index
    {
        get => _index ?? string.Empty;
        init => _index = value;
    }

    /// <summary>
    /// Validates that required fields are present and non-empty.
    /// Call this after deserialization or construction to catch incomplete data.
    /// </summary>
    public bool IsValid(out string? validationError)
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            validationError = "PackageInfo.Id is required";
            return false;
        }
        if (string.IsNullOrWhiteSpace(FileName))
        {
            validationError = "PackageInfo.FileName is required";
            return false;
        }
        if (string.IsNullOrWhiteSpace(FileLocation))
        {
            validationError = "PackageInfo.FileLocation is required";
            return false;
        }
        if (string.IsNullOrWhiteSpace(Version))
        {
            validationError = "PackageInfo.Version is required";
            return false;
        }

        validationError = null;
        return true;
    }
}
