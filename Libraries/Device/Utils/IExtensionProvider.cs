using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Utils;

public interface IExtensionProvider
{
    void Initialize(object context);
    Task ExecuteAsync(string action, params object[] args);
    object? GetData(string key);
    void SetData(string key, object value);
    void Dispose();
}
