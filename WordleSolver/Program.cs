
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using WordleSolver.Services;
using WordleSolver.Strategies;

var host = Host.CreateDefaultBuilder()
    .ConfigureServices(services =>
    {
        // Core game logic
        services.AddSingleton<WordleService>();

        // Student-supplied strategy
        services.AddSingleton<IWordleSolverStrategy, AwsomeStudentSolver>();

        // Driver that runs many games
        services.AddSingleton<StudentGuesserService>();
    })
    .Build();

var runner = host.Services.GetRequiredService<StudentGuesserService>();
Stopwatch stopWatch = new Stopwatch();
stopWatch.Start();

runner.Run(2000);

stopWatch.Stop();
TimeSpan ts = stopWatch.Elapsed;
string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours, ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);
Console.WriteLine("RunTime " + elapsedTime);