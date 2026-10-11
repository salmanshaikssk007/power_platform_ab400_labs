using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace CasePlugins
{
    public class CaseWorkerCaseload : PluginBase
    {
        public CaseWorkerCaseload(): base(typeof(CaseWorkerCaseload))
        {
        }
        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
            {
                throw new ArgumentNullException(nameof(localPluginContext));
            }

            var context = localPluginContext.PluginExecutionContext;

            //  trace the execution context with the message name, entity name, stage, mode and depth
            localPluginContext.Trace(
            $"Msg={context.MessageName} Entity={context.PrimaryEntityName} " +
            $"Stage={context.Stage} Mode={context.Mode} Depth={context.Depth}");

            // Loop gaurd: worker updates we work one level deep to avoid infinite loops
            if (context.Depth > 1)
            {
                return;
            }
            // target is the only record thats get's updated
            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity target))
            {
                return;
            }
            // If Assigned Worker isn't part of this update, nothing to do.
            // (A cleared lookup IS in Target with a null value; an untouched one is absent.)
            if (!target.Contains("cr36f_assignedworker"))
            {
                return;
            }
            // PreImage  = row data before the update, we need to register a pre-image in the plugin registration tool
            Entity preImage = context.PreEntityImages.Contains("PreImage") ? context.PreEntityImages["PreImage"] : null;

            // get the caseworker from the target record
            var caseOldWorker = preImage?.GetAttributeValue<EntityReference>("cr36f_assignedworker");
            var caseNewWorker = target.GetAttributeValue<EntityReference>("cr36f_assignedworker");

            // if same worker, no need to update the caseload
            if (caseOldWorker != null && caseNewWorker != null && caseOldWorker.Id == caseNewWorker.Id)
            {
                return;
            }

            localPluginContext.Trace($"CaseWorkerCaseload: caseOldWorker={caseOldWorker?.Id.ToString()} caseNewWorker={caseNewWorker?.Id.ToString()}");

            var service = localPluginContext.PluginUserService;

            if (caseOldWorker != null)
            {
                AdjustCaseLoad(service, caseOldWorker, -1, localPluginContext);
            }
            if (caseNewWorker != null)
            {
                AdjustCaseLoad(service, caseNewWorker, 1, localPluginContext);
            }
        }
        private static void AdjustCaseLoad(IOrganizationService service,EntityReference worker , int delta , ILocalPluginContext localPluginContext)
        {
            // read-only the column we need
            var row = service.Retrieve("cr36f_worker", worker.Id, new ColumnSet("cr36f_caseloadcount"));
            int currentLoad = row.GetAttributeValue<int?>("cr36f_caseloadcount") ?? 0;
            int next = Math.Max(0, currentLoad + delta);

            // write ONLY the column using new data
            var update = new Entity("cr36f_worker", worker.Id);
            update["cr36f_caseloadcount"] = next;
            service.Update(update);

            localPluginContext.Trace($"CaseWorkerCaseload: AdjustCaseLoad worker={worker.Id.ToString()} currentLoad={currentLoad} next={next}");
        }
    }

}
