using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ImageConverterPlus.Base
{
    public abstract class NotifyPropertyChangedBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetValue<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, newValue))
            {
                field = newValue;
                RaisePropertyChanged(propertyName);
                return true;
            }
            return false;
        }

        public void RegisterPropertyChangedEventWeak(EventHandler<PropertyChangedEventArgs> handler) =>
            WeakEventManager<NotifyPropertyChangedBase, PropertyChangedEventArgs>.AddHandler(this, nameof(PropertyChanged), handler);

        public void UnregisterPropertyChangedEventWeak(EventHandler<PropertyChangedEventArgs> handler) =>
            WeakEventManager<NotifyPropertyChangedBase, PropertyChangedEventArgs>.RemoveHandler(this, nameof(PropertyChanged), handler);
    }
}
