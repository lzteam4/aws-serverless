using AWSServerless1.Models;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AWSServerless1.Helpers
{
    class FirebaseCloudMessagingHelper
    {
        private static Uri FireBasePushNotificationsURL = new Uri("https://fcm.googleapis.com/fcm/send");
        private static string ServerKey = "AAAALCpSHPQ:APA91bFkLW2CRQgjyUzdrHViXQL8ebrjhCyqKDLa7XQ85OMfndWWKK1XBtFfWi7lU0TGeONiG21QWYimDM3uALiFe0c8cVJSkwPMxWKZs2Irc4zxpvW8kQLwomTJskOyFrgZfS7ehXNx";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="to">token of a user or a topic name i.e. /topic/topice_name</param>
        /// <param name="title">Title of notification</param>
        /// <param name="body">Description of notification</param>
        /// <param name="icon">Icon url of notification</param>
        /// <param name="data">Object with all extra information you want to send hidden in the notification</param>
        /// <returns></returns>
        public static async Task<bool> SendPushNotification(string to, string title, string body, string icon, object data)
        {
            if (!string.IsNullOrEmpty(to) && !string.IsNullOrWhiteSpace(to))
            {
                //Object creation

                var messageInformation = new FcmMessage()
                {
                    notification = new FcmNotification()
                    {
                        title = title,
                        body = body,
                        icon = icon
                    },
                    data = data,
                    to = to
                };
                return await SendPushNotification(messageInformation);
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="deviceTokens">List of all devices assigned to a user</param>
        /// <param name="title">Title of notification</param>
        /// <param name="body">Description of notification</param>
        /// <param name="icon">Icon url of notification</param>
        /// <param name="data">Object with all extra information you want to send hidden in the notification</param>
        /// <returns></returns>
        public static async Task<bool> SendPushNotification(string[] deviceTokens, string title, string body, string icon, object data)
        {
            if (deviceTokens.Count() > 0)
            {
                var messageInformation = new FcmMessage()
                {
                    notification = new FcmNotification()
                    {
                        title = title,
                        body = body,
                        icon = icon
                    },
                    data = data,
                    registration_ids = deviceTokens
                };
                return await SendPushNotification(messageInformation);
            }
            return false;
        }

       
        public static async Task<bool> SendPushNotification(FcmMessage fcmMessage)
        {
            bool sent = false;

            if (fcmMessage != null)
            {
                //Object to JSON STRUCTURE => using Newtonsoft.Json;
                string jsonMessage = JsonConvert.SerializeObject(fcmMessage);

                //Create request to Firebase API
                var request = new HttpRequestMessage(HttpMethod.Post, FireBasePushNotificationsURL);

                request.Headers.TryAddWithoutValidation("Authorization", "key=" + ServerKey);
                request.Content = new StringContent(jsonMessage, Encoding.UTF8, "application/json");

                HttpResponseMessage result;
                using (var client = new HttpClient())
                {
                    result = await client.SendAsync(request);
                    sent = sent && result.IsSuccessStatusCode;
                }
            }

            return sent;
        }
    }
}
