using ImageMagick;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using Platybot.Resources;
using System.Net.NetworkInformation;
using Platybot.Data;
using Microsoft.Extensions.DependencyInjection;
using Discord;
using Platybot.Constants;
using Platybot.Enums;

namespace Platybot.Services
{
    internal class ImageService
    {
        private readonly HttpClient _httpClient;
        private readonly AIService _AIService;

        private readonly Random random = new();

        private async Task<byte[]> DownloadImage(string url)
        {
            return await _httpClient.GetByteArrayAsync(url);
        }

        public ImageService(IServiceProvider services)
        {
            _AIService = services.GetRequiredService<AIService>();
            _httpClient = new HttpClient();
        }

        public async Task<(byte[] data, string extension)> GetImage(string keyword, ImageProvider imageProvider)
        {
            string url = null;

            switch (imageProvider)
            {
                case ImageProvider.Unsplash:
                    url = ServiceConstants.IMAGE_PROVIDER_UNSPLASH_URL + keyword;
                    break;
                case ImageProvider.UnsplashCollection:
                    string[] collections = keyword.Split(',');
                    url = ServiceConstants.IMAGE_PROVIDER_UNSPLASH_COLLECTION_URL + collections[random.Next(0, collections.Length - 1)];
                    break;
                case ImageProvider.Inspirobot:
                    return await GetRandomInspirobotImage();
            }

            byte[] data = await DownloadImage(url);
            return (data, ".jpg");
        }

        // public async Task<Stream> GetAiImage(string prompt)
        // {
        //     return null;
        //     var image = await _AIService.GenerateImage(prompt);
        //     return image;
        // }

        public async Task<(byte[] data, string extension)> GetRandomInspirobotImage()
        {
            var imageLink = await _httpClient.GetStringAsync(ServiceConstants.INSPIRIBOT_IMAGE_URL);
            string extension = imageLink[imageLink.LastIndexOf('.')..];

            byte[] data = await DownloadImage(imageLink);
            return (data, extension);
        }

        public async Task<(string, Rule34Status, int)> GetRandomHentaiImageUrl(string[] tags)
        {
            string assemblyDirectory = Path.GetDirectoryName(AppContext.BaseDirectory);
            var cursedTagsPath = Path.Join(assemblyDirectory, PathConstants.RESOURCES_FOLDER, PathConstants.LISTS_FOLDER, PathConstants.CURSED_TAGS_FILENAME);
            List<string> cursedTags = File.ReadAllLines(cursedTagsPath).Where(x => !x.StartsWith('#') && !string.IsNullOrWhiteSpace(x)).ToList();

            var cursedIntersection = tags.Intersect(cursedTags);
            if (cursedIntersection.Any())
            {
                return (null, Rule34Status.Cursed, 0);
            }

            var tagParams = string.Join("+", tags) + "%20score:>50";
            var cursedTagParams = string.Join("+", cursedTags.Select(x => "-" + x));
            var url = ServiceConstants.RULE34_API + tagParams + "+" + cursedTagParams;
            var response = await _httpClient.GetStringAsync(url);

            var xml = new XmlDocument();
            xml.LoadXml(response);
            XPathNavigator navigator = xml.CreateNavigator();

            var postNodes = navigator.Select("/posts/post");
            var selectedNode = postNodes;

            if (postNodes.Count == 0)
            {
                return (null, Rule34Status.NotFound, 0);
            }

            var selectedRandom = random.Next(0, postNodes.Count - 1);

            for (int i = 0; i < postNodes.Count; i++)
            {
                selectedNode.MoveNext();
                if (selectedNode.CurrentPosition == selectedRandom)
                    break;
            }

            var resultTagsString = selectedNode.Current.GetAttribute("tags", "").ToString();
            var resultId = selectedNode.Current.GetAttribute("id", "");
            var resultTags = resultTagsString.Split(" ");

            var cursedIntersectionResult = resultTags.Intersect(cursedTags);
            if (cursedIntersectionResult.Any())
            {
                return (null, Rule34Status.NotFound, postNodes.Count);
            }

            var imageLink = selectedNode.Current.GetAttribute("file_url", "");

            return ($"{imageLink}?{resultId}", Rule34Status.Valid, postNodes.Count);
        }

        public static Stream GetCrabRave(string topTextString, string bottomTextString)
        {
            using (var images = new MagickImageCollection())
            {
                string assemblyDirectoryPath = Path.GetDirectoryName(AppContext.BaseDirectory);
                string raveCrabDirectoryPath = Path.Combine(assemblyDirectoryPath, PathConstants.RESOURCES_FOLDER, PathConstants.IMAGES_FOLDER, "RaveCrab");
                var directory = new DirectoryInfo(raveCrabDirectoryPath);
                FileInfo[] files = directory.GetFiles("*.png").OrderBy(f => f.Name).ToArray();

                foreach (FileInfo file in files)
                {
                    images.Add(new MagickImage(file));
                }

                var transparentColor = MagickColor.FromRgba(0, 0, 0, 0);

                var topText = new MagickImage(transparentColor, 480, 270);
                new Drawables()
                    .FontPointSize(40)
                    .Font("Raleway", FontStyleType.Normal, FontWeight.Light, FontStretch.Normal)
                    .FillColor(MagickColors.White)
                    .Gravity(Gravity.Center)
                    .Text(0, -25, topTextString.ToUpper())
                    .Draw(topText);

                var topTextBlur = new MagickImage(transparentColor, 480, 270);
                new Drawables()
                    .FontPointSize(40)
                    .Font("Raleway", FontStyleType.Normal, FontWeight.Light, FontStretch.Normal)
                    .FillColor(MagickColors.Black)
                    .Gravity(Gravity.Center)
                    .Text(0, -25, topTextString.ToUpper())
                    .Draw(topTextBlur);
                topTextBlur.Blur(0, 2);

                var bottomText = new MagickImage(transparentColor, 480, 270);
                new Drawables()
                    .FontPointSize(40)
                    .Font("Raleway", FontStyleType.Normal, FontWeight.Light, FontStretch.Normal)
                    .FillColor(MagickColors.White)
                    .Gravity(Gravity.Center)
                    .Text(0, 25, bottomTextString.ToUpper())
                    .Draw(bottomText);

                var bottomTextBlur = new MagickImage(transparentColor, 480, 270);
                new Drawables()
                    .FontPointSize(40)
                    .Font("Raleway", FontStyleType.Normal, FontWeight.Light, FontStretch.Normal)
                    .FillColor(MagickColors.Black)
                    .Gravity(Gravity.Center)
                    .Text(0, 25, bottomTextString.ToUpper())
                    .Draw(bottomTextBlur);
                bottomTextBlur.Blur(0, 2);

                var separator = new MagickImage(transparentColor, 480, 270);
                new Drawables()
                    .StrokeColor(MagickColors.White)
                    .StrokeWidth(1)
                    .Line(100, 135, 380, 135)
                    .Draw(separator);

                var separatorBlur = new MagickImage(transparentColor, 480, 270);
                new Drawables()
                    .StrokeColor(MagickColors.Black)
                    .StrokeWidth(1)
                    .Line(100, 135, 380, 135)
                    .Draw(separatorBlur);
                separatorBlur.Blur(0, 2);

                var overlay = new MagickImage(transparentColor, 480, 270);
                overlay.Composite(topTextBlur, CompositeOperator.Over);
                overlay.Composite(topText, CompositeOperator.Over);
                overlay.Composite(bottomTextBlur, CompositeOperator.Over);
                overlay.Composite(bottomText, CompositeOperator.Over);
                overlay.Composite(separatorBlur, CompositeOperator.Over);
                overlay.Composite(separator, CompositeOperator.Over);

                foreach (MagickImage image in images.Cast<MagickImage>())
                {
                    image.Composite(overlay, CompositeOperator.Over);
                    image.AnimationDelay = 6;
                }

                var stream = new MemoryStream();
                images.Write(stream, MagickFormat.Gif);
                return stream;
            }
        }

        public static Stream GetGreetings(string username, string message)
        {
            string assemblyDirectoryPath = Path.GetDirectoryName(AppContext.BaseDirectory);
            string greetingsImagePath = Path.Combine(assemblyDirectoryPath, PathConstants.RESOURCES_FOLDER, "greetings.png");
            var transparentColor = MagickColor.FromRgba(0, 0, 0, 0);
            var greetings = new MagickImage(new FileInfo(greetingsImagePath));

            var usernameImage = new MagickImage(transparentColor, 500, 200);
            new Drawables()
                    .FontPointSize(30)
                    .Font("Segoe UI", FontStyleType.Italic, FontWeight.Bold, FontStretch.Normal)
                    .Gravity(Gravity.Center)
                    .Text(0, -3, username)
                    .FillColor(MagickColors.White)
                    .Draw(usernameImage);

            var serverNameImage = new MagickImage(transparentColor, 500, 200);
            new Drawables()
                    .FontPointSize(24)
                    .Font("Segoe UI", FontStyleType.Normal, FontWeight.Bold, FontStretch.Normal)
                    .Gravity(Gravity.Center)
                    .Text(65, 35, $"to {message}")
                    .FillColor(MagickColors.White)
                    .Draw(serverNameImage);

            var welcomeImage = new MagickImage(transparentColor, 500, 200);
            welcomeImage.Composite(greetings, CompositeOperator.Over);
            welcomeImage.Composite(usernameImage, CompositeOperator.Over);
            welcomeImage.Composite(serverNameImage, CompositeOperator.Over);

            var stream = new MemoryStream();
            welcomeImage.Write(stream, MagickFormat.Png);

            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        public static Stream GetSilenceCrab(string text)
        {
            string assemblyDirectoryPath = Path.GetDirectoryName(AppContext.BaseDirectory);
            string silenceCrabImagePath = Path.Combine(assemblyDirectoryPath, PathConstants.RESOURCES_FOLDER, "silence_crab.png");
            var transparentColor = MagickColor.FromRgba(0, 0, 0, 0);
            var silenceCrabImage = new MagickImage(new FileInfo(silenceCrabImagePath));

            var textImage = new MagickImage(transparentColor, 742, 560);
            new Drawables()
                    .FontPointSize(40)
                    .Font("Segoe UI", FontStyleType.Normal, FontWeight.Bold, FontStretch.Normal)
                    .Text(190, 195, text)
                    .TextAlignment(TextAlignment.Center)
                    .FillColor(MagickColors.White)
                    .Draw(textImage);

            var finalImage = new MagickImage(transparentColor, 742, 560);
            finalImage.Composite(silenceCrabImage, CompositeOperator.Over);
            finalImage.Composite(textImage, CompositeOperator.Over);

            var stream = new MemoryStream();
            finalImage.Write(stream, MagickFormat.Png);

            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }
    }
}
