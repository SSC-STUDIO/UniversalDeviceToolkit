using Xunit;

namespace UniversalDeviceToolkit.Tests.Settings;

[CollectionDefinition(TestCollections.Localization, DisableParallelization = true)]
public sealed class LocalizationTestCollectionDefinition;

[CollectionDefinition(TestCollections.Settings, DisableParallelization = true)]
public sealed class SettingsTestCollectionDefinition;

[CollectionDefinition(TestCollections.ProcessState, DisableParallelization = true)]
public sealed class ProcessStateTestCollectionDefinition;
