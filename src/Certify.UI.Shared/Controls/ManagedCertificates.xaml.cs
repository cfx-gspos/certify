﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Certify.UI.Controls
{

    public delegate void OnDuplicateManagedCertificate(Certify.Models.ManagedCertificate original);  // delegate


    /// <summary>
    /// Interaction logic for ManagedCertificates.xaml 
    /// </summary>
    public partial class ManagedCertificates
    {
        protected ViewModel.AppViewModel _appViewModel => ViewModel.AppViewModel.Current;
        protected ViewModel.ManagedCertificateViewModel _itemViewModel => ViewModel.ManagedCertificateViewModel.Current;

        private string _sortOrder { get; set; } = "NameAsc";

        /// <summary>
        /// event for Duplicate option
        /// </summary>
        public event OnDuplicateManagedCertificate OnDuplicate;

        public ManagedCertificates()
        {
            InitializeComponent();
            DataContext = _appViewModel;
            MainItemView.DataContext = _itemViewModel;

            SetFilter(); // start listening

            _appViewModel.PropertyChanged -= AppViewModel_PropertyChanged;
            _appViewModel.PropertyChanged += AppViewModel_PropertyChanged;

        }

        private void AppViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ManagedCertificates" || (e.PropertyName == "SelectedItem" &&
                    _appViewModel.ManagedCertificates != null))
            {
                SetFilter(); // reset listeners when ManagedCertificates are reset
                _itemViewModel.RaisePropertyChangedEvent("SelectedItem");
                _itemViewModel.RaisePropertyChangedEvent("IsSelectedItemValid");
            }
        }

        private void SetFilter()
        {
            CollectionViewSource.GetDefaultView(_appViewModel.ManagedCertificates).Filter = (item) =>
            {
                var filter = txtFilter.Text.Trim();

                return filter == "" || filter.Split(';').Where(f => f.Trim() != "").Any(f =>
                    (((Models.ManagedCertificate)item).Health == Models.ManagedCertificateHealth.Error && f == "[Status=Error]") ||
                    (((Models.ManagedCertificate)item).Health == Models.ManagedCertificateHealth.OK && f == "[Status=OK]") ||
                    (((Models.ManagedCertificate)item).Health == Models.ManagedCertificateHealth.Warning && f == "[Status=Warning]") ||
                    (((Models.ManagedCertificate)item).Health == Models.ManagedCertificateHealth.AwaitingUser && f == "[Status=AwaitingUser]") ||
                    ((Models.ManagedCertificate)item).Name.IndexOf(f, StringComparison.OrdinalIgnoreCase) > -1 ||
                    (((Models.ManagedCertificate)item).DomainOptions?.Any(d => d.Domain.IndexOf(f, StringComparison.OrdinalIgnoreCase) > -1) ?? false) ||
                    (((Models.ManagedCertificate)item).Comments ?? "").IndexOf(f, StringComparison.OrdinalIgnoreCase) > -1);
            };

            //sort by name ascending
            CollectionViewSource.GetDefaultView(_appViewModel.ManagedCertificates).SortDescriptions.Clear();

            if (_sortOrder == "NameAsc")
            {
                CollectionViewSource.GetDefaultView(_appViewModel.ManagedCertificates).SortDescriptions.Add(
                   new System.ComponentModel.SortDescription("Name", System.ComponentModel.ListSortDirection.Ascending)
               );
            }

            if (_sortOrder == "ExpiryDateAsc")
            {
                CollectionViewSource.GetDefaultView(_appViewModel.ManagedCertificates).SortDescriptions.Add(
                   new System.ComponentModel.SortDescription("DateExpiry", System.ComponentModel.ListSortDirection.Ascending)
               );
            }
        }

        private async void ListViewItem_InteractionEvent(object sender, InputEventArgs e)
        {
            var item = (ListViewItem)sender;
            var ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            if (item != null && item.DataContext != null && item.DataContext is Models.ManagedCertificate)
            {
                var site = (Models.ManagedCertificate)item.DataContext;

                site = site == _appViewModel.SelectedItem && ctrl ? null : site;

                if (_appViewModel.SelectedItem != site)
                {
                    if (await _itemViewModel.ConfirmDiscardUnsavedChanges())
                    {
                        SelectAndFocus(site);
                    }
                    e.Handled = true;
                }
            }
        }

        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            var defaultView = CollectionViewSource.GetDefaultView(lvManagedCertificates.ItemsSource);

            defaultView.Refresh();

            if (lvManagedCertificates.SelectedIndex == -1 && _appViewModel.SelectedItem != null)
            {
                // if the data model's selected item has come into view after filter box text
                // changed, select the item in the list
                if (defaultView.Filter(_appViewModel.SelectedItem))
                {
                    lvManagedCertificates.SelectedItem = _appViewModel.SelectedItem;
                }
            }
        }

        private async void TxtFilter_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ResetFilter();
            }

            if (e.Key == Key.Enter || e.Key == Key.Down)
            {
                if (lvManagedCertificates.Items.Count > 0)
                {
                    // get selected index of filtered list or 0
                    var index = lvManagedCertificates.Items.IndexOf(_appViewModel.SelectedItem);
                    var item = lvManagedCertificates.Items[index == -1 ? 0 : index];

                    // if navigating away, confirm discard
                    if (item != _appViewModel.SelectedItem &&
                        !await _itemViewModel.ConfirmDiscardUnsavedChanges())
                    {
                        return;
                    }

                    // if confirmed, select and focus
                    e.Handled = true;
                    SelectAndFocus(item);
                }
            }
        }

        private void ResetFilter()
        {
            txtFilter.Text = "";
            txtFilter.Focus();

            if (lvManagedCertificates.SelectedItem != null)
            {
                lvManagedCertificates.ScrollIntoView(lvManagedCertificates.SelectedItem);
            }
        }

        private void SelectAndFocus(object obj)
        {
            var managedCert = obj as Models.ManagedCertificate;

            lvManagedCertificates.Items.Refresh();

            if (lvManagedCertificates.Items.Count > 0 && lvManagedCertificates.Items.Contains(managedCert))
            {

                // lvManagedCertificates.UpdateLayout(); // ensure containers exist

                if (lvManagedCertificates.ItemContainerGenerator.ContainerFromItem(managedCert) is ListViewItem item)
                {
                    item.Focus();
                    item.IsSelected = true;
                }
            }

            this.Dispatcher.Invoke(new Action(() => { _appViewModel.SelectedItem = managedCert; }));
        }

        private async void ListViewItem_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ResetFilter();
                return;
            }

            if (e.Key == Key.Delete && lvManagedCertificates.SelectedItem != null)
            {
                var itemToDelete = lvManagedCertificates.SelectedItem as Certify.Models.ManagedCertificate;
                if (itemToDelete != null)
                {
                    await _appViewModel.DeleteManagedCertificate(itemToDelete);

                    if (lvManagedCertificates.Items.Count > 0)
                    {
                        if (lvManagedCertificates.SelectedItem != null)
                        {
                            SelectAndFocus(lvManagedCertificates.SelectedItem);
                        }
                    }
                }
                return;
            }

            object next = _appViewModel.SelectedItem;

            var item = ((ListViewItem)sender);

            var index = lvManagedCertificates.Items.IndexOf(item.DataContext);

            var ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            var pagesize = (int)(lvManagedCertificates.ActualHeight / item.ActualHeight);

            switch (e.Key)
            {
                case Key.Enter:
                    next = item.DataContext;
                    break;

                case Key.Space:
                    next = _appViewModel.SelectedItem != null && ctrl ? null : item.DataContext;
                    break;

                case Key.Up:
                    next = lvManagedCertificates.Items[index - 1 > -1 ? index - 1 : 0];
                    break;

                case Key.Down:
                    next = lvManagedCertificates.Items[index + 1 < lvManagedCertificates.Items.Count ? index + 1 : lvManagedCertificates.Items.Count - 1];
                    break;

                case Key.Home:
                    next = lvManagedCertificates.Items[0];
                    break;

                case Key.End:
                    next = lvManagedCertificates.Items[lvManagedCertificates.Items.Count - 1];
                    break;

                case Key.PageUp:
                    next = lvManagedCertificates.Items[index - pagesize > -1 ? index - pagesize : 0];
                    break;

                case Key.PageDown:
                    next = lvManagedCertificates.Items[index + pagesize < lvManagedCertificates.Items.Count ? index + pagesize : lvManagedCertificates.Items.Count - 1];
                    break;
            }

            if (next != _appViewModel.SelectedItem)
            {
                if (await _itemViewModel.ConfirmDiscardUnsavedChanges())
                {
                    SelectAndFocus(next);
                }

                e.Handled = true;
            }
        }

        private int lastSelectedIndex = -1;

        private void lvManagedCertificates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_appViewModel.SelectedItem != null &&
                !_appViewModel.ManagedCertificates.Contains(_appViewModel.SelectedItem))
            {
                if (lvManagedCertificates.Items.Count == 0)
                {
                    _appViewModel.SelectedItem = null;
                    txtFilter.Focus();
                }
                else
                {
                    // selected item was deleted
                    var newIndex = lastSelectedIndex;

                    while (newIndex >= lvManagedCertificates.Items.Count && newIndex >= -1)
                    {
                        newIndex--;
                    }

                    SelectAndFocus(newIndex == -1 ? null : lvManagedCertificates.Items[newIndex]);
                }
            }
            lastSelectedIndex = lvManagedCertificates.SelectedIndex;
        }

        private void UserControl_OnLoaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);

            if (window != null) // null in XAML designer
            {
                KeyEventHandler p = (obj, args) =>
                                   {
                                       if (args.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
                                       {
                                           txtFilter.Focus();
                                           txtFilter.SelectAll();
                                       }
                                   };

                window.KeyDown -= p;
                window.KeyDown += p;
            }
        }

        private void SetListSortOrder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                var menu = sender as MenuItem;

                _sortOrder = menu.Tag.ToString();
                SetFilter();
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await _appViewModel.RefreshManagedCertificates();
        }

        private async void Duplicate_Click(object sender, RoutedEventArgs e)
        {
            if (OnDuplicate != null)
            {
                var selectedItem = lvManagedCertificates.SelectedItem;
                if (selectedItem != null && selectedItem is Certify.Models.ManagedCertificate)
                {
                    OnDuplicate.Invoke(selectedItem as Certify.Models.ManagedCertificate);
                }
            }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (_appViewModel.IsFeatureEnabled(Models.FeatureFlags.SERVER_CONNECTIONS))
            {
                _appViewModel.ChooseConnection(this);
            }
        }

        private void GettingStarted_FilterApplied(string filter)
        {
            this.txtFilter.Text = filter;
        }
    }
}
