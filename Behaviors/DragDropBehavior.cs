using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;

namespace ImageConverterPlus.Behaviors
{
    public static class DragDropBehavior
    {
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.RegisterAttached(
                "Command",
                typeof(ICommand),
                typeof(DragDropBehavior),
                new PropertyMetadata(
                    null,
                    CommandChanged));

        public static ICommand GetCommand(DependencyObject obj) => (ICommand)obj.GetValue(CommandProperty);
        public static void SetCommand(DependencyObject obj, ICommand value) => obj.SetValue(CommandProperty, value);

        private static void CommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                if (e.OldValue is ICommand oldCmd)
                {
                    element.DragEnter -= (sender, e) => DragEventHandler(e, oldCmd);
                    element.DragOver -= (sender, e) => DragEventHandler(e, oldCmd);

                    element.Drop -= (sender, e) => DropEventHandler(oldCmd, e);
                }

                if (e.NewValue is ICommand newCmd)
                {
                    element.DragEnter += (sender, e) => DragEventHandler(e, newCmd);
                    element.DragOver += (sender, e) => DragEventHandler(e, newCmd);

                    element.Drop += (sender, e) => DropEventHandler(newCmd, e);
                }
            }
        }

        private static void DragEventHandler(DragEventArgs e, ICommand cmd)
        {
            e.Effects = cmd.CanExecute(e) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private static void DropEventHandler(ICommand cmd, DragEventArgs e)
        {
            if (cmd.CanExecute(e))
            {
                cmd.Execute(e);
            }
        }
    }
}
