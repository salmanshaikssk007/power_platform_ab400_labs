using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace CasePlugins
{
    public class AssignWorker : PluginBase
    {
        public AssignWorker(): base (typeof(AssignWorker))
        {
        }
        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
            {
                return   ;
            }
            var context = localPluginContext.PluginExecutionContext;
            var service = localPluginContext.InitiatingUserService;

            // 1. read the request parameters to get the case record being created
            if (!context.InputParameters.Contains("CaseId"))
            {
                throw new InvalidPluginExecutionException("CaseId parameter is missing.");
            }
            Guid caseId = (Guid)context.InputParameters["CaseId"];
            localPluginContext.Trace($"AssignWorker: caseId={caseId}");

            // find worker with lowest caseload
            var query = new QueryExpression("cr36f_worker")
            {
              ColumnSet = new ColumnSet("cr36f_name", "cr36f_caseloadcount")  ,
              TopCount = 1,
            };
            query.AddOrder("cr36f_caseloadcount", OrderType.Ascending);

            var result = service.RetrieveMultiple(query);
            if (result.Entities.Count == 0)
            {
                throw new InvalidPluginExecutionException("No workers found.");
            }
            localPluginContext.Trace($"AssignWorker: found {result.Entities.Count} workers");

            // assign the cast to workers with lowest caseload
            var worker = result.Entities[0];
            String workerName = worker.GetAttributeValue<string>("cr36f_name");
            localPluginContext.Trace($"AssignWorker: assigning case to worker {workerName}");

            // assign worker to case
            var caseToUpdate = new Entity("cr36f_case", caseId);
            caseToUpdate["cr36f_assignedworker"] = worker.ToEntityReference();
            service.Update(caseToUpdate);

            // return the response property. key is the unique name 
            context.OutputParameters["WorkerName"] = workerName;
            localPluginContext.Trace($"AssignWorker: returning WorkerName={workerName}");
        }
    }
}