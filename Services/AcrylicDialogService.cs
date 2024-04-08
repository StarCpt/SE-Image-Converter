using ImageConverterPlus.Data.Interfaces;
using ImageConverterPlus.Services.interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageConverterPlus.Services
{
    public class AcrylicDialogService : IDialogService
    {
        private readonly IDialogPresenter _dialogPresenter;

        public AcrylicDialogService(IDialogPresenter dialogPresenter)
        {
            _dialogPresenter = dialogPresenter;
        }

        public Task ShowAsync(IDialog dialog)
        {
            return _dialogPresenter.ShowAsync(dialog);
        }
    }
}
