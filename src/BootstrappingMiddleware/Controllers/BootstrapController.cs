using Microsoft.AspNetCore.Mvc;

namespace BootstrappingMiddleware.Controllers
{
    public class BootstrapController : Controller
    {
        [BootstrappedData("/")]
        public object BootstrapHome()
        {
            return new
            {
                Example = "ConfigValue",
            };
        }
    }
}