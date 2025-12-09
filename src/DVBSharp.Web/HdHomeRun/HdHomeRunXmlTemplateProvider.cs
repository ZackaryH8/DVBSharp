namespace DVBSharp.Web.HdHomeRun;

internal sealed class HdHomeRunXmlTemplateProvider
{
    private readonly string _deviceTemplate;
    private readonly string _connectionManagerTemplate;
    private readonly string _contentDirectoryTemplate;

    public HdHomeRunXmlTemplateProvider(IWebHostEnvironment env)
    {
        var templatesRoot = Path.Combine(env.ContentRootPath, "HdHomeRun", "Templates");
        _deviceTemplate = ReadTemplate(Path.Combine(templatesRoot, "device.xml"));
        _connectionManagerTemplate = ReadTemplate(Path.Combine(templatesRoot, "connection-manager.xml"));
        _contentDirectoryTemplate = ReadTemplate(Path.Combine(templatesRoot, "content-directory.xml"));
    }

    public string GetDeviceXml(HdHomeRunOptions options, string baseUrl)
    {
        var manufacturer = string.IsNullOrWhiteSpace(options.Manufacturer)
            ? "DVBSharp"
            : options.Manufacturer;

        var manufacturerUrl = string.IsNullOrWhiteSpace(manufacturer)
            ? "https://dvbsharp.local"
            : $"https://{manufacturer.ToLowerInvariant()}.example.com";

        return _deviceTemplate
            .Replace("{{BASE_URL}}", baseUrl, StringComparison.OrdinalIgnoreCase)
            .Replace("{{FRIENDLY_NAME}}", options.FriendlyName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{MANUFACTURER}}", manufacturer, StringComparison.OrdinalIgnoreCase)
            .Replace("{{MANUFACTURER_URL}}", manufacturerUrl, StringComparison.OrdinalIgnoreCase)
            .Replace("{{MODEL_NUMBER}}", options.ModelNumber, StringComparison.OrdinalIgnoreCase)
            .Replace("{{DEVICE_ID}}", options.DeviceId, StringComparison.OrdinalIgnoreCase);
    }

    public string GetConnectionManagerXml() => _connectionManagerTemplate;

    public string GetContentDirectoryXml() => _contentDirectoryTemplate;

    private static string ReadTemplate(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"HDHomeRun template missing: {path}");
        }

        return File.ReadAllText(path);
    }
}
