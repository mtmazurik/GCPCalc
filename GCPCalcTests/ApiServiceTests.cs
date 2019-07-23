using Microsoft.VisualStudio.TestTools.UnitTesting;
using GCPCalc.Services;
using System.Net.Http;
//using Google.Apis.Http;
using Microsoft.Extensions.DependencyInjection;                                  

namespace GCPCalcTests
{
    [TestClass]
    public class ApiServiceTests
    {
        IApiService _api;

        [TestInitialize]
        public void Initialize()
        {
            var services = new ServiceCollection();                             // .NET Core autofac DI setup
            services.AddHttpClient();                                           // for IHttpClientFactory injection
            services.AddTransient<IApiService, ApiService>();
            var serviceProvider = services.BuildServiceProvider();              // similar to Startup.cs in your service project

            _api = serviceProvider.GetService<IApiService>();                   // injection (simulated) via GetService()
        }

        [TestMethod]
        public void GetBillingComputeDataTest()
        {
            string billingData = _api.GetBillingComputeData().Result;
        }
        [TestMethod]
        public void GetAccessTokenTest()
        {
            string token = _api.GetAccessToken("./airy-gate-240816-b9f9d29d332b.json", "https://www.googleapis.com/auth/cloud-platform");   // Cloud Billing API, v1 scope
            Assert.IsNotNull(token);
        }

    }
}
