﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Certify.Models;

namespace Certify.UI.Windows
{
    /// <summary>
    /// Edit details for an ACME Account
    /// </summary>
    public partial class EditAccountDialog
    {
        public ContactRegistration Item { get; set; }

        public Certify.UI.ViewModel.AppViewModel MainViewModel => ViewModel.AppViewModel.Current;

        public IEnumerable<CertificateAuthority> CertificateAuthorities => MainViewModel.CertificateAuthorities;

        public EditAccountDialog()
        {
            InitializeComponent();

            Item = new ContactRegistration();

            DataContext = this;

            this.Width *= MainViewModel.UIScaleFactor;
            this.Height *= MainViewModel.UIScaleFactor;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Arrow;
            Close();
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            //add/update contact

            var ca = MainViewModel.CertificateAuthorities.FirstOrDefault(c => c.Id == Item?.CertificateAuthorityId);

            if (ca == null)
            {
                MessageBox.Show("Certificate authority not selected - cannot proceed. Ensure the app has loaded correctly and the Certify background service is running.");
                return;
            }

            // if ca requires email address, check that first
            if (ca.RequiresEmailAddress)
            {
                var isValidEmail = true;
                if (string.IsNullOrEmpty(Item.EmailAddress))
                {
                    isValidEmail = false;
                }
                else
                {
                    if (!Regex.IsMatch(Item.EmailAddress,
                                @"^(?("")("".+?(?<!\\)""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" +
                                @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-\w]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$",
                                RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250)))
                    {
                        isValidEmail = false;
                    }
                }

                if (!isValidEmail)
                {
                    MessageBox.Show(Certify.Locales.SR.New_Contact_EmailError);

                    return;
                }
            }

            if (ca.RequiresExternalAccountBinding)
            {
                if (string.IsNullOrEmpty(Item.EabKeyId) || string.IsNullOrEmpty(Item.EabKey))
                {
                    MessageBox.Show(ca.EabInstructions ?? "An external account binding Key Id and (HMAC) Key are required and will be provided by your Certificate Authority. You can enter these on the Advanced tab.");
                    return;
                }
            }

            if (Item.IsStaging && string.IsNullOrEmpty(ca.StagingAPIEndpoint))
            {
                MessageBox.Show("This certificate authority does not have a staging (test) API so can't be used for Staging certificate requests.");
                return;
            }

            if (Item.AgreedToTermsAndConditions)
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var result = await MainViewModel.AddContactRegistration(Item);

                Mouse.OverrideCursor = Cursors.Arrow;

                if (result.IsSuccess)
                {
                    await MainViewModel.RefreshAccountsList();

                    Close();
                }
                else
                {
                    MessageBox.Show(result.Message);
                }
            }
            else
            {
                MessageBox.Show(Certify.Locales.SR.New_Contact_NeedAgree);
            }
        }

        private void CertificateAuthorityList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}
