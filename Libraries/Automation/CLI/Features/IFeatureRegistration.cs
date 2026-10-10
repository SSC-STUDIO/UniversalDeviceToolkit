using System.Collections.Generic;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Automation.CLI.Features;

public interface IFeatureRegistration
{
    string Name { get; }
    Task<bool> IsSupportedAsync();
    Task<IEnumerable<string>> GetValuesAsync();
    Task<string> GetValueAsync();
    Task SetValueAsync(string value);
}
