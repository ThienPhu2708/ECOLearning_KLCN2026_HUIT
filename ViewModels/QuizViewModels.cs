using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace ECOLearning.ViewModels
{
    public class QuizQuestion
    {
        public int Id { get; set; }
        public string CourseId { get; set; }
        [Required, StringLength(120)] public string Topic { get; set; }
        [Range(1,3)] public int Difficulty { get; set; } = 1;
        [Required, StringLength(4000)] public string Text { get; set; }
        public string OptionsJson { get; set; }
        [Range(0,3)] public int CorrectOption { get; set; }
        [StringLength(2000)] public string Explanation { get; set; }
        public bool ShuffleOptions { get; set; } = true;
        public bool Active { get; set; } = true;
        // Leave null for MVC's array binder; a preallocated array cannot be cleared.
        public string[] Options { get; set; }
        public void LoadOptions() { Options = JsonConvert.DeserializeObject<string[]>(OptionsJson); }
        public string Level => Difficulty == 1 ? "Dễ" : Difficulty == 2 ? "Trung bình" : "Khó";
    }
    public class QuizConfig
    {
        public string CourseId { get; set; }
        [Required, StringLength(200)] public string Title { get; set; } = "Quiz cuối khóa";
        [Range(1,180)] public int Minutes { get; set; } = 30;
        [Range(1,100)] public int PassPercent { get; set; } = 80;
        [Range(1,100)] public int MaxAttempts { get; set; } = 3;
        [Range(0,100)] public int EasyCount { get; set; } = 5;
        [Range(0,100)] public int MediumCount { get; set; } = 3;
        [Range(0,100)] public int HardCount { get; set; } = 2;
        [StringLength(120)] public string Topic { get; set; }
        public bool RandomSelection { get; set; } = true;
        public string SelectedIdsJson { get; set; } = "[]";
        public bool ShuffleQuestions { get; set; } = true;
        public bool ShuffleAnswers { get; set; } = true;
        public bool Published { get; set; }
        public int Total => EasyCount + MediumCount + HardCount;
    }
    public class QuizSnapshotQuestion
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public string[] Options { get; set; }
        public int CorrectOption { get; set; }
        public string Explanation { get; set; }
        public int Difficulty { get; set; }
        public string Topic { get; set; }
    }
    public class QuizAttempt
    {
        public int Id { get; set; }
        public string CourseId { get; set; }
        public int StudentId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string SnapshotJson { get; set; }
        public string AnswersJson { get; set; }
        public int Total { get; set; }
        public int? Correct { get; set; }
        public decimal? Score { get; set; }
        public bool? Passed { get; set; }
        public bool TimedOut { get; set; }
        public int PassPercent { get; set; }
        public string Title { get; set; }
    }
    public class QuizCertificate
    {
        public string CourseId { get; set; }
        public int StudentId { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? IssuedAt { get; set; }
        public int? IssuedBy { get; set; }
        public string Code { get; set; }
    }
    public class QuizStudentRow
    {
        public int StudentId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string CourseId { get; set; }
        public string CourseName { get; set; }
        public int TotalLessons { get; set; }
        public int CompletedLessons { get; set; }
        public int Attempts { get; set; }
        public int ExtraAttempts { get; set; }
        public decimal? BestScore { get; set; }
        public int? BestCorrect { get; set; }
        public int? BestTotal { get; set; }
        public bool Passed { get; set; }
        public bool HasOpenAttempt { get; set; }
        public DateTime? RequestedAt { get; set; }
        public DateTime? IssuedAt { get; set; }
        public string Code { get; set; }
        public bool CompletedBeforeRevision { get; set; }
        public bool LessonsDone => CompletedBeforeRevision || (TotalLessons > 0 && CompletedLessons >= TotalLessons);
        public int Progress => CompletedBeforeRevision ? 100 : TotalLessons == 0 ? 0 : Math.Min(100, (int)Math.Floor(100.0 * CompletedLessons / TotalLessons));
        public string QuizStatus => Passed ? "Đã đạt" : HasOpenAttempt ? "Đang làm" : Attempts > 0 ? "Chưa đạt" : !LessonsDone ? "Chưa đủ điều kiện làm" : "Chưa làm";
        public string CertificateStatus => IssuedAt.HasValue ? "Đã cấp" : LessonsDone && Passed ? "Đủ điều kiện nhận" : "Chưa đủ điều kiện";
    }
    public class QuizAttemptGrant
    {
        public int Id { get; set; }
        public string CourseId { get; set; }
        public int StudentId { get; set; }
        public int TeacherId { get; set; }
        public string Reason { get; set; }
        public DateTime GrantedAt { get; set; }
        public int Quantity { get; set; }
    }
    public class QuizOverview
    {
        public string CourseId { get; set; }
        public string CourseName { get; set; }
        public QuizConfig Config { get; set; }
        public QuizStudentRow Student { get; set; }
        public List<QuizAttempt> Attempts { get; set; }
        public int AllowedAttempts => (Config?.MaxAttempts ?? 0) + (Student?.ExtraAttempts ?? 0);
    }
    public class QuizTakeViewModel
    {
        public QuizAttempt Attempt { get; set; }
        public List<QuizSnapshotQuestion> Questions { get; set; }
        public int[] Answers { get; set; }
        public bool Review { get; set; }
    }
}
