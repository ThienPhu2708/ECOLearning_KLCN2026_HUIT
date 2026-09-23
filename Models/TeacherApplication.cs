using System;

namespace ECOLearning.Models
{
    public class TeacherApplication
    {
        public string ApplicationId { get; set; }
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Specialization { get; set; }
        public string BioExperience { get; set; }
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string AccountName { get; set; }
        public string CertificateFilePath { get; set; }
        public string CertificateFileName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string Status { get; set; } // "Pending", "Approved", "Rejected"
        public string RejectionReason { get; set; }

        public TeacherApplication()
        {
            CreatedAt = DateTime.Now;
            Status = "Pending";
        }
    }
}
