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
using System.Text;
using System.Web;
using GTS.Plugin.PluginContracts;

namespace GTS.Plugin.DemoPlugin
{
    [Export(typeof(IAbstractPluginModule))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [ExportMetadata("DisplayName", "Demo Plugin")]
    [ExportMetadata("Description", "This is demo plugin to test MEF ")]
    [ExportMetadata("Version", "2.5.1")]
    [ExportMetadata("GuidNumber", "0a9c0850-7c84-476f-8ff6-3437a9428f70")]
    public class DemoPlugin : IIndirectPluginModule
    {
        public string ModuleType => "PaymentAType";

        public IEnumerable<IPluginConfigField> GetConfigFields()
        {

            var customFields = new List<IPluginConfigField>
            {
                new PluginConfigFields
                {
                    Key = Keys.MerchantId,
                    Label = "Merchant ID",
                    Description = "this is merchant ID",
                    Required = true,
                    RequiredValidationMessage = "* Merchant Id is Required",
                    EncryptionEnabled = false
                },
                new PluginConfigFields
                {
                    Key = Keys.CurrencyIsoCode,
                    Label = "Currency ISO Code",
                    Description = "this is a description for Currency ISO code ",
                    Required = false,
                    RequiredValidationMessage = "Iso Code is Required",
                    EncryptionEnabled = false
                },
                new PluginConfigFields
                {
                    Key = "UserId",
                    Label = "User Id",
                    Description = "Enter User Id ",
                    Required = true,
                    RequiredValidationMessage = "* UserId is Required",
                    EncryptionEnabled = false
                },
                new PluginConfigFields
                {
                    Key = "Password",
                    Label = "Password",
                    Description = "Enter Password ",
                    Required = true,
                    RequiredValidationMessage = "* Password is Required",
                    EncryptionEnabled = true
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

                // Get the parameters to post
                var formData = BuildPostData(payment);
                
                // Replace the tokens in the html template
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "GTS.Plugin.DemoPlugin.files.DemoHTML.html";
                var postHtml = string.Empty;

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                using (var reader = new StreamReader(stream))
                {
                    postHtml = reader.ReadToEnd();
                }

                // read from app config file 
                var redirectUrl = GetRedirectUrl();

                postHtml = postHtml.Replace(Tokens.ActionURL, redirectUrl);
                postHtml = postHtml.Replace(Tokens.FormData, formData);

                // Get the text for submit link
                var submitLink = "submit my payment";

                postHtml = postHtml.Replace(Tokens.SubmitDescription, submitDescription);
                postHtml = postHtml.Replace(Tokens.SubmitLink, submitLink);

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

                case "CurrencyIsoCode":
                    switch (value)
                    {
                        case "208":
                        case "524" :
                        case "998":
                            break;

                        default:
                            errorMessage = "Valid ISO's, [208, 524, 998]";
                            result = false;
                            break;
                    }
                    break;
            }

            return result;
        }

        private string BuildPostData(IGenericPayment payment)
        {
            var formData = new StringBuilder();
            // base fields
            formData.AppendFormat("<input type=\"hidden\" name=\"{0}\" value=\"{1}\">", Keys.AcceptUrl, payment.AcceptUrl);
            formData.AppendFormat("<input type=\"hidden\" name=\"{0}\" value=\"{1}\">", Keys.CancelUrl, payment.CancelUrl);
            formData.AppendFormat("<input type=\"hidden\" name=\"{0}\" value=\"{1}\">", Keys.FopCode, payment.FopCode);
            formData.AppendFormat("<input type=\"hidden\" name=\"{0}\" value=\"{1}\">", Keys.SubmitType, payment.SubmitType);
            formData.AppendFormat("<input type=\"hidden\" name=\"{0}\" value=\"{1}\">", Keys.OrderId, payment.OrderId);
            formData.AppendFormat("<input type=\"hidden\" name=\"{0}\" value=\"{1}\">", Keys.OrderAmount, payment.OrderAmount);
            formData.AppendFormat("<input type=\"hidden\" name=\"{0}\" value=\"{1}\">", Keys.OrderString, payment.Order);

            // customFields
            var postParameters = payment.ConfigFields.ToArray();
            foreach (IPaymentConfigField t in postParameters)
            {
                formData.AppendFormat("<input type=\"hidden\" name=\"{0}{1}\" value=\"{2}\">", "configfield-", t.Key, t.Value);
                formData.AppendLine();
            }
            return formData.ToString();
        }

        private string GetRedirectUrl()
        {
            //Open the configuration file using the dll location
            var myDllConfig = ConfigurationManager.OpenExeConfiguration(this.GetType().Assembly.Location);

            // Get the appSettings section
            var myDllConfigAppSettings = (AppSettingsSection)myDllConfig.GetSection("appSettings");

            // return the desired field 
            return myDllConfigAppSettings.Settings["PaymentRedirectUrlKey"].Value;

        }
        public struct Keys
        {
            public const string FopCode = "FOPCode";
            public const string SubmitType = "SubmitType";
            public const string CurrencyIsoCode = "CurrencyIsoCode";
            public const string MerchantId = "MerchantId";
            public const string AcceptUrl = "AcceptUrl";
            public const string CancelUrl = "CancelUrl";
            public const string OrderId = "OrderId";
            public const string OrderAmount = "OrderAmount";
            public const string OrderString = "Order";
        }

        private struct Tokens
        {
            public const string ActionURL = "[ActionURL]";
            public const string FormData = "[FormData]";
            public const string SubmitDescription = "[SubmitDescription]";
            public const string SubmitLink = "[SubmitLink]";
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
