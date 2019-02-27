using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using AWSServerless1.Helpers;
using AWSServerless1.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace AWSServerless1.Functions
{
    class BidFunctions
    {
        // This const is the name of the environment variable that the serverless.template will use to set
        // the name of the DynamoDB table used to store bid posts.
        const string TABLENAME_ENVIRONMENT_VARIABLE_LOOKUP = "BidTable";

        public const string ID_QUERY_STRING_NAME = "Id";
        public const string PRODUCT_ID_QUERY_STRING_NAME = "ProductId";
        IDynamoDBContext DDBContext { get; set; }

        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public BidFunctions()
        {
            // Check to see if a table name was passed in through environment variables and if so 
            // add the table mapping.
            var tableName = System.Environment.GetEnvironmentVariable(TABLENAME_ENVIRONMENT_VARIABLE_LOOKUP);
            if (!string.IsNullOrEmpty(tableName))
            {
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(Bid)] = new Amazon.Util.TypeMapping(typeof(Bid), tableName);
            }

            var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
            this.DDBContext = new DynamoDBContext(new AmazonDynamoDBClient(), config);
        }

        /// <summary>
        /// Constructor used for testing passing in a preconfigured DynamoDB client.
        /// </summary>
        /// <param name="ddbClient"></param>
        /// <param name="tableName"></param>
        public BidFunctions(IAmazonDynamoDB ddbClient, string tableName)
        {
            if (!string.IsNullOrEmpty(tableName))
            {
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(Bid)] = new Amazon.Util.TypeMapping(typeof(Bid), tableName);
            }

            var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
            this.DDBContext = new DynamoDBContext(ddbClient, config);
        }

        /// <summary>
        /// A Lambda function that returns back a page worth of bid posts.
        /// </summary>
        /// <param name="request"></param>
        /// <returns>The list of bids</returns>
        public async Task<APIGatewayProxyResponse> GetBidsAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("Getting bids");
            var search = this.DDBContext.ScanAsync<Bid>(null);
            var page = await search.GetNextSetAsync();
            context.Logger.LogLine($"Found {page.Count} bids");

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(page),
                Headers = HeaderHelper.GetHeaderAttributes()
            };

            return response;
        }

        /// <summary>
        /// A Lambda function that returns the bid identified by bidId
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> GetBidAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            string bidId = null;
            if (request.PathParameters != null && request.PathParameters.ContainsKey(ID_QUERY_STRING_NAME))
                bidId = request.PathParameters[ID_QUERY_STRING_NAME];
            else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(ID_QUERY_STRING_NAME))
                bidId = request.QueryStringParameters[ID_QUERY_STRING_NAME];

            if (string.IsNullOrEmpty(bidId))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Body = $"Missing required parameter {ID_QUERY_STRING_NAME}"
                };
            }

            context.Logger.LogLine($"Getting bid {bidId}");
            var bid = await DDBContext.LoadAsync<Bid>(bidId);
            context.Logger.LogLine($"Found bid: {bid != null}");

            if (bid == null)
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.NotFound
                };
            }

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(bid),
                Headers = HeaderHelper.GetHeaderAttributes()
            };
            return response;
        }

        /// <summary>
        /// A Lambda function that returns the bid identified by bidId
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> GetBidsByProductIdAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            string productId = null;
            if (request.PathParameters != null && request.PathParameters.ContainsKey(PRODUCT_ID_QUERY_STRING_NAME))
                productId = request.PathParameters[PRODUCT_ID_QUERY_STRING_NAME];
            else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(PRODUCT_ID_QUERY_STRING_NAME))
                productId = request.QueryStringParameters[PRODUCT_ID_QUERY_STRING_NAME];

            if (string.IsNullOrEmpty(productId))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Body = $"Missing required parameter {ID_QUERY_STRING_NAME}"
                };
            }

            context.Logger.LogLine($"Getting bids {productId}");

            var conditions = new List<ScanCondition>();
            conditions.Add(new ScanCondition("ProductId", Amazon.DynamoDBv2.DocumentModel.ScanOperator.Equal, productId));
            var search = this.DDBContext.ScanAsync<Bid>(conditions);
            var page = await search.GetNextSetAsync();
            context.Logger.LogLine($"Found {page.Count} bids");

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(page),
                Headers = HeaderHelper.GetHeaderAttributes()
            };
            return response;
        }

        /// <summary>
        /// A Lambda function that adds a bid post.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> AddBidAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var bid = JsonConvert.DeserializeObject<Bid>(request?.Body);
            bid.Id = Guid.NewGuid().ToString();
            bid.CreatedTimestamp = DateTime.Now;

            context.Logger.LogLine($"Saving bid with id {bid.Id}");
            await DDBContext.SaveAsync<Bid>(bid);

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(bid),
                Headers = HeaderHelper.GetHeaderAttributes()
            };
            return response;
        }

        /// <summary>
        /// A Lambda function that removes a bid post from the DynamoDB table.
        /// </summary>
        /// <param name="request"></param>
        public async Task<APIGatewayProxyResponse> RemoveBidAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            string bidId = null;
            if (request.PathParameters != null && request.PathParameters.ContainsKey(ID_QUERY_STRING_NAME))
                bidId = request.PathParameters[ID_QUERY_STRING_NAME];
            else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(ID_QUERY_STRING_NAME))
                bidId = request.QueryStringParameters[ID_QUERY_STRING_NAME];

            if (string.IsNullOrEmpty(bidId))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Body = $"Missing required parameter {ID_QUERY_STRING_NAME}"
                };
            }

            context.Logger.LogLine($"Deleting bid with id {bidId}");
            await this.DDBContext.DeleteAsync<Bid>(bidId);

            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK
            };
        }
    }
}
