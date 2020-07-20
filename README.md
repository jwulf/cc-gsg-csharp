# Getting Started with Camunda Cloud using C# and ASP .NET Core 3

The [Zeebe C# Client](https://github.com/zeebe-io/zeebe-client-csharp) is available for .NET Zeebe applications. 

Watch a [video tutorial on YouTube](https://youtu.be/AOj64vzEZ_8) walking through this Getting Started Guide.

[![](assets/getting-started-node-thumbnail.jpg)](https://youtu.be/AOj64vzEZ_8)

## Prerequisites

* [Zeebe Modeler](https://github.com/zeebe-io/zeebe-modeler/releases)

## Scaffolding the project

* Create a new .NET Core Web API application:

```bash
dotnet new webapi -o Cloudstarter
cd Cloudstarter
```

[Video link](https://youtu.be/AOj64vzEZ_8?t=30)

* Add the [Zeebe C# Client](https://www.nuget.org/packages/zb-client/) from Nuget:

```bash
dotnet add package zb-client --version 0.16.1
```

## Configure NLog for logging 

* Install NLog packages (we'll use NLog):

```bash
dotnet add package NLog 
dotnet add package NLog.Schema 
dotnet add package NLog.Web.AspNetCore 
```

* Create a file `NLog.config`, with the following content:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd" 
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      autoReload="true"
>
    <extensions>
        <add assembly="NLog.Web.AspNetCore"/>
    </extensions>
    
    <targets>
        <target name="logconsole" xsi:type="Console" 
                layout="${longdate} | ${level:uppercase=true} | ${logger} | ${message} ${exception:format=tostring}"/>
     </targets>
    
    <rules>
       <logger name="*" minlevel="Trace" writeTo="logconsole" />
     </rules>
</nlog>
```

* Edit the file `Program.cs` to configure NLog:

```c#
public class Program
{
    public static async Task Main(string[] args)
    {
        var logger = NLogBuilder.ConfigureNLog("NLog.config").GetCurrentClassLogger();
        try
        {
            logger.Debug("init main");
            await CreateHostBuilder(args).Build().RunAsync();
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Stopped program because of exception");
            throw;
        }
        finally
        {
            NLog.LogManager.Shutdown();
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Trace);
            })
            .UseNLog();  
}
```

[Video link](https://youtu.be/AOj64vzEZ_8?t=95)

## Create Camunda Cloud cluster

[Video link](https://youtu.be/AOj64vzEZ_8?t=198)

* Log in to [https://camunda.io](https://camunda.io).
* Create a new Zeebe 0.23.3 cluster.
* When the new cluster appears in the console, create a new set of client credentials. 
* Copy the client Connection Info environment variables block.

## Configure connection

[Video link](https://youtu.be/AOj64vzEZ_8?t=454)

* Add the `dotenv.net` package to the project:

```bash
dotnet add package dotenv.net.DependencyInjection.Microsoft
```

* Edit `Startup.cs` and add the service in the `ConfigureServices` method: 

```c#
public void ConfigureServices(IServiceCollection services)
{
    // ...
    services.AddEnv(builder => {
        builder
            .AddEnvFile("CamundaCloud.env")
            .AddThrowOnError(false)
            .AddEncoding(Encoding.ASCII);
    });
    services.AddEnvReader();
}
```

* Create a file in the root of the project `CamundaCloud.env`, and paste the client connection details into it, removing the `export` from each line:

```bash
ZEEBE_ADDRESS=656a9fc4-c874-49a3-b67b-20c31ae12fa0.zeebe.camunda.io:443
ZEEBE_CLIENT_ID=~2WQlDeV1yFdtePBRQgsrNXaKMs4IwAw
ZEEBE_CLIENT_SECRET=3wFRuCJb4YPcKL4W9Fn7kXlsepSNNJI5h7Mlkqxk2E.coMEtYdA5E58lnkCmoN_0
ZEEBE_AUTHORIZATION_SERVER_URL=https://login.cloud.camunda.io/oauth/token
```

**Note:** if you change cluster configuration at a later date, you may need to delete the file `~/zeebe/cloud.token`. See [this bug report](https://github.com/zeebe-io/zeebe-client-csharp/issues/146).

* Add an `ItemGroup` in `CloudStarter.csproj` to copy the `.env` file into the build:

```xml
<ItemGroup>
    <None Update="CamundaCloud.env" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

* Create a file in `Services/ZeebeService.cs`, with the following content:

```c#
namespace Cloudstarter.Services
{
    public interface IZeebeService
    {
        public Task<ITopology> Status();
    }
    public class ZeebeService: IZeebeService
    {
        private readonly IZeebeClient _client;
        private readonly ILogger<ZeebeService> _logger;

        public ZeebeService(IEnvReader envReader, ILogger<ZeebeService> logger)
        {
            _logger = logger;
            var authServer = envReader.GetStringValue("ZEEBE_AUTHORIZATION_SERVER_URL"); 
            var clientId = envReader.GetStringValue("ZEEBE_CLIENT_ID");
            var clientSecret = envReader.GetStringValue("ZEEBE_CLIENT_SECRET");
            var zeebeUrl = envReader.GetStringValue("ZEEBE_ADDRESS");
            char[] port =
            {
                '4', '3', ':'
            };
            var audience = zeebeUrl?.TrimEnd(port);

            _client =
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

        public Task<ITopology> Status()
        {
            return _client.TopologyRequest().Send();
        }
    }
}
```

* Save the file.

## Test Connection with Camunda Cloud

[Video link](https://youtu.be/AOj64vzEZ_8?t=155)

We will create a controller route at `/status` that retrieves the status and topology of the cluster.

* Create a file `Controllers/ZeebeController.cs`, with the following content:

```c#
namespace Cloudstarter.Controllers
{
    public class ZeebeController : Controller
    {
        private readonly IZeebeService _zeebeService;

        public ZeebeController(IZeebeService zeebeService)
        {
            _zeebeService = zeebeService;
        }

        [Route("/status")]
        [HttpGet]
        public async Task<string> Get()
        {
            return (await _zeebeService.Status()).ToString();
        }
    }
}
```

* Edit the file `Startup.cs`, and inject the `ZeebeService` class into the service container in the `ConfigureServices` method, like this: 

```c#
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IZeebeService, ZeebeService>();
    services.AddControllers();
}
```

* Run the application with the command `dotnet run` (remember to set the client connection variables in the environment first).

Note: you can use `dotnet watch run` to automatically restart your application when you change your code.

* Open [http://localhost:5000/status](http://localhost:5000/status) in your web browser.

You will see the topology response from the cluster.

## Create a BPMN model

[Video link](https://youtu.be/AOj64vzEZ_8?t=753)

* Download and install the [Zeebe Modeler](https://github.com/zeebe-io/zeebe-modeler/releases).
* Open Zeebe Modeler and create a new BPMN Diagram.
* Create a new BPMN diagram.
* Add a StartEvent, an EndEvent, and a Task.
* Click on the Task, click on the little spanner/wrench icon, and select "Service Task".
* Set the _Name_ of the Service Task to `Get Time`, and the _Type_ to `get-time`.

It should look like this:

![](img/first-model.png)

* Click on the blank canvas of the diagram, and set the _Id_ to `test-process`, and the _Name_ to "Test Process".
* Save the diagram to `Resources/test-process.bpmn` in your project.

## Deploy the BPMN model to Camunda Cloud

[Video Link](https://youtu.be/AOj64vzEZ_8?t=908)

We need to copy the bpmn file into the build, so that it is available to our program at runtime.

* Edit the `Cloudstarter.csproj` file, and add the following to the `ItemGroup`:

```xml
<ItemGroup>
    <None Update="Resources\**" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Now we create a method in our service to deploy a bpmn model to the cluster.

* Edit `ZeebeService.cs`, and add a `Deploy` method:

```c#
public Task<IDeployResponse> Deploy(string modelFile)
{
    var filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory!, "Resources", modelFile);
    var deployment = await _client.NewDeployCommand().AddResourceFile(filename).Send();
    var res = deployment.Workflows[0];
    _logger.LogInformation("Deployed BPMN Model: " + res?.BpmnProcessId +
                " v." + res?.Version);
    return deployment;
}
```

* In the `ZeebeService.cs` file, update the interface definition:

```c#
public interface IZeebeService
{
    public Task<IDeployResponse> Deploy(string modelFile);
    public Task<ITopology> Status();
}
```

Now, we call the `Deploy` method during the initialization of the service at startup. We need to do it here, because the service is not instantiated 

* Edit `Startup.cs`, and add the following lines to the `Configure` method:

```c#
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    var zeebeService = app.ApplicationServices.GetService<IZeebeService>();
    zeebeService.Deploy("test-process.bpmn"); 
    // ...
}
```
## Start a Workflow Instance

[Video Link](https://youtu.be/AOj64vzEZ_8?t=1037)

We will create a controller route at `/start` that will start a new instance of the workflow.

* Add fastJSON to the project:

```bash
dotnet add fastJSON
```

* Edit `Services/ZeebeService.cs` and add a `StartWorkflowInstance` method:

```c#
public async Task<String> StartWorkflowInstance(string bpmProcessId)
{
    var instance = await _client.NewCreateWorkflowInstanceCommand()
            .BpmnProcessId(bpmProcessId)
            .LatestVersion()
            .Send();
    var jsonParams = new JSONParameters {ShowReadOnlyProperties = true};
    return JSON.ToJSON(instance, jsonParams);
}
```

* Update the service interface definition:

```c#
public interface IZeebeService
{
    public Task<IDeployResponse> Deploy(string modelFile);
    public Task<ITopology> Status();
    public Task<String> StartWorkflowInstance(string bpmProcessId);
}
```

* Edit `Controllers/ZeebeController.cs`, and add a REST method to start an instance
of the workflow:

```c#
// ...
public class ZeebeController : Controller
    // ...

    [Route("/start")]
    [HttpGet]
    public async Task<string> StartWorkflowInstance()
    {
        var instance = await _zeebeService.StartWorkflowInstance("test-process");
        return instance;
    }
}
```

* Run the program with the command: `dotnet run`.

* Visit [http://localhost:5000/start](http://localhost:5000/start) in your browser.

You will see output similar to the following: 

```
{"$types":{"Zeebe.Client.Impl.Responses.WorkflowInstanceResponse, Client, Version=0.16.1.0, Culture=neutral, PublicKeyToken=null":"1"},"$type":"1","WorkflowKey":2251799813685454,"BpmnProcessId":"test-process","Version":3,"WorkflowInstanceKey":2251799813686273}
``` 

A workflow instance has been started. Let's view it in Operate.

## View a Workflow Instance in Operate

[Video Link](https://youtu.be/AOj64vzEZ_8?t=1137)

* Go to your cluster in the [Camunda Cloud Console](https://camunda.io).
* In the cluster detail view, click on "_View Workflow Instances in Camunda Operate_".
* In the "_Instances by Workflow_" column, click on "_Test Process - 1 Instance in 1 Version_".
* Click the Instance Id to open the instance.
* You will see the token is stopped at the "_Get Time_" task.

Let's create a task worker to serve the job represented by this task.

## Create a Job Worker

[Video Link](https://youtu.be/AOj64vzEZ_8?t=1244)

We will create a worker program that logs out the job metadata, and completes the job with success.

* Edit the `Services/ZeebeService.cs` file, and add a `_createWorker` method to the `ZeebeService` class:

```c#
// ...
private void _createWorker(String jobType, JobHandler handleJob)
{
    _client.NewWorker()
            .JobType(jobType)
            .Handler(handleJob)
            .MaxJobsActive(5)
            .Name(jobType)
            .PollInterval(TimeSpan.FromSeconds(50))
            .PollingTimeout(TimeSpan.FromSeconds(50))
            .Timeout(TimeSpan.FromSeconds(10))
            .Open();
}
```

* Now add a `CreateGetTimeWorker` method, where we supply the task-type for the worker, and a job handler function:

```c#
public void CreateGetTimeWorker()
{
    _createWorker("get-time", async (client, job) =>
    {
        _logger.LogInformation("Received job: " + job);
        await client.NewCompleteJobCommand(job.Key).Send();
    });    
}
```
The worker handler function is `async` so that it runs on its own thread.

* Now create a method `StartWorkers`:

```c#
public void StartWorkers()
{
    CreateGetTimeWorker();
}
```

* And add it to the `IZeebeService` interface:

```c#
public interface IZeebeService
{
    public Task<IDeployResponse> Deploy(string modelFile);
    public Task<ITopology> Status();
    public Task<string> StartWorkflowInstance(string bpmProcessId);
    public void StartWorkers();
}
```
* Now call this method in the `Configure` method in `Startup.cs`:

```c#
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    var zeebeService = app.ApplicationServices.GetService<IZeebeService>();

    zeebeService.Deploy("test-process.bpmn");
    zeebeService.StartWorkers();
    // ...
}
```

* Run the program with the command: `dotnet run`.

You will see output similar to: 

```
2020-07-16 20:34:25.4971 | DEBUG | Zeebe.Client.Impl.Worker.JobWorker | Job worker (get-time) activated 1 of 5 successfully. 
2020-07-16 20:34:25.4971 | INFO | Cloudstarter.Services.ZeebeService | Received job: Key: 2251799813686173, Type: get-time, WorkflowInstanceKey: 2251799813686168, BpmnProcessId: test-process, WorkflowDefinitionVersion: 3, WorkflowKey: 2251799813685454, ElementId: Activity_1ucrvca, ElementInstanceKey: 2251799813686172, Worker: get-time, Retries: 3, Deadline: 07/16/2020 20:34:35, Variables: {}, CustomHeaders: {} 
```

* Go back to Operate. You will see that the workflow instance is gone.
* Click on "Running Instances".
* In the filter on the left, select "_Finished Instances_".

You will see the completed workflow instance.

## Create and Await the Outcome of a Workflow Instance 

We will now create the workflow instance, and get the final outcome in the calling code.

* Edit the `ZeebeService.cs` file, and edit the `StartWorkflowInstance` method, to make it look like this:

```c#
// ...
public async Task<String> StartWorkflowInstance(string bpmProcessId)
{
    var instance = await _client.NewCreateWorkflowInstanceCommand()
                .BpmnProcessId(bpmProcessId)
                .LatestVersion()
                .WithResult()
                .Send();
    var jsonParams = new JSONParameters {ShowReadOnlyProperties = true};
    return JSON.ToJSON(instance, jsonParams);
}
```

* Run the program with the command: `dotnet run`.

* Visit [http://localhost:5000/start](http://localhost:5000/start) in your browser.

You will see output similar to the following:

```
{"$types":{"Zeebe.Client.Impl.Responses.WorkflowInstanceResultResponse, Client, Version=0.16.1.0, Culture=neutral, PublicKeyToken=null":"1"},"$type":"1","WorkflowKey":2251799813686366,"BpmnProcessId":"test-process","Version":4,"WorkflowInstanceKey":2251799813686409,"Variables":"{}"}
```

## Call a REST Service from the Worker 

[Video link](https://youtu.be/AOj64vzEZ_8?t=1426)

We are going to make a REST call in the worker handler, to query a remote API for the current GMT time.

* Edit the `ZeebeService.cs` file, and edit the `CreateGetTimeWorker` method, to make it look like this:

```c#
// ...
public void CreateGetTimeWorker()
{
    _createWorker("get-time", async (client, job) =>
    {
        _logger.LogInformation("Received job: " + job);
            using (var httpClient = new HttpClient())
            {
                using (var response = await httpClient.GetAsync("https://json-api.joshwulf.com/time"))
                {
                    string apiResponse = await response.Content.ReadAsStringAsync();
                    
                    await client.NewCompleteJobCommand(job.Key)
                        .Variables("{\"time\":" + apiResponse + "}")
                        .Send();
                }
            }
    });    
}
// ...
```

* Run the program with the command: `dotnet run`.
* Visit [http://localhost:5000/start](http://localhost:5000/start) in your browser.

You will see output similar to the following:

```
{"$types":{"Zeebe.Client.Impl.Responses.WorkflowInstanceResultResponse, Client, Version=0.16.1.0, Culture=neutral, PublicKeyToken=null":"1"},"$type":"1","WorkflowKey":2251799813686366,"BpmnProcessId":"test-process","Version":4,"WorkflowInstanceKey":2251799813686463,"Variables":"{\"time\":{\"time\":\"Thu, 16 Jul 2020 10:26:13 GMT\",\"hour\":10,\"minute\":26,\"second\":13,\"day\":4,\"month\":6,\"year\":2020}}"}
```

## Make a Decision 

[Video link](https://youtu.be/AOj64vzEZ_8?t=1781)

We will edit the model to add a Conditional Gateway.

* Open the BPMN model file `bpmn/test-process.bpmn` in the Zeebe Modeler.
* Drop a Gateway between the Service Task and the End event.
* Add two Service Tasks after the Gateway.
* In one, set the _Name_ to `Before noon` and the _Type_ to `make-greeting`.
* Switch to the _Headers_ tab on that Task, and create a new Key `greeting` with the Value `Good morning`.
* In the second, set the _Name_ to `After noon` and the _Type_ to `make-greeting`.
* Switch to the _Headers_ tab on that Task, and create a new Key `greeting` with the Value `Good afternoon`.
* Click on the arrow connecting the Gateway to the _Before noon_ task.
* Under _Details_ enter the following in _Condition expression_: 

```
=time.hour >=0 and time.hour <=11
```

* Click on the arrow connecting the Gateway to the _After noon_ task. 
* Click the spanner/wrench icon and select "Default Flow".
* Connect both Service Tasks to the End Event.

It should look like this:

![](img/second-model.png) 

## Create a Worker that acts based on Custom Headers 

[Video link](https://youtu.be/AOj64vzEZ_8?t=2081)

We will create a second worker that combines the value of a custom header with the value of a variable in the workflow.

* Edit the `ZeebeService.cs` file and create a couple of DTO classes to aid with deserialization of the job:

```c#
public class MakeGreetingCustomHeadersDTO
{
    public string greeting { get; set; }
}

public class MakeGreetingVariablesDTO
{
    public string name { get; set; }
}
```
 
 
* In the same file, create a `CreateMakeGreetingWorker` method:

```c#
 public void CreateMakeGreetingWorker()
{
    _createWorker("make-greeting", async (client, job) =>
    {
        _logger.LogInformation("Make Greeting Received job: " + job);
        var headers = JSON.ToObject<MakeGreetingCustomHeadersDTO>(job.CustomHeaders);
        var variables = JSON.ToObject<MakeGreetingVariablesDTO>(job.Variables);
        string greeting = headers.greeting;
        string name = variables.name;

        await client.NewCompleteJobCommand(job.Key)
            .Variables("{\"say\": \"" + greeting + " " + name + "\"}")
            .Send();
        _logger.LogInformation("Make Greeting Worker completed job");
    });    
}
```

* Now call this method in the `ZeebeService` constructor:

```c#
 public ZeebeService(IConfiguration config, ILogger<ZeebeService> logger)
{
   //...
    CreateGetTimeWorker();
    CreateMakeGreetingWorker();
}
```

* Edit the `startWorkflowInstance` method, and pass in a variable `name` when you create the workflow:

```c#
// ...
public async Task<String> StartWorkflowInstance(string bpmProcessId)
{
    var instance = await _client.NewCreateWorkflowInstanceCommand()
        .BpmnProcessId(bpmProcessId)
        .LatestVersion()
        .Variables("{\"name\": \"Josh Wulf\"}")
        .WithResult()
        .Send();
    var jsonParams = new JSONParameters {ShowReadOnlyProperties = true};
    return JSON.ToJSON(instance, jsonParams);
}
```

You can change the variable `name` value to your own name (or derive it from the url path or a parameter).

* Run the program with the command: `dotnet run`.
* Visit [http://localhost:5000/start](http://localhost:5000/start) in your browser.

You will see output similar to the following:

```
{"$types":{"Zeebe.Client.Impl.Responses.WorkflowInstanceResultResponse, Client, Version=0.16.1.0, Culture=neutral, PublicKeyToken=null":"1"},"$type":"1","WorkflowKey":2251799813686683,"BpmnProcessId":"test-process","Version":5,"WorkflowInstanceKey":2251799813687157,"Variables":"{\"say\":\"Good Afternoon Josh Wulf\",\"name\":\"Josh Wulf\",\"time\":{\"time\":\"Thu, 16 Jul 2020 12:45:33 GMT\",\"hour\":12,\"minute\":45,\"second\":33,\"day\":4,\"month\":6,\"year\":2020}}"}
```

## Profit!

Congratulations. You've completed the Getting Started Guide for Camunda Cloud using C# and ASP .NET Core.

