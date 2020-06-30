using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NLog;
using Zeebe.Client;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Impl.Builder;
using Zeebe.Client.Impl.Responses;

namespace Cloudstarter.Services
{

    public interface IZeebeService
    {
        public Task<IDeployResponse> Deploy();
        public Task<ITopology> Status();
    }
    public class ZeebeService: IZeebeService
    {
        public readonly IZeebeClient Client;
        public readonly Logger Logger;

        public ZeebeService(IConfiguration config)
        {
            Configuration = config;
            var authServer = Configuration["ZEEBE_AUTHORIZATION_SERVER_URL"];
            var clientId = Configuration["ZEEBE_CLIENT_ID"];
            var clientSecret = Configuration["ZEEBE_CLIENT_SECRET"];
            var zeebeUrl = Configuration["ZEEBE_ADDRESS"];
            char[] port =
            {
                '4', '3', ':'
            };
            var audience = zeebeUrl?.TrimEnd(port);

            Logger = LogManager.GetCurrentClassLogger();
            Client =
                ZeebeClient.Builder()
                    .UseGatewayAddress(zeebeUrl)
                    .UseTransportEncryption()
                    .UseAccessTokenSupplier(
                        CamundaCloudTokenProvider.Builder()
                            .UseAuthServer(authServer)
                            .UseClientId(clientId)
                            .UseClientSecret(clientSecret)
                            .UseAudience(audience)
                            .Build())
                    .Build(); 
        }

        public Task<IDeployResponse> Deploy()
        {
            var filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory!, "Resources", "test-process.bpmn");
            return Client.NewDeployCommand().AddResourceFile(filename).Send();
        }
        public Task<ITopology> Status()
        {
            return Client.TopologyRequest().Send();
        }
        public IConfiguration Configuration { get; set; }
    }
}