using CommunityToolkit.Mvvm.Input;
using ImageConverterPlus.Data.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ImageConverterPlus.ViewModels
{
    public class MessageDialogViewModel : IDialog
    {
        public string Name { get; }
        public string Text { get; }
        public string CloseButtonText { get; init; } = "Ok";
        public Task ResultTask => _resultTaskSource.Task;

        public ICommand CloseDialogCommand { get; }

        private readonly TaskCompletionSource _resultTaskSource = new();

        public MessageDialogViewModel(string name, string text)
        {
            Name = name;
            Text = text;

            CloseDialogCommand = new RelayCommand(() =>
            {
                _resultTaskSource.TrySetResult();
            }, () => !_resultTaskSource.Task.IsCompleted);
        }
    }
}
