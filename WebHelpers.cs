using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ImageConverterPlus.Services;
using System.Net.Http;
using CommunityToolkit.Mvvm.DependencyInjection;
using ImageConverterPlus.Services.interfaces;
using ImageConverterPlus.ViewModels;

namespace ImageConverterPlus
{
    public static class WebHelpers
    {
        public static async Task HandleHtmlDropThreadAsync(IDataObject data, ConvertManagerService convMgr)
        {
            var mainVm = Ioc.Default.GetRequiredService<MainWindowViewModel>();
            if (await UrlContainsImageAsync(WebUtility.HtmlDecode((string)data.GetData(DataFormats.Text))))
            {
                string url = WebUtility.HtmlDecode((string)data.GetData(DataFormats.Text));

                Bitmap? image = await DownloadImageAsync(url);
                convMgr.SourceImage = Helpers.BitmapToBitmapSourceFast(image, true);
                if (image != null)
                {
                    convMgr.ProcessImage(bitmap =>
                    {
                        mainVm.ResetImageZoomAndPanNoAnim();
                        if (bitmap != null)
                        {
                            mainVm.CurrentImagePath = "Loaded from URL";
                            mainVm.CurrentImagePathLong = url;
                            App.Log.Log($"Image loaded from image URL ({url})");
                        }
                        else
                        {
                            Ioc.Default.GetService<IDialogService>()?.ShowAsync(new MessageDialogViewModel("Error", new System.Diagnostics.StackTrace().ToString()));
                        }
                    });
                }
            }
            else
            {
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml((string)data.GetData(DataFormats.Html));
                HtmlNodeCollection imgNodes = doc.DocumentNode.SelectNodes("//img");

                if (imgNodes != null && imgNodes.Count > 0)
                {
                    string src = imgNodes[0].GetAttributeValue("src", null);
                    src = WebUtility.HtmlDecode(src);
                    Bitmap? image = await DownloadImageAsync(src);
                    convMgr.SourceImage = Helpers.BitmapToBitmapSourceFast(image, true);
                    if (image != null)
                    {
                        convMgr.ProcessImage(lcdStr =>
                        {
                            mainVm.ResetImageZoomAndPanNoAnim();
                            if (lcdStr != null)
                            {
                                mainVm.CurrentImagePath = "Loaded from HTML";
                                mainVm.CurrentImagePathLong = src;
                                App.Log.Log($"Image loaded from HTML ({src})");
                            }
                            else
                            {
                                Ioc.Default.GetService<IDialogService>()?.ShowAsync(new MessageDialogViewModel("Error", new System.Diagnostics.StackTrace().ToString()));
                            }
                        });
                    }
                }
                else
                {
                    Ioc.Default.GetService<IDialogService>()?.ShowAsync(new MessageDialogViewModel("Error", "Dropped html does not contain any image links!"));
                }
            }
        }

        public static async Task<bool> UrlContainsImageAsync(string url)
        {
            try
            {
                using HttpClient client = new HttpClient();
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, url);
                using HttpResponseMessage response = await client.SendAsync(request);

                response.EnsureSuccessStatusCode();

                return response.Headers.GetValues("Content-Type")
                    .Select(i => i.ToLowerInvariant())
                    .Any(i => i.StartsWith("image/"));
            }
            catch (Exception e)
            {
                App.Log.Log(e);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url">Returns null if anything fails for whatever reason</param>
        /// <returns></returns>
        public static async Task<Bitmap?> DownloadImageAsync(string url)
        {
            try
            {
                using HttpClient client = new HttpClient();
                using HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var contentTypes = response.Headers.GetValues("Content-Type").Select(i => i.ToLowerInvariant().Replace("image/", ""));

                if (Helpers.SupportedImageFileTypes.Any(t => t.EqualsAny(contentTypes)))
                {
                    using Stream stream = await response.Content.ReadAsStreamAsync();
                    
                    if (contentTypes.Any(i => i.Equals("webp")))
                    {
                        SixLabors.ImageSharp.Formats.Webp.WebpDecoder webpDecoder = new SixLabors.ImageSharp.Formats.Webp.WebpDecoder();
                        using SixLabors.ImageSharp.Image webpImg = webpDecoder.Decode(SixLabors.ImageSharp.Configuration.Default, stream, System.Threading.CancellationToken.None);
                        SixLabors.ImageSharp.Formats.Bmp.BmpEncoder enc = new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder();

                        using MemoryStream ms = new MemoryStream();
                        await webpImg.SaveAsync(ms, enc);
                        return new Bitmap(stream);
                    }
                    else
                    {
                        return new Bitmap(stream);
                    }
                }

                return null;

            }
            catch (HttpRequestException e)
            {
                App.Log.Log(e.ToString());
                Ioc.Default.GetService<IDialogService>()?.ShowAsync(new MessageDialogViewModel("Error", $"Http request error, code {(int?)e.StatusCode} {e.StatusCode}"));
                return null;
            }
            catch (Exception e)
            {
                App.Log.Log(e.ToString());
                Ioc.Default.GetService<IDialogService>()?.ShowAsync(new MessageDialogViewModel("Error", "Error occurred while decoding the image! (It might be a video)"));
                return null;
            }
        }
    }
}
