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
    class UserFunctions
    {
        // This const is the name of the environment variable that the serverless.template will use to set
        // the name of the DynamoDB table used to store user posts.
        const string TABLENAME_ENVIRONMENT_VARIABLE_LOOKUP = "UserTable";

        public const string ID_QUERY_STRING_NAME = "Id";
        public const string UserName_QUERY_STRING_NAME = "UserName";
        public const string Password_QUERY_STRING_NAME = "Password";
        IDynamoDBContext DDBContext { get; set; }

        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public UserFunctions()
        {
            // Check to see if a table name was passed in through environment variables and if so 
            // add the table mapping.
            var tableName = System.Environment.GetEnvironmentVariable(TABLENAME_ENVIRONMENT_VARIABLE_LOOKUP);
            if (!string.IsNullOrEmpty(tableName))
            {
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(User)] = new Amazon.Util.TypeMapping(typeof(User), tableName);
            }

            var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
            this.DDBContext = new DynamoDBContext(new AmazonDynamoDBClient(), config);
        }

        /// <summary>
        /// Constructor used for testing passing in a preconfigured DynamoDB client.
        /// </summary>
        /// <param name="ddbClient"></param>
        /// <param name="tableName"></param>
        public UserFunctions(IAmazonDynamoDB ddbClient, string tableName)
        {
            if (!string.IsNullOrEmpty(tableName))
            {
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(User)] = new Amazon.Util.TypeMapping(typeof(User), tableName);
            }

            var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
            this.DDBContext = new DynamoDBContext(ddbClient, config);
        }

        /// <summary>
        /// A Lambda function that returns back a page worth of user posts.
        /// </summary>
        /// <param name="request"></param>
        /// <returns>The list of users</returns>
        public async Task<APIGatewayProxyResponse> GetUsersAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("Getting users");
            var search = this.DDBContext.ScanAsync<User>(null);
            var page = await search.GetNextSetAsync();
            context.Logger.LogLine($"Found {page.Count} users");

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(page),
                Headers = HeaderHelper.GetHeaderAttributes()
            };

            return response;
        }

        /// <summary>
        /// A Lambda function that returns the user identified by userId
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> GetUserAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            string userId = null;
            if (request.PathParameters != null && request.PathParameters.ContainsKey(ID_QUERY_STRING_NAME))
                userId = request.PathParameters[ID_QUERY_STRING_NAME];
            else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(ID_QUERY_STRING_NAME))
                userId = request.QueryStringParameters[ID_QUERY_STRING_NAME];

            if (string.IsNullOrEmpty(userId))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Body = $"Missing required parameter {ID_QUERY_STRING_NAME}"
                };
            }

            context.Logger.LogLine($"Getting user {userId}");
            var user = await DDBContext.LoadAsync<User>(userId);
            context.Logger.LogLine($"Found user: {user != null}");

            if (user == null)
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.NotFound
                };
            }

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(user),
                Headers = HeaderHelper.GetHeaderAttributes()
            };
            return response;
        }

        /// <summary>
        /// A Lambda function that returns the user identified by username & password
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> UserLoginAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            string userName = null, password = null;
            if (request.PathParameters != null && request.PathParameters.ContainsKey(UserName_QUERY_STRING_NAME))
                userName = request.PathParameters[UserName_QUERY_STRING_NAME];
            else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(UserName_QUERY_STRING_NAME))
                userName = request.QueryStringParameters[UserName_QUERY_STRING_NAME];

            if (request.PathParameters != null && request.PathParameters.ContainsKey(Password_QUERY_STRING_NAME))
                password = request.PathParameters[Password_QUERY_STRING_NAME];
            else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(Password_QUERY_STRING_NAME))
                password = request.QueryStringParameters[Password_QUERY_STRING_NAME];

            if (string.IsNullOrEmpty(userName) && string.IsNullOrEmpty(password))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Body = $"Missing required parameters {UserName_QUERY_STRING_NAME} & {Password_QUERY_STRING_NAME}"
                };
            }

            context.Logger.LogLine($"Getting user for {userName} - {password}");

            var conditions = new List<ScanCondition>();
            conditions.Add(new ScanCondition("UserName", Amazon.DynamoDBv2.DocumentModel.ScanOperator.Equal, userName));
            conditions.Add(new ScanCondition("Password", Amazon.DynamoDBv2.DocumentModel.ScanOperator.Equal, password));
            var search = this.DDBContext.ScanAsync<User>(conditions);
            var page = await search.GetNextSetAsync();
            var user = page.Find(userTemp => userTemp.UserName == userName && userTemp.Password == password);
            context.Logger.LogLine($"Found user: {user != null}");

            if (user == null)
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.NotFound
                };
            }

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(user),
                Headers = HeaderHelper.GetHeaderAttributes()
            };
            return response;
        }

        /// <summary>
        /// A Lambda function that adds a user post.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> AddUserAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var user = JsonConvert.DeserializeObject<User>(request?.Body);
            user.Id = Guid.NewGuid().ToString();
            user.CreatedTimestamp = DateTime.Now;

            context.Logger.LogLine($"Saving user with id {user.Id}");
            await DDBContext.SaveAsync<User>(user);

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(user),
                Headers = HeaderHelper.GetHeaderAttributes()
            };
            return response;
        }

        /// <summary>
        /// A Lambda function that adds a user post.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> UpdateUserAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var user = JsonConvert.DeserializeObject<User>(request?.Body);

            string userId = null;
            if (request.PathParameters != null && request.PathParameters.ContainsKey(ID_QUERY_STRING_NAME))
                userId = request.PathParameters[ID_QUERY_STRING_NAME];
            else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(ID_QUERY_STRING_NAME))
                userId = request.QueryStringParameters[ID_QUERY_STRING_NAME];

            if (string.IsNullOrEmpty(userId))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Body = $"Missing required parameter {ID_QUERY_STRING_NAME}"
                };
            }
            else
            {
                user.Id = userId;
            }

            context.Logger.LogLine($"Saving user with id {user.Id}");
            await DDBContext.SaveAsync<User>(user);

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(user),
                Headers = HeaderHelper.GetHeaderAttributes()
            };
            return response;
        }

        /// <summary>
        /// A Lambda function that removes a user post from the DynamoDB table.
        /// </summary>
        /// <param name="request"></param>
        public async Task<APIGatewayProxyResponse> RemoveUserAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            string userId = null;
            if (request.PathParameters != null && request.PathParameters.ContainsKey(ID_QUERY_STRING_NAME))
                userId = request.PathParameters[ID_QUERY_STRING_NAME];
            else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(ID_QUERY_STRING_NAME))
                userId = request.QueryStringParameters[ID_QUERY_STRING_NAME];

            if (string.IsNullOrEmpty(userId))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Body = $"Missing required parameter {ID_QUERY_STRING_NAME}"
                };
            }

            context.Logger.LogLine($"Deleting user with id {userId}");
            await this.DDBContext.DeleteAsync<User>(userId);

            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK
            };
        }
    }
}
