namespace Gratis.Rendering;

public partial class MapDrawingConfig {
    public FilterBarSettings Filters = new();
    [global::System.Text.Json.Serialization.JsonIgnore]
    public FilterBarSettings FilterOptions => Filters ??= new();
    public void ResetFilterBar() => Filters = new();
}
