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
        /// <returns>zaure endpoint object</returns>
        private QnAMakerClient AzureEndpoint()
        {
            return azureEndpoint ?? CreateConfiguredAzureEndpoint();
        }
        /// <summary>
        /// QnA endpoint is used to query and train knowledge base. 
        /// </summary>
        /// <returns></returns>
        private async Task<QnAMakerRuntimeClient> QnAEndpoint()
        {
            return qnaEndpoint ?? await CreateQnAEndpoint();
        }
        /// <summary>
        /// Creates a knowledge base, polls service until the knowledge base is created and accessible. 
        /// </summary>
        /// <param name="qnaData">Information used in the creation of the knowledge base</param>
        /// <returns></returns>
        private async Task<Operation> CreateKnowledgeBase(CreateKbDTO qnaData)
        {
            QnAMakerClient endpoint = AzureEndpoint();
            Operation op = await endpoint.Knowledgebase.CreateAsync(qnaData);
            op = await MonitorOperation(op);
            return op;
        }
        /// <summary>
        /// Download knowledge base creating dictionary where the lookup is the answer. There can be multiple questions per answer.
        /// </summary>
        /// <param name="published"></param>
        /// <returns>dictionary of entire knowledge base.</returns>
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
        /// 
        /// </summary>
        /// <param name="entries"></param>
        /// <param name="published"></param>
        /// <param name="overrideQuestions"></param>
        /// <returns></returns>
        private async Task<(string, string)> ModifyQnA(IList<QnADTO> entries, bool published, bool overrideQuestions)
        {
            Dictionary<string, QnADTO> existing = await GetExistingAnswers(published);

            List<UpdateQnaDTO> updates = new List<UpdateQnaDTO>();
            List<QnADTO> additions = new List<QnADTO>();
            if (entries != null && entries.Count > 0)
            {
                foreach (QnADTO update in entries)
                {
                    QnADTO value = null;
                    if (existing.TryGetValue(update.Answer, out value))
                    {
                        UpdateQnaDTO modified = new UpdateQnaDTO();
                        modified.Answer = value.Answer;
                        modified.Id = value.Id;
                        modified.Questions = new UpdateQnaDTOQuestions(update.Questions, overrideQuestions ? value.Questions : null);
                        updates.Add(modified);
                    }
                    else
                    {
                        additions.Add(update);
                    }
                }
            }
            return await AlterKb(additions, updates, null);
        }
        //==================================================================
        // Helper methods targeted to QnAServicePrive only, not docuemented.
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
        //=============================================================
        // Data members, not intended to be used outside QnAServicePriv
        private IConfiguration config;                // Values found in appsettings.json
        private QnAMakerClient azureEndpoint;         // Access to qna service endpoint in azure (see azure portal)
        private QnAMakerRuntimeClient qnaEndpoint;    // Access to qna maker service endpoint (see www.qnamaker.ai)

        private string knowledgeBaseID;               // Current knowledge base being accessed
        private string queryEndpointKey;              // Authorization key used to access qna maker service
    }
}

