﻿using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WinRTXamlToolkit.Controls.Extensions;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace WinRTXamlToolkit.Debugging.ViewModels
{
    public class VisualTreeViewModel : BindableBase
    {
        private Point _pointerPosition;
        private bool _isShiftPressed;
        private bool _isCtrlPressed;

        #region IsShown
        private bool _isShown;
        public bool IsShown
        {
            get { return _isShown; }
            set
            {
                if (this.SetProperty(ref _isShown, value))
                {
                    if (value)
                    {
                        UpdateHighlight();
                    }
                    else
                    {
                        HighlightVisibility = Visibility.Collapsed;
                    }
                }
            }
        }
        #endregion
        
        #region RootElements
        private readonly ObservableCollection<TreeItemViewModel> _rootElements = new ObservableCollection<TreeItemViewModel>();
        /// <summary>
        /// Gets or sets the root elements in the visual tree.
        /// </summary>
        public ObservableCollection<TreeItemViewModel> RootElements
        {
            get { return _rootElements; }
            //set { this.SetProperty(ref _rootElements, value); }
        }
        #endregion

        #region SelectedItem
        private TreeItemViewModel _selectedItem;
        public TreeItemViewModel SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                var oldSelectedItem = _selectedItem as DependencyObjectViewModel;

                if (this.SetProperty(ref _selectedItem, value))
                {
                    var newSelectedItem = _selectedItem as DependencyObjectViewModel;

                    OnSelectedItemChanged(oldSelectedItem, newSelectedItem);
                }
            }
        }

        private void OnSelectedItemChanged(
            DependencyObjectViewModel oldSelectedItem,
            DependencyObjectViewModel newSelectedItem)
        {
            if (oldSelectedItem != null)
            {
                oldSelectedItem.ModelPropertyChanged -= OnModelPropertyChanged;
            }

            if (newSelectedItem != null)
            {
                newSelectedItem.ModelPropertyChanged += OnModelPropertyChanged;
            }

            UpdateHighlight();
        }

        private async void OnModelPropertyChanged(object sender, EventArgs eventArgs)
        {
            UpdateHighlight();

            // Wait for pending layout updates
            await Task.Delay(100);

            UpdateHighlight();
        }
        #endregion

        #region HighlightMargin
        private Thickness _highlightMargin;
        public Thickness HighlightMargin
        {
            get { return _highlightMargin; }
            set { this.SetProperty(ref _highlightMargin, value); }
        }
        #endregion

        #region HighlightVisibility
        private Visibility _highlightVisibility = Visibility.Collapsed;
        public Visibility HighlightVisibility
        {
            get { return _highlightVisibility; }
            set { this.SetProperty(ref _highlightVisibility, value); }
        }
        #endregion

        #region IsPreviewShown
        //private bool _isPreviewShown;
        //public bool IsPreviewShown
        //{
        //    get { return _isPreviewShown; }
        //    set { this.SetProperty(ref _isPreviewShown, value); }
        //}
        #endregion

        #region ShowDefaultedProperties
        private bool _showDefaultedProperties;
        public bool ShowDefaultedProperties
        {
            get { return _showDefaultedProperties; }
            set { this.SetProperty(ref _showDefaultedProperties, value); }
        }
        #endregion

        #region ShowReadOnlyProperties
        private bool _showReadOnlyProperties;
        public bool ShowReadOnlyProperties
        {
            get { return _showReadOnlyProperties; }
            set { this.SetProperty(ref _showReadOnlyProperties, value); }
        }
        #endregion
        
        public VisualTreeViewModel()
        {
#pragma warning disable 4014
            Build();
#pragma warning restore 4014
            Window.Current.CoreWindow.KeyDown += OnKeyDown;
            Window.Current.CoreWindow.KeyUp += OnKeyUp;
            Window.Current.CoreWindow.PointerMoved += OnPointerMoved;
        }

        private void OnPointerMoved(CoreWindow sender, PointerEventArgs args)
        {
            _pointerPosition = args.CurrentPoint.Position;

            if (!_isShiftPressed ||
                !_isCtrlPressed)
            {
                return;
            }

#pragma warning disable 4014
            SelectElementUnderPointer();
#pragma warning restore 4014
        }

        internal async Task SelectElementUnderPointer()
        {
            var hoveredElement = VisualTreeHelper.FindElementsInHostCoordinates(
                _pointerPosition,
                Window.Current.Content).First();

            await SelectItem(hoveredElement, true);
        }

        internal async Task SelectFocused()
        {
            var focusedElement = FocusManager.GetFocusedElement() as UIElement;

            if (focusedElement != null)
            {
                await SelectItem(focusedElement, true);
            }
        }

        internal async Task<bool> SelectItem(UIElement element, bool refreshOnFail = false)
        {
            var ancestors = new[] { element }.Concat(element.GetAncestors()).ToList();
            var vm = this.RootElements[0] as DependencyObjectViewModel;
            var ancestorIndex = ancestors.IndexOf(vm.Model);

            if (ancestorIndex < 0)
            {
                await Refresh();
            }

            ancestorIndex = ancestors.IndexOf(vm.Model);

            if (ancestorIndex < 0)
            {
                System.Diagnostics.Debug.WriteLine("Something's wrong, but let's not throw exceptions here.");

                //Debugger.Break();
                if (refreshOnFail)
                {
                    await Refresh();
                    return await SelectItem(element, false);
                }

                return false;
            }

            //Debug.Assert(vm.Model == ancestors[0]);

            for (ancestorIndex = ancestorIndex - 1; ancestorIndex >= 0; ancestorIndex--)
            {
                if (!vm.IsExpanded)
                {
                    await vm.LoadChildren();
                    vm.IsExpanded = true;
                }
                var child =
                    vm.Children.OfType<DependencyObjectViewModel>()
                        .FirstOrDefault(dovm => dovm.Model == ancestors[ancestorIndex]);
                if (child == null)
                {
                    System.Diagnostics.Debug.WriteLine("Something's wrong, but let's not throw exceptions here.");

                    //Debugger.Break();
                    if (refreshOnFail)
                    {
                        await Refresh();
                        return await SelectItem(element, false);
                    }

                    return false;
                }

                vm = child;
            }

            await Task.Delay(100);
            vm.IsSelected = true;

            return true;
        }

        private void OnKeyUp(CoreWindow sender, KeyEventArgs args)
        {
            if (args.VirtualKey == VirtualKey.Shift)
            {
                _isShiftPressed = false;
            }
            else if (args.VirtualKey == VirtualKey.Control)
            {
                _isCtrlPressed = false;
            }
        }

        private void OnKeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (args.VirtualKey == VirtualKey.Shift)
            {
                _isShiftPressed = true;

                if (_isCtrlPressed)
                {
#pragma warning disable 4014
                    SelectElementUnderPointer();
#pragma warning restore 4014
                }
            }
            else if (args.VirtualKey == VirtualKey.Control)
            {
                _isCtrlPressed = true;

                if (_isShiftPressed)
                {
#pragma warning disable 4014
                    SelectElementUnderPointer();
#pragma warning restore 4014
                }
            }
        }

#pragma warning disable 1998
        private async Task Build()
#pragma warning restore 1998
        {
            this.RootElements.Clear();
            var rootElement = Window.Current.Content as UIElement;

            if (rootElement != null)
            {
                this.RootElements.Add(new DependencyObjectViewModel(this, null, rootElement));
            }
        }

        internal async Task Refresh()
        {
            //TODO: Work on getting a partial refresh working. Right now if a refreshed element is not in visual tree - it doesn't rebuild the tree as it should.
            //if (this.SelectedItem != null)
            //{
            //    await this.SelectedItem.Refresh();
            //}
            //else 
            if (this.RootElements.Count == 1 &&
                this.RootElements[0] is DependencyObjectViewModel &&
                ((DependencyObjectViewModel)this.RootElements[0]).Model == Window.Current.Content)
            {
                await this.RootElements[0].Refresh();
            }
            else
            {
                this.RootElements.Clear();
                await this.Build();
            }
        }

        private void UpdateHighlight()
        {
            var dovm = this.SelectedItem as DependencyObjectViewModel;

            if (dovm == null)
            {
                HighlightVisibility = Visibility.Collapsed;
                return;
            }

            var fe = dovm.Model as FrameworkElement;

            if (fe == null)
            {
                HighlightVisibility = Visibility.Collapsed;
                return;
            }

            try
            {
                var ancestors = fe.GetAncestors().ToArray();

                if (!ancestors.Contains(Window.Current.Content))
                {
                    HighlightVisibility = Visibility.Collapsed;
                    return;
                }

                var elementBounds = fe.GetBoundingRect();
                var windowBounds = Window.Current.Bounds;

                HighlightMargin = new Thickness(
                    elementBounds.Left,
                    elementBounds.Top,
                    windowBounds.Width - elementBounds.Right,
                    windowBounds.Height - elementBounds.Bottom);
            }
#pragma warning disable 168
            catch (Exception ex)
#pragma warning restore 168
            {
                HighlightVisibility = Visibility.Collapsed;
            }

            HighlightVisibility = Visibility.Visible;
        }
    }
}
