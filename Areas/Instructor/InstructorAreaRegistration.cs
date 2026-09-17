using System.Web.Mvc;

namespace ECOLearning.Areas.Instructor
{
    public class InstructorAreaRegistration : AreaRegistration 
    {
        public override string AreaName 
        {
            get 
            {
                return "Instructor";
            }
        }

        public override void RegisterArea(AreaRegistrationContext context) 
        {
            context.MapRoute(
                "Instructor_default",
                "Instructor/{controller}/{action}/{id}",
                new { controller = "Dashboard", action = "Index", id = UrlParameter.Optional },
                new[] { "ECOLearning.Areas.Instructor.Controllers" }
            );
        }
    }
}
