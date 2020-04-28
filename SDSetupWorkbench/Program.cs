using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.Extensions.DependencyInjection;
using SDSetupCommon.Communications;
using BlazorStrap;
using SDSetupCommon.Data;

namespace SDSetupManager {
    public class Program {
        public static async Task Main(string[] args) {

            EndpointSettings.serverInformation = new ServerInformation();

            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("app");

            WebAssemblyHttpMessageHandlerOptions.DefaultCredentials = FetchCredentialsOption.Include;

            builder.Services.AddBaseAddressHttpClient();
            builder.Services.AddBootstrapCss();

            await builder.Build().RunAsync();
        }
    }
}
