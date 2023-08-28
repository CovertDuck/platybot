using Platybot.Constants;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Platybot.Services
{
    internal class TwitterService
    {
        private HttpClient HttpClient { get; set; }
        private string BearerKey { get; set; }

        public TwitterService()
        {
            HttpClient = new HttpClient();
            BearerKey = ServiceConstants.TWITTER_BEARER_KEY;
        }

        public async Task<string> GetUserInfoAsync(string username)
        {
            // curl "https://api.twitter.com/2/users/by/username/piranot" -H "Authorization: Bearer $BEARER"
            // Create the HttpContent for the form to be posted.
            var requestContent = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>(ServiceConstants.AUTHORIZATION, BearerKey),
            });

            // Get the response.
            HttpResponseMessage response = await HttpClient.PostAsync(
                ServiceConstants.TWITTER_POST_URL,
                requestContent);

            // Get the response content.
            HttpContent responseContent = response.Content;

            string results = "";

            // Get the stream of the content.
            using (var reader = new StreamReader(await responseContent.ReadAsStreamAsync()))
            {
                results += await reader.ReadToEndAsync();
                Console.WriteLine(await reader.ReadToEndAsync());
            }

            return results;

            //curl "https://api.twitter.com/2/users/by/username/$USERNAME" -H "Authorization: Bearer $ACCESS_TOKEN"
        }
    }
}
