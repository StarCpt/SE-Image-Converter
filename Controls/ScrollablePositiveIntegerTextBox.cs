using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ImageConverterPlus.Controls
{
    public class ScrollablePositiveIntegerTextBox : PositiveIntegerTextBox
    {
        public static readonly DependencyProperty ScrollChangeProperty =
            DependencyProperty.Register(
                nameof(ScrollChange),
                typeof(int),
                typeof(ScrollablePositiveIntegerTextBox),
                new PropertyMetadata(
                    default(int),
                    null));

        public int ScrollChange
        {
            get => (int)GetValue(ScrollChangeProperty);
            set => SetValue(ScrollChangeProperty, value);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            base.OnPreviewMouseWheel(e);

            this.Value += ScrollChange * Math.Sign(e.Delta);
            e.Handled = true;
        }
    }
}
