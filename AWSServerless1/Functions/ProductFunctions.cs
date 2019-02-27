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

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AWSServerless1.Functions
{
    public class ProductFunctions
    {
        // This const is the name of the environment variable that the serverless.template will use to set
        // the name of the DynamoDB table used to store product posts.
        const string TABLENAME_ENVIRONMENT_VARIABLE_LOOKUP = "ProductTable";

        public const string ID_QUERY_STRING_NAME = "Id";
        IDynamoDBContext DDBContext { get; set; }

        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public ProductFunctions()
        {
            // Check to see if a table name was passed in through environment variables and if so 
            // add the table mapping.
            var tableName = System.Environment.GetEnvironmentVariable(TABLENAME_ENVIRONMENT_VARIABLE_LOOKUP);
            if (!string.IsNullOrEmpty(tableName))
            {
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(Product)] = new Amazon.Util.TypeMapping(typeof(Product), tableName);
            }

            var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
            this.DDBContext = new DynamoDBContext(new AmazonDynamoDBClient(), config);
        }

        /// <summary>
        /// Constructor used for testing passing in a preconfigured DynamoDB client.
        /// </summary>
        /// <param name="ddbClient"></param>
        /// <param name="tableName"></param>
        public ProductFunctions(IAmazonDynamoDB ddbClient, string tableName)
        {
            if (!string.IsNullOrEmpty(tableName))
            {
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(Product)] = new Amazon.Util.TypeMapping(typeof(Product), tableName);
            }

            var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
            this.DDBContext = new DynamoDBContext(ddbClient, config);
        }

        /// <summary>
        /// A Lambda function that returns back a page worth of product posts.
        /// </summary>
        /// <param name="request"></param>
        /// <returns>The list of products</returns>
        public async Task<APIGatewayProxyResponse> GetProductsAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("Getting products");
            var search = this.DDBContext.ScanAsync<Product>(null);
            var page = await search.GetNextSetAsync();
            context.Logger.LogLine($"Found {page.Count} products");

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(page),
                Headers = HeaderHelper.GetHeaderAttributes()

            };

            return response;
        }

        /// <summary>
        /// A Lambda function that returns the product identified by productId
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> GetProductAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            string productId = null;
            if (request.PathParameters != null && request.PathParameters.ContainsKey(ID_QUERY_STRING_NAME))
                productId = request.PathParameters[ID_QUERY_STRING_NAME];
            else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(ID_QUERY_STRING_NAME))
                productId = request.QueryStringParameters[ID_QUERY_STRING_NAME];

            if (string.IsNullOrEmpty(productId))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Body = $"Missing required parameter {ID_QUERY_STRING_NAME}"
                };
            }

            context.Logger.LogLine($"Getting product {productId}");
            var product = await DDBContext.LoadAsync<Product>(productId);
            context.Logger.LogLine($"Found product: {product != null}");

            if (product == null)
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.NotFound
                };
            }
                       
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(product),
                Headers = HeaderHelper.GetHeaderAttributes()
            };
            return response;
        }

        /// <summary>
        /// A Lambda function that adds a product post.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> AddProductAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var product = JsonConvert.DeserializeObject<Product>(request?.Body);
            product.Id = Guid.NewGuid().ToString();
            product.CreatedTimestamp = DateTime.Now;

            context.Logger.LogLine($"Saving product with id {product.Id}");
            await DDBContext.SaveAsync<Product>(product);
            context.Logger.LogLine($"Conditions - {!string.IsNullOrEmpty(product.Id)} && {!string.IsNullOrWhiteSpace(product.Id)}");
            if (!string.IsNullOrEmpty(product.Id) && !string.IsNullOrWhiteSpace(product.Id))
            {
                context.Logger.LogLine($"Product Category - {product.Category}");
                // await FirebaseCloudMessagingHelper.SendPushNotification("dfrrFgYOHiU:APA91bGYyzADHof0ZLQg-on8l3JHIPYerYQtF8SS2VdUusVSh2bO3NntOZKy_W4_BUQ5_JB5kD7NZIZo915vEcdYwZEBKbwPg1n1gdR5pEV0kkiCIvhhD5i5alPY5Tv4VM8sPuNXmcJr", product.Name, "Just added and available for bidding.", product.ImageUrl, null);
                await FirebaseCloudMessagingHelper.SendPushNotification("/topics/" + product.Category, product.Name, "Just added and available for bidding.", product.ImageUrl, product);
            }

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(product),
                Headers = HeaderHelper.GetHeaderAttributes()
            };
            return response;
        }

        /// <summary>
        /// A Lambda function that update a product post.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> UpdateProductAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var product = JsonConvert.DeserializeObject<Product>(request?.Body);

            string productId = null;
            if (request.PathParameters != null && request.PathParameters.ContainsKey(ID_QUERY_STRING_NAME))
                productId = request.PathParameters[ID_QUERY_STRING_NAME];
            else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(ID_QUERY_STRING_NAME))
                productId = request.QueryStringParameters[ID_QUERY_STRING_NAME];

            if (string.IsNullOrEmpty(productId))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Body = $"Missing required parameter {ID_QUERY_STRING_NAME}"
                };
            }
            else
            {
                product.Id = productId;
            }

            context.Logger.LogLine($"Saving product with id {product.Id}");
            await DDBContext.SaveAsync<Product>(product);

            if (!string.IsNullOrEmpty(product.Id) && !string.IsNullOrWhiteSpace(product.Id))
            {
                // await FirebaseCloudMessagingHelper.SendPushNotification("dfrrFgYOHiU:APA91bGYyzADHof0ZLQg-on8l3JHIPYerYQtF8SS2VdUusVSh2bO3NntOZKy_W4_BUQ5_JB5kD7NZIZo915vEcdYwZEBKbwPg1n1gdR5pEV0kkiCIvhhD5i5alPY5Tv4VM8sPuNXmcJr", product.Name, "Just added and available for bidding.", product.ImageUrl, null);
                await FirebaseCloudMessagingHelper.SendPushNotification("/topic/" + product.Category, product.Name, "Just updated and available for bidding.", product.ImageUrl, null);
            }

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(product),
                Headers = HeaderHelper.GetHeaderAttributes()
            };
            return response;
        }

        /// <summary>
        /// A Lambda function that removes a product post from the DynamoDB table.
        /// </summary>
        /// <param name="request"></param>
        public async Task<APIGatewayProxyResponse> RemoveProductAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            string productId = null;
            if (request.PathParameters != null && request.PathParameters.ContainsKey(ID_QUERY_STRING_NAME))
                productId = request.PathParameters[ID_QUERY_STRING_NAME];
            else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(ID_QUERY_STRING_NAME))
                productId = request.QueryStringParameters[ID_QUERY_STRING_NAME];

            if (string.IsNullOrEmpty(productId))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Body = $"Missing required parameter {ID_QUERY_STRING_NAME}"
                };
            }

            context.Logger.LogLine($"Deleting product with id {productId}");
            await this.DDBContext.DeleteAsync<Product>(productId);

            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK
            };
        }
    }
}
