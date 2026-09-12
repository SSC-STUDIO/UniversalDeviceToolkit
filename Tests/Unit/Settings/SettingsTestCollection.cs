using Xunit;

namespace UniversalDeviceToolkit.Tests.Settings;

// xUnit collections are per-assembly. Unit tests that mutate Compatibility's
// static machine-information cache must share ProcessState so they cannot
// overwrite each other under parallelizeTestCollections=true.

[CollectionDefinition(TestCollections.Localization, DisableParallelization = true)]
public sealed class LocalizationTestCollectionDefinition;

[CollectionDefinition(TestCollections.Settings, DisableParallelization = true)]
public sealed class SettingsTestCollectionDefinition;

[CollectionDefinition(TestCollections.ProcessState, DisableParallelization = true)]
public sealed class ProcessStateTestCollectionDefinition;
