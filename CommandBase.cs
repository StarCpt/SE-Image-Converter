using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ImageConverterPlus
{
    public abstract class CommandBase : ICommand
    {
        public virtual event EventHandler? CanExecuteChanged;

        readonly Action<object?> executeCallback;

        public CommandBase(Action<object?> executeCallback)
        {
            this.executeCallback = executeCallback;
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public virtual bool CanExecute(object? parameter) => true;
        public virtual void Execute(object? parameter) => executeCallback.Invoke(parameter);
    }
}
