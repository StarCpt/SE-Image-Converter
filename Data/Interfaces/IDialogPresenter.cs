using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageConverterPlus.Data.Interfaces
{
    public interface IDialogPresenter
    {
        Task ShowAsync(IDialog dialog);
    }
}
