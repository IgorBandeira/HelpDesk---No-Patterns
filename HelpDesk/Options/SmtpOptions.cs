namespace HelpDesk.Options
{
    public class SmtpOptions
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public bool UseStartTls { get; set; } = true;
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string FromName { get; set; } = "HelpDesk";
        public string FromEmail { get; set; } = "";
        public bool DisableDelivery { get; set; } = false;

    }
}
