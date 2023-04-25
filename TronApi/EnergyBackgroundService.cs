using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class EnergyBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;

    public EnergyBackgroundService(IServiceProvider services)
    {
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await IncreaseEnergyAsync();

            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }

    private async Task IncreaseEnergyAsync()
    {
        using (var scope = _services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            var userStats = await dbContext.UsersStats.ToListAsync();

            foreach (var userStat in userStats)
            {
                if (userStat.Energy < 72)
                {
                    userStat.Energy++;
                }
            }

            await dbContext.SaveChangesAsync();
        }
    }
}
