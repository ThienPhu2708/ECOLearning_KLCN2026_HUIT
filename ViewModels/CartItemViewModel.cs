using System;

namespace ECOLearning.ViewModels
{
    public class CartItemViewModel
    {
        public string CourseId { get; set; }
        public string CourseName { get; set; }
        public string InstructorName { get; set; }
        public string DurationText { get; set; }
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; }
        public string ThumbnailUrl { get; set; }
    }
}
