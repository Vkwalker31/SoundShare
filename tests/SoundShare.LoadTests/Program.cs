// NBomber load test of the room queue: baseline (1 user), load (target 100 RPS) and stress (step up to degradation).
// Usage: dotnet run -c Release --project tests/SoundShare.LoadTests -- <baseline|load|stress> [baseUrl]
using System.Net.Http.Json;
using System.Security.Cryptography;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;
using NBomber.Http.CSharp;
using SoundShare.LoadTests;

var mode = args.Length > 0 ? args[0] : "load";
var baseUrl = new Uri(args.Length > 1 ? args[1] : "http://127.0.0.1:5290");
using var http = new HttpClient(new SocketsHttpHandler { UseProxy = false, MaxConnectionsPerServer = 1000 }) { BaseAddress = baseUrl };

var users = await LoadData.PrepareAsync(http, mode == "baseline" ? 2 : 60);

var readQueue = Scenario.Create("read_queue", async context =>
    {
        var user = users.Members[(int)(context.InvocationNumber % users.Members.Count)];
        var request = Http.CreateRequest("GET", new Uri(baseUrl, $"api/rooms/{users.RoomId}/queue").ToString())
            .WithHeader("Authorization", "Bearer " + user.Token);
        return await Http.Send(http, request);
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(5));

var vote = Scenario.Create("vote", async context =>
    {
        var user = users.Members[RandomNumberGenerator.GetInt32(users.Members.Count)];
        var item = users.ItemIds[RandomNumberGenerator.GetInt32(users.ItemIds.Count)];
        var value = RandomNumberGenerator.GetInt32(2) == 0 ? 1 : -1;
        var request = Http.CreateRequest("PUT", new Uri(baseUrl, $"api/rooms/{users.RoomId}/queue/items/{item}/vote").ToString())
            .WithHeader("Authorization", "Bearer " + user.Token)
            .WithBody(JsonContent.Create(new { value }));
        return await Http.Send(http, request);
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(5));

var listRooms = Scenario.Create("list_rooms", async context =>
    {
        var user = users.Members[(int)(context.InvocationNumber % users.Members.Count)];
        var request = Http.CreateRequest("GET", new Uri(baseUrl, "api/rooms").ToString())
            .WithHeader("Authorization", "Bearer " + user.Token);
        return await Http.Send(http, request);
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(5));

LoadSimulation[] Steady(int rate, int seconds) => [Simulation.Inject(rate, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(seconds))];

LoadSimulation[] Steps(params int[] rates) =>
    rates.Select(r => Simulation.Inject(r, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30))).ToArray();

ScenarioProps[] scenarios = mode switch
{
    "baseline" =>
    [
        readQueue.WithLoadSimulations(Simulation.KeepConstant(1, TimeSpan.FromSeconds(30))),
    ],
    "stress" =>
    [
        readQueue.WithLoadSimulations(Steps(70, 140, 280, 420, 560, 700)),
        vote.WithLoadSimulations(Steps(20, 40, 80, 120, 160, 200)),
        listRooms.WithLoadSimulations(Steps(10, 20, 40, 60, 80, 100)),
    ],
    _ =>
    [
        readQueue.WithLoadSimulations(Steady(70, 60)),
        vote.WithLoadSimulations(Steady(20, 60)),
        listRooms.WithLoadSimulations(Steady(10, 60)),
    ],
};

NBomberRunner
    .RegisterScenarios(scenarios)
    .WithTestSuite("SoundShare")
    .WithTestName(mode)
    .WithReportFolder(Path.Combine("reports", mode))
    .WithReportFormats(ReportFormat.Html, ReportFormat.Md, ReportFormat.Csv)
    .Run();
