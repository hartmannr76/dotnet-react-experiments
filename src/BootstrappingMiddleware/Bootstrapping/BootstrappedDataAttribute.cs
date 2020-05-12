using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BootstrappingMiddleware
{
    public class BootstrappedDataAttribute : HttpGetAttribute, IActionConstraint, IActionFilter
    {
        public BootstrappedDataAttribute(string template) : base(template)
        {
            
        } 
        
        /// <summary>
        /// We only want the function to run if we have not bootstrapped data already
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool Accept(ActionConstraintContext context)
        {
            if (context.RouteContext.HttpContext.Items.ContainsKey("bootstrapped"))
            {
                return false;
            }

            return true;
        }

        public int Order { get; }
        
        public void OnActionExecuting(ActionExecutingContext context) { }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.HttpContext.Items.ContainsKey("bootstrapped"))
            {
                return;
            }
            context.HttpContext.Items.Add("bootstrapped", true);
        }
    }
}