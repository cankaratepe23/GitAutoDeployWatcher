using System.Management.Automation;
using System.ServiceProcess;
using Timer = System.Timers.Timer;

if (!OperatingSystem.IsWindows())
    return;

var repos = new List<string>();

var intervalMinutes = 1;
if (args.Length != 0)
{
    for (var i = 0; i < args.Length; i+=2)
    {
        switch (args[i])
        {
            case "--interval":
                intervalMinutes = Convert.ToInt32(args[i + 1]);
                break;
            case "--repo":
                repos.Add(args[i+1]);
                break;
        }
    }
}

var manualResetEvent = new ManualResetEvent(false);
var checkTimer = new Timer(TimeSpan.FromMinutes(intervalMinutes).TotalMilliseconds)
{
    AutoReset = true
};
checkTimer.Elapsed += (sender, eventArgs) => CheckRepos();

Console.CancelKeyPress += (sender, eventArgs) =>
{
    manualResetEvent.Set();
    checkTimer.Stop();
    checkTimer.Dispose();
};

CheckRepos();
manualResetEvent.WaitOne();

void CheckRepos()
{
    foreach (var repoPath in repos)
    {
        PowerShell.Create().AddScript($"Set-Location \"{repoPath}\"").AddScript("git fetch").Invoke();
        var localHeadRef = PowerShell.Create().AddScript($"Set-Location \"{repoPath}\"").AddScript("git rev-parse HEAD").Invoke().FirstOrDefault();
        var remoteHeadRef = PowerShell.Create().AddScript($"Set-Location \"{repoPath}\"").AddScript("git rev-parse \"@{u}\"").Invoke().FirstOrDefault();
        if (localHeadRef == null || remoteHeadRef == null || localHeadRef.Equals(remoteHeadRef))
        {
            continue;
        }
        Console.WriteLine("Changed detected. Stopping service.");
        var serviceController = new ServiceController("CriServerSecure");
        serviceController.Stop();
        Console.WriteLine("Pulling repo");
        PowerShell.Create().AddScript($"Set-Location \"{repoPath}\"").AddScript("git pull").Invoke();
        Console.WriteLine("Restarting service");
        serviceController.Start();
    }

    checkTimer.Start();
}

