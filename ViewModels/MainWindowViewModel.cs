﻿using ImageConverterPlus.Base;
using ImageConverterPlus.ImageConverter;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace ImageConverterPlus.ViewModels
{
    public class MainWindowViewModel : NotifyPropertyChangedBase
    {
        public enum LCDPresetType
        {
            Custom = 0,
            LCDPanel = 1,
            TextPanel = 2,
            WideLCDPanel = 3,
            WideLCDPanelTall = 4,
        }

        private static MainWindow view => MainWindow.Static; //temp

        public bool EnableDithering { get => enableDithering; set => SetValue(ref enableDithering, value); }
        public BitDepth ColorDepth { get => colorDepth; set => SetValue(ref colorDepth, value); }
        public InterpolationMode InterpolationMode { get => interpolationMode; set => SetValue(ref interpolationMode, value); }
        public LCDPresetType SelectedLCD { get => selectedLCD; set => SetValue(ref selectedLCD, value); }
        public int LCDWidth { get => lcdWidth; set => SetValue(ref lcdWidth, value); }
        public int LCDHeight { get => lcdHeight; set => SetValue(ref lcdHeight, value); }
        public Size ImageSplitSize
        {
            get => imageSplitSize;
            set
            {
                bool widthChanged = imageSplitSize.Width != value.Width;
                bool heightChanged = imageSplitSize.Height != value.Height;

                SetValue(ref imageSplitSize, value);

                if (widthChanged)
                    RaisePropertyChanged(nameof(ImageSplitWidth));

                if (heightChanged)
                    RaisePropertyChanged(nameof(ImageSplitHeight));
            }
        }
        public int ImageSplitWidth
        {
            get => ImageSplitSize.Width;
            set
            {
                ImageSplitSize = new Size(value, ImageSplitHeight);
            }
        }
        public int ImageSplitHeight
        {
            get => ImageSplitSize.Height;
            set
            {
                ImageSplitSize = new Size(ImageSplitWidth, value);
            }
        }
        public bool ShowPreviewGrid { get => showPreviewGrid; set => SetValue(ref showPreviewGrid, value); }
        public bool IsMouseOverScrollableTextBox { get => isMouseOverScrollableTextBox; set => SetValue(ref isMouseOverScrollableTextBox, value); }

        public ICommand OpenLogsCommand { get; }
        public ICommand BrowseFilesCommand { get; }
        public ICommand ZoomToFitCommand { get; }
        public ICommand ZoomToFillCommand { get; }
        public ICommand ResetZoomAndPanCommand { get; }
        public ICommand ImageTransformCommand { get; }
        public ICommand CopyImageToClipboardCommand { get; }

        private bool enableDithering;
        private BitDepth colorDepth;
        private InterpolationMode interpolationMode;
        private LCDPresetType selectedLCD;
        private int lcdWidth;
        private int lcdHeight;
        private bool showPreviewGrid;
        private Size imageSplitSize;
        private bool isMouseOverScrollableTextBox;

        public MainWindowViewModel()
        {
            OpenLogsCommand = new ButtonCommand(ExecuteOpenLogsCommand);
            BrowseFilesCommand = new ButtonCommand(ExecuteBrowseFilesCommand);
            ZoomToFitCommand = new ButtonCommand(ExecuteZoomToFitCommand);
            ZoomToFillCommand = new ButtonCommand(ExecuteZoomToFillCommand);
            ResetZoomAndPanCommand = new ButtonCommand(ExecuteResetZoomAndPanCommand);
            ImageTransformCommand = new ButtonCommand(ExecuteImageTransformCommand);
            CopyImageToClipboardCommand = new ButtonCommand(ExecuteCopyImageToClipboardCommand);

            colorDepth = BitDepth.Color3;
            enableDithering = true;
            interpolationMode = InterpolationMode.HighQualityBicubic;
            selectedLCD = LCDPresetType.LCDPanel;
            lcdWidth = 178;
            lcdHeight = 178;
            showPreviewGrid = false;
            imageSplitSize = new Size(1, 1);
            isMouseOverScrollableTextBox = false;
        }

        private void ExecuteOpenLogsCommand(object? param)
        {
            MainWindow.Logging.OpenLogFileAsync();
        }

        private void ExecuteBrowseFilesCommand(object? param)
        {
            view.BrowseImageFiles();
        }

        private void ExecuteZoomToFitCommand(object? param)
        {
            view.ZoomToFit();
        }

        private void ExecuteZoomToFillCommand(object? param)
        {
            view.ZoomToFill();
        }

        private void ExecuteResetZoomAndPanCommand(object? param)
        {
            view.ResetPreviewZoomAndPan(true);
        }

        private void ExecuteImageTransformCommand(object? param)
        {
            if (param is RotateFlipType type)
            {
                view.TransformImage(type);
            }
        }

        private void ExecuteCopyImageToClipboardCommand(object? param)
        {
            view.CopyToClipClicked(param);
        }
    }
}
