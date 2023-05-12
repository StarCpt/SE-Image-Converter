using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;

namespace ImageConverterPlus.Behaviors
{
    public static class LoadedBehavior
    {
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.RegisterAttached(
                "Command",
                typeof(ICommand),
                typeof(LoadedBehavior),
                new FrameworkPropertyMetadata(
                    null,
                    CommandPropertyChanged));

        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.RegisterAttached(
                "CommandParameter",
                typeof(object),
                typeof(LoadedBehavior),
                new FrameworkPropertyMetadata(
                    null,
                    CommandPropertyChanged));

        public static ICommand GetCommand(FrameworkElement target)
        {
            return (ICommand)target.GetValue(CommandProperty);
        }

        public static void SetCommand(FrameworkElement target, ICommand command)
        {
            target.SetValue(CommandProperty, command);
        }

        public static object? GetCommandParameter(FrameworkElement target)
        {
            return target.GetValue(CommandProperty);
        }

        public static void SetCommandParameter(FrameworkElement target, object? parameter)
        {
            target.SetValue(CommandProperty, parameter);
        }

        private static void CommandPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((FrameworkElement)d).Loaded -= FrameworkElementLoaded;
            if (e.NewValue != null)
                ((FrameworkElement)d).Loaded += FrameworkElementLoaded;
        }

        private static void FrameworkElementLoaded(object sender, RoutedEventArgs e)
        {
            var element = (FrameworkElement)sender;
            if (element.GetValue(CommandProperty) is ICommand command &&
                command.CanExecute(element.GetValue(CommandParameterProperty)))
            {
                command.Execute(element.GetValue(CommandParameterProperty));
            }
        }
    }
}
