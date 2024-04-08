using ImageConverterPlus.Services.interfaces;
using ImageConverterPlus.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ImageConverterPlus.Services
{
    public class ClipboardService
    {
        private readonly object _tokenSourceLock = new object();
        private CancellationTokenSource? _clipboardDelaySetTokenSource = null;

        private readonly IDialogService _dialogService;

        public ClipboardService(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public async void SetClipboardDelayed(string text, int delayMs)
        {
            ArgumentNullException.ThrowIfNull(text, nameof(text));

            CancellationToken myToken;
            lock (_tokenSourceLock)
            {
                _clipboardDelaySetTokenSource?.Cancel();
                _clipboardDelaySetTokenSource?.Dispose();
                _clipboardDelaySetTokenSource = new CancellationTokenSource();
                myToken = _clipboardDelaySetTokenSource.Token;
            }
            await Task.Delay(delayMs);

            if (myToken.IsCancellationRequested)
                return;

            try
            {
                Clipboard.SetDataObject(text, true);
            }
            catch (Exception e)
            {
                _ = _dialogService.ShowAsync(new MessageDialogViewModel("Error", $"Clipboard error, try again! {e}"));
            }
        }
    }
}
