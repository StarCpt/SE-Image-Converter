using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ImageConverterPlus.Data.Interfaces
{
    public interface IDialog
    {
        string Name { get; }
        ICommand CloseDialogCommand { get; }
        Task ResultTask { get; }
    }
}
