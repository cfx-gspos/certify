﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Certify.Client;
using Certify.Management;
using Certify.Models;
using Microsoft.ApplicationInsights;
using Newtonsoft.Json;

namespace Certify.CLI
{
    public partial class CertifyCLI
    {
        private TelemetryClient _tc = null;
        private ICertifyClient _certifyClient = null;
        private Preferences _prefs = new Preferences();
        private PluginManager _pluginManager { get; set; }

        public CertifyCLI()
        {
            _certifyClient = new CertifyServiceClient(new SharedUtils.ServiceConfigManager());
        }

        public async Task<bool> IsServiceAvailable()
        {
            var isAvailable = false;

            try
            {
                await _certifyClient.GetAppVersion();
                isAvailable = true;
            }
            catch (Exception exp)
            {
                System.Console.WriteLine(exp.ToString());
                isAvailable = false;
            }
            return isAvailable;
        }

        private void InitPlugins()
        {
            if (_pluginManager == null)
            {
                _pluginManager = new Management.PluginManager();

                _pluginManager.LoadPlugins(new List<string> { "Licensing" });
            }
        }

        private bool IsRegistered()
        {
            var licensingManager = _pluginManager.LicensingManager;
            if (licensingManager != null)
            {
                if (licensingManager.IsInstallRegistered(1, Certify.Management.Util.GetAppDataFolder()))
                {
                    return true;
                }
            }
            return false;
        }

        public async Task LoadPreferences() => _prefs = await _certifyClient.GetPreferences();

        private bool IsTelematicsEnabled() => _prefs.EnableAppTelematics;

        private string GetInstrumentationKey() => Certify.Locales.ConfigResources.AIInstrumentationKey;

        private async Task<string> GetAppVersion()
        {
            try
            {
                return await _certifyClient.GetAppVersion();
            }
            catch (Exception)
            {
                return await Task.FromResult("--- (Service Not Started)");
            }
        }

        private string GetAppWebsiteURL() => Certify.Locales.ConfigResources.AppWebsiteURL;

        private void InitTelematics()
        {
            if (IsTelematicsEnabled())
            {
                _tc = new TelemetryClient();
                _tc.Context.InstrumentationKey = GetInstrumentationKey();
                _tc.InstrumentationKey = GetInstrumentationKey();

                // Set session data:

                _tc.Context.Session.Id = Guid.NewGuid().ToString();
                _tc.Context.Component.Version = GetAppVersion().Result;
                _tc.Context.Device.OperatingSystem = Environment.OSVersion.ToString();
                _tc.TrackEvent("StartCLI");
            }
        }

        internal void ShowVersion()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            System.Console.WriteLine("Certify SSL Manager - CLI Certify.Core v" + GetAppVersion().Result.Replace("\"", ""));
            Console.ForegroundColor = ConsoleColor.White;
            System.Console.WriteLine("For more information see " + GetAppWebsiteURL());
            System.Console.WriteLine("");
        }

        internal void ShowACMEInfo()
        {
            /*
                        var certifyManager = new CertifyManager();
                        string vaultInfo = certifyManager.GetVaultSummary();
                        string acmeInfo = certifyManager.GetAcmeSummary();

                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        System.Console.WriteLine("ACME API: " + acmeInfo);
                        System.Console.WriteLine("ACMESharp Vault: " + vaultInfo);
            */
            System.Console.WriteLine("");
            Console.ForegroundColor = ConsoleColor.White;
        }

        internal void ShowHelp()
        {
            Console.ForegroundColor = ConsoleColor.White;
            System.Console.WriteLine("Usage: certify <command> \n");
            System.Console.WriteLine("certify renew : renew certificates for all auto renewed managed sites");
            System.Console.WriteLine("certify deploy \"<ManagedCertName>\" \"<TaskName>\" : run a specific deployment task for the given managed certificate");
            System.Console.WriteLine("certify list : list managed certificates and current running/not running status in IIS");
            System.Console.WriteLine("certify diag : check existing ssl bindings and managed certificate integrity");
            System.Console.WriteLine("certify importcsv : import managed certificates from a CSV file.");
            System.Console.WriteLine("certify add <managed cert id or new> <domain1;domain2> : add domains to a managed cert using the default validation, use --perform-request to immediately attempt cert request");
            System.Console.WriteLine("certify remove <managed cert id> <domain1;domain2> : remove domains from managed cert, use --perform-request to immediately attempt cert request");
            System.Console.WriteLine("\n\n");
            System.Console.WriteLine("For help, see the docs at https://docs.certifytheweb.com");

        }

        internal async Task PerformAutoRenew(string[] args)
        {
            var forceRenewal = false;

            var renewalMode = Models.RenewalMode.Auto;


            if (args.Contains("--force-renew-all"))
            {
                renewalMode = RenewalMode.All;
                forceRenewal = true;
            }

            if (args.Contains("--renew-witherrors"))
            {
                // renew errored items
                renewalMode = RenewalMode.RenewalsWithErrors;
            }

            if (args.Contains("--renew-newitems"))
            {
                // renew only new items
                renewalMode = RenewalMode.NewItems;
            }

            if (args.Contains("--renew-all-due"))
            {
                // renew only new items
                renewalMode = RenewalMode.RenewalsDue;
            }

            List<string> targetItemIds = new List<string> { };

            if (args.Any(a => a.StartsWith("id=")))
            {
                var idArg = args.FirstOrDefault(a => a.StartsWith("id="));
                if (idArg != null)
                {
                    var ids = idArg.Replace("id=", "").Split(',');
                    foreach (var id in ids)
                    {
                        targetItemIds.Add(id.Trim());
                    }
                }
            }

            var isPreviewMode = false;
            if (args.Contains("--preview"))
            {
                // don't perform real requests
                isPreviewMode = true;
            }

            if (_tc == null)
            {
                InitTelematics();
            }

            if (_tc != null)
            {
                _tc.TrackEvent("CLI_BeginAutoRenew");
            }

            Console.ForegroundColor = ConsoleColor.White;
            System.Console.WriteLine("\nPerforming Auto Renewals..\n");
            if (forceRenewal)
            {
                System.Console.WriteLine("\nForcing auto renew (--force-renewal-all specified). \n");
            }

            //go through list of items configured for auto renew, perform renewal and report the result
            var results = await _certifyClient.BeginAutoRenewal(new RenewalSettings { Mode = renewalMode, IsPreviewMode = isPreviewMode, TargetManagedCertificates = targetItemIds.Any() ? targetItemIds : null });

            Console.ForegroundColor = ConsoleColor.White;

            foreach (var r in results)
            {
                if (r.ManagedItem != null)
                {
                    System.Console.WriteLine("--------------------------------------");
                    if (r.IsSuccess)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        System.Console.WriteLine(r.ManagedItem.Name);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        System.Console.WriteLine(r.ManagedItem.Name);

                        if (r.Message != null)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            System.Console.WriteLine(r.Message);
                        }
                    }
                }
            }
            Console.ForegroundColor = ConsoleColor.White;

            System.Console.WriteLine("Completed:" + results.Where(r => r.IsSuccess == true).Count());
            if (results.Any(r => r.IsSuccess == false))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                System.Console.WriteLine("Failed:" + results.Where(r => r.IsSuccess == false).Count());
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        internal void ListManagedCertificates(string[] args)
        {
            var managedCertificates = _certifyClient.GetManagedCertificates(new ManagedCertificateFilter()).Result;

            // check for path argument and if present output json file
            var jsonArgIndex = Array.IndexOf(args, "--json");

            if (jsonArgIndex != -1)
            {
                // if we have a file argument, go ahead an export the list
                if (args.Length > (jsonArgIndex + 1))
                {
                    var pathArg = args[jsonArgIndex + 1];

                    try
                    {
                        var jsonOutput = JsonConvert.SerializeObject(managedCertificates, Formatting.Indented);

                        System.IO.File.WriteAllText(pathArg, jsonOutput);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"Failed to write output to file. Check folder exists and permissions allow write. " + pathArg);
                    }
                }
                else
                {
                    Console.WriteLine($"Output file path argument is required for json output.");
                }

            }
            else
            {
                // output list to console
                foreach (var site in managedCertificates)
                {
                    Console.ForegroundColor = ConsoleColor.White;

                    Console.WriteLine($"{site.Name},{site.DateExpiry},{site.Id},{site.Health.ToString()}");
                }
            }
        }
    }
}
