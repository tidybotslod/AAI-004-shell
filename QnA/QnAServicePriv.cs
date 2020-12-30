using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.CognitiveServices.Knowledge.QnAMaker.Models;
using Microsoft.Azure.CognitiveServices.Knowledge.QnAMaker;

namespace AAI
{
    public partial class QnAService
    {

        //
        // Data members
        //
        private IConfiguration config;                // Values found in appsettings.json
        private QnAMakerClient azureEndpoint;         // Access to qna service endpoint in azure (see azure portal)
        private QnAMakerRuntimeClient qnaEndpoint;    // Access to qna maker service endpoint (see www.qnamaker.ai)
        private string knowledgeBaseID;               // Current knowledge base being accessed
        private string queryEndpointKey;              // Authorization key used to access qna maker service
        //
        // Provided methods
        //
        /// <summary>
        /// Configuration, use appsettings.json to retain the Knowledge Base ID written out when QnA knowledge base is created
        /// and the query key endpoint as well.
        /// </summary>
        public QnAService()
        {
            config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).Build();
            queryEndpointKey = config["QueryEndpointKey"];
            if (queryEndpointKey != null && queryEndpointKey.Length == 0)
            {
                queryEndpointKey = null;
            }
            knowledgeBaseID = config["KnowledgeBaseID"];
            if (knowledgeBaseID != null && knowledgeBaseID.Length == 0)
            {
                knowledgeBaseID = null;
            }
        }
        /// <summary>
        /// Knowledge base id used for operations defined in QnAService.
        /// </summary>
        /// <summary>
        /// 
        /// </summary>
        public string KnowledgeBaseID
        {
            get
            {
                return knowledgeBaseID;
            }
            set
            {
                knowledgeBaseID = value;
                queryEndpointKey = null;
            }
        }
        //======================================
        // Methods to be used only by QnAService
        /// <summary>
        /// Azure endpoint is used to create, publish, download, update, and delete QnA knowledge bases. Creates
        /// one using configured data. 
        /// </summary>
        /// <returns>azure endpoint object</returns>
        private QnAMakerClient AzureEndpoint()
        {
            return azureEndpoint ?? CreateConfiguredAzureEndpoint();
        }
        /// <summary>
        /// Make alterations to the knowledge base, polls until the knowledge base has been updated.
        /// </summary>
        /// <param name="additions"></param>
        /// <param name="updates"></param>
        /// <param name="deletes"></param>
        /// <returns>(Operation state, optional error response)</returns>
        private async Task<(string, string)> AlterKb(IList<QnADTO> additions, IList<UpdateQnaDTO> updates, IList<Nullable<Int32>> deletes)
        {
            var update = new UpdateKbOperationDTO
            {
                Add = additions != null && additions.Count > 0 ? new UpdateKbOperationDTOAdd { QnaList = additions } : null,
                Update = updates != null && updates.Count > 0 ? new UpdateKbOperationDTOUpdate { QnaList = updates } : null,
                Delete = deletes != null && deletes.Count > 0 ? new UpdateKbOperationDTODelete { Ids = deletes } : null
            };
            var op = await AzureEndpoint().Knowledgebase.UpdateAsync(knowledgeBaseID, update);
            op = await MonitorOperation(op);
            return (op.OperationState, op.ErrorResponse == null ? null : op.ErrorResponse.ToString());
        }
        /// <summary>
        /// Download knowledge base creating dictionary where the lookup is the answer. There can be multiple questions per answer.
        /// </summary>
        /// <param name="published"></param>
        /// <returns>dictionary of entire knowledge base</returns>
        private async Task<Dictionary<string, QnADTO>> GetExistingAnswers(bool published)
        {
            QnADocumentsDTO kb = await AzureEndpoint().Knowledgebase.DownloadAsync(knowledgeBaseID, published ? EnvironmentType.Prod : EnvironmentType.Test);
            Dictionary<string, QnADTO> existing = new Dictionary<string, QnADTO>();
            foreach (QnADTO entry in kb.QnaDocuments)
            {
                existing.Add(entry.Answer, entry);
            }
            return existing;
        }
        /// <summary>
        /// Moniter long running operations by polling the status of an operation occuring in Azure QnA service
        /// </summary>
        /// <param name="operation">Operation returned from call to monitor</param>
        /// <returns>Final operation</returns>
        private async Task<Operation> MonitorOperation(Operation operation)
        {
            // Loop while operation is success
            for (int i = 0;
                i < 20 && (operation.OperationState == OperationStateType.NotStarted || operation.OperationState == OperationStateType.Running);
                i++)
            {
                Console.WriteLine("Waiting for operation: {0} to complete.", operation.OperationId);
                await Task.Delay(5000);
                operation = await AzureEndpoint().Operations.GetDetailsAsync(operation.OperationId);
            }

            if (operation.OperationState != OperationStateType.Succeeded)
            {
                throw new Exception($"Operation {operation.OperationId} failed to completed.");
            }
            return operation;
        }
        /// <summary>
        /// QnA endpoint is used to query and train knowledge base. 
        /// </summary>
        /// <returns></returns>
        private async Task<QnAMakerRuntimeClient> QnAEndpoint()
        {
            return qnaEndpoint ?? await CreateQnAEndpoint();
        }
        //==================================================================
        // Helper methods targeted for QnAServicePriv only, not docuemented.
        private QnAMakerClient CreateConfiguredAzureEndpoint()
        {
            ApiKeyServiceClientCredentials credentials = new ApiKeyServiceClientCredentials(config["AuthoringKey"]);
            azureEndpoint = new QnAMakerClient(credentials);
            azureEndpoint.Endpoint = $"https://{config["ResourceName"]}.cognitiveservices.azure.com";
            return azureEndpoint;
        }
        private async Task<QnAMakerRuntimeClient> CreateQnAEndpoint()
        {
            var endpointKey = await QueryKey();
            var credentials = new EndpointKeyServiceClientCredentials(endpointKey);
            string queryEndpoint = $"https://{config["ApplicationName"]}.azurewebsites.net";
            qnaEndpoint = new QnAMakerRuntimeClient(credentials) { RuntimeEndpoint = queryEndpoint };
            return qnaEndpoint;
        }
        private async Task<string> QueryKey()
        {
            return queryEndpointKey ?? await RetrieveEndpointKey();
        }
        private async Task<string> RetrieveEndpointKey()
        {
            var endpointKeysObject = await AzureEndpoint().EndpointKeys.GetKeysAsync();
            queryEndpointKey = endpointKeysObject.PrimaryEndpointKey;
            return queryEndpointKey;
        }
    }
}
