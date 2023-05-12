using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ImageConverterPlus.Controls
{
    public class PositiveIntegerTextBox : TextBox
    {
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(
                nameof(Maximum),
                typeof(int),
                typeof(PositiveIntegerTextBox),
                new FrameworkPropertyMetadata(
                    default(int),
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    MaximumPropertyChanged));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(
                nameof(Minimum),
                typeof(int),
                typeof(PositiveIntegerTextBox),
                new FrameworkPropertyMetadata(
                    default(int),
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    MinimumPropertyChanged));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(int),
                typeof(PositiveIntegerTextBox),
                new FrameworkPropertyMetadata(
                    default(int),
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    ValuePropertyChanged));

        public static readonly RoutedEvent ValueChangedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(ValueChanged),
                RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<int>),
                typeof(PositiveIntegerTextBox));

        public event RoutedPropertyChangedEventHandler<int> ValueChanged
        {
            add => AddHandler(ValueChangedEvent, value);
            remove => RemoveHandler(ValueChangedEvent, value);
        }

        public int Maximum
        {
            get => (int)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public int Minimum
        {
            get => (int)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public PositiveIntegerTextBox()
        {
            DataObject.AddPastingHandler(this, OnPasting);
        }

        private void OnPasting(object sender, DataObjectPastingEventArgs e)
        {
            //var debug = e.SourceDataObject.GetFormats(true);
            if (e.SourceDataObject.GetData(DataFormats.StringFormat, true) is not string data || !Helpers.IsNumeric(data))
            {
                e.CancelCommand();
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            e.Handled = (e.Key == Key.Space);

            base.OnPreviewKeyDown(e);
        }

        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            e.Handled = !Helpers.IsNumeric(e.Text);

            base.OnPreviewTextInput(e);
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            bool textChanged = false;

            if (!Helpers.IsNumeric(this.Text) || !long.TryParse(this.Text, out long result))
            {
                this.Text = Value.ToString();
                textChanged = true;
            }
            else if (result > Maximum && Maximum > Minimum)
            {
                this.Text = Maximum.ToString();
                textChanged = true;
            }
            else if (result < Minimum)
            {
                this.Text = Minimum.ToString();
                textChanged = true;
            }

            if (this.Text.Length > 1 && this.Text[0] == '0')
            {
                string trimmed = this.Text.TrimStart('0');
                this.Text = trimmed.Length != 0 ? trimmed : "0";
                textChanged = true;
            }

            if (textChanged)
            {
                this.CaretIndex = this.Text.Length;
                e.Handled = true;
                return;
            }

            if (int.TryParse(this.Text, out int val))
            {
                this.Value = val;
            }

            base.OnTextChanged(e);
        }

        private static void MaximumPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            PositiveIntegerTextBox control = (PositiveIntegerTextBox)d;
            int newValue = (int)e.NewValue;

            if (control.Value > newValue)
            {
                control.Value = newValue;
            }
        }

        private static void MinimumPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            PositiveIntegerTextBox control = (PositiveIntegerTextBox)d;
            int newValue = (int)e.NewValue;

            if (control.Value < newValue)
            {
                control.Value = newValue;
            }
        }

        private static void ValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PositiveIntegerTextBox)d).Text = e.NewValue.ToString();
            ((PositiveIntegerTextBox)d).OnValueChanged((int)e.OldValue, (int)e.NewValue);
        }

        protected virtual void OnValueChanged(int oldValue, int newValue)
        {
            RoutedPropertyChangedEventArgs<int> args = new RoutedPropertyChangedEventArgs<int>(oldValue, newValue)
            {
                RoutedEvent = ValueChangedEvent,
                Source = this,
            };

            RaiseEvent(args);
        }
    }
}
