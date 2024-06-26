﻿
using ImageConverterPlus.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ImageConverterPlus.Behaviors
{
    public static class ScrollAnimationBehavior
    {
        public static double intendedLocation = 0;
        public static double lastDelta = 0;
        

        #region Private ScrollViewer for ListBox

        private static ScrollViewer _listBoxScroller = new ScrollViewer();

        #endregion

        #region VerticalOffset Property

        public static DependencyProperty VerticalOffsetProperty =
            DependencyProperty.RegisterAttached("VerticalOffset",
                                                typeof(double),
                                                typeof(ScrollAnimationBehavior),
                                                new UIPropertyMetadata(0.0, OnVerticalOffsetChanged));

        public static void SetVerticalOffset(FrameworkElement target, double value)
        {
            target.SetValue(VerticalOffsetProperty, value);
        }

        public static double GetVerticalOffset(FrameworkElement target)
        {
            return (double)target.GetValue(VerticalOffsetProperty);
        }

        #endregion

        #region TimeDuration Property

        public static DependencyProperty TimeDurationProperty =
            DependencyProperty.RegisterAttached("TimeDuration",
                                                typeof(TimeSpan),
                                                typeof(ScrollAnimationBehavior),
                                                new PropertyMetadata(new TimeSpan(0, 0, 0, 0, 0)));

        public static void SetTimeDuration(FrameworkElement target, TimeSpan value)
        {
            target.SetValue(TimeDurationProperty, value);
        }

        public static TimeSpan GetTimeDuration(FrameworkElement target)
        {
            return (TimeSpan)target.GetValue(TimeDurationProperty);
        }

        #endregion

        #region PointsToScroll Property

        public static DependencyProperty PointsToScrollProperty =
            DependencyProperty.RegisterAttached("PointsToScroll",
                                                typeof(double),
                                                typeof(ScrollAnimationBehavior),
                                                new PropertyMetadata(0.0));

        public static void SetPointsToScroll(FrameworkElement target, double value)
        {
            target.SetValue(PointsToScrollProperty, value);
        }

        public static double GetPointsToScroll(FrameworkElement target)
        {
            return (double)target.GetValue(PointsToScrollProperty);
        }

        #endregion

        #region OnVerticalOffset Changed

        private static void OnVerticalOffsetChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            ScrollViewer scrollViewer = (ScrollViewer)target;

            if (scrollViewer != null)
            {
                scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
            }
        }

        #endregion

        #region IsEnabled Property

        public static DependencyProperty IsEnabledProperty =
                                                DependencyProperty.RegisterAttached("IsEnabled",
                                                typeof(bool),
                                                typeof(ScrollAnimationBehavior),
                                                new UIPropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(FrameworkElement target, bool value)
        {
            target.SetValue(IsEnabledProperty, value);
        }

        public static bool GetIsEnabled(FrameworkElement target)
        {
            return (bool)target.GetValue(IsEnabledProperty);
        }

        #endregion

        #region OnIsEnabledChanged Changed

        private static void OnIsEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var target = sender;

            if (target != null && target is ScrollViewer scroller)
            {
                scroller.Loaded += new RoutedEventHandler(scrollerLoaded);
            }

            if (target != null && target is ListBox listbox)
            {
                listbox.Loaded += new RoutedEventHandler(listboxLoaded);
            }
        }

        #endregion

        #region AnimateScroll Helper

        private static void AnimateScroll(ScrollViewer scrollViewer, double ToValue)
        {
            scrollViewer.BeginAnimation(VerticalOffsetProperty, null);
            DoubleAnimation verticalAnimation = new DoubleAnimation(scrollViewer.VerticalOffset, ToValue, GetTimeDuration(scrollViewer));
            scrollViewer.BeginAnimation(VerticalOffsetProperty, verticalAnimation);
        }

        #endregion

        #region NormalizeScrollPos Helper

        private static double NormalizeScrollPos(ScrollViewer scroll, double scrollChange, Orientation o)
        {
            double returnValue = scrollChange;

            if (scrollChange < 0)
            {
                returnValue = 0;
            }

            if (o == Orientation.Vertical && scrollChange > scroll.ScrollableHeight)
            {
                returnValue = scroll.ScrollableHeight;
            }
            else if (o == Orientation.Horizontal && scrollChange > scroll.ScrollableWidth)
            {
                returnValue = scroll.ScrollableWidth;
            }

            return returnValue;
        }

        #endregion

        #region UpdateScrollPosition Helper

        private static void UpdateScrollPosition(object sender)
        {
            if (sender is ListBox listbox)
            {
                double scrollTo = 0;

                for (int i = 0; i < (listbox.SelectedIndex); i++)
                {
                    if (listbox.ItemContainerGenerator.ContainerFromItem(listbox.Items[i]) is ListBoxItem tempItem)
                    {
                        scrollTo += tempItem.ActualHeight;
                    }
                }

                AnimateScroll(_listBoxScroller, scrollTo);
            }
        }

        #endregion

        #region SetEventHandlersForScrollViewer Helper

        private static void SetEventHandlersForScrollViewer(ScrollViewer scroller)
        {
            scroller.PreviewMouseWheel += new MouseWheelEventHandler(ScrollViewerPreviewMouseWheel);
            scroller.PreviewKeyDown += new KeyEventHandler(ScrollViewerPreviewKeyDown);
            scroller.PreviewMouseLeftButtonUp += Scroller_PreviewMouseLeftButtonUp;
        }

        private static void Scroller_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            intendedLocation = ((ScrollViewer)sender).VerticalOffset;
        }

        #endregion

        #region scrollerLoaded Event Handler

        private static void scrollerLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is ScrollViewer scroller)
            {
                SetEventHandlersForScrollViewer(scroller);
            }
        }

        #endregion

        #region listboxLoaded Event Handler
        //https://github.com/MozzieMD/Popcorn/blob/9edf4e7345357c6d449b54f0fe282cadf2dfb6b9/Popcorn/Helpers/Extensions.cs
        // and https://stackoverflow.com/questions/665719/wpf-animate-listbox-scrollviewer-horizontaloffset?rq=1
        public static class FindVisualChildHelper
        {
            public static T? GetFirstChildOfType<T>(DependencyObject dependencyObject) where T : DependencyObject
            {
                if (dependencyObject == null)
                {
                    return null;
                }

                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(dependencyObject); i++)
                {
                    var child = VisualTreeHelper.GetChild(dependencyObject, i);

                    var result = (child as T) ?? GetFirstChildOfType<T>(child);

                    if (result != null)
                    {
                        return result;
                    }
                }

                return null;
            }
        }

        public static class FindVisualParentHelper
        {
            public static T? FindParentOfType<T>(DependencyObject dependencyObject) where T : DependencyObject
            {
                while (dependencyObject != null)
                {
                    if ((dependencyObject = VisualTreeHelper.GetParent(dependencyObject)) is T)
                    {
                        return (T)dependencyObject;
                    }
                }

                return null;
            }
        }

        private static void listboxLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is ListBox listbox)
            {
                _listBoxScroller = FindVisualChildHelper.GetFirstChildOfType<ScrollViewer>(listbox);
                SetEventHandlersForScrollViewer(_listBoxScroller);

                SetTimeDuration(_listBoxScroller, new TimeSpan(0, 0, 0, 0, 200));
                SetPointsToScroll(_listBoxScroller, 16.0);

                listbox.SelectionChanged += new SelectionChangedEventHandler(ListBoxSelectionChanged);
                listbox.Loaded += new RoutedEventHandler(ListBoxLoaded);
                listbox.LayoutUpdated += new EventHandler(ListBoxLayoutUpdated);
            }
        }

        #endregion

        #region ScrollViewerPreviewMouseWheel Event Handler

        private static void ScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Mouse.DirectlyOver is DependencyObject dp &&
                FindVisualParentHelper.FindParentOfType<ScrollablePositiveIntegerTextBox>(dp) is not null)
            {
                return;
            }

            double mouseWheelChange = e.Delta;
            ScrollViewer scroller = (ScrollViewer)sender;

            bool alreadyReachedEnd = (e.Delta < 0 && intendedLocation >= scroller.ScrollableHeight) || (e.Delta > 0 && intendedLocation <= 0);

            double newVOffset = intendedLocation - (mouseWheelChange / 2);

            if (!alreadyReachedEnd)
            {
                AnimateScroll(scroller, newVOffset);
            }

            intendedLocation = Math.Clamp(newVOffset, 0, scroller.ScrollableHeight);

            e.Handled = true;
        }

        #endregion

        #region ScrollViewerPreviewKeyDown Handler

        private static void ScrollViewerPreviewKeyDown(object sender, KeyEventArgs e)
        {
            ScrollViewer scroller = (ScrollViewer)sender;

            Key keyPressed = e.Key;
            double newVerticalPos = GetVerticalOffset(scroller);
            bool isKeyHandled = false;

            if (keyPressed == Key.Down)
            {
                newVerticalPos = NormalizeScrollPos(scroller, (newVerticalPos + GetPointsToScroll(scroller)), Orientation.Vertical);
                intendedLocation = newVerticalPos;
                isKeyHandled = true;
            }
            else if (keyPressed == Key.PageDown)
            {
                newVerticalPos = NormalizeScrollPos(scroller, (newVerticalPos + scroller.ViewportHeight), Orientation.Vertical);
                intendedLocation = newVerticalPos;
                isKeyHandled = true;
            }
            else if (keyPressed == Key.Up)
            {
                newVerticalPos = NormalizeScrollPos(scroller, (newVerticalPos - GetPointsToScroll(scroller)), Orientation.Vertical);
                intendedLocation = newVerticalPos;
                isKeyHandled = true;
            }
            else if (keyPressed == Key.PageUp)
            {
                newVerticalPos = NormalizeScrollPos(scroller, (newVerticalPos - scroller.ViewportHeight), Orientation.Vertical);
                intendedLocation = newVerticalPos;
                isKeyHandled = true;
            }

            if (newVerticalPos != GetVerticalOffset(scroller))
            {
                intendedLocation = newVerticalPos;
                AnimateScroll(scroller, newVerticalPos);
            }

            e.Handled = isKeyHandled;
        }

        #endregion

        #region ListBox Event Handlers

        private static void ListBoxLayoutUpdated(object sender, EventArgs e)
        {
            UpdateScrollPosition(sender);
        }

        private static void ListBoxLoaded(object sender, RoutedEventArgs e)
        {
            UpdateScrollPosition(sender);
        }

        private static void ListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateScrollPosition(sender);
        }

        #endregion
    }
}
