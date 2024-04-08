using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static ImageConverterPlus.MainWindow;
using System.Windows;
using ImageConverterPlus.ImageConverter;

namespace ImageConverterPlus
{
    public static class WebHelpers
    {
        public static async Task HandleHtmlDropThreadAsync(IDataObject Data)
        {
            if (await UrlContainsImageAsync(WebUtility.HtmlDecode((string)Data.GetData(DataFormats.Text))))
            {
                string url = WebUtility.HtmlDecode((string)Data.GetData(DataFormats.Text));

                Bitmap? image = await DownloadImageFromUrlAsync(url);
                ConvertManager.Instance.SourceImage = Helpers.BitmapToBitmapSourceFast(image, true);
                if (image != null)
                {
                    ConvertManager.Instance.ProcessImage(bitmap =>
                    {
                        MainWindow.Static.ResetZoomAndPan(false);
                        if (bitmap != null)
                        {
                            MainWindow.Static.UpdateBrowseImagesBtn("Loaded from URL", url);
                            App.Instance.Log.Log($"Image loaded from image URL ({url})");
                        }
                        else
                        {
                            MainWindow.ConversionFailedDialog();
                        }
                    });
                }
            }
            else
            {
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml((string)Data.GetData(DataFormats.Html));
                HtmlNodeCollection imgNodes = doc.DocumentNode.SelectNodes("//img");

                if (imgNodes != null && imgNodes.Count > 0)
                {
                    string src = imgNodes[0].GetAttributeValue("src", null);
                    src = WebUtility.HtmlDecode(src);
                    Bitmap? image = await DownloadImageFromUrlAsync(src);
                    ConvertManager.Instance.SourceImage = Helpers.BitmapToBitmapSourceFast(image, true);
                    if (image != null)
                    {
                        ConvertManager.Instance.ProcessImage(lcdStr =>
                        {
                            MainWindow.Static.ResetZoomAndPan(false);
                            if (lcdStr != null)
                            {
                                MainWindow.Static.UpdateBrowseImagesBtn("Loaded from HTML", src);
                                App.Instance.Log.Log($"Image loaded from HTML ({src})");
                            }
                            else
                            {
                                MainWindow.ConversionFailedDialog();
                            }
                        });
                    }
                }
                else
                {
                    ShowAcrylDialog("Dropped html does not contain any image links!");
                }
            }
        }

        public static async Task<bool> UrlContainsImageAsync(string url)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Method = "HEAD";
            using (var response = await request.GetResponseAsync())
            {
                return response.ContentType
                    .ToLowerInvariant()
                    .StartsWith("image/");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url">Returns null if anything fails for whatever reason</param>
        /// <returns></returns>
        public static async Task<Bitmap?> DownloadImageFromUrlAsync(string url)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
                request.Method = "GET";
                using (var response = await request.GetResponseAsync())
                {
                    string imageType = response.ContentType
                        .ToLowerInvariant()
                        .Replace("image/", "");
                    if (SupportedFileTypes.Any(t => t.Equals(imageType)))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            await response.GetResponseStream().CopyToAsync(ms);
                            ms.Position = 0;

                            if (imageType == "webp")
                            {
                                SixLabors.ImageSharp.Formats.Webp.WebpDecoder webpDecoder = new SixLabors.ImageSharp.Formats.Webp.WebpDecoder();
                                SixLabors.ImageSharp.Image webpImg = webpDecoder.Decode(SixLabors.ImageSharp.Configuration.Default, ms, System.Threading.CancellationToken.None);

                                SixLabors.ImageSharp.Formats.Bmp.BmpEncoder enc = new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder();

                                using (MemoryStream stream = new MemoryStream())
                                {
                                    await webpImg.SaveAsync(stream, enc);
                                    return new Bitmap(stream);
                                }
                            }
                            else
                            {
                                return new Bitmap(ms);
                            }
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                App.Instance.Log.Log(e.ToString());
                ShowAcrylDialog("Error occurred while decoding the image! (It might be a video?)");
                return null;
            }
        }
    }
}
