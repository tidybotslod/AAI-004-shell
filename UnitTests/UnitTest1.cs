#if (TestCrud || TestUpdate)
#define CreateEnabled
#define AskEnabled
#define AddEnabled
#define UpdateEnabled
#elif (TestCreate)
#define CreateEnabled
#elif (TestAsk)
#define CreateEnabled
#define AskEnabled
#elif (TestAdd)
#define CreateEnabled
#define AskEnabled
#define AddEnabled
#endif

using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Azure.CognitiveServices.Knowledge.QnAMaker.Models;

namespace AAI
{
    [TestClass()]
    public class UnitTest1
    {
        public static QnAService service;

        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context)
        {
            IConfiguration config; // Load configuration data found in appsettings.json, need Azure authoring key and resource name to build URL to azure.
            config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).Build();
            service = new QnAService
            {
                AuthoringKey = ConfigurationValue(config, "AuthoringKey"),
                ResourceName = ConfigurationValue(config, "ResourceName"),
                ApplicationName = ConfigurationValue(config, "ApplicationName"),
                KnowledgeBaseID = ConfigurationValue(config, "KnowledgeBaseID"),
                QueryEndpointKey = ConfigurationValue(config, "QueryEndpointKey")
            };
        }
#if (CreateEnabled)
        //
        // Create QnA knowledge base.
        // Will contain one Question and Answer, the answer has temporary text that will be removed in the update test.
        private async Task<bool> CreateDatabase()
        {
            Guid name = Guid.NewGuid();
            var create = new CreateKbDTO
            {
                Name = $"{name} Test KB",
                QnaList = QnAFile.LoadCSV("..\\..\\..\\..\\Data\\create-faq.csv")
            };

            var (status, id) = await service.CreateQnA(create);
            Assert.AreEqual(OperationStateType.Succeeded, status);
            Assert.IsNotNull(id);
            service.KnowledgeBaseID = id;
            return true;
        }
        //
        // Publish the QnA knowledge base.
        private async Task<bool> PublishDatabase()
        {
            await service.Publish();
            return true;
        }
        //
        // Delete the data base
        private async Task<bool> CleanUp()
        {
            await service.DeleteKnowledgeBase();
            return true;
        }
#endif
#if (AskEnabled)
        //
        // Create QnA knowledge base.
        // Will contain one Question and Answer, the answer has temporary text that will be removed in the update test.
        private async Task<bool> AskQuestion(bool production)
        {
            // Question has unwanted text in it. Check for the unwanted text. Also question should
            // only return 1 match. The other Answer added should never match this question.
            QnASearchResultList answer = await service.Ask("HOW CAN I CHANGE MY SHIPPING ADDRESS?", production);
            Assert.IsNotNull(answer);
            Assert.IsNotNull(answer.Answers);
            Assert.AreEqual(1, answer.Answers.Count);
            Assert.IsNotNull(answer.Answers[0].Questions);
            Assert.AreEqual(1, answer.Answers[0].Questions.Count);
            bool found = answer.Answers[0].Questions[0].Contains("##REPLACE##");
            return found;
        }
#endif
#if (AddEnabled && AskEnabled)
        //
        // Add an additional Question and Answer, the answer has temporary text that will be removed in the update test.
        private async Task<bool> AddQuestion(bool production)
        {
            var (status, error) = await service.AddToQnA(QnAFile.LoadCSV("..\\..\\..\\..\\Data\\add-faq.csv"));
            Assert.AreEqual(OperationStateType.Succeeded, status);
            Assert.IsNull(error);
            if(production)
            {
                await service.Publish();
            }
            // Question has unwanted text in it. Check for the unwanted text. Also question should
            // only return 1 match. The other Answer added should never match this question.
            QnASearchResultList answer = await service.Ask("WHAT DO YOU MEAN BY POINTS", production);
            Assert.IsNotNull(answer);
            Assert.IsNotNull(answer.Answers);
            Assert.AreEqual(1, answer.Answers.Count);
            Assert.IsNotNull(answer.Answers[0].Questions);
            Assert.AreEqual(1, answer.Answers[0].Questions.Count);
            bool found = answer.Answers[0].Questions[0].Contains("##REPLACE##");
            return found;
        }
#endif
#if (UpdateEnabled && AskEnabled)
        //
        // Update the existing two quesions to remove temporary text.
        private async Task<bool> UpdateQuestions(bool production)
        {
            var (status, error) = await service.UpdateQnA(QnAFile.LoadCSV("..\\..\\..\\..\\Data\\update-faq.csv"));
            Assert.AreEqual(OperationStateType.Succeeded, status);
            Assert.IsNull(error);
            if (production)
            {
                await service.Publish();
            }
            // Bad text in question is removed.
            QnASearchResultList answer = await service.Ask("WHAT DO YOU MEAN BY POINTS", production);
            Assert.IsNotNull(answer);
            Assert.IsNotNull(answer.Answers);
            Assert.AreEqual(1, answer.Answers.Count);
            Assert.IsNotNull(answer.Answers[0].Questions);
            Assert.AreEqual(1, answer.Answers[0].Questions.Count);
            bool question1 = answer.Answers[0].Questions[0].Contains("##REPLACE##") == false;

            // Bad text in question is removed.
            answer = await service.Ask("HOW CAN I CHANGE MY SHIPPING ADDRESS", production);
            Assert.IsNotNull(answer);
            Assert.IsNotNull(answer.Answers);
            Assert.AreEqual(1, answer.Answers.Count);
            Assert.IsNotNull(answer.Answers[0].Questions);
            Assert.AreEqual(1, answer.Answers[0].Questions.Count);
            bool question2 = answer.Answers[0].Questions[0].Contains("##REPLACE##") == false;

            return question1 && question2;
        }
#endif
#if (AddEnabled && AskEnabled)
//
        // Add full text, will add additional questions to the first two entries.
        private async Task<bool> AddFullText(bool production)
        {
            var (status, error) = await service.AddToQnA(QnAFile.LoadCSV("..\\..\\..\\..\\Data\\full-faq.csv"));
            Assert.AreEqual(OperationStateType.Succeeded, status);
            Assert.IsNull(error);
            if (production)
            {
                await service.Publish();
            }
            // Question has unwanted text in it. Check for the unwanted text. Also question should
            // only return 1 match. The other Answer added should never match this question.
            QnASearchResultList answer = await service.Ask("What perks do I get for shopping with you", production);
            Assert.IsNotNull(answer);
            Assert.IsNotNull(answer.Answers);
            Assert.AreEqual(1, answer.Answers.Count);
            Assert.IsNotNull(answer.Answers[0].Questions);
            Assert.AreEqual(2, answer.Answers[0].Questions.Count);
            bool found = answer.Answers[0].Answer.Contains("Because you are important to us");
            return found;
        }
#endif
#if (TestCreate)
        [TestMethod()]
        public void TestCreate()
        {
            try
            {
                Task.Run(async () => { Assert.IsTrue(await CreateDatabase()); }).Wait();
            }
            finally
            {
                if (service.KnowledgeBaseID != null && service.KnowledgeBaseID.Length > 0)
                {
                    Task.Run(async () => { Assert.IsTrue(await CleanUp()); }).Wait();
                }
            }
        }
#endif
#if (TestAsk)
        [TestMethod()]
        public void TestCreate()
        {
            try
            {
                bool production = false;
                Task.Run(async () => { Assert.IsTrue(await CreateDatabase()); }).Wait();
                Task.Run(async () => { Assert.IsTrue(await AskQuestion(production)); }).Wait();
            }
            finally
            {
                if (service.KnowledgeBaseID != null && service.KnowledgeBaseID.Length > 0)
                {
                    Task.Run(async () => { Assert.IsTrue(await CleanUp()); }).Wait();
                }
            }
        }
#endif
#if (TestAdd)
        [TestMethod()]
        public void TestAdd()
        {
            try
            {
                bool production = false;
                Task.Run(async () => { Assert.IsTrue(await CreateDatabase()); }).Wait();
                Task.Run(async () => { Assert.IsTrue(await AskQuestion(production)); }).Wait();
                Task.Run(async () => { Assert.IsTrue(await AddQuestion(production)); }).Wait();
            }
            finally
            {
                if (service.KnowledgeBaseID != null && service.KnowledgeBaseID.Length > 0)
                {
                    Task.Run(async () => { Assert.IsTrue(await CleanUp()); }).Wait();
                }
            }
        }
#endif
#if (TestUpdate)
        [TestMethod()]
        public void TestUpdate()
        {
            try
            {
                bool production = false;
                Task.Run(async () => { Assert.IsTrue(await CreateDatabase()); }).Wait();
                Task.Run(async () => { Assert.IsTrue(await AskQuestion(production)); }).Wait();
                Task.Run(async () => { Assert.IsTrue(await AddQuestion(production)); }).Wait();
                Task.Run(async () => { Assert.IsTrue(await UpdateQuestions(production)); }).Wait();
            }
            finally
            {
                if (service.KnowledgeBaseID != null && service.KnowledgeBaseID.Length > 0)
                {
                    Task.Run(async () => { Assert.IsTrue(await CleanUp()); }).Wait();
                }
            }
        }
#endif
#if (TestCrud)
        [TestMethod()]
        public void CrudTest()
        {
            try
            {
                bool production = true;
                Task.Run(async () => { Assert.IsTrue(await CreateDatabase()); }).Wait();
                Task.Run(async () => { Assert.IsTrue(await PublishDatabase());  }).Wait();
                Task.Run(async () => { Assert.IsTrue(await AskQuestion(production)); }).Wait();
                Task.Run(async () => { Assert.IsTrue(await AddQuestion(production)); }).Wait();
                Task.Run(async () => { Assert.IsTrue(await UpdateQuestions(production)); }).Wait();
                Task.Run(async () => { Assert.IsTrue(await AddFullText(production)); }).Wait();
            }
            finally
            {
                if (service.KnowledgeBaseID != null && service.KnowledgeBaseID.Length > 0)
                {
                    Task.Run(async () => { Assert.IsTrue(await CleanUp()); }).Wait();
                }
            }
        }

#endif

        private static string ConfigurationValue(IConfiguration config, string name)
        {
            string value = config[name];
            if (value != null && value.Length == 0)
            {
                value = null;
            }
            return value;
        }
    }
}
