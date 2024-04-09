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
using ImageConverterPlus.ViewModels;
using ImageConverterPlus.Services;
using ImageConverterPlus.Data;
using ImageConverterPlus.Data.Interfaces;
using ImageConverterPlus.Views;
using ImageConverterPlus.Services.interfaces;
using ReactiveUI;
using System.Reactive.Linq;

namespace ImageConverterPlus
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDialogPresenter
    {
        public new MainWindowViewModel DataContext => (MainWindowViewModel)base.DataContext;

        private readonly LogService _logger;

        public MainWindow(ConvertManagerService convertManager, LogService logger, IDialogService dialogService, MainWindowViewModel mainViewModel)
        {
            _logger = logger;
            base.DataContext = mainViewModel;

            InitializeComponent();

            dialogService.SetPresenter(this);

            convertManager.Delay = previewNew.animationDuration.TotalMilliseconds;

            convertManager.WhenAnyValue(x => x.SourceImage)
                .Subscribe(ConvMgr_SourceImageChanged);

            DataContext.WhenAnyValue(
                    x => x.LCDWidth,
                    x => x.LCDHeight,
                    (x, y) => new Int32Size(x, y))
                .Subscribe(LcdSizeChanged);
            DataContext.WhenAnyValue(
                    x => x.ImageSplitWidth, 
                    x => x.ImageSplitHeight,
                    (x, y) => new Int32Size(x, y))
                .Subscribe(ImageSplitSizeChanged);

            UpdatePreviewGrid();

            _logger.Log("MainWindow initialized");
        }

        private void LcdSizeChanged(Int32Size newSize)
        {
            UpdatePreviewContainerSize();
            ResetZoomAndPanOnPreviewNewSizeChanged(); //so jank
            //ResetZoomAndPan(false);
        }

        private void ImageSplitSizeChanged(Int32Size newSize)
        {
            UpdatePreviewGrid();
            ResetZoomAndPanOnPreviewNewSizeChanged();
            //ResetZoomAndPan(false);
        }

        private void UpdatePreviewContainerSize()
        {
            Int32Size lcd = new Int32Size(DataContext.LCDWidth, DataContext.LCDHeight);
            Int32Size split = new Int32Size(DataContext.ImageSplitWidth, DataContext.ImageSplitHeight);
            if (lcd.Width * split.Width > lcd.Height * split.Height)
            {
                previewNew.Width = PreviewContainerGridSize;
                previewNew.Height = PreviewContainerGridSize * ((double)(lcd.Height * split.Height) / (lcd.Width * split.Width));
            }
            else
            {
                previewNew.Width = PreviewContainerGridSize * ((double)(lcd.Width * split.Width) / (lcd.Height * split.Height));
                previewNew.Height = PreviewContainerGridSize;
            }
        }

        // TODO: Find out what this callback does and why
        private void ConvMgr_SourceImageChanged(BitmapSource? sourceImg)
        {
            if (DataContext.PreviewImageSource == null)
            {
                ResetZoomAndPanOnPreviewNewImageSizeChanged();
            }
            DataContext.PreviewImageSource = null;
            //ResetZoomAndPanOnPreviewNewImageSizeChanged();
        }

        private void ResetZoomAndPanOnPreviewNewSizeChanged()
        {
            previewNew.SizeChanged += SizeChangedOTEHandler;
        }

        private void ResetZoomAndPanOnPreviewNewImageSizeChanged()
        {
            previewNew.image.SizeChanged += SizeChangedOTEHandler; //so damn jank dude
        }

        /// <summary>
        /// please for the love of god dont use this thing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void SizeChangedOTEHandler(object sender, SizeChangedEventArgs e)
        {
            ResetZoomAndPan(false);
            previewNew.SizeChanged -= SizeChangedOTEHandler;
            previewNew.image.SizeChanged -= SizeChangedOTEHandler;
        }

        public Task ShowAsync(IDialog dialog)
        {
            ArgumentNullException.ThrowIfNull(dialog, nameof(dialog));
            if (dialog.ResultTask.IsCompleted)
            {
                throw new InvalidOperationException("Dialogs cannot be shown more than once");
            }

            new AcrylicDialog(this, dialog).ShowDialog();

            return dialog.ResultTask;
        }
    }
}
