using System;
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
using System.Windows.Interop;
using System.IO;
using Rectangle = System.Windows.Shapes.Rectangle;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using System.Text.RegularExpressions;
using System.Windows.Controls.Primitives;
using System.Net;
using HtmlAgilityPack;
using System.Diagnostics;
using BitDepth = SEImageToLCD_15BitColor.Program.BitDepth;
using DitherMode = SEImageToLCD_15BitColor.Program.DitherMode;
using Size = System.Drawing.Size;
using System.Timers;

namespace SEImageToLCD_15BitColor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string ConvertedImageStr;
        private Bitmap ImageCache;//load image here first then convert so it can be used again
        public static MainWindow Main;
        private static bool InstantChanges;

        private readonly Dictionary<ToggleButton, Size> lcdButtons;
        private readonly Dictionary<ToggleButton, InterpolationMode> scaleButtons;
        private readonly Dictionary<ToggleButton, BitDepth> colorBitDepthButtons;

        private bool lcdPicked;
        private static readonly Regex numericRegex = new Regex("[^0-9]+");

        public static Logging Logging;
        Stopwatch sw = new Stopwatch();

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
            ToggleBtn_InstantChanges.IsChecked = false;
            InstantChanges = false;
            UpdateCurrentConvertBtnToolTip("No images loaded", true);
            ConvertBtn.IsEnabled = (!InstantChanges && ImageCache != null);
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

            MainWindowWindow.Title = "Star's Image Converter v0.3";
            AppTitleText.Content = "Star's Image Converter v0.3";
            AppBigTitle.Content = "Star's Image Converter";

            Logging.Log("MainWindow initialized.");
        }

        private void OnBrowseImagesClicked(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new()
            {
                Filter = "Image files (*.jpeg, *.jpg, *.jfif, *.png, *.tiff, *.bmp, *.gif, *.ico)|*.jpeg;*.jpg;*.jfif;*.png;*.tiff;*.bmp;*.gif;*.ico",
            };

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                if (IsFileTypeSupported(dialog.FileName) && TryGetBitmapImage(dialog.FileName, out ImageCache))
                {
                    UpdateBrowseImagesBtn(dialog.FileName.GetFileName());
                    UpdateCurrentConvertBtnToolTip(dialog.FileName, true);
                    if (InstantChanges && ImageCache != null)
                    {
                        TryConvertImage(ImageCache);
                    }
                }
                else
                {
                    ShowAcrylDialog("This file type is not supported!");
                }
            }
        }

        private bool TryGetBitmapImage(string filePath, out Bitmap result)
        {
            try
            {
                Bitmap bitmapImage = new(filePath);
                result = bitmapImage;
                return true;
            }
            catch (Exception e)
            {
                Logging.Log(e.ToString());
                result = null;
                return false;
            }
        }

        private static readonly string[] SupportedFileTypes = 
            { "png", "jpg", "jpeg", "jfif", "tiff", "bmp", "gif", "ico", };

        private bool IsFileTypeSupported(string file)
        {
            try
            {
                string fileExtension = file.Split('.').LastOrDefault();

                return SupportedFileTypes
                    .Any(i => i.Equals(fileExtension, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception e)
            {
                Logging.Log($"Caught exception at MainWindow.IsFileTypeSupported(string) ({file})");
                Logging.Log(e.ToString());
                return false;
            }
        }
        private void ChangeImagePreview(BitmapImage image)
        {
            ImagePreview.Source = image;
            CopyToClipBtn.IsEnabled = true;
        }

        private void OnConvertClicked(object sender, RoutedEventArgs e)
        {
            if (ImageCache != null)
            {
                TryConvertImage(ImageCache);
            }
            else
            {
                ShowAcrylDialog("Choose an image first!");
            }
        }

        /// <summary>
        /// displays error dialogs automatically. does file type checks as well. does not check if path is empty/null
        /// </summary>
        /// <param name="imagePath"></param>
        /// <returns></returns>
        private bool TryConvertFromFile(string imagePath, bool showErrorDialogs = true, bool cache = true)
        {
            try
            {
                if (IsFileTypeSupported(imagePath))
                {
                    Bitmap bitImage = new Bitmap(imagePath);
                    return TryConvertImage(bitImage, showErrorDialogs, cache);
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
        private bool TryConvertImage(Bitmap image, bool showErrorDialogs = true, bool cache = true)
        {
            try
            {
                if (!TryGetColorBitDepth(out BitDepth colorDepth))
                {
                    colorDepth = BitDepth.Color3;
                    Logging.Log("Color depth error! Defaulting to 3 bits.");
                    ShowAcrylDialog("Color depth error! Defaulting to 3 bits.");
                }

                if (!TryGetInterpolationMode(out InterpolationMode interpolationMode))
                {
                    interpolationMode = InterpolationMode.NearestNeighbor;
                    Logging.Log("Scaling mode error! Defaulting to Nearest.");
                    ShowAcrylDialog("Scaling mode error! Defaulting to Nearest.");
                }

                if (!TryGetLCDSize(out Size lcdSize))
                {
                    lcdSize = new Size(178, 178);
                    Logging.Log("LCD size error! Defaulting to 178x178.");
                    ShowAcrylDialog("LCD size error! Defaulting to 178x178.");
                }

                if (!TryGetDitherMode(out DitherMode dither))
                {
                    dither = DitherMode.NoDither;
                    Logging.Log("Dithering option error! Defaulting to None.");
                    ShowAcrylDialog("Dithering option error! Defaulting to None.");
                }

                Logging.Log($"Begin Conversion {colorDepth.ToString()} {interpolationMode.ToString()} {image.Size.ToShortString()} to {lcdSize.ToShortString()} {dither.ToString()} {ConvertBtnToolTip.Content.ToString()}");
                sw.Restart();
                Tuple<string, BitmapImage> result = Program.ConvertImage(image, dither, colorDepth, lcdSize, interpolationMode);
                sw.Stop();
                Logging.Log($"Conversion took {sw.ElapsedMilliseconds} ms");
                ConvertedImageStr = result.Item1;
                ChangeImagePreview(result.Item2);

                ImageCache = image;

                return true;
            }
            catch (Exception e)
            {
                Logging.Log($"Caught exception at TryConvertImage(Bitmap, bool, bool), ({ConvertBtnToolTip.Content.ToString()})");
                Logging.Log(e.ToString());
                ShowAcrylDialog("Error occurred during image conversion!");
                return false;
            }
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
                result = Program.BitDepth.Invalid;
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
        private bool TryGetLCDSize(out Size result)
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
                    result = new Size(int.Parse(ImageWidthSetting.Text), int.Parse(ImageHeightSetting.Text));
                    return true;
                }
                catch (Exception e)
                {
                    Logging.Log(e.ToString());
                    result = new Size(0, 0);
                    return false;
                }
            }
        }
        private bool TryGetDitherMode(out Program.DitherMode result)
        {
            if (ToggleBtn_Dithering.IsChecked == true)
            {
                result = Program.DitherMode.FloydSteinberg;
                return true;
            }
            else //PLACEHOLDER!! CHANGE WHEN THERE ARE MORE THAN 1 OPTION
            {
                result = Program.DitherMode.NoDither;
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
                ShowAcrylDialog($"Convert {(ImageCache != null ? "the" : "an")} image first!");
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

            DoInstantChange();
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

            DoInstantChange();
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (numericRegex.IsMatch(e.Text))
            {
                e.Handled = true;
            }
            else
            {
                CustomImageSizeChangedManually();
            }
        }

        private void TextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {

            if (e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true))
            {
                string text = (string)e.SourceDataObject.GetData(typeof(string));
                if (numericRegex.IsMatch(text))
                {
                    e.CancelCommand();
                }
                else
                {
                    CustomImageSizeChangedManually();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void TextBox_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            TextBox thisTextBox = sender as TextBox;
            TextBox_MouseWheelIncrement(thisTextBox, e, 1);
            CustomImageSizeChangedManually();
        }
        private void TextBox_MouseWheelIncrement(TextBox thisTextBox, MouseWheelEventArgs e, uint maxChangeAmount = 1)
        {
            if (!string.IsNullOrEmpty(thisTextBox.Text))
            {
                int num = int.Parse(thisTextBox.Text);
                thisTextBox.Text = (num + e.Delta.Clamp((int)-maxChangeAmount, (int)maxChangeAmount)).ToString();
            }
        }

        private void CustomImageSizeChangedManually()
        {
            if (lcdPicked && lcdButtons != null)
            {
                lcdPicked = false;
                foreach (var btn in lcdButtons)
                {
                    btn.Key.IsChecked = false;
                }
                ImageWidthSetting.Foreground = Brushes.White;
                ImageHeightSetting.Foreground = Brushes.White;
            }

            DoInstantChange();
        }

        private void ScaleOption_Clicked(object sender, RoutedEventArgs e)
        {
            ToggleButton thisBtn = sender as ToggleButton;
            foreach (var btn in scaleButtons)
            {
                btn.Key.IsChecked = (btn.Key == thisBtn);
            }

            DoInstantChange();
        }

        private void DitherOption_Clicked(object sender, RoutedEventArgs e) => DoInstantChange();

        private void ContextMenuItem_DeleteCache(object sender, RoutedEventArgs e)
        {
            ImageCache = null;
            UpdateBrowseImagesBtn(string.Empty);
            UpdateCurrentConvertBtnToolTip("No images loaded", true);
        }

        private void RemoveImagePreview(object sender, RoutedEventArgs e) => RemoveImagePreview();

        private void RemoveImagePreview()
        {
            ImagePreview.Source = null;
            ConvertedImageStr = string.Empty;
            CopyToClipBtn.IsEnabled = false;
        }

        private void ImagePreview_PreviewDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files)
                {
                    if (TryConvertFromFile(file, false))
                    {
                        UpdateBrowseImagesBtn(file.GetFileName());
                        UpdateCurrentConvertBtnToolTip(file, true);
                        Logging.Log("Image drag & dropped (FileDrop)");
                        return;
                    }
                }

                //when file type doesnt match
                ShowAcrylDialog("This file type is not supported!");
            }
            else if (e.Data.GetDataPresent(DataFormats.Bitmap))
            {
                Bitmap bitImage = (Bitmap)e.Data.GetData(DataFormats.Bitmap);
                if (TryConvertImage(bitImage))
                {
                    UpdateBrowseImagesBtn("Drag & Droped Image");
                    UpdateCurrentConvertBtnToolTip("Drag & Droped Image", true);
                    Logging.Log("Image drag & dropped (Bitmap)");
                }
            }
            else if (e.Data.GetDataPresent(DataFormats.Html))
            {
                try
                {
                    HtmlDocument doc = new();
                    doc.LoadHtml((string)e.Data.GetData(DataFormats.Html));
                    HtmlNodeCollection imgNodes = doc.DocumentNode.SelectNodes("//img");
                    if (imgNodes != null)
                    {
                        string imgSrc = imgNodes.First().GetAttributeValue("src", null);

                        WebClient client = new();
                        Stream stream = client.OpenRead(imgSrc);//try downloading the image
                        Bitmap bitmap = new(stream);
                        if (TryConvertImage(bitmap))
                        {
                            UpdateBrowseImagesBtn("Loaded from the web");
                            UpdateCurrentConvertBtnToolTip("Image loaded from the web", true);
                            Logging.Log($"Image loaded from HTML ({imgSrc})");
                        }
                    }
                    else
                    {
                        ShowAcrylDialog("This item is not supported!");
                    }
                }
                catch (Exception excep)
                {
                    Logging.Log("Caught exception while parsing HTML");
                    Logging.Log(excep.ToString());
                    ShowAcrylDialog("Invalid Web Item");
                }
            }
            else
            {
                ShowAcrylDialog("Clipboard does not contain any images");
            }
        }

        private void PasteFromClipboard(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsImage())
            {
                if (TryConvertImage(MainWindowUtils.BitmapSourceToBitmap(Clipboard.GetImage())))
                {
                    UpdateBrowseImagesBtn("Loaded from Clipboard");
                    UpdateCurrentConvertBtnToolTip("Image loaded from Clipboard", true);
                    Logging.Log("Image loaded from Clipboard (Bitmap)");
                }
            }
            else if (Clipboard.ContainsFileDropList())
            {
                System.Collections.Specialized.StringCollection filedroplist = Clipboard.GetFileDropList();
                foreach (string file in filedroplist)
                {
                    if (TryConvertFromFile(file))
                    {
                        UpdateBrowseImagesBtn(file.GetFileName());
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

            ConvertBtn.IsEnabled = (!InstantChanges && ImageCache != null);
            if (!ConvertBtn.IsEnabled)
            {
                UpdateCurrentConvertBtnToolTip("No images loaded", true);
            }

            RemoveImagePreviewBtn.IsEnabled = !InstantChanges;

            DoInstantChange();
        }

        private void DoInstantChange()
        {
            if (InstantChanges && ImageCache != null)
            {
                TryConvertImage(ImageCache);
            }
        }

        private void UpdateBrowseImagesBtn(string text)
        {
            ConvertBtn.IsEnabled = (!InstantChanges && ImageCache != null);
            if (!string.IsNullOrEmpty(text))
            {
                BrowseFilesToolTip.Content = text;
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
            Logging.OpenLogFile();
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
    }

    public class Logging
    {
        private readonly Timer LogTimer;
        private readonly StringBuilder LogBuffer = new StringBuilder();

        public readonly string LogFilePath;
        private const string LoggingDateTimeFormat = "MM/dd/yyyy HH:mm:ss.fff";

        public Logging(string FilePath, string FileNameWithExtension, int WriteIntervalInMilliseconds, bool DeleteOldLog)
        {
            LogTimer = new Timer(WriteIntervalInMilliseconds);
            LogTimer.Elapsed += OnTimerElapsed;
            LogTimer.AutoReset = true;
            LogTimer.Enabled = true;
            LogFilePath = System.IO.Path.Combine(FilePath, FileNameWithExtension);

            if (DeleteOldLog)
            {
                File.CreateText(LogFilePath);
            }

            Log("Started logging.");
        }

        /// <summary>
        /// Adds text to the buffer to be written later.
        /// </summary>
        /// <param name="text"></param>
        public void Log(string text)
        {
            LogBuffer.AppendLine(DateTime.Now.ToString(LoggingDateTimeFormat) + "    " + text);
        }

        private void OnTimerElapsed(object source, ElapsedEventArgs e) => WriteBufferToDisk();

        public void WriteBufferToDisk()
        {
            if (LogBuffer.Length > 0)
            {
                using (StreamWriter file = new StreamWriter(LogFilePath, true, Encoding.UTF8))
                {
                    file.Write(LogBuffer.ToString());
                    file.Close();
                }
                LogBuffer.Clear();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="editor">Editor to use to open the log file. Uses notepad by default.</param>
        public void OpenLogFile(string editor = "notepad.exe")
        {
            WriteBufferToDisk();
            if (!File.Exists(LogFilePath))
            {
                File.CreateText(LogFilePath);
            }
            Process.Start(editor, LogFilePath);
        }
    }

    public static class MainWindowUtils
    {
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

        //Source: https://stackoverflow.com/questions/94456/load-a-wpf-bitmapimage-from-a-system-drawing-bitmap
        public static BitmapImage BitmapToBitmapImage(Bitmap bitmap)
        {
            using (MemoryStream memory = new())
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
