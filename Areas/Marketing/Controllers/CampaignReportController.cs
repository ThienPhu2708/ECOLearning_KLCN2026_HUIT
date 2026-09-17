using System;
using System.Web.Mvc;

namespace ECOLearning.Areas.Marketing.Controllers
{
    [Authorize(Roles = "Marketing")]
    public class CampaignReportController : Controller
    {
        // GET: Marketing/CampaignReport (UC18: Báo cáo hiệu quả chiến dịch Marketing)
        public ActionResult Index(string campaignId = null, string period = "month")
        {
            return View();
        }
    }
}
