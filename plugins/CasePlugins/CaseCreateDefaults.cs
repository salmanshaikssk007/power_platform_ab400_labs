using Microsoft.Xrm.Sdk;
using System;

namespace CasePlugins
{
    /// <summary>
    /// Plugin development guide: https://docs.microsoft.com/powerapps/developer/common-data-service/plug-ins
    /// Best practices and guidance: https://docs.microsoft.com/powerapps/developer/common-data-service/best-practices/business-logic/
    /// </summary>
    public class CaseCreateDefaults : PluginBase
    {
        private readonly string unsecureConfiguration;
        private readonly string secureConfiguration;
        public CaseCreateDefaults(string _unsecureConfiguration, string _secureConfiguration)
            : base(typeof(CaseCreateDefaults))
        {
            // TODO: Implement your custom configuration handling
            // https://docs.microsoft.com/powerapps/developer/common-data-service/register-plug-in#set-configuration-data
            _unsecureConfiguration = unsecureConfiguration;
            _secureConfiguration = secureConfiguration;
        }

        // Entry point for custom business logic execution
        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
            {
                throw new ArgumentNullException(nameof(localPluginContext));
            }

            var context = localPluginContext.PluginExecutionContext;

            // TODO: Implement your custom business logic

            // Check for the entity on which the plugin would be registered
            //if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            //{
            //    var entity = (Entity)context.InputParameters["Target"];

            //    // Check for entity name on which this plugin would be registered
            //    if (entity.LogicalName == "account")
            //    {

            //    }
            //}

            // execution context : whats happening in the system when this plugin is triggered
            localPluginContext.Trace(
            $"Msg={context.MessageName} Entity={context.PrimaryEntityName} " +
            $"Stage={context.Stage} Mode={context.Mode} Depth={context.Depth}");

            localPluginContext.Trace($"UserId={context.UserId} InitiatingUserId= {context.InitiatingUserId})");
            // get record being created
            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity target))
            {
                return;
            }

            var open = target.GetAttributeValue<DateTime?>("cr36f_opendate");
            var due = target.GetAttributeValue<DateTime?>("cr36f_ReviewDueDate");

            // Bussiness Logic
            if (open.HasValue && !due.HasValue)
            {
                target["cr36f_ReviewDueDate"] = open.Value.AddDays(30);
                localPluginContext.Trace($"Setting Review Due Date to {target["cr36f_ReviewDueDate"]}");
            }else if(open.HasValue && due.HasValue && due.Value < open.Value)
            {
                throw new InvalidPluginExecutionException("Review Due Date cannot be earlier than Open Date");
            }
        }
    }
}
