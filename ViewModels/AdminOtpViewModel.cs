namespace ECOLearning.ViewModels
{
    public sealed class AdminOtpViewModel
    {
        public string MaskedEmail { get; set; }
        public int ResendSeconds { get; set; }
        public int ExpiresSeconds { get; set; }
        public string Notice { get; set; }
    }
}
