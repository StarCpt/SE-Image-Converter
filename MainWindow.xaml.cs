﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Brushes = System.Windows.Media.Brushes;
using System.Windows.Controls.Primitives;
using System.Net;
using HtmlAgilityPack;
using System.Diagnostics;
using BitDepth = SEImageToLCD_15BitColor.ConvertThread.BitDepth;
using DitherMode = SEImageToLCD_15BitColor.ConvertThread.DitherMode;
using Size = System.Drawing.Size;
using System.Timers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using ImageSharp = SixLabors.ImageSharp;
using System.Threading;
using System.Windows.Media.Animation;

namespace SEImageToLCD_15BitColor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string ConvertedImageStr;
        private ImageInfo ImageCache;//load image here first then convert so it can be used again
        public static MainWindow Main { get; private set; }
        private static bool InstantChanges;

        private readonly Dictionary<ToggleButton, Size> lcdButtons;
        private readonly Dictionary<ToggleButton, InterpolationMode> scaleButtons;
        private readonly Dictionary<ToggleButton, BitDepth> colorBitDepthButtons;
        private readonly Dictionary<Button, RotateFlipType> imageTransformButtons;

        private static bool lcdPicked;

        public static Logging Logging { get; private set; }
        public readonly Stopwatch sw = new Stopwatch();

        ConvertThread ConvertThread;
        PreviewConvertThread PreviewConvertThread;
        private Task ConversionTask;
        private Task PreviewConvertTask;
        private ConvertThread QueuedConversion;
        private PreviewConvertThread QueuedPreviewConversion;

        private enum ImageInfoType
        {
            UploadedFile = 1,
            DraggedFile = 2,
            DraggedBitmap = 4,
            DraggedWebHTML = 8,
            ClipboardFile = 16,
            ClipboardBitmap = 32,
        }

        private struct ImageInfo
        {
            public Bitmap Image;
            public Size ImageSize;
            public string FileNameOrImageSource;
            public bool IsFile;

            public ImageInfo(Bitmap image, Size imageSize, string fileNameOrOther, bool isFile)
            {
                Image = image;
                ImageSize = imageSize;
                FileNameOrImageSource = fileNameOrOther;
                IsFile = isFile;
            }
        }

        public MainWindow()
        {
            Logging = new Logging(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName + ".log", 1000, true);

            Main = this;
            InitializeComponent();
            ToggleBtn_3BitColor.IsChecked = true;
            ToggleBtn_LCDPanel.IsChecked = true;
            lcdPicked = true;
            ToggleBtn_ScaleBicubic.IsChecked = true;
            ImageWidthSetting.Foreground = Brushes.DarkGray;
            ImageHeightSetting.Foreground = Brushes.DarkGray;
            InstantChanges = true;
            ToggleBtn_InstantChanges.IsChecked = InstantChanges;
            RemoveImagePreviewBtn.IsEnabled = !InstantChanges;
            UpdateCurrentConvertBtnToolTip("No images loaded", true);
            ConvertBtn.IsEnabled = (!InstantChanges && ImageCache.Image != null);
            CopyToClipBtn.IsEnabled = !string.IsNullOrEmpty(ConvertedImageStr);
            OpenLogBtnToolTip.Content = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName + ".log");

            //add buttons to their respective dictionaries.
            lcdButtons = new()
            {
                { ToggleBtn_LCDPanel, new(178, 178) },
                { ToggleBtn_TextPanel, new(178, 107) },
                { ToggleBtn_WidePanelWide, new(356, 178) },
                { ToggleBtn_WidePanelTall, new(178, 356) },
            };

            scaleButtons = new()
            {
                { ToggleBtn_ScaleNearest, InterpolationMode.NearestNeighbor },
                { ToggleBtn_ScaleBilinear, InterpolationMode.HighQualityBilinear },
                { ToggleBtn_ScaleBicubic, InterpolationMode.HighQualityBicubic },
            };

            colorBitDepthButtons = new()
            {
                { ToggleBtn_3BitColor, BitDepth.Color3 },
                { ToggleBtn_5BitColor, BitDepth.Color5 },
            };

            imageTransformButtons = new()
            {
                { ToggleBtn_FlipVertical, RotateFlipType.RotateNoneFlipY },
                { ToggleBtn_FlipHorizontal, RotateFlipType.RotateNoneFlipX },
                { ToggleBtn_RotateRight, RotateFlipType.Rotate90FlipNone },
            };

            MainWindowWindow.Title = "Star's Image Converter v0.6.1";
            AppTitleText.Content = "Star's Image Converter v0.6.1";
            AppBigTitle.Content = "Star's Image Converter";

            Logging.Log("MainWindow initialized.");

            InitImagePreview();
        }

        private void OnBrowseImagesClicked(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new()
            {
                Filter = "Image files (*.jpeg, *.jpg, *.jfif, *.png, *.tiff, *.bmp, *.gif, *.ico, *.webp)|*.jpeg;*.jpg;*.jfif;*.png;*.tiff;*.bmp;*.gif;*.ico;*.webp",
            };

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                var supportedFlag = IsFileTypeSupported(dialog.FileName);
                if (supportedFlag != IsFileSupportedEnum.NotSupported && TryGetImageInfo(dialog.FileName, supportedFlag, out ImageCache))
                {
                    UpdateBrowseImagesBtn(dialog.FileName.GetFileName(), dialog.FileName);
                    UpdateCurrentConvertBtnToolTip(dialog.FileName, true);
                    if (InstantChanges && ImageCache.Image != null)
                    {
                        TryConvertImageThreaded(ImageCache, true, true, true);
                    }
                }
                else
                {
                    ShowAcrylDialog("This file type is not supported!");
                }
            }
        }

        private bool TryGetImageInfo(string filePath, IsFileSupportedEnum supEnum, out ImageInfo result)
        {
            try
            {
                if (supEnum == IsFileSupportedEnum.Supported)
                {
                    Bitmap bitmapImage = new(filePath);
                    result = new ImageInfo(bitmapImage, bitmapImage.Size, filePath, true);
                    return true;
                }
                else if (supEnum == IsFileSupportedEnum.Webp)
                {
                    Bitmap bitmapImage = DecodeWebpImage(filePath);
                    result = new ImageInfo(bitmapImage, bitmapImage.Size, filePath, true);
                    return true;
                }
                else
                {
                    result = new ImageInfo(null, Size.Empty, filePath, true);
                    return false;
                }
            }
            catch (Exception e)
            {
                Logging.Log(e.ToString());
                result = new ImageInfo(null, Size.Empty, filePath, true);
                return false;
            }
        }

        private enum IsFileSupportedEnum
        {
            NotSupported = 0,
            Supported = 1,
            Webp = 2,
        }

        private static readonly string[] SupportedFileTypes = 
            { "png", "jpg", "jpeg", "jfif", "tiff", "bmp", "gif", "ico", "webp" };

        private IsFileSupportedEnum IsFileTypeSupported(string file)
        {
            try
            {
                string fileExtension = file.Split('.').Last();

                if (SupportedFileTypes.Any(i => i.Equals(fileExtension, StringComparison.OrdinalIgnoreCase)))
                {
                    if (fileExtension.Equals("webp", StringComparison.OrdinalIgnoreCase))
                    {
                        return IsFileSupportedEnum.Webp;
                    }
                    else
                    {
                        return IsFileSupportedEnum.Supported;
                    }
                }
                else
                {
                    return IsFileSupportedEnum.NotSupported;
                }
            }
            catch (Exception e)
            {
                Logging.Log($"Caught exception at MainWindow.IsFileTypeSupported(string) ({file})");
                Logging.Log(e.ToString());
                return IsFileSupportedEnum.NotSupported;
            }
        }

        private void OnConvertClicked(object sender, RoutedEventArgs e)
        {
            if (ImageCache.Image != null)
            {
                TryConvertImageThreaded(ImageCache, true, true, true);
            }
            else //should not happen
            {
                ShowAcrylDialog("Choose an image first!");
            }
        }

        private Bitmap DecodeWebpImage(string filePath)
        {
            WebpDecoder webpDecoder = new WebpDecoder();
            ImageSharp.Image webpImg = webpDecoder.Decode(Configuration.Default, new FileStream(filePath, FileMode.Open, FileAccess.Read), CancellationToken.None);

            ImageSharp.Formats.Bmp.BmpEncoder enc = new ImageSharp.Formats.Bmp.BmpEncoder();

            using (MemoryStream stream = new MemoryStream())
            {
                webpImg.Save(stream, enc);
                return new Bitmap(stream);
            }
        }

        /// <summary>
        /// displays error dialogs automatically. does file type checks as well. does not check if path is empty/null
        /// </summary>
        /// <param name="imagePath"></param>
        /// <returns></returns>
        private bool TryConvertFromFile(string imagePath, bool showErrorDialogs, bool cache)
        {
            try
            {
                IsFileSupportedEnum supportedFlag = IsFileTypeSupported(imagePath);
                if (supportedFlag != IsFileSupportedEnum.NotSupported)
                {
                    if (TryGetImageInfo(imagePath, supportedFlag, out ImageInfo bitImageInfo))
                    {
                        return TryConvertImageThreaded(bitImageInfo, showErrorDialogs, cache, true);
                    }
                    else
                    {
                        if (showErrorDialogs)
                        {
                            ShowAcrylDialog("This file type is not supported!");
                        }
                        return false;
                    }
                }
                else
                {
                    if (showErrorDialogs)
                    {
                        ShowAcrylDialog("This file type is not supported!");
                    }
                    return false;
                }
            }
            catch (Exception e)
            {
                Logging.Log($"Caught exception at TryConvertFromFile(string, bool, bool) ({imagePath})");
                Logging.Log(e.ToString());
                if (showErrorDialogs)
                {
                    ShowAcrylDialog("Error occurred while decoding the file! Make sure file type is valid.");
                }
                return false;
            }
        }

        /// <summary>
        /// Gets the settings, converts, and updates the preview and ConvertedImageStr. Diaplays error dialogs automagically
        /// </summary>
        /// <param name="image"></param>
        /// <returns>whether or not the operation succeeded</returns>
        private bool TryConvertImageThreaded(ImageInfo image, bool showErrorDialogs, bool cache, bool resetZoom)
        {
            try
            {
                if (!TryGetColorBitDepth(out BitDepth colorDepth))
                {
                    colorDepth = BitDepth.Color3;
                    Logging.Log("Color depth error! Defaulting to 3 bits.");
                    if (showErrorDialogs)
                    {
                        ShowAcrylDialog("Color depth error! Defaulting to 3 bits.");
                    }
                }

                if (!TryGetInterpolationMode(out InterpolationMode interpolationMode))
                {
                    interpolationMode = InterpolationMode.NearestNeighbor;
                    Logging.Log("Scaling mode error! Defaulting to Nearest.");
                    if (showErrorDialogs)
                    {
                        ShowAcrylDialog("Scaling mode error! Defaulting to Nearest.");
                    }
                }

                if (!TryGetLCDSize(out Size? lcdSize))
                {
                    if (lcdSize.HasValue)
                    {
                        RemovePreview();
                        return true;
                    }
                    else
                    {
                        lcdSize = new Size(178, 178);
                        Logging.Log("LCD size error! Defaulting to 178x178.");
                        if (showErrorDialogs)
                        {
                            ShowAcrylDialog("LCD size error! Defaulting to 178x178.");
                        }
                    }
                }

                if (!TryGetDitherMode(out DitherMode dither))
                {
                    dither = DitherMode.NoDither;
                    Logging.Log("Dithering option error! Defaulting to None.");
                    if (showErrorDialogs)
                    {
                        ShowAcrylDialog("Dithering option error! Defaulting to None.");
                    }
                }

                if (image.Image != null)
                {
                    var tt = GetTranslateTransform(ImagePreview);
                    var NewThread = new ConvertThread(image.Image, dither, colorDepth, lcdSize.Value, interpolationMode, new ConvertCallback(ConvertResultCallback), resetZoom, (float)((tt.X - (PreviewTopLeft.X)) / (ImagePreviewBorder.ActualWidth / lcdSize.Value.Width)), (float)((tt.Y - (PreviewTopLeft.Y)) / (ImagePreviewBorder.ActualHeight / lcdSize.Value.Height)));

                    if (ConversionTask != null && !ConversionTask.IsCompleted)
                    {
                        QueuedConversion = NewThread;
                    }
                    else
                    {
                        Logging.Log($"Begin Conversion {colorDepth.ToString()} {interpolationMode.ToString()} {image.Image.Size.ToShortString()} to {lcdSize.Value.ToShortString()} {dither.ToString()} {image.FileNameOrImageSource.ToString()}");
                        sw.Restart();
                        ConvertThread = NewThread;
                        ConversionTask = Task.Run(ConvertThread.ConvertImageThreadedFast);
                    }

                    if (cache)
                    {
                        ImageCache = image;
                    }

                    return true;
                }
                else
                {
                    Logging.Log($"Caught exception at TryConvertImage(Bitmap, bool, bool), ({image.FileNameOrImageSource.ToString()}), Image is null");
                    if (showErrorDialogs)
                    {
                        ShowAcrylDialog("Error occurred during image conversion! (2)");
                    }
                    return false;
                }
        }
            catch (Exception e)
            {
                Logging.Log($"Caught exception at TryConvertImage(Bitmap, bool, bool), ({image.FileNameOrImageSource.ToString()})");
                Logging.Log(e.ToString());
                if (showErrorDialogs)
                {
                    ShowAcrylDialog("Error occurred during image conversion! (1)");
    }
                return false;
            }
        }

        public delegate void ConvertCallback(string resultStr, BitmapImage resultImg, Size lcdSize, bool resetZoom);

        public void ConvertResultCallback(string resultStr, BitmapImage resultImg, Size lcdSize, bool resetZoom)
        {
            if (QueuedConversion != null)
            {
                sw.Restart();
                ConvertThread = QueuedConversion;
                QueuedConversion = null;
                ConversionTask = Task.Run(ConvertThread.ConvertImageThreadedFast);
            }
            else
            {
                sw.Stop();
                Logging.Log($"Conversion took {sw.ElapsedMilliseconds} ms");
                ConvertedImageStr = resultStr;
                ChangePreviewFromOtherThread(resultImg, lcdSize, resetZoom);
            }

            Logging.Log($"callback done. {Main.sw.ElapsedMilliseconds} ms elapsed.");
        }

        private bool TryGetColorBitDepth(out BitDepth result)
        {
            if (colorBitDepthButtons.Any(b => b.Key.IsChecked == true))
            {
                result = colorBitDepthButtons.FirstOrDefault(b => b.Key.IsChecked == true).Value;
                return true;
            }
            else //failsafe. shouldn't ever be triggered
            {
                result = BitDepth.Invalid;
                return false;
            }
        }
        private bool TryGetInterpolationMode(out InterpolationMode result)
        {
            if (scaleButtons.Any(b => b.Key.IsChecked == true))
            {
                result = scaleButtons.FirstOrDefault(b => b.Key.IsChecked == true).Value;
                return true;
            }
            else
            {
                result = InterpolationMode.Invalid;
                return false;
            }
        }
        private bool TryGetLCDSize(out Size? result)
        {
            if (lcdButtons.Any(b => b.Key.IsChecked == true))
            {
                result = lcdButtons.FirstOrDefault(b => b.Key.IsChecked == true).Value;
                return true;
            }
            else
            {
                try
                {
                    if (ImageWidthSetting.Text.TrimStart('0').Length == 0 || ImageHeightSetting.Text.TrimStart('0').Length == 0)
                    {
                        result = new Size(0, 0);
                        return false;
                    }
                    result = new Size(int.Parse(ImageWidthSetting.Text), int.Parse(ImageHeightSetting.Text));
                    return true;
                }
                catch (Exception e)
                {
                    Logging.Log(e.ToString());
                    result = null;
                    return false;
                }
            }
        }
        private bool TryGetDitherMode(out DitherMode result)
        {
            if (ToggleBtn_Dithering.IsChecked == true)
            {
                result = DitherMode.FloydSteinberg;
                return true;
            }
            else //PLACEHOLDER!! CHANGE WHEN THERE ARE MORE THAN 1 OPTION
            {
                result = DitherMode.NoDither;
                return true;
            }
        }

        private void OnCopyToClipClicked(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(ConvertedImageStr))
            {
                Clipboard.SetText(ConvertedImageStr);
                Logging.Log("Copied converted image to clipboard");
            }
            else
            {
                ShowAcrylDialog($"Convert {(ImageCache.Image != null ? "the" : "an")} image first!");
            }
        }

        private void MainWindowWindow_Deactivated(object sender, EventArgs e)
        {
            //backgroundbrush.Fill.Opacity = 0.95;
            backgroundbrush.Fill.Opacity = 1;
            AppTitleBackground.Fill.Opacity = 1;
        }
        private void MainWindowWindow_Activated(object sender, EventArgs e)
        {
            //backgroundbrush.Fill.Opacity = 0.85;
            backgroundbrush.Fill.Opacity = 0.9;
            AppTitleBackground.Fill.Opacity = 0.9;
        }

        private void ColorDepthOption_Clicked(object sender, RoutedEventArgs e)
        {
            ToggleButton thisBtn = sender as ToggleButton;
            foreach (var btn in colorBitDepthButtons)
            {
                btn.Key.IsChecked = (btn.Key == thisBtn);
            }

            DoInstantChange(false);
        }

        private void LCDOption_Clicked(object sender, RoutedEventArgs e)
        {
            lcdPicked = true;
            ToggleButton thisBtn = sender as ToggleButton;
            foreach (var btn in lcdButtons)
            {
                btn.Key.IsChecked = (btn.Key == thisBtn);
            }

            ImageWidthSetting.Text = lcdButtons[thisBtn].Width.ToString();
            ImageHeightSetting.Text = lcdButtons[thisBtn].Height.ToString();
            ImageWidthSetting.Foreground = Brushes.DarkGray;
            ImageHeightSetting.Foreground = Brushes.DarkGray;

            DoInstantChange(true);
        }

        public static bool IsNumeric(string str)
        {
            return !str.Any(c => c < '0' || c > '9');
        }

        private void ImageSize_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!IsNumeric(e.Text))
            {
                e.Handled = true;
            }
            else
            {
                lcdPicked = false;
            }
        }

        private void ImageSize_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            //intercepts spaces
            e.Handled = e.Key == Key.Space;
        }

        private void ImageSize_TextChanged_Manually(object sender, TextChangedEventArgs e)
        {
            if (!lcdPicked)
            {
                TextBox box = sender as TextBox;
                int trimmedTextLength = box.Text.TrimStart('0').Length;
                if (trimmedTextLength > 3)
                {
                    box.Text = "999";
                    box.CaretIndex = 3;
                }
                else if (trimmedTextLength != box.Text.Length)
                {
                    int removeLength = (box.Text.Length - 3).Clamp(0, 3);
                    box.Text = box.Text.Substring(removeLength);
                    box.CaretIndex = e.Changes.Last().Offset - removeLength + e.Changes.Last().AddedLength;
                }

                if (lcdButtons != null)
                {
                    foreach (var btn in lcdButtons)
                    {
                        btn.Key.IsChecked = false;
                    }
                    ImageWidthSetting.Foreground = Brushes.White;
                    ImageHeightSetting.Foreground = Brushes.White;
                }

                DoInstantChange(true);
            }
        }

        private void ImageSize_Pasting(object sender, DataObjectPastingEventArgs e)
        {

            if (e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true))
            {
                string text = (string)e.SourceDataObject.GetData(typeof(string));
                if (!IsNumeric(text))
                {
                    e.CancelCommand();
                }
                else
                {
                    lcdPicked = false;
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        public void ImageSize_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            TextBox thisTextBox = sender as TextBox;

            if (!string.IsNullOrEmpty(thisTextBox.Text))
            {
                lcdPicked = false;
                int num = int.Parse(thisTextBox.Text);
                int changeDirection = e.Delta > 0 ? 1 : -1;
                thisTextBox.Text = (num + changeDirection).Clamp(0, 999).ToString();
            }
        }

        private void ScaleOption_Clicked(object sender, RoutedEventArgs e)
        {
            ToggleButton thisBtn = sender as ToggleButton;
            foreach (var btn in scaleButtons)
            {
                btn.Key.IsChecked = (btn.Key == thisBtn);
            }

            DoInstantChange(false);
        }

        private void DitherOption_Clicked(object sender, RoutedEventArgs e) => DoInstantChange(false);

        private void ContextMenuItem_DeleteCache(object sender, RoutedEventArgs e)
        {
            ImageCache.Image = null;
            UpdateBrowseImagesBtn(string.Empty, null);
            UpdateCurrentConvertBtnToolTip("No images loaded", true);
        }

        private void PasteFromClipboard(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsImage())
            {
                var image = Utils.BitmapSourceToBitmap(Clipboard.GetImage());
                if (TryConvertImageThreaded(new ImageInfo(image, image.Size, "Image loaded from Clipboard", false),  true, true, true))
                {
                    UpdateBrowseImagesBtn("Loaded from Clipboard", null);
                    UpdateCurrentConvertBtnToolTip("Image loaded from Clipboard", true);
                    Logging.Log("Image loaded from Clipboard (Bitmap)");
                }
            }
            else if (Clipboard.ContainsFileDropList())
            {
                System.Collections.Specialized.StringCollection filedroplist = Clipboard.GetFileDropList();
                foreach (string file in filedroplist)
                {
                    if (TryConvertFromFile(file, true, true))
                    {
                        UpdateBrowseImagesBtn(file.GetFileName(), file);
                        UpdateCurrentConvertBtnToolTip(file, true);
                        Logging.Log("Image loaded from Clipboard (FileDrop)");
                        break;
                    }
                }
            }
            else
            {
                ShowAcrylDialog("Unsupported File");
            }
        }

        private void ShowAcrylDialog(string message) => new AcrylicDialog(MainWindowWindow, message).ShowDialog();

        private void ToggleBtn_InstantChanges_Click(object sender, RoutedEventArgs e)
        {
            InstantChanges = (ToggleBtn_InstantChanges.IsChecked == true);
            Logging.Log($"Instant Changes {(InstantChanges ? "en" : "dis")}abled");

            ConvertBtn.IsEnabled = (!InstantChanges && ImageCache.Image != null);
            if (!ConvertBtn.IsEnabled)
            {
                UpdateCurrentConvertBtnToolTip("No images loaded", true);
            }

            RemoveImagePreviewBtn.IsEnabled = !InstantChanges;

            DoInstantChange(false);
        }

        private void DoInstantChange(bool resetZoom)
        {
            if (InstantChanges && ImageCache.Image != null)
            {
                TryConvertImageThreaded(ImageCache, true, false, resetZoom);
            }
        }

        private void UpdateBrowseImagesBtn(string text, string fullpath)
        {
            ConvertBtn.IsEnabled = (!InstantChanges && ImageCache.Image != null);
            if (!string.IsNullOrEmpty(text))
            {
                if (!string.IsNullOrEmpty(fullpath))
                {
                    BrowseFilesToolTip.Content = fullpath;
                }
                else
                {
                    BrowseFilesToolTip.Content = text;
                }
                if (text.Length > 20)
                {
                    text = text.Substring(0, 20) + "...";
                }
                BrowseFilesBtn.Content = text;
                BrowseFilesBtn.FontSize = 12;
                BrowseFilesBtn.Foreground = Brushes.DarkGray;
                UpdateCurrentConvertBtnToolTip("No images loaded", true);
            }
            else
            {
                BrowseFilesToolTip.Content = "Browse Files";
                BrowseFilesBtn.Content = "Browse Files";
                BrowseFilesBtn.FontSize = 15;
                BrowseFilesBtn.Foreground = Brushes.White;
            }
        }

        private void UpdateCurrentConvertBtnToolTip(string tooltip, bool enable)
        {
            switch (string.IsNullOrEmpty(tooltip))
            {
                case true:
                    ConvertBtnToolTip.Content = "No images loaded";
                    break;
                case false:
                    ConvertBtnToolTip.Content = tooltip;
                    break;
            }
        }

        private void OpenLogs_Clicked(object sender, RoutedEventArgs e)
        {
            Logging.OpenLogFileAsync();
        }

        //custom "Click" event for the app icon
        bool LeftMouseDownOnIcon = false;
        private void AppTitleIcon_MouseLeftButtonChanged(object sender, MouseButtonEventArgs e)
        {
            switch (e.ButtonState)
            {
                case MouseButtonState.Pressed:
                    LeftMouseDownOnIcon = true;
                    break;
                case MouseButtonState.Released:
                    if (LeftMouseDownOnIcon)
                    {
                        (sender as System.Windows.Controls.Image).ContextMenu.IsOpen = true;
                    }
                    break;  
            }
        }
        private void AppTitleIcon_LostMouseCapture(object sender, MouseEventArgs e)
        {
            LeftMouseDownOnIcon = false;
        }

        private void OpenAppDirBtn_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory);
        }

        public static bool isMouseOverSizeTextbox = false;
        private void ImageSize_MouseEnteredOrLeft(object sender, MouseEventArgs e)
        {
            isMouseOverSizeTextbox = (sender as TextBox).IsMouseOver;
        }

        private void TransformImage(RotateFlipType type)
        {
            if (ImageCache.Image != null)
            {
                ImageCache.Image.RotateFlip(type);

                TryConvertImageThreaded(ImageCache, true, true, true);

                Logging.Log($"Image Transformed ({type.ToString()})");
            }
        }

        private void ImageTransformClicked(object sender, RoutedEventArgs e)
        {
            TransformImage(imageTransformButtons[sender as Button]);
        }
    }

    public static class Utils
    {
        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }

        public static double ClampDoubleExt(this double val, double min, double max)
        {
            if (min > max)
            {
                if (val > min) return min;
                else if (val < max) return max;
                else return val;
            }
            else
            {
                if (val < min) return min;
                else if (val > max) return max;
                else return val;
            }
        }

        public static string GetFileName(this string filePath)
        {
            return filePath.Split('\\').LastOrDefault();
        }

        public static string ToShortString(this Size val)
        {
            return $"{val.Width}x{val.Height}";
        }

        //from https://stackoverflow.com/questions/6484357/converting-bitmapimage-to-bitmap-and-vice-versa
        public static Bitmap BitmapSourceToBitmap(BitmapSource bitmapImage)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                Bitmap bitmap = new(outStream);

                return bitmap;
            }
        }

        public static Bitmap BitmapImageToBitmap(BitmapImage bitmapImage)
        {
            // BitmapImage bitmapImage = new BitmapImage(new Uri("../Images/test.png", UriKind.Relative));

            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                Bitmap bitmap = new Bitmap(outStream);

                return new Bitmap(bitmap);
            }
        }

        //Source: https://stackoverflow.com/questions/94456/load-a-wpf-bitmapimage-from-a-system-drawing-bitmap
        public static BitmapImage BitmapToBitmapImage(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }
    }
}
