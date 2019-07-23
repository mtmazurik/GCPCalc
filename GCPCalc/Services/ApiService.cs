using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;

namespace GCPCalc.Services
{
    public class ApiService : IApiService
    {
        private readonly IHttpClientFactory _httpFactory;
        private string _csvResponse;
        private Stack<KeyValuePair<string, string>> _stack;
        public ApiService(IHttpClientFactory factory)   //ctor
        {
            _httpFactory = factory;
        }

        // Get Billing Compute Data - straight REST API (HTTP) using a "Service Account" credential/JSONfile obtained Bearer Access Token
        public async Task<string> GetBillingComputeData()
        {
            const string COMPUTE_SERVICE_ID = "6F81-5844-456A";                     
            const string JSON_SERVICE_KEY_FILE = "./airy-gate-240816-b9f9d29d332b.json";
            const string BEARER_TYPE = "Bearer";
            const string BASE_URL = "https://cloudbilling.googleapis.com/";
            const string GOOGLE_AUTH_URL = "https://www.googleapis.com/auth/cloud-platform";

            var accessToken = GetAccessToken(JSON_SERVICE_KEY_FILE, GOOGLE_AUTH_URL); // OAuth2 token

            var client = _httpFactory.CreateClient();
            client.BaseAddress = new Uri(BASE_URL);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(BEARER_TYPE, accessToken);

            string singlePageResponse = string.Empty;                           //local-loop vars
            string nextPageToken = string.Empty;
            string partialUri = $"v1/services/{COMPUTE_SERVICE_ID}/skus?fields=nextPageToken,skus";
            _csvResponse = string.Empty;
            _stack = new Stack<KeyValuePair<string,string>>();
            do
            {                                                                   // page size default: 5000,  COMPUTE_SERVICE_ID appears to only have 2 pages (today)
                if (nextPageToken != string.Empty)
                {
                    partialUri = $"v1/services/{COMPUTE_SERVICE_ID}/skus?pageToken={nextPageToken}&fields=nextPageToken,skus";
                }

                var response = await client.GetAsync(partialUri);               // return all SKUs info, for Compute Service permutations

                singlePageResponse = await response.Content.ReadAsStringAsync();
                nextPageToken = GetNextPageToken(singlePageResponse);
                ProcessJSONPageIntoCSVFormat(singlePageResponse);
            } while ( nextPageToken != string.Empty);                           // iterate, using nextPageToken each time
            return _csvResponse;
        }
        // ParseJsonPricingInfo - pick out the pricingInfo from correct category withing the source JSON
        //                        category: { "resourceFamily": "Compute"  } initial criterion
        public void ProcessJSONPageIntoCSVFormat(string incoming)
        {
            string outgoing = string.Empty;
            using (var reader = new JsonTextReader(new StringReader(incoming)))
            {
                while(reader.Read())
                {
                    if (reader.Value != null)
                    {
                        EvaluationBlockOfBillingData(reader);
                    }
                }
                string a = "stop here";
            }
        }
        // Evaluate block of billing data:   parses through JSON blocks of data, token by token 
        //                                   when it reaches 'resourceFamily':'compute' and 'resourceGroup':'RAM' or 'CPU'
        //                                   if burrows down in and plucks out the billing data from the nano field
        void EvaluationBlockOfBillingData(JsonTextReader reader)    
        {
            string key = reader.Value.ToString();
            switch( key )
            {
                case "skuId":
                    PushNextToken(key, reader);
                    break;
                case "description":
                    PushNextToken(key, reader);
                    break;
                case "resourceFamily":
                    reader.Read();
                    if (reader.Value.ToString().CompareTo("Compute")==0)
                    {
                        reader.Read(); // resourceGroup
                        reader.Read();
                        if (0 == reader.Value.ToString().CompareTo("RAM") ||
                            0 == reader.Value.ToString().CompareTo("CPU"))
                        {
                            PushCurrentToken("resourceGroup", reader.Value.ToString());
                            reader.Read(); // usageType
                            PushNextToken("usageType", reader);
                            reader.Read(); // EndObject
                            reader.Read(); // serviceRegions
                            reader.Read(); // StartArray
                            reader.Read();
                            PushCurrentToken("serviceRegion", reader.Value.ToString());
                            DrillIntoPricingInfo(reader);
                            PopStackAndConvertToCSV();
                            FinishReadingToEndOfBlock(reader);
                        }
                        else
                        {
                            FinishReadingToEndOfBlock(reader);
                            _stack.Clear();
                        }
                    }
                    else
                    {
                        FinishReadingToEndOfBlock(reader);
                        _stack.Clear();
                    }
                    break;
                default:
                    break;
            }
        }

        private void FinishReadingToEndOfBlock(JsonTextReader reader)
        {
            string token = string.Empty;
            do // read until find "serviceProviderName" (end of section)
            {
                reader.Read();
                if (reader.Value != null)
                {
                    token = reader.Value.ToString();
                }
                else
                {
                    continue;
                }
            } while (0 != token.CompareTo("serviceProviderName"));
            reader.Read(); // and read one more "Google" or "?"
            reader.Read(); // closing curly
        }

        void DrillIntoPricingInfo(JsonTextReader reader)
        {

            string token = string.Empty;
            do
            {
                reader.Read();
                if (reader.Value != null)
                {
                    token = reader.Value.ToString();
                }
                else
                {
                    continue;
                }
            } while (0 != token.CompareTo("nanos"));
            reader.Read();
            //Debug.Write("++++++++++++++++++++" + Environment.NewLine);
            //Debug.WriteLine("TokenType: " + reader.TokenType);
            //Debug.WriteLine("Value: " + reader.Value);
            //Debug.WriteLine("ValueType: " + reader.ValueType);
            float nanos = Convert.ToSingle(reader.Value.ToString());
            float rateValue = nanos / 1000000000.0F;
            string rateString = rateValue.ToString("0.000000");  // 6 decimal places
            PushCurrentToken("rate", rateString);
        }
        void PopStackAndConvertToCSV()
        {
            string csvGrouping = string.Empty;
            KeyValuePair<string, string> item6 = _stack.Pop();  // reverse order
            KeyValuePair<string, string> item5 = _stack.Pop();
            KeyValuePair<string, string> item4 = _stack.Pop();
            KeyValuePair<string, string> item3 = _stack.Pop();
            KeyValuePair<string, string> item2 = _stack.Pop();
            KeyValuePair<string, string> item1 = _stack.Pop();

            csvGrouping = item1.Value + ","
                        + item2.Value + ","
                        + item3.Value + ","
                        + item4.Value + ","
                        + item5.Value + ","
                        + item6.Value + Environment.NewLine;
            _csvResponse += csvGrouping;

            

        }
        void PushNextToken(string name, JsonTextReader reader)
        {
            reader.Read(); // read value
            _stack.Push(new KeyValuePair<string, string>(name, reader.Value.ToString()));
        }
        void PushCurrentToken(string name, string value)
        {
             _stack.Push(new KeyValuePair<string, string>(name, value));
        }
        // GetNextPageToken - parse string looking for the nextPageToken value and return it, or empty string
        public string GetNextPageToken(string stringResponse)
        {
            JObject o = JObject.Parse(stringResponse);
            return (string)o.SelectToken("nextPageToken");
        }

        // Get Access Token - leverage Google Client class (GoogleCredential) to get Access token from a Service Account (Service to Service) credential
        public string GetAccessToken(string jsonKeyFilePath, params string[] scopes)
        {
            using (var stream = new FileStream(jsonKeyFilePath, FileMode.Open, FileAccess.Read))
            {
                return GoogleCredential
                    .FromStream(stream)     // loads JSON key file  
                    .CreateScoped(scopes)   // gathers scopes requested  
                    .UnderlyingCredential   // gets the credentials  
                    .GetAccessTokenForRequestAsync().Result; // gets the Access Token (synchronously)
            }
        }
    }
}
