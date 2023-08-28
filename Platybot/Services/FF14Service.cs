using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Platybot.Constants;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Platybot.Services
{
    internal class FF14Service
    {
        private RestClient ClientXIV { get; }
        private RestClient ClientUniv { get; }

        public FF14Service()
        {
            ClientXIV = new RestClient(ServiceConstants.XIVAPI_URL);
            ClientUniv = new RestClient(ServiceConstants.UNIVERSALIS_APP_URL);
        }

        private async Task<List<ItemXIV>> GetItems(string name)
        {
            var request = new RestRequest($"search?string={name}", (Method)DataFormat.Json);
            var response = await ClientXIV.GetAsync(request);
            dynamic content = JObject.Parse(response.Content);

            string results = JsonConvert.SerializeObject(content.Results);
            var items = JsonConvert.DeserializeObject<List<ItemXIV>>(results);

            return items;
        }

        private async Task<List<ItemUniversalis>> GetMarketItems(int itemID)
        {
            var request = new RestRequest($"crystal/{itemID}", (Method)DataFormat.Json);
            var response = await ClientUniv.GetAsync(request);
            dynamic content = JObject.Parse(response.Content);

            string results = JsonConvert.SerializeObject(content.listings);
            var items = JsonConvert.DeserializeObject<List<ItemUniversalis>>(results);

            return items;
        }

        public async Task<List<string>> GetItemNamesAsync(string search)
        {
            var names = (await GetItems(search)).Select(x => x.Name).ToList();

            return names;
        }

        public async Task<List<LoreXIV>> GetLoreAsync(string search)
        {
            var request = new RestRequest($"lore?string={search}", (Method)DataFormat.Json);
            var response = await ClientXIV.GetAsync(request);
            dynamic content = JObject.Parse(response.Content);

            string results = JsonConvert.SerializeObject(content.Results);
            var items = JsonConvert.DeserializeObject<List<LoreXIV>>(results);

            return items;
        }
    }

    /**
     * Code review : why is the following code commented ? 
     * Can it be deleted ?
     */

    //[Command("item")]
    //public async Task ItemAsync([Remainder] string search)
    //{

    //    var clientXIV = new RestClient("https://xivapi.com");
    //    var requestXIV = new RestRequest($"search?string={search}", (Method)DataFormat.Json);
    //    var responseXIV = await clientXIV.GetAsync(requestXIV);
    //    dynamic contentXIV = JObject.Parse(responseXIV.Content);

    //    string resultsXIV = JsonConvert.SerializeObject(contentXIV.Results);
    //    var itemsXIV = JsonConvert.DeserializeObject<List<ItemXIV>>(resultsXIV);

    //    var firstItemXIV = itemsXIV.First();

    //    var itemName = firstItemXIV.Name;
    //    var iconUrl = $"https://xivapi.com{firstItemXIV.Icon}";
    //    var itemID = firstItemXIV.ID;
    //    var itemType = firstItemXIV.UrlType;

    //    var clientUniv = new RestClient("https://universalis.app/api");
    //    var requestUniv = new RestRequest($"crystal/{itemID}", (Method)DataFormat.Json);
    //    var responseUniv = await clientUniv.GetAsync(requestUniv);
    //    dynamic contentUniv = JObject.Parse(responseUniv.Content);

    //    string resultsUniv = JsonConvert.SerializeObject(contentUniv.listings);
    //    var itemsUniv = JsonConvert.DeserializeObject<List<ItemUniversalis>>(resultsUniv);

    //    var firstItemUniv = itemsUniv.First();

    //    var worldName = firstItemUniv.worldName;
    //    var retainerName = firstItemUniv.retainerName;
    //    var price = firstItemUniv.total;

    //    var description =
    //        $"ID: **{itemID}**\n" +
    //        $"Type: **{itemType}**\n" +
    //        $"Seller: **{worldName}/{retainerName}**\n" +
    //        $"Price: **{price}** Gil";

    //    var embedBuilder = new EmbedBuilder
    //    {
    //        Title = itemName,
    //        Description = description,
    //        Color = Color.DarkPurple,
    //        ThumbnailUrl = iconUrl,
    //    };

    //    await ReplyAsync(null, false, embedBuilder.Build());
    //}
}
