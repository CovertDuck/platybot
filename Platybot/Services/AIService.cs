using Microsoft.Extensions.DependencyInjection;
using OpenAI.GPT3;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3.ObjectModels.RequestModels;
using Platybot.Data;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Platybot.Services
{
    internal class AIService
    {
        private DataContext DataContext;
        // private OpenAIService OpenAiService;

        public AIService(IServiceProvider services)
        {
            DataContext = services.GetRequiredService<DataContext>();
        }


        /**
         * Lot of commented code below
         * Can it be deleted ?
         */

        public void Init()
        {
            //OpenAiService = new OpenAIService(new OpenAiOptions()
            //{
            //    ApiKey = DataContext.Parameters.OpenaiApiKey
            //}); 
        }

        // public async Task<Stream> GenerateImage(string prompt)
        // {
        //     throw new NotImplementedException();
        //     var moderationRequest = new CreateModerationRequest()
        //     {
        //         Input = prompt
        //     };

        //     var moderationResponse = await OpenAiService.CreateModeration(moderationRequest);

        //     if (moderationResponse.Results.First().Flagged)
        //         return null;

        //     var images = (await OpenAiService.Image.CreateImage(prompt)).Results;
        //     Stream imageStream;

        //     using (var httpClient = new HttpClient())
        //     {
        //         var image = images.FirstOrDefault();
        //         imageStream = await httpClient.GetStreamAsync(image.Url);
        //     }

        //     return imageStream;
        // }
    }
}
