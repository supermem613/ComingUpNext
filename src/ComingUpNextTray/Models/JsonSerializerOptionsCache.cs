namespace ComingUpNextTray.Models
{
    using System.Text.Json;

    /// <summary>
    /// Central cache of JsonSerializerOptions instances to satisfy CA1869 (avoid repeatedly creating new instances).
    /// </summary>
    internal static class JsonSerializerOptionsCache
    {
        /// <summary>Gets indented output options for human readable config file.</summary>
        public static JsonSerializerOptions IndentedOptions { get; } = new JsonSerializerOptions { WriteIndented = true };
    }
}
