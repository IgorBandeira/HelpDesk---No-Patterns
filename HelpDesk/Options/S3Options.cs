namespace HelpDesk.Options
{
    public class S3Options
    {
        public string Bucket { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string PublicBaseUrl { get; set; } = string.Empty;
        public string? Prefix { get; set; }
    }
}
