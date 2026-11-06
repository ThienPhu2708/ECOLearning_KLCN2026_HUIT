namespace ECOLearning.ViewModels
{
    public sealed class AdminOtpViewModel
    {
        public string MaskedEmail { get; set; }
        public int ResendSeconds { get; set; }
        public int CooldownSeconds { get; set; }
        public bool IsLocked => CooldownSeconds > 0;
        public int ExpiresSeconds { get; set; }
        public string Notice { get; set; }
    }
}
