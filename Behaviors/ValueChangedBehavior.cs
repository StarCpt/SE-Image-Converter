using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using ImageConverterPlus.Controls;

namespace ImageConverterPlus.Behaviors
{
    public class ValueChangedBehavior
    {
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.RegisterAttached(
                "Command",
                typeof(ICommand),
                typeof(ValueChangedBehavior),
                new FrameworkPropertyMetadata(
                    null,
                    CommandPropertyChanged));

        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.RegisterAttached(
                "CommandParameter",
                typeof(object),
                typeof(ValueChangedBehavior),
                new FrameworkPropertyMetadata(
                    null,
                    CommandPropertyChanged));

        public static ICommand GetCommand(PositiveIntegerTextBox target)
        {
            return (ICommand)target.GetValue(CommandProperty);
        }

        public static void SetCommand(PositiveIntegerTextBox target, ICommand command)
        {
            target.SetValue(CommandProperty, command);
        }

        public static object? GetCommandParameter(PositiveIntegerTextBox target)
        {
            return target.GetValue(CommandProperty);
        }

        public static void SetCommandParameter(PositiveIntegerTextBox target, object? parameter)
        {
            target.SetValue(CommandProperty, parameter);
        }

        private static void CommandPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PositiveIntegerTextBox)d).ValueChanged -= ValueChanged;
            if (e.NewValue != null)
                ((PositiveIntegerTextBox)d).ValueChanged += ValueChanged;
        }

        private static void ValueChanged(object sender, RoutedEventArgs e)
        {
            var element = (PositiveIntegerTextBox)sender;
            if (element.GetValue(CommandProperty) is ICommand command &&
                command.CanExecute(element.GetValue(CommandParameterProperty)))
            {
                command.Execute(element.GetValue(CommandParameterProperty));
            }
        }
    }
}
