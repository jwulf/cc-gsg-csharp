using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NLog;
using Zeebe.Client;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;
using Zeebe.Client.Impl.Builder;
using Zeebe.Client.Impl.Responses;

namespace Cloudstarter.Services
{

    public interface IZeebeService
    {
        public void CreateGetTimeWorker();
        public Task<IDeployResponse> Deploy(string modelFile);
        public Task<ITopology> Status();
        public Task<IWorkflowInstanceResponse> StartWorkflowInstance(string bpmProcessId);
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

        public Task<IDeployResponse> Deploy(string modelFile)
        {
            var filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory!, "Resources", modelFile);
            return Client.NewDeployCommand().AddResourceFile(filename).Send();
        }
        public Task<ITopology> Status()
        {
            return Client.TopologyRequest().Send();
        }

        public Task<IWorkflowInstanceResponse> StartWorkflowInstance(string bpmProcessId)
        {
            return Client.NewCreateWorkflowInstanceCommand().BpmnProcessId(bpmProcessId).LatestVersion().Send();
        }

        public void CreateGetTimeWorker()
        {
            _createWorker("get-time", (client, job) =>
            {
                Console.Out.WriteLine(job.ToString());
            });    
        }
        private void _createWorker(String jobType, JobHandler handleJob)
        {
            Client.NewWorker()
                    .JobType(jobType)
                    .Handler(handleJob)
                    .MaxJobsActive(5)
                    .Name(jobType)
                    .AutoCompletion()
                    .PollInterval(TimeSpan.FromSeconds(50))
                    .Timeout(TimeSpan.FromSeconds(10))
                    .Open();
        }
        public IConfiguration Configuration { get; set; }
    }
}