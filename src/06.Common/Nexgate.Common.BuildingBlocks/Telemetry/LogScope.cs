using System.Collections;

namespace Nexgate.Common.BuildingBlocks.Telemetry;

/// <summary>
/// Scope de log de un solo par clave/valor. Se representa como texto legible ("CorrelationId:abc")
/// y como par estructurado en los logs JSON.
/// </summary>
public sealed class LogScope(string key, object value) : IReadOnlyList<KeyValuePair<string, object>>
{
    private readonly KeyValuePair<string, object> _pair = new(key, value);

    public int Count => 1;

    public KeyValuePair<string, object> this[int index] =>
        index == 0 ? _pair : throw new ArgumentOutOfRangeException(nameof(index));

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        yield return _pair;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => $"{_pair.Key}:{_pair.Value}";
}
