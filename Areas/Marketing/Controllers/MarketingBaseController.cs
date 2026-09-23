using System;
using System.Web.Mvc;
using System.Web.Routing;
using ECOLearning.Models;

namespace ECOLearning.Areas.Marketing.Controllers
{
    public class MarketingBaseController : Controller
    {
        protected QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var role = Session["Role"]?.ToString();
            var userId = Session["UserId"];

            // Cho phép Marketing và Admin kiểm thử
            bool isAllowed = userId != null && !string.IsNullOrEmpty(role) &&
                (role.Equals("Marketing", StringComparison.OrdinalIgnoreCase) ||
                 role.Equals("Admin", StringComparison.OrdinalIgnoreCase));

            if (!isAllowed)
            {
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary(new { 
                        controller = "Account", 
                        action = "Login", 
                        area = "", 
                        returnUrl = filterContext.HttpContext.Request.RawUrl 
                    })
                );
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && db != null)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
