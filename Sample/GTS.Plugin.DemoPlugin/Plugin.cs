/*
 *              Copyright (C) 2016 Gateway Ticketing Systems, Inc.
 *                          ALL RIGHTS RESERVED
 * 
 * Use, copy, photocopy, reproduction, translation, reduction, disassembly
 * or distribution  in  whole  or in part,  of this software by any means
 * mechanical,  electronic,  optical,  chemical,  magnetic,  or otherwise,
 * without the express, written consent of Gateway Ticketing Systems, Inc.
 * is strictly prohibited.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using GTS.Plugin.PluginContracts;

namespace GTS.Plugin.PayUIntegration
{
    [Export(typeof(IAbstractPluginModule))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [ExportMetadata("DisplayName", "PayUIntegration")]
    [ExportMetadata("Description", "This plugin allows us to make payments for PayU")]
    [ExportMetadata("Version", "2.5.1")]
    [ExportMetadata("GuidNumber", "0a9c0850-7c84-476f-8ff6-3437a9428f70")]
    public class PayUPlugin : IIndirectPluginModule
    {
        public string ModuleType => "PayU";

        public IEnumerable<IPluginConfigField> GetConfigFields()
        {

            var customFields = new List<IPluginConfigField>
            {
                new PluginConfigFields
                {
                    Key = Keys.AccountId,
                    Label = "Account ID",
                    Description = "The PayU provided Account Id",
                    Required = true,
                    RequiredValidationMessage = "* Account Id is Required",
                    EncryptionEnabled = false
                },
                new PluginConfigFields
                {
                    Key = Keys.MerchantId,
                    Label = "Merchant ID",
                    Description = "The PayU provided Merchant Id",
                    Required = true,
                    RequiredValidationMessage = "* Merchant Id is Required",
                    EncryptionEnabled = false
                },
                new PluginConfigFields
                {
                    Key = Keys.APIKey,
                    Label = "API Key",
                    Description = "The API Key provided by PayU",
                    Required = false,
                    RequiredValidationMessage = "API Key is Required",
                    EncryptionEnabled = false
                },
                new PluginConfigFields
                {
                    Key = Keys.TaxPercentage,
                    Label = "Tax Percentage",
                    Description = "The tax percentage (use 19 for Colombia)",
                    Required = false,
                    RequiredValidationMessage = "Tax Percentage is Required",
                    EncryptionEnabled = false
                },
                new PluginConfigFields
                {
                    Key = Keys.CurrencyISOCode,
                    Label = "Currency ISO Code",
                    Description = "The currency ISO Code (Use COP for Colombia)",
                    Required = false,
                    RequiredValidationMessage = "ISO Code is Required",
                    EncryptionEnabled = false
                }
            };

            return customFields;
        }

        public void StartPayment(IGenericPayment payment, HttpResponse httpResponse)
        {
            try
            {
                var submitDescription = string.Empty;

                switch (payment.SubmitType)
                {
                    case SubmitTypes.Test:
                    case SubmitTypes.TestSSL:
                        submitDescription = "This is Test mode payment page.";
                        break;

                    case SubmitTypes.Production:
                        submitDescription = "This is a Production mode payment page.";
                        break;
                }

                // Replace the tokens in the html template
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "GTS.Plugin.PayUIntegration.files.index.html";
                var postHtml = string.Empty;

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                using (var reader = new StreamReader(stream))
                {
                    postHtml = reader.ReadToEnd();
                }

                // read from app config file 
                var submitUrl = GetSubmitUrl();
                var responseUrl = GetResponseUrl();
                var confirmationUrl = GetResponseUrl();

                BuildPostData(payment, postHtml);
                postHtml = postHtml.Replace(Tokens.SubmitURL, submitUrl);
                postHtml = postHtml.Replace(Tokens.ResponseURL, responseUrl);
                postHtml = postHtml.Replace(Tokens.ConfirmationURL, confirmationUrl);

                httpResponse.Write(postHtml);
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
            }
        }

        public bool ValidatePayment(IGenericPayment payment, HttpRequest httpRequest, out string errorMessage)
        {
            errorMessage = string.Empty;

            return true;
        }

        public bool ValidateConfigField(string key, string value, out string errorMessage)
        {
            var result = true;
            errorMessage = string.Empty;

            switch (key)
            {
                case "MerchantId":
                    int id;
                    int.TryParse(value, out id);

                    if (id == 0)
                    {
                        errorMessage = "Merchant Id must be numeric and not longer than 10 digits...";
                        result = false;
                    }
                    break;
            }

            return result;
        }

        private void BuildPostData(IGenericPayment payment, ref string postHtml)
        {
            var merchantIdField = payment.ConfigFields
                .FirstOrDefault(field => field.Key == Keys.MerchantId);
            postHtml = postHtml.Replace(Tokens.MerchantId, merchantIdField.Value);

            var accountIdField = payment.ConfigFields
                .FirstOrDefault(field => field.Key == Keys.AccountId);
            postHtml = postHtml.Replace(Tokens.AccountId, merchantIdField.Value);

            var orderDescription = payment.Order;
            postHtml =
                postHtml.Replace(Tokens.OrderDescription, orderDescription);

            var orderId = payment.OrderId;
            postHtml =
                postHtml.Replace(Tokens.OrderId, orderId);

            var taxPercentage = Decimal.Parse(payment.ConfigFields
                .FirstOrDefault(field => field.Key == Keys.TaxPercentage).Value);
            var orderAmountTotal = payment.OrderAmount;
            var orderAmountBase = orderAmountTotal / 1 + (taxPercentage / 100);
            var orderAmountTax = orderAmountBase * (taxPercentage / 100);

            using (MD5 md5Hash = MD5.Create())
            {
                var apiKey = payment.ConfigFields
                    .FirstOrDefault(field => field.Key == Keys.APIKey);
                var currencyISOCode = payment.ConfigFields
                    .FirstOrDefault(field => field.Key == Keys.CurrencyISOCode);

                var signature = String.Format("{0}~{1}~{2}~{3}~{4}",
                    apiKey, orderId, orderDescription, orderAmountTotal, currencyISOCode);
                postHtml =
                    postHtml.Replace(Tokens.Signature, GetMd5Hash(md5Hash, signature));
            }

            postHtml =
                postHtml.Replace(Tokens.OrderAmountTotal, orderAmountTotal.ToString());
            postHtml =
                postHtml.Replace(Tokens.OrderAmountBase, orderAmountBase.ToString());
            postHtml =
                postHtml.Replace(Tokens.OrderAmountTax, orderAmountTax.ToString());

        }

        private static string GetMd5Hash(MD5 md5Hash, string input)
        {

            // Convert the input string to a byte array and compute the hash.
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }


        private AppSettingsSection GetAppSettings()
        {
            //Open the configuration file using the dll location
            var myDllConfig = ConfigurationManager.OpenExeConfiguration(this.GetType().Assembly.Location);

            // Get the appSettings section
            return (AppSettingsSection)myDllConfig.GetSection("appSettings");
        }

        private string GetSubmitUrl()
        {
            return GetAppSettings().Settings["PaymentSubmitUrlKey"].Value;
        }

        private string GetResponseUrl()
        {
            return GetAppSettings().Settings["PaymentResponseUrlKey"].Value;

        }

        public struct Keys
        {
            public const string AccountId = "AccountId";
            public const string MerchantId = "MerchantId";
            public const string APIKey = "APIKey";

            public const string TaxPercentage = "TaxPercentage";
            public const string CurrencyISOCode = "CurrencyISOCode";
        }

        private struct Tokens
        {
            public const string MerchantId = "[MerchantId]";
            public const string AccountId = "[AccountId]";

            public const string OrderDescription = "[OrderDescription]";
            public const string OrderId = "[OrderId]";

            public const string OrderAmountTotal = "[OrderAmountTotal]";
            public const string OrderAmountTax = "[OrderAmountTax]";
            public const string OrderAmountBase = "[OrderAmountBase]";

            public const string Signature = "[Signature]";

            public const string SubmitURL = "[SubmitURL]";
            public const string ResponseURL = "[ResponseURL]";
            public const string ConfirmationURL = "[ConfirmationURL]";
        }

    }

    public class PluginConfigFields : IPluginConfigField
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
        public string RequiredValidationMessage { get; set; }
        public bool EncryptionEnabled { get; set; }
    }
   
}
