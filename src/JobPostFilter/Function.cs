using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Newtonsoft.Json.Linq;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace JobPostFilter
{
    public class Function
    {
        static AmazonDynamoDBClient client = new AmazonDynamoDBClient();
        Table bodyTable = Table.LoadTable(client, GlobalVars.BODY_TABLE);
        Table urlTable = Table.LoadTable(client, GlobalVars.URL_TABLE);

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {

        }


        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an SQS event object and can be used 
        /// to respond to SQS messages.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
        {
            IDBFacade db = new AWSDB();
            IQueueFacade queue = new AWSQueue();

            foreach (var message in evnt.Records)
            {
                await ProcessMessageAsync(message, context, db, queue);
            }
        }

        public async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context, IDBFacade db, IQueueFacade queue)
        {
            JObject jobPost = JObject.Parse(message.Body);
            string queueUri = await GetQueueForMessage(jobPost, db, context);
            // publish message to the corresponding SQS queue

            await queue.PublishToQueue(message.Body, queueUri);

            await Task.CompletedTask;
        }

        public async Task<string> GetQueueForMessage(JObject jobPost, IDBFacade db, ILambdaContext context)
        {
            bool isValid = Utility.IsSchemaValid(jobPost);
            string queueUri = "";

            if (isValid)
            {
                string jobPostUrl = jobPost.Value<string>("sourceId");
                string jobPostHash = jobPost.Value<string>("hash");

                bool urlPresent = await db.ItemExists(jobPostUrl, urlTable);

                if (urlPresent == false)
                {
                    db.PutItem(jobPostUrl, urlTable, "url");

                    bool bodyPresent = await db.ItemExists(jobPostHash, bodyTable);

                    if (bodyPresent == false)
                    {
                        db.PutItem(jobPostHash, bodyTable, "sourceHash");
                        queueUri = "https://sqs.eu-west-1.amazonaws.com/833191605868/" + GlobalVars.SUCESS_QUEUE;
                    }
                    else
                        queueUri = "https://sqs.eu-west-1.amazonaws.com/833191605868/" + GlobalVars.EXISTING_QUEUE;
                }
                else
                    queueUri = "https://sqs.eu-west-1.amazonaws.com/833191605868/" + GlobalVars.EXISTING_QUEUE;
            }
            else
                queueUri = "https://sqs.eu-west-1.amazonaws.com/833191605868/" + GlobalVars.INVALID_QUEUE;

            return queueUri;
        }
    }
}